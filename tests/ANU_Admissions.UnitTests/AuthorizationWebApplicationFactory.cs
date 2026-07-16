using System.Security.Claims;
using System.Text.Encodings.Web;
using ANU_Admissions.Data;
using ANU_Admissions.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ANU_Admissions.UnitTests;

public sealed class AuthorizationWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"AuthorizationTests-{Guid.NewGuid():N}";
    private readonly SemaphoreSlim _seedLock = new(1, 1);
    private bool _studentSeeded;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApplicationUrls:PublicBaseUrl"] = "https://localhost",
                ["AllowedHosts"] = "localhost",
                ["AdminSeed:Email"] = string.Empty,
                ["AdminSeed:Password"] = string.Empty
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<AppDbContext>();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            var sqlServerOptionConfigurations = services
                .Where(descriptor =>
                    descriptor.ServiceType.IsGenericType
                    && descriptor.ServiceType.Name == "IDbContextOptionsConfiguration`1"
                    && descriptor.ServiceType.GenericTypeArguments[0] == typeof(AppDbContext))
                .ToArray();
            foreach (var descriptor in sqlServerOptionConfigurations)
            {
                services.Remove(descriptor);
            }
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));

            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, HeaderTestAuthenticationHandler>(
                    HeaderTestAuthenticationHandler.SchemeName,
                    _ => { });

            // Keep Identity's cookie challenge/forbid schemes so redirects use
            // the real Login and AccessDenied paths. Only authentication itself
            // comes from the deterministic test header.
            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme =
                    HeaderTestAuthenticationHandler.SchemeName;
            });
        });
    }

    public async Task EnsureStudentUserAsync(CancellationToken cancellationToken)
    {
        await _seedLock.WaitAsync(cancellationToken);
        try
        {
            if (_studentSeeded)
            {
                return;
            }

            using var scope = Services.CreateScope();
            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByIdAsync(
                HeaderTestAuthenticationHandler.StudentUserId);
            if (user == null)
            {
                user = new ApplicationUser
                {
                    Id = HeaderTestAuthenticationHandler.StudentUserId,
                    UserName = "integration.student@anu.local",
                    Email = "integration.student@anu.local",
                    EmailConfirmed = true,
                    FullName = "Integration Test Student"
                };
                var createResult = await userManager.CreateAsync(user);
                EnsureSucceeded(createResult);

                var roleResult = await userManager.AddToRoleAsync(user, "Student");
                EnsureSucceeded(roleResult);
            }

            _studentSeeded = true;
        }
        finally
        {
            _seedLock.Release();
        }
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(
                "; ",
                result.Errors.Select(error => error.Description)));
        }
    }
}

public sealed class HeaderTestAuthenticationHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "IntegrationTest";
    public const string RoleHeader = "X-Test-Role";
    public const string StudentUserId = "integration-student-id";
    public const string AdminUserId = "integration-admin-id";

    public HeaderTestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(RoleHeader, out var roleValues))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var role = roleValues.ToString();
        if (role is not ("Admin" or "Student"))
        {
            return Task.FromResult(AuthenticateResult.Fail("Unknown test role."));
        }

        var userId = role == "Student" ? StudentUserId : AdminUserId;
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, $"integration-{role.ToLowerInvariant()}@anu.local"),
            new Claim(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
