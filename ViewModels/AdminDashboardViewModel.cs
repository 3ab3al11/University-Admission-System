namespace ANU_Admissions.ViewModels;

public class AdminDashboardViewModel
{
    // Statistics
    public int TotalStudents { get; set; }
    public int CompletedProfiles { get; set; }
    public int StudentsWithPreferences { get; set; }
    public int AllocatedCount { get; set; }
    public int PendingAllocation { get; set; }
    public int DocumentsUploadedCount { get; set; }
    public int TotalColleges { get; set; }

    // Latest Activity
    public List<RecentProfileDto> LatestProfiles { get; set; } = new();
    public List<RecentAllocationDto> LatestAllocations { get; set; } = new();
    public List<RecentDocumentDto> LatestDocuments { get; set; } = new();
}

public class RecentProfileDto
{
    public string StudentName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public decimal EquivalentPercentage { get; set; }
    public string Section { get; set; } = string.Empty;
    public DateTime ApplicationDate { get; set; }
}

public class RecentAllocationDto
{
    public string StudentName { get; set; } = string.Empty;
    public string CollegeName { get; set; } = string.Empty;
    public string? CollegeNameEn { get; set; }
    public decimal StudentScore { get; set; }
    public DateTime AllocationDate { get; set; }
}

public class RecentDocumentDto
{
    public string StudentName { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
}
