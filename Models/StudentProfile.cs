namespace ANU_Admissions.Models;

public class StudentProfile
{
    public int Id { get; set; }

    // Link to Identity User
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public int? StudentId { get; set; }

    // Link to the official imported record (one-to-one). Nullable until the
    // student is linked via ApplicationForm in a later phase.
    public int? OfficialRecordId { get; set; }
    public OfficialStudentRecord? OfficialRecord { get; set; }

    // Application info
    public DateTime ApplicationDate { get; set; }
    public string? CertificateType { get; set; } // Egyptian, Arabic, American, German
    public string? NationalId { get; set; } // For Egyptian students
    public string? SeatNumber { get; set; } // For Egyptian students

    public DateTime DateOfBirth { get; set; }
    public string Gender { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Governorate { get; set; } = string.Empty;

    // High school information
    public string HighSchoolName { get; set; } = string.Empty;
    public int GraduationYear { get; set; }
    public decimal TotalScore { get; set; }
    public decimal Percentage { get; set; }
    public decimal EquivalentPercentage { get; set; } // For different certificate types
    public string Section { get; set; } = string.Empty; // علمي رياضة، علمي علوم، أدبي

    // Navigation property
    public ICollection<Preference> Preferences { get; set; } = new List<Preference>();
    public ICollection<Allocation> Allocations { get; set; } = new List<Allocation>();
    public ICollection<Document> Documents { get; set; } = new List<Document>();
}
