using System.Net;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ANU_Admissions.UnitTests;

public sealed class SecurityIntegrationTests
    : IClassFixture<AuthorizationWebApplicationFactory>
{
    private readonly AuthorizationWebApplicationFactory _factory;

    public SecurityIntegrationTests(AuthorizationWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Admin/Dashboard")]
    public async Task SecurityHeadersArePresentOnPublicAndRedirectResponses(string path)
    {
        using var client = CreateClient();

        using var response = await client.GetAsync(
            path,
            TestContext.Current.CancellationToken);

        AssertHeader(response, "X-Content-Type-Options", "nosniff");
        AssertHeader(response, "X-Frame-Options", "DENY");
        AssertHeader(response, "Referrer-Policy", "strict-origin-when-cross-origin");
        AssertHeader(response, "Permissions-Policy", "camera=(), microphone=(), geolocation=()");

        var requestId = GetHeader(response, "X-Request-ID");
        Assert.Matches("^[0-9a-f]{32}$", requestId);

        var policy = GetHeader(response, "Content-Security-Policy");
        Assert.Contains("default-src 'self';", policy);
        Assert.Contains("script-src-attr 'none';", policy);
        Assert.Contains("style-src-attr 'unsafe-inline';", policy);
        Assert.Contains("frame-ancestors 'none';", policy);
        Assert.Contains("object-src 'none';", policy);
        Assert.DoesNotContain("script-src 'self' 'unsafe-inline'", policy);

        var nonceMatch = Regex.Match(
            policy,
            "script-src 'self' 'nonce-(?<nonce>[A-Za-z0-9+/]{43}=)';");
        Assert.True(nonceMatch.Success, "CSP did not contain a 256-bit script nonce.");
        Assert.Contains(
            $"style-src 'self' 'nonce-{nonceMatch.Groups["nonce"].Value}'",
            policy);
    }

    [Fact]
    public async Task ContentSecurityPolicyNonceIsRenderedAndChangesPerRequest()
    {
        using var client = CreateClient();

        using var first = await client.GetAsync(
            "/",
            TestContext.Current.CancellationToken);
        using var second = await client.GetAsync(
            "/",
            TestContext.Current.CancellationToken);

        var firstNonce = GetNonce(first);
        var secondNonce = GetNonce(second);
        var body = await first.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);

        Assert.NotEqual(firstNonce, secondNonce);
        Assert.Contains(
            $"nonce=\"{HtmlEncoder.Default.Encode(firstNonce)}\"",
            body);
    }

    [Fact]
    public async Task UnknownRouteReturnsLocalizedNotFoundPageWithOriginalStatus()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync(
            "/route-that-does-not-exist",
            TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("الصفحة غير موجودة", WebUtility.HtmlDecode(body));
        Assert.Matches("^[0-9a-f]{32}$", GetHeader(response, "X-Request-ID"));
    }

    [Fact]
    public async Task ErrorPageShowsResponseRequestIdWithoutDevelopmentInstructions()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync(
            "/Home/Error",
            TestContext.Current.CancellationToken);
        var requestId = GetHeader(response, "X-Request-ID");
        var body = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(requestId, body);
        Assert.DoesNotContain("Development Mode", body);
    }

    [Fact]
    public async Task AuthenticatedDashboardCannotBeStoredInBrowserCache()
    {
        using var client = CreateClient("Admin");

        using var response = await client.GetAsync(
            "/Admin/Dashboard",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertHeader(response, "Cache-Control", "no-store, no-cache, max-age=0");
        AssertHeader(response, "Pragma", "no-cache");
        AssertHeader(response, "Expires", "Thu, 01 Jan 1970 00:00:00 GMT");
    }

    [Theory]
    [InlineData("/Account/Login", null)]
    [InlineData("/Admin/ToggleAdmissions", "Admin")]
    public async Task UnsafeRequestWithoutAntiforgeryTokenIsRejected(
        string path,
        string? role)
    {
        using var client = CreateClient(role);
        using var content = new FormUrlEncodedContent([]);

        using var response = await client.PostAsync(
            path,
            content,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task AnonymousHealthEndpointReturnsMinimalUncachedStatus(string path)
    {
        using var client = CreateClient();

        using var response = await client.GetAsync(
            path,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        AssertHeader(response, "Cache-Control", "no-store, no-cache, max-age=0");
        var body = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);
        Assert.Equal("{\"status\":\"Healthy\"}", body);
    }

    private HttpClient CreateClient(string? role = null)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        if (role != null)
        {
            client.DefaultRequestHeaders.Add(
                HeaderTestAuthenticationHandler.RoleHeader,
                role);
        }

        return client;
    }

    private static void AssertHeader(
        HttpResponseMessage response,
        string name,
        string expectedValue)
    {
        var found = response.Headers.TryGetValues(name, out var values)
            || response.Content.Headers.TryGetValues(name, out values);
        Assert.True(
            found,
            $"Response did not contain the {name} header.");
        Assert.Contains(expectedValue, values!);
    }

    private static string GetHeader(HttpResponseMessage response, string name)
    {
        var found = response.Headers.TryGetValues(name, out var values)
            || response.Content.Headers.TryGetValues(name, out values);
        Assert.True(found, $"Response did not contain the {name} header.");
        return Assert.Single(values!);
    }

    private static string GetNonce(HttpResponseMessage response)
    {
        var policy = GetHeader(response, "Content-Security-Policy");
        var match = Regex.Match(policy, "script-src 'self' 'nonce-(?<nonce>[^']+)';");
        Assert.True(match.Success, "CSP did not contain a script nonce.");
        return match.Groups["nonce"].Value;
    }
}
