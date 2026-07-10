using System.ComponentModel.DataAnnotations;

namespace ANU_Admissions.ViewModels;

// All ErrorMessage / Display values below are RESOURCE KEYS resolved via
// SharedResource (wired in Program.cs through AddDataAnnotationsLocalization).
public class LoginViewModel
{
    [Required(ErrorMessage = "DA_EmailRequired")]
    [EmailAddress(ErrorMessage = "DA_EmailInvalid")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "DA_PasswordRequired")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "RememberMe")]
    public bool RememberMe { get; set; }
}
