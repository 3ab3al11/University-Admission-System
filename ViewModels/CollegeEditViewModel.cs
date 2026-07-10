using System.ComponentModel.DataAnnotations;

namespace ANU_Admissions.ViewModels;

/// <summary>
/// Admin create/edit form for a college. Maps only to EXISTING College fields
/// (no schema change). Id = 0 means create.
/// </summary>
public class CollegeEditViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "اسم الكلية (بالعربية) مطلوب")]
    [StringLength(150)]
    [Display(Name = "اسم الكلية (عربي)")]
    public string NameAr { get; set; } = string.Empty;

    [StringLength(150)]
    [Display(Name = "اسم الكلية (إنجليزي)")]
    public string? NameEn { get; set; }

    [Required(ErrorMessage = "كود الكلية مطلوب")]
    [StringLength(20)]
    [Display(Name = "كود الكلية")]
    public string Code { get; set; } = string.Empty;

    [Range(0, int.MaxValue, ErrorMessage = "السعة يجب أن تكون رقمًا أكبر من أو يساوي 0")]
    [Display(Name = "السعة (Capacity)")]
    public int Capacity { get; set; }

    [Range(0, 100, ErrorMessage = "الحد الأدنى يجب أن يكون بين 0 و 100")]
    [Display(Name = "الحد الأدنى المطلوب %")]
    public decimal MinimumScore { get; set; }

    // Selected via checkboxes in the form; combined into a comma-separated
    // AllowedSections string server-side. Values must be from CanonicalSections.
    [Display(Name = "الشعب المسموح بها")]
    public List<string> SelectedSections { get; set; } = new();

    // Read-only string shown in the list view.
    public string? AllowedSections { get; set; }

    // The valid section values used across the system.
    public static readonly string[] CanonicalSections =
        { "علمي علوم", "علمي رياضة", "أدبي" };

    public bool IsActive { get; set; } = true;

    // Display-only (not editable in this form)
    public int PreferencesCount { get; set; }
    public int AllocationsCount { get; set; }
}
