namespace ANU_Admissions.Models;

public class SystemSetting
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime LastModified { get; set; }
    public string? ModifiedBy { get; set; }
}
