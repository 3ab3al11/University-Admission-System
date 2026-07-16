namespace ANU_Admissions.Services;

/// <summary>
/// Safe fallback for environments where no real email provider is configured.
/// It intentionally logs no recipient, subject, body, reset link, or token.
/// </summary>
public sealed class DisabledEmailSender : IAppEmailSender
{
    private readonly ILogger<DisabledEmailSender> _logger;

    public DisabledEmailSender(ILogger<DisabledEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendEmailAsync(string toEmail, string subject, string htmlBody)
    {
        _logger.LogWarning(
            "Email delivery is not configured. A message was discarded " +
            "without logging its recipient or content.");

        return Task.CompletedTask;
    }
}
