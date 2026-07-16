namespace ANU_Admissions.Models;

public class Preference
{
    public int Id { get; set; }
    public int StudentProfileId { get; set; }
    public int CollegeId { get; set; }
    public int Rank { get; set; } // 1 = first choice, 2 = second choice, etc.

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public StudentProfile StudentProfile { get; set; } = null!;
    public College College { get; set; } = null!;
}
