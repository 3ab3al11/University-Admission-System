using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ANU_Admissions.UnitTests;

public sealed class AuthorizationIntegrationTests
    : IClassFixture<AuthorizationWebApplicationFactory>
{
    private readonly AuthorizationWebApplicationFactory _factory;

    public AuthorizationIntegrationTests(AuthorizationWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/Admin/Dashboard")]
    [InlineData("/Student/Dashboard")]
    public async Task AnonymousUserIsRedirectedToLogin(string path)
    {
        using var client = CreateClient();

        using var response = await client.GetAsync(
            path,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains(
            "/Account/Login",
            response.Headers.Location?.OriginalString,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StudentCannotAccessAdminDashboard()
    {
        using var client = CreateClient("Student");

        using var response = await client.GetAsync(
            "/Admin/Dashboard",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains(
            "/Account/AccessDenied",
            response.Headers.Location?.OriginalString,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AdminCannotAccessStudentDashboard()
    {
        using var client = CreateClient("Admin");

        using var response = await client.GetAsync(
            "/Student/Dashboard",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains(
            "/Account/AccessDenied",
            response.Headers.Location?.OriginalString,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AdminCanAccessAdminDashboard()
    {
        using var client = CreateClient("Admin");

        using var response = await client.GetAsync(
            "/Admin/Dashboard",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task StudentCanAccessStudentDashboard()
    {
        await _factory.EnsureStudentUserAsync(TestContext.Current.CancellationToken);
        using var client = CreateClient("Student");

        using var response = await client.GetAsync(
            "/Student/Dashboard",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HomePageRemainsPublic()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync(
            "/",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
}
