using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ANU_Admissions.ViewModels;

public class ApplicationFormViewModel
{
    // ---------------------------------------------------------------
    // Verification (editable)
    // ---------------------------------------------------------------
    [Required(ErrorMessage = "DA_NationalIdRequired")]
    [RegularExpression(@"^\d{14}$", ErrorMessage = "DA_NationalIdFormat")]
    [Display(Name = "NationalId")]
    public string? NationalId { get; set; }

    [Required(ErrorMessage = "DA_SeatNumberRequired")]
    [StringLength(20, MinimumLength = 1, ErrorMessage = "DA_SeatNumberInvalid")]
    [Display(Name = "SeatNumber")]
    public string? SeatNumber { get; set; }

    [Required(ErrorMessage = "DA_SectionRequired")]
    [Display(Name = "Section")]
    public string? Track { get; set; }

    // ---------------------------------------------------------------
    // Contact (editable, optional — prefilled from the account)
    // ---------------------------------------------------------------
    [RegularExpression(@"^\d{11}$", ErrorMessage = "DA_PhoneFormatPlain")]
    [Display(Name = "PhoneNumber")]
    public string? PhoneNumber { get; set; }

    [RegularExpression(@"^\d{11}$", ErrorMessage = "DA_ParentPhoneFormatPlain")]
    [Display(Name = "ParentPhoneNumber")]
    public string? ParentPhoneNumber { get; set; }

    [StringLength(250, ErrorMessage = "DA_AddressTooLong")]
    [Display(Name = "Address")]
    public string? Address { get; set; }

    // ---------------------------------------------------------------
    // Display-only. [BindNever] makes the model binder DISCARD anything the
    // client posts for these — the server never reads scores from the form.
    // ---------------------------------------------------------------
    [BindNever] public bool IsLinked { get; set; }
    [BindNever] public string? Email { get; set; }
    [BindNever] public string? FullName { get; set; }
    [BindNever] public decimal? TotalScore { get; set; }
    [BindNever] public decimal? MaxScore { get; set; }
    [BindNever] public decimal? Percentage { get; set; }
    [BindNever] public decimal? EquivalentPercentage { get; set; }
    [BindNever] public string? StatusDescription { get; set; }
}
