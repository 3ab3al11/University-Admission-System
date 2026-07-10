namespace ANU_Admissions.Services;

/// <summary>
/// Identity data returned by the official identity provider (the future
/// university API). Holds WHO the person is and their official seat number —
/// it intentionally does NOT carry grades. Scores come only from
/// OfficialStudentRecord, looked up by SeatNumber.
/// </summary>
public class OfficialIdentityResult
{
    public string NationalId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? FatherName { get; set; }
    public DateTime? BirthDate { get; set; }
    public string SeatNumber { get; set; } = string.Empty;
}
