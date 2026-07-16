using ANU_Admissions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ANU_Admissions.UnitTests;

public sealed class OperationalHealthCheckTests
{
    [Fact]
    public async Task MissingDatabaseRegistrationReturnsGenericUnhealthyResult()
    {
        await using var provider = new ServiceCollection().BuildServiceProvider();
        var check = new DatabaseHealthCheck(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<DatabaseHealthCheck>.Instance);

        var result = await check.CheckHealthAsync(
            new HealthCheckContext(),
            TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("Database is unavailable.", result.Description);
        Assert.Null(result.Exception);
    }
}
