using ANU_Admissions.Services;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Xunit;

namespace ANU_Admissions.UnitTests;

public class PasswordResetLinkFactoryTests
{
    [Theory]
    [InlineData("https://admissions.example.edu", true, true)]
    [InlineData("http://localhost:5023", false, true)]
    [InlineData("http://admissions.example.edu", true, false)]
    [InlineData("ftp://admissions.example.edu", false, false)]
    [InlineData("not-a-url", false, false)]
    [InlineData("", false, false)]
    public void ValidatesSchemeAndRequiredHttps(
        string value,
        bool requireHttps,
        bool expected)
    {
        Assert.Equal(expected, PublicUrlRules.IsValidBaseUrl(value, requireHttps));
    }

    [Theory]
    [InlineData("https://user:pass@admissions.example.edu")]
    [InlineData("https://admissions.example.edu/app")]
    [InlineData("https://admissions.example.edu?source=test")]
    [InlineData("https://admissions.example.edu#fragment")]
    public void RejectsOriginsWithUnexpectedComponents(string value)
    {
        Assert.False(PublicUrlRules.IsValidBaseUrl(value, requireHttps: true));
    }

    [Fact]
    public void BuildsEncodedLinkFromConfiguredOrigin()
    {
        var factory = CreateFactory("https://trusted.example.edu");

        var result = factory.Create("student+one@example.edu", "token/with?symbols=");

        var uri = new Uri(result);
        var query = QueryHelpers.ParseQuery(uri.Query);
        Assert.Equal("https", uri.Scheme);
        Assert.Equal("trusted.example.edu", uri.Host);
        Assert.Equal("/Account/ResetPassword", uri.AbsolutePath);
        Assert.Equal("student+one@example.edu", query["email"].ToString());
        Assert.Equal("token/with?symbols=", query["token"].ToString());
    }

    [Fact]
    public void RejectsInvalidConfigurationEvenWhenConstructedDirectly()
    {
        Assert.Throws<InvalidOperationException>(() => CreateFactory("https://example.edu/app"));
    }

    private static PasswordResetLinkFactory CreateFactory(string publicBaseUrl) =>
        new(Options.Create(new ApplicationUrlOptions
        {
            PublicBaseUrl = publicBaseUrl
        }));
}
