namespace ANU_Admissions.Services;

/// <summary>
/// Local-only email sender. It exposes reset links in the console so developers
/// can test the workflow without an external provider.
/// </summary>
public sealed class DevEmailSender : IAppEmailSender
{
    private readonly ILogger<DevEmailSender> _logger;
    private readonly IHostEnvironment _environment;

    public DevEmailSender(
        ILogger<DevEmailSender> logger,
        IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public Task SendEmailAsync(string toEmail, string subject, string htmlBody)
    {
        // Defense in depth: even an accidental production registration cannot
        // write an address, message body, reset link, or token to logs.
        if (!EmailDeliveryRules.CanLogSensitiveContent(_environment.EnvironmentName))
        {
            _logger.LogError(
                "DevEmailSender is disabled outside Development. " +
                "Email content was discarded without being logged.");
            return Task.CompletedTask;
        }

        _logger.LogWarning(
            "\n================ DEV EMAIL (not actually sent) ================\n" +
            "To: {To}\nSubject: {Subject}\n{Body}\n" +
            "==============================================================\n",
            toEmail, subject, htmlBody);

        return Task.CompletedTask;
    }
}
