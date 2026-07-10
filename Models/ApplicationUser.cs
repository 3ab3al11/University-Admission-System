using Microsoft.AspNetCore.Identity;

namespace ANU_Admissions.Models;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    
    // PhoneNumber is inherited from IdentityUser (student phone)
    // Add parent/backup phone
    public string? ParentPhoneNumber { get; set; }
    
    // Navigation property
    public StudentProfile? StudentProfile { get; set; }
}
