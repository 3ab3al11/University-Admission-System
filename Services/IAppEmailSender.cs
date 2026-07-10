namespace ANU_Admissions.Services;

/// <summary>
/// Minimal email-sending abstraction. The development implementation logs the
/// message (including any reset link) so it can be picked up from the console
/// during testing. Replace with a real SMTP/provider implementation in
/// production (configured via environment variables / user-secrets).
/// </summary>
public interface IAppEmailSender
{
    Task SendEmailAsync(string toEmail, string subject, string htmlBody);
}
