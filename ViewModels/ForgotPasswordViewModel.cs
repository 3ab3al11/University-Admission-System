using System.ComponentModel.DataAnnotations;

namespace ANU_Admissions.ViewModels;

public class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "DA_EmailRequired")]
    [EmailAddress(ErrorMessage = "DA_EmailInvalid")]
    [Display(Name = "DA_RegisteredEmail")]
    public string Email { get; set; } = string.Empty;
}
