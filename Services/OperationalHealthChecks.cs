using ANU_Admissions.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ANU_Admissions.Services;

/// <summary>
/// Checks database connectivity in an isolated dependency-injection scope.
/// Health responses deliberately never expose exception or connection details.
/// </summary>
public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DatabaseHealthCheck> _logger;

    public DatabaseHealthCheck(
        IServiceScopeFactory scopeFactory,
        ILogger<DatabaseHealthCheck> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            return await database.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Database is unavailable.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Database readiness check failed");
            return HealthCheckResult.Unhealthy("Database is unavailable.");
        }
    }
}

public static class OperationalHealthResponseWriter
{
    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        // Health responses change with infrastructure state and must not be
        // cached by browsers, proxies, or orchestration health monitors.
        context.Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
        return context.Response.WriteAsJsonAsync(
            new { status = report.Status.ToString() },
            context.RequestAborted);
    }
}
