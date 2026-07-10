namespace ANU_Admissions.Models;

public class College
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    public int Capacity { get; set; } = 100; // Number of available seats
    public decimal MinimumScore { get; set; }
    public string AllowedSections { get; set; } = string.Empty; // Comma-separated: "علمي رياضة,علمي علوم"
    public decimal? FinalCutoff { get; set; } // Final cutoff after allocation
    
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public ICollection<Preference> Preferences { get; set; } = new List<Preference>();
    public ICollection<Allocation> Allocations { get; set; } = new List<Allocation>();
}
