using System.ComponentModel.DataAnnotations;

namespace ANU_Admissions.ViewModels;

public class ResetPasswordViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    // The (Base64Url-encoded) password reset token. Hidden field — never shown.
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "DA_NewPasswordRequired")]
    [DataType(DataType.Password)]
    [Display(Name = "NewPassword")]
    public string NewPassword { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "ConfirmPassword")]
    [Compare(nameof(NewPassword), ErrorMessage = "DA_PasswordsMustMatch")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
