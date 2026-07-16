using ANU_Admissions.Services;
using Xunit;

namespace ANU_Admissions.UnitTests;

public sealed class ContentSecurityPolicyTests
{
    [Fact]
    public void CreateNonceReturnsUnique256BitValues()
    {
        var first = ContentSecurityPolicy.CreateNonce();
        var second = ContentSecurityPolicy.CreateNonce();

        Assert.NotEqual(first, second);
        Assert.Equal(32, Convert.FromBase64String(first).Length);
        Assert.Equal(32, Convert.FromBase64String(second).Length);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BuildHeaderAppliesNonceAndEnvironmentSpecificHttpsRules(bool requireHttps)
    {
        const string nonce = "test-nonce";

        var policy = ContentSecurityPolicy.BuildHeader(nonce, requireHttps);

        Assert.Contains("script-src 'self' 'nonce-test-nonce';", policy);
        Assert.Contains("style-src 'self' 'nonce-test-nonce'", policy);
        Assert.Contains("script-src-attr 'none';", policy);
        Assert.Equal(requireHttps, policy.Contains("upgrade-insecure-requests;"));
        Assert.Equal(requireHttps, policy.Contains("block-all-mixed-content;"));
    }

    [Fact]
    public void BuildHeaderRejectsMissingNonce()
    {
        Assert.Throws<ArgumentException>(() =>
            ContentSecurityPolicy.BuildHeader(" ", requireHttps: false));
    }
}
