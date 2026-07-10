namespace ANU_Admissions.Models;

public class OfficialStudentRecord
{
    public int Id { get; set; }

    public string SeatNumber { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public decimal TotalScore { get; set; }
    public decimal MaxScore { get; set; }
    public decimal Percentage { get; set; }
    public decimal EquivalentPercentage { get; set; }

    public string? StatusDescription { get; set; }
    public bool IsEligible { get; set; }

    public string? NationalId { get; set; }

    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
    public string? ImportBatch { get; set; }

    public StudentProfile? StudentProfile { get; set; }
}
