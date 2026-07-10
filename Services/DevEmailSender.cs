namespace ANU_Admissions.Services;

/// <summary>
/// Development email sender — does NOT actually send mail. It writes the email
/// (subject + body, which contains the reset link) to the application log so a
/// developer can copy the reset link from the console.
///
/// Production should register a real implementation (SMTP/provider) whose
/// credentials come from environment variables / user-secrets — never from
/// appsettings in source control.
/// </summary>
public class DevEmailSender : IAppEmailSender
{
    private readonly ILogger<DevEmailSender> _logger;

    public DevEmailSender(ILogger<DevEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendEmailAsync(string toEmail, string subject, string htmlBody)
    {
        _logger.LogWarning(
            "\n================ DEV EMAIL (not actually sent) ================\n" +
            "To: {To}\nSubject: {Subject}\n{Body}\n" +
            "==============================================================\n",
            toEmail, subject, htmlBody);

        return Task.CompletedTask;
    }
}
