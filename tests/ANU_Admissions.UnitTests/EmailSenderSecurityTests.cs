using ANU_Admissions.Services;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ANU_Admissions.UnitTests;

public class EmailSenderSecurityTests
{
    private const string Recipient = "student@example.edu";
    private const string Subject = "Reset password";
    private const string SecretBody = "https://trusted.example/reset?token=TOP_SECRET_TOKEN";

    [Theory]
    [InlineData("Development", true)]
    [InlineData("development", true)]
    [InlineData("Production", false)]
    [InlineData("Staging", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void SensitiveContentIsAllowedOnlyInDevelopment(
        string? environmentName,
        bool expected)
    {
        Assert.Equal(
            expected,
            EmailDeliveryRules.CanLogSensitiveContent(environmentName));
    }

    [Fact]
    public async Task DevelopmentSenderLogsMessageOnlyInDevelopment()
    {
        var logger = new RecordingLogger<DevEmailSender>();
        var sender = new DevEmailSender(
            logger,
            new TestHostEnvironment(Environments.Development));

        await sender.SendEmailAsync(Recipient, Subject, SecretBody);

        var output = logger.CombinedOutput;
        Assert.Contains(Recipient, output);
        Assert.Contains(Subject, output);
        Assert.Contains("TOP_SECRET_TOKEN", output);
    }

    [Fact]
    public async Task DevelopmentSenderCannotLeakContentInProduction()
    {
        var logger = new RecordingLogger<DevEmailSender>();
        var sender = new DevEmailSender(
            logger,
            new TestHostEnvironment(Environments.Production));

        await sender.SendEmailAsync(Recipient, Subject, SecretBody);

        AssertNoSensitiveContent(logger.CombinedOutput);
    }

    [Fact]
    public async Task DisabledSenderNeverLogsMessageContent()
    {
        var logger = new RecordingLogger<DisabledEmailSender>();
        var sender = new DisabledEmailSender(logger);

        await sender.SendEmailAsync(Recipient, Subject, SecretBody);

        AssertNoSensitiveContent(logger.CombinedOutput);
    }

    private static void AssertNoSensitiveContent(string output)
    {
        Assert.DoesNotContain(Recipient, output);
        Assert.DoesNotContain(Subject, output);
        Assert.DoesNotContain("TOP_SECRET_TOKEN", output);
        Assert.DoesNotContain("trusted.example", output);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string environmentName)
        {
            EnvironmentName = environmentName;
        }

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "ANU_Admissions.UnitTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        private readonly List<string> _messages = [];

        public string CombinedOutput => string.Join(Environment.NewLine, _messages);

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _messages.Add(formatter(state, exception));
        }
    }
}
