using System.ComponentModel.DataAnnotations;

namespace ANU_Admissions.ViewModels;

public class AllocationStatusViewModel
{
    public string Status { get; set; } = "Pending"; // "Pending", "Allocated", "Not Allocated"
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public decimal EquivalentPercentage { get; set; }
    public string Track { get; set; } = string.Empty;
    
    public List<PreferenceDisplay> Preferences { get; set; } = new();
    public AllocationResultDisplay? Allocation { get; set; }
}

public class PreferenceDisplay
{
    public int Rank { get; set; }
    public string CollegeName { get; set; } = string.Empty;
    public decimal MinimumScore { get; set; }
    public decimal? FinalCutoff { get; set; }
}

public class AllocationResultDisplay
{
    public string CollegeName { get; set; } = string.Empty;
    public decimal FinalCutoff { get; set; }
    public DateTime AllocationDate { get; set; }
}
