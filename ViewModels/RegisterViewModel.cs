using System.ComponentModel.DataAnnotations;

namespace ANU_Admissions.ViewModels;

// All ErrorMessage / Display values below are RESOURCE KEYS resolved via
// SharedResource (wired in Program.cs through AddDataAnnotationsLocalization).
public class RegisterViewModel
{
    [Required(ErrorMessage = "DA_FullNameRequired")]
    [Display(Name = "FullName")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "DA_EmailRequired")]
    [EmailAddress(ErrorMessage = "DA_EmailInvalid")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "DA_StudentPhoneRequired")]
    [RegularExpression(@"^01[0-9]{9}$", ErrorMessage = "DA_PhoneFormat")]
    [Display(Name = "DA_StudentMobile")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "DA_ParentPhoneRequired")]
    [RegularExpression(@"^01[0-9]{9}$", ErrorMessage = "DA_PhoneFormat")]
    [Display(Name = "DA_ParentMobile")]
    public string? ParentPhoneNumber { get; set; }

    [Required(ErrorMessage = "DA_PasswordRequired")]
    [StringLength(100, ErrorMessage = "DA_PasswordMinLen", MinimumLength = 6)]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "ConfirmPassword")]
    [Compare("Password", ErrorMessage = "DA_PasswordsMustMatch")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
