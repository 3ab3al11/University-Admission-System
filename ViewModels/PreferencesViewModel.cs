using System.ComponentModel.DataAnnotations;

namespace ANU_Admissions.ViewModels;

public class PreferencesViewModel
{
    public string StudentTrack { get; set; } = string.Empty;
    public decimal EquivalentPercentage { get; set; }

    // Max preferences allowed for this student's section (set by the server).
    public int MaxPreferences { get; set; } = 3;

    public List<CollegeOption> AvailableColleges { get; set; } = new();

    [Required(ErrorMessage = "DA_PrefAtLeastOne")]
    public List<int> SelectedCollegeIds { get; set; } = new();
}

public class CollegeOption
{
    public int Id { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string Code { get; set; } = string.Empty;
    public decimal MinimumScore { get; set; }
    public decimal? FinalCutoff { get; set; } // Cutoff after allocation
    public bool IsEligible { get; set; }
}
