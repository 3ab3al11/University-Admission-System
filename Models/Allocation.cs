namespace ANU_Admissions.Models;

public class Allocation
{
    public int Id { get; set; }
    public int StudentProfileId { get; set; }
    public int CollegeId { get; set; }
    
    public DateTime AllocationDate { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Allocated"; // Allocated, Confirmed, Rejected
    
    public bool DocumentsSubmitted { get; set; } = false;
    public DateTime? DocumentsSubmissionDate { get; set; }
    
    // Navigation properties
    public StudentProfile StudentProfile { get; set; } = null!;
    public College College { get; set; } = null!;
}
