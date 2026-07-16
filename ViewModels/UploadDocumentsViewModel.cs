using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace ANU_Admissions.ViewModels;

public class UploadDocumentsViewModel
{
    [Required(ErrorMessage = "صورة بطاقة الرقم القومي مطلوبة")]
    [Display(Name = "صورة بطاقة الرقم القومي")]
    public IFormFile? NationalIdCard { get; set; }

    [Display(Name = "صورة شخصية")]
    public IFormFile? PersonalPhoto { get; set; }

    [Display(Name = "ورقة الترشيح")]
    public IFormFile? NominationPaper { get; set; }
}
