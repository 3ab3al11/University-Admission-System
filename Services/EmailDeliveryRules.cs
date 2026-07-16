namespace ANU_Admissions.Services;

public static class EmailDeliveryRules
{
    public static bool CanLogSensitiveContent(string? environmentName) =>
        string.Equals(
            environmentName,
            Environments.Development,
            StringComparison.OrdinalIgnoreCase);
}
