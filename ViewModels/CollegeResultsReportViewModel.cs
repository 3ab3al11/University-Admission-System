namespace ANU_Admissions.ViewModels;

/// <summary>
/// Admin-only, read-only report of students accepted into a college after
/// allocation, ordered by EquivalentPercentage descending. Server-built.
/// </summary>
public class CollegeResultsReportViewModel
{
    // Dropdown
    public List<CollegeDropdownItem> Colleges { get; set; } = new();
    public int? SelectedCollegeId { get; set; }
    public bool HasSelection => SelectedCollegeId.HasValue;

    // Report header
    public string CollegeName { get; set; } = string.Empty;
    public string? CollegeNameEn { get; set; }
    public int Capacity { get; set; }
    public int AcceptedCount { get; set; }
    public decimal? HighestPercentage { get; set; }
    public decimal? LowestPercentage { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.Now;

    public List<CollegeResultStudentRowViewModel> Students { get; set; } = new();
}

public class CollegeDropdownItem
{
    public int Id { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string? NameEn { get; set; }
}

public class CollegeResultStudentRowViewModel
{
    public int Rank { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string SeatNumber { get; set; } = string.Empty;
    public string NationalIdMasked { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    public decimal EquivalentPercentage { get; set; }
    public int? PreferenceRank { get; set; }
    public DateTime AllocationDate { get; set; }
}
