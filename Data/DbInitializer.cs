using ANU_Admissions.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ANU_Admissions.Data;

public static class DbInitializer
{
    public static async Task Initialize(IServiceProvider serviceProvider)
    {
        var context = serviceProvider.GetRequiredService<AppDbContext>();
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();

        // Production/development use real relational migrations. Test hosts can
        // replace SQL Server with EF's in-memory provider and still initialize
        // the complete Identity/domain model without relational-only APIs.
        if (context.Database.IsRelational())
        {
            await context.Database.MigrateAsync();
        }
        else
        {
            await context.Database.EnsureCreatedAsync();
        }

        // Seed roles
        string[] roleNames = { "Admin", "Student" };
        foreach (var roleName in roleNames)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }

        // Seed the admin user from configuration. Credentials MUST come from a
        // secret store — User Secrets in development, environment variables in
        // production — and are NEVER hardcoded here or in appsettings.json.
        // This only CREATES the admin when missing; it never resets an existing
        // admin's password, so current logins are unaffected.
        var logger = serviceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("DbInitializer");

        var adminEmail = configuration["AdminSeed:Email"];
        var adminPassword = configuration["AdminSeed:Password"];
        var adminFullName = configuration["AdminSeed:FullName"] ?? "مسؤول النظام";

        // Fail safe: if no credentials are configured, do NOT fall back to a
        // weak/default admin. Skip seeding and warn instead, so a fresh clone
        // can never come up with a guessable admin account.
        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
        {
            logger.LogWarning(
                "AdminSeed:Email/Password are not configured — skipping admin seeding. " +
                "Set them via User Secrets (development) or environment variables (production).");
            return;
        }

        var adminUser = await userManager.FindByEmailAsync(adminEmail);

        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FullName = adminFullName,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(adminUser, adminPassword);

            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
                logger.LogInformation("Seeded admin user '{Email}'.", adminEmail);
            }
            else
            {
                // Most likely the configured password failed the Identity policy.
                // Don't leave a half-created account; surface the reason clearly.
                logger.LogError("Failed to seed admin user '{Email}': {Errors}",
                    adminEmail,
                    string.Join("; ", result.Errors.Select(e => e.Description)));
            }
        }
    }
}
