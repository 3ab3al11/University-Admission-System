using System.Net;
using System.Threading.RateLimiting;
using ANU_Admissions.Services;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace ANU_Admissions.UnitTests;

public class AuthRateLimitPoliciesTests
{
    [Fact]
    public void UsesRemoteIpv4AddressAsPartitionKey()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.25");

        var key = AuthRateLimitPolicies.GetClientPartitionKey(context);

        Assert.Equal("192.0.2.25", key);
    }

    [Fact]
    public void NormalizesIpv4MappedIpv6Address()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("::ffff:192.0.2.25");

        var key = AuthRateLimitPolicies.GetClientPartitionKey(context);

        Assert.Equal("192.0.2.25", key);
    }

    [Fact]
    public void UsesStableFallbackWhenRemoteAddressIsUnavailable()
    {
        var context = new DefaultHttpContext();

        var key = AuthRateLimitPolicies.GetClientPartitionKey(context);

        Assert.Equal("unknown", key);
    }

    [Fact]
    public void LoginLimiterAllowsTenRequestsPerMinuteWithoutQueueing()
    {
        var options = AuthRateLimitPolicies.CreateLoginLimiterOptions();

        AssertOptions(options, 10, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void PasswordRecoveryLimitersAreStrictAndDoNotQueue()
    {
        var forgotOptions = AuthRateLimitPolicies.CreateForgotPasswordLimiterOptions();
        var resetOptions = AuthRateLimitPolicies.CreateResetPasswordLimiterOptions();

        AssertOptions(forgotOptions, 3, TimeSpan.FromMinutes(15));
        AssertOptions(resetOptions, 5, TimeSpan.FromMinutes(15));
    }

    private static void AssertOptions(
        FixedWindowRateLimiterOptions options,
        int permitLimit,
        TimeSpan window)
    {
        Assert.Equal(permitLimit, options.PermitLimit);
        Assert.Equal(window, options.Window);
        Assert.Equal(0, options.QueueLimit);
        Assert.True(options.AutoReplenishment);
    }
}
