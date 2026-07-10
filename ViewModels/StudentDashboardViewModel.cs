namespace ANU_Admissions.ViewModels;

public class StudentDashboardViewModel
{
    // User Information
    public string FullName { get; set; } = string.Empty;
    // Official name from the linked OfficialStudentRecord (preferred when present)
    public string? OfficialFullName { get; set; }
    public string DisplayName => string.IsNullOrWhiteSpace(OfficialFullName) ? FullName : OfficialFullName;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string ParentPhoneNumber { get; set; } = string.Empty;
    
    // Academic Information
    public decimal EquivalentPercentage { get; set; }
    public string Section { get; set; } = string.Empty;
    
    // Status Flags
    public bool HasProfile { get; set; }
    public bool HasPreferences { get; set; }
    public bool HasAllocation { get; set; }
    public bool HasUploadedDocuments { get; set; }
    
    // Application Status
    public ApplicationStatus Status { get; set; }
    
    // Helper methods for view
    public string GetStatusText()
    {
        return Status switch
        {
            ApplicationStatus.NeedsProfile => "يرجى استكمال نموذج التقديم",
            ApplicationStatus.NeedsPreferences => "يرجى اختيار رغبات الكليات",
            ApplicationStatus.PendingAllocation => "قيد المراجعة - انتظار نتيجة التنسيق",
            ApplicationStatus.Allocated => "تم القبول",
            ApplicationStatus.NeedsDocuments => "يرجى رفع المستندات المطلوبة",
            ApplicationStatus.Completed => "تم إتمام التسجيل بنجاح",
            _ => "حالة غير معروفة"
        };
    }
    
    public string GetStatusBadgeClass()
    {
        return Status switch
        {
            ApplicationStatus.NeedsProfile => "bg-danger",
            ApplicationStatus.NeedsPreferences => "bg-warning text-dark",
            ApplicationStatus.PendingAllocation => "bg-warning text-dark",
            ApplicationStatus.Allocated => "bg-success",
            ApplicationStatus.NeedsDocuments => "bg-info text-dark",
            ApplicationStatus.Completed => "bg-success",
            _ => "bg-secondary"
        };
    }
    
    public string GetNextActionUrl()
    {
        return Status switch
        {
            ApplicationStatus.NeedsProfile => "/Student/ApplicationForm",
            ApplicationStatus.NeedsPreferences => "/Student/Preferences",
            ApplicationStatus.PendingAllocation => "/Student/AllocationStatus",
            ApplicationStatus.Allocated => "/Student/AllocationResult",
            ApplicationStatus.NeedsDocuments => "/Student/AllocationResult",
            ApplicationStatus.Completed => "/Student/AllocationResult",
            _ => "/Student/Dashboard"
        };
    }
    
    public string GetNextActionText()
    {
        return Status switch
        {
            ApplicationStatus.NeedsProfile => "ابدأ التقديم",
            ApplicationStatus.NeedsPreferences => "اختر الرغبات",
            ApplicationStatus.PendingAllocation => "تابع الحالة",
            ApplicationStatus.Allocated => "عرض النتيجة",
            ApplicationStatus.NeedsDocuments => "عرض النتيجة",
            ApplicationStatus.Completed => "عرض التفاصيل",
            _ => "الرئيسية"
        };
    }
}

public enum ApplicationStatus
{
    NeedsProfile,
    NeedsPreferences,
    PendingAllocation,
    Allocated,
    NeedsDocuments,
    Completed
}
