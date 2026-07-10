namespace ANU_Admissions.Models;

public class Document
{
    public int Id { get; set; }
    public int StudentProfileId { get; set; }
    
    public string DocumentType { get; set; } = string.Empty; // NationalId, HighSchoolCertificate, Photo, etc.
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ContentType { get; set; } = string.Empty;
    
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public bool IsVerified { get; set; } = false;
    
    // Navigation property
    public StudentProfile StudentProfile { get; set; } = null!;
}
