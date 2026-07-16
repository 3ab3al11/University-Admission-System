using System.Data;
using ANU_Admissions.Data;
using ANU_Admissions.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ANU_Admissions.Services;

public sealed record RegistrationRequest(
    string FullName,
    string Email,
    string StudentPhoneNumber,
    string? ParentPhoneNumber,
    string Password);

public enum RegistrationStatus
{
    Succeeded,
    StudentPhoneTaken,
    ParentPhoneLimitReached,
    IdentityFailed,
    SaveFailed
}

public sealed record RegistrationOutcome(
    RegistrationStatus Status,
    ApplicationUser? User = null,
    IReadOnlyCollection<IdentityError>? IdentityErrors = null);

public interface IRegistrationService
{
    Task<RegistrationOutcome> RegisterAsync(
        RegistrationRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Creates the account and Student role atomically. Serializable isolation makes
/// the parent-phone count stable while the database unique index is the final
/// guard against concurrent reuse of a student's phone number.
/// </summary>
public sealed class RegistrationService : IRegistrationService
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<RegistrationService> _logger;

    public RegistrationService(
        AppDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<RegistrationService> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<RegistrationOutcome> RegisterAsync(
        RegistrationRequest request,
        CancellationToken cancellationToken = default)
    {
        var studentPhone = RegistrationRules.NormalizePhone(request.StudentPhoneNumber);
        var parentPhone = RegistrationRules.NormalizePhone(request.ParentPhoneNumber);

        if (studentPhone == null)
        {
            return new RegistrationOutcome(RegistrationStatus.IdentityFailed);
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            if (await _context.Users.AnyAsync(
                    user => user.PhoneNumber == studentPhone,
                    cancellationToken))
            {
                return new RegistrationOutcome(RegistrationStatus.StudentPhoneTaken);
            }

            if (parentPhone != null
                && await _context.Users.CountAsync(
                    user => user.ParentPhoneNumber == parentPhone,
                    cancellationToken) >= 3)
            {
                return new RegistrationOutcome(
                    RegistrationStatus.ParentPhoneLimitReached);
            }

            var email = request.Email.Trim();
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = request.FullName.Trim(),
                PhoneNumber = studentPhone,
                ParentPhoneNumber = parentPhone
            };

            var createResult = await _userManager.CreateAsync(user, request.Password);
            if (!createResult.Succeeded)
            {
                return new RegistrationOutcome(
                    RegistrationStatus.IdentityFailed,
                    IdentityErrors: createResult.Errors.ToArray());
            }

            var roleResult = await _userManager.AddToRoleAsync(user, "Student");
            if (!roleResult.Succeeded)
            {
                return new RegistrationOutcome(
                    RegistrationStatus.IdentityFailed,
                    IdentityErrors: roleResult.Errors.ToArray());
            }

            await transaction.CommitAsync(cancellationToken);
            return new RegistrationOutcome(RegistrationStatus.Succeeded, user);
        }
        catch (DbUpdateException exception)
            when (RegistrationRules.IsStudentPhoneConflict(exception))
        {
            return new RegistrationOutcome(RegistrationStatus.StudentPhoneTaken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Registration transaction failed");
            return new RegistrationOutcome(RegistrationStatus.SaveFailed);
        }
    }
}

public static class RegistrationRules
{
    public const string StudentPhoneIndexName = "IX_AspNetUsers_PhoneNumber";

    public static string? NormalizePhone(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static bool IsStudentPhoneConflict(Exception exception)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            if (current.Message.Contains(
                    StudentPhoneIndexName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
