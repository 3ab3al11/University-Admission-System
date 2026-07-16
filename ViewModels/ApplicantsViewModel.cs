namespace ANU_Admissions.ViewModels;

public class ApplicantsListViewModel
{
    public string? SearchTerm { get; set; }
    public string? StatusFilter { get; set; }

    public List<ApplicantRowDto> Applicants { get; set; } = new();
}

public class ApplicantRowDto
{
    public int ProfileId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? NationalId { get; set; }
    public string Section { get; set; } = string.Empty;
    public decimal EquivalentPercentage { get; set; }
    public int PreferencesCount { get; set; }
    public bool HasAllocation { get; set; }
    public int DocumentsCount { get; set; }
    public DateTime ApplicationDate { get; set; }

    // Status as a resource KEY (localized in the view), not pre-rendered HTML —
    // so the badge label flips with the current culture.
    public string StatusKey =>
        HasAllocation ? "StatusAllocated"
        : PreferencesCount > 0 ? "StatusPending"
        : EquivalentPercentage > 0 ? "StatusReadyForPrefs"
        : "StatusIncomplete";

    public string StatusBgClass =>
        HasAllocation ? "bg-success"
        : PreferencesCount > 0 ? "bg-warning"
        : EquivalentPercentage > 0 ? "bg-info"
        : "bg-secondary";
}

public class ApplicantDetailsViewModel
{
    // Profile Info
    public int ProfileId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? NationalId { get; set; }
    public string? SeatNumber { get; set; }
    public DateTime DateOfBirth { get; set; }
    public string Gender { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Governorate { get; set; } = string.Empty;

    // Academic Info
    public string HighSchoolName { get; set; } = string.Empty;
    public int GraduationYear { get; set; }
    public decimal TotalScore { get; set; }
    public decimal Percentage { get; set; }
    public decimal EquivalentPercentage { get; set; }
    public string Section { get; set; } = string.Empty;
    public string CertificateType { get; set; } = string.Empty;
    public DateTime ApplicationDate { get; set; }

    // Preferences
    public List<PreferenceDetailDto> Preferences { get; set; } = new();

    // Allocation
    public AllocationDetailDto? Allocation { get; set; }

    // Documents
    public List<DocumentDetailDto> Documents { get; set; } = new();
}

public class PreferenceDetailDto
{
    public int Rank { get; set; }
    public string CollegeName { get; set; } = string.Empty;
    public decimal MinimumScore { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AllocationDetailDto
{
    public string CollegeName { get; set; } = string.Empty;
    public DateTime AllocationDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool DocumentsSubmitted { get; set; }
}

public class DocumentDetailDto
{
    public int Id { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime UploadedAt { get; set; }
    public bool IsVerified { get; set; }

    public string GetFileSizeFormatted()
    {
        if (FileSize < 1024)
            return $"{FileSize} B";
        if (FileSize < 1024 * 1024)
            return $"{FileSize / 1024.0:F1} KB";
        return $"{FileSize / (1024.0 * 1024.0):F1} MB";
    }
}
