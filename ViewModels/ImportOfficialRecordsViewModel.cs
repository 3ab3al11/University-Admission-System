using System.ComponentModel.DataAnnotations;

namespace ANU_Admissions.ViewModels;

public class ImportOfficialRecordsViewModel
{
    [Required(ErrorMessage = "الرجاء اختيار ملف Excel")]
    [Display(Name = "ملف نتائج الثانوية العامة (Excel)")]
    public IFormFile? ExcelFile { get; set; }

    [Required(ErrorMessage = "النهاية العظمى مطلوبة")]
    [Range(1, 9999, ErrorMessage = "النهاية العظمى يجب أن تكون بين 1 و 9999")]
    [Display(Name = "النهاية العظمى (MaxScore)")]
    public decimal MaxScore { get; set; } = 700m;

    [StringLength(100)]
    [Display(Name = "اسم الدفعة (اختياري)")]
    public string? ImportBatch { get; set; }

    [Display(Name = "إلغاء الاستيراد بالكامل لو وُجد صف نسبته > 100%")]
    public bool AbortOnAnyOverflow { get; set; } = true;
}

public class ImportOfficialRecordsResultViewModel
{
    public int TotalRowsRead { get; set; }
    public int Imported { get; set; }
    public int NotEligibleImported { get; set; }
    public int SkippedOverMaxScore { get; set; }
    public int SkippedMissingData { get; set; }
    public int SkippedDuplicateInFile { get; set; }
    public int SkippedAlreadyInDb { get; set; }

    public bool Aborted { get; set; }
    public string? AbortReason { get; set; }

    public decimal MaxScoreUsed { get; set; }
    public string? ImportBatch { get; set; }
    public TimeSpan Duration { get; set; }

    public List<ImportRowError> FirstErrors { get; set; } = new();
}

public class ImportRowError
{
    public int RowNumber { get; set; }
    public string? SeatNumber { get; set; }
    public string Reason { get; set; } = string.Empty;
}
