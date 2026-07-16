using ANU_Admissions.Data;
using ANU_Admissions.Models;
using Microsoft.EntityFrameworkCore;

namespace ANU_Admissions.Services;

public sealed record StudentVerificationRequest(
    string NationalId,
    string SeatNumber,
    string Section,
    string? PhoneNumber,
    string? ParentPhoneNumber,
    string? Address);

public enum StudentVerificationStatus
{
    Succeeded,
    AlreadyLinked,
    InvalidSection,
    StudentPhoneTaken,
    ParentPhoneLimitReached,
    IdentityLookupFailed,
    NationalIdNotFound,
    SeatNumberMismatch,
    SeatNotInOfficialRecords,
    StudentNotEligible,
    SeatAlreadyLinked,
    PhoneConflict,
    SaveFailed
}

public sealed record StudentVerificationOutcome(
    StudentVerificationStatus Status,
    StudentProfile? Profile = null);

public interface IStudentVerificationService
{
    Task<StudentVerificationOutcome> VerifyAndSaveAsync(
        ApplicationUser user,
        StudentProfile? profile,
        StudentVerificationRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Verifies a logged-in student against the identity provider and imported
/// official results, then copies only server-trusted academic data to the profile.
/// </summary>
public sealed class StudentVerificationService : IStudentVerificationService
{
    private readonly AppDbContext _context;
    private readonly IOfficialIdentityProvider _identityProvider;

    public StudentVerificationService(
        AppDbContext context,
        IOfficialIdentityProvider identityProvider)
    {
        _context = context;
        _identityProvider = identityProvider;
    }

    public async Task<StudentVerificationOutcome> VerifyAndSaveAsync(
        ApplicationUser user,
        StudentProfile? profile,
        StudentVerificationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (profile?.OfficialRecord != null)
        {
            return new StudentVerificationOutcome(
                StudentVerificationStatus.AlreadyLinked,
                profile);
        }

        if (!StudentVerificationRules.IsAllowedSection(request.Section))
        {
            return new StudentVerificationOutcome(StudentVerificationStatus.InvalidSection);
        }

        var phoneNumber = request.PhoneNumber?.Trim();
        if (!string.IsNullOrEmpty(phoneNumber)
            && await _context.Users.AnyAsync(
                other => other.PhoneNumber == phoneNumber && other.Id != user.Id,
                cancellationToken))
        {
            return new StudentVerificationOutcome(StudentVerificationStatus.StudentPhoneTaken);
        }

        var parentPhoneNumber = request.ParentPhoneNumber?.Trim();
        if (!string.IsNullOrEmpty(parentPhoneNumber)
            && await _context.Users.CountAsync(
                other => other.ParentPhoneNumber == parentPhoneNumber && other.Id != user.Id,
                cancellationToken) >= 3)
        {
            return new StudentVerificationOutcome(
                StudentVerificationStatus.ParentPhoneLimitReached);
        }

        OfficialIdentityResult? identity;
        try
        {
            identity = await _identityProvider.GetByNationalIdAsync(request.NationalId.Trim());
        }
        catch
        {
            return new StudentVerificationOutcome(
                StudentVerificationStatus.IdentityLookupFailed);
        }

        if (identity == null)
        {
            return new StudentVerificationOutcome(StudentVerificationStatus.NationalIdNotFound);
        }

        if (!StudentVerificationRules.SeatNumbersMatch(
                identity.SeatNumber,
                request.SeatNumber))
        {
            return new StudentVerificationOutcome(StudentVerificationStatus.SeatNumberMismatch);
        }

        var officialSeatNumber = identity.SeatNumber!.Trim();
        var officialRecord = await _context.OfficialStudentRecords
            .FirstOrDefaultAsync(
                record => record.SeatNumber == officialSeatNumber,
                cancellationToken);

        if (officialRecord == null)
        {
            return new StudentVerificationOutcome(
                StudentVerificationStatus.SeatNotInOfficialRecords);
        }

        if (!officialRecord.IsEligible)
        {
            return new StudentVerificationOutcome(StudentVerificationStatus.StudentNotEligible);
        }

        var seatAlreadyLinked = await _context.StudentProfiles.AnyAsync(
            other => other.OfficialRecordId == officialRecord.Id && other.UserId != user.Id,
            cancellationToken);
        if (seatAlreadyLinked)
        {
            return new StudentVerificationOutcome(StudentVerificationStatus.SeatAlreadyLinked);
        }

        if (profile == null)
        {
            profile = new StudentProfile
            {
                UserId = user.Id,
                ApplicationDate = DateTime.UtcNow
            };
            _context.StudentProfiles.Add(profile);
        }

        profile.OfficialRecordId = officialRecord.Id;
        profile.OfficialRecord = officialRecord;
        profile.SeatNumber = officialRecord.SeatNumber;
        profile.NationalId = identity.NationalId;
        profile.TotalScore = officialRecord.TotalScore;
        profile.Percentage = officialRecord.Percentage;
        profile.EquivalentPercentage = officialRecord.EquivalentPercentage;
        profile.CertificateType = "Egyptian";
        profile.Section = request.Section;

        if (!string.IsNullOrWhiteSpace(request.Address))
            profile.Address = request.Address.Trim();
        if (!string.IsNullOrWhiteSpace(phoneNumber))
            user.PhoneNumber = phoneNumber;
        if (!string.IsNullOrWhiteSpace(parentPhoneNumber))
            user.ParentPhoneNumber = parentPhoneNumber;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            var message = exception.InnerException?.Message ?? exception.Message;
            if (message.Contains("IX_StudentProfiles_PhoneNumber", StringComparison.Ordinal)
                || RegistrationRules.IsStudentPhoneConflict(exception))
                return new StudentVerificationOutcome(StudentVerificationStatus.PhoneConflict);
            if (message.Contains("OfficialRecordId", StringComparison.Ordinal))
                return new StudentVerificationOutcome(StudentVerificationStatus.SeatAlreadyLinked);

            return new StudentVerificationOutcome(StudentVerificationStatus.SaveFailed);
        }

        return new StudentVerificationOutcome(StudentVerificationStatus.Succeeded, profile);
    }
}

/// <summary>Pure verification rules shared by the service and unit tests.</summary>
public static class StudentVerificationRules
{
    private static readonly HashSet<string> AllowedSections =
        ["علمي علوم", "علمي رياضة", "أدبي"];

    public static bool IsAllowedSection(string? section)
        => !string.IsNullOrWhiteSpace(section) && AllowedSections.Contains(section.Trim());

    public static bool SeatNumbersMatch(string? officialSeatNumber, string? enteredSeatNumber)
        => !string.IsNullOrWhiteSpace(officialSeatNumber)
           && !string.IsNullOrWhiteSpace(enteredSeatNumber)
           && string.Equals(
               officialSeatNumber.Trim(),
               enteredSeatNumber.Trim(),
               StringComparison.Ordinal);
}
