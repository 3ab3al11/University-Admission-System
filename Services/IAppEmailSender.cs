namespace ANU_Admissions.Services;

/// <summary>
/// Minimal email-sending abstraction. Development can use a local console
/// implementation; hosted environments should replace the safe disabled
/// fallback with an SMTP/API implementation configured through secrets.
/// </summary>
public interface IAppEmailSender
{
    Task SendEmailAsync(string toEmail, string subject, string htmlBody);
}
