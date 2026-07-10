namespace ANU_Admissions.Models;

public class AuditLog
{
    public int Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string PerformedBy { get; set; } = string.Empty;
    public DateTime PerformedAt { get; set; }
    public string? Details { get; set; }
    public string IpAddress { get; set; } = string.Empty;
}
