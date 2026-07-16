using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;

namespace ANU_Admissions.Services;

/// <summary>
/// Names, client partitioning, and limiter settings for public authentication
/// endpoints. Each factory returns a new options instance for its IP partition.
/// </summary>
public static class AuthRateLimitPolicies
{
    public const string Login = "auth-login";
    public const string ForgotPassword = "auth-forgot-password";
    public const string ResetPassword = "auth-reset-password";

    public static string GetClientPartitionKey(HttpContext context)
    {
        var address = context.Connection.RemoteIpAddress;
        if (address == null)
        {
            return "unknown";
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        return address.ToString();
    }

    public static FixedWindowRateLimiterOptions CreateLoginLimiterOptions() =>
        CreateOptions(permitLimit: 10, window: TimeSpan.FromMinutes(1));

    public static FixedWindowRateLimiterOptions CreateForgotPasswordLimiterOptions() =>
        CreateOptions(permitLimit: 3, window: TimeSpan.FromMinutes(15));

    public static FixedWindowRateLimiterOptions CreateResetPasswordLimiterOptions() =>
        CreateOptions(permitLimit: 5, window: TimeSpan.FromMinutes(15));

    private static FixedWindowRateLimiterOptions CreateOptions(
        int permitLimit,
        TimeSpan window) => new()
        {
            PermitLimit = permitLimit,
            Window = window,
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        };
}
