using ANU_Admissions.Models;
using ANU_Admissions.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ANU_Admissions.SqlServerTests;

public sealed class SqlServerProviderIntegrationTests
{
    [Fact]
    public async Task UniqueStudentPhoneIndexRejectsConcurrentStyleDuplicate()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync(
            TestContext.Current.CancellationToken);

        await using (var firstContext = database.CreateContext())
        {
            firstContext.Users.Add(CreateUser("first", "+201000000001"));
            await firstContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var secondContext = database.CreateContext();
        secondContext.Users.Add(CreateUser("second", "+201000000001"));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() =>
            secondContext.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.Contains(
            RegistrationRules.StudentPhoneIndexName,
            exception.ToString(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OfficialRecordDeletionUsesRealSqlServerLockAndTransaction()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync(
            TestContext.Current.CancellationToken);
        await using var context = database.CreateContext();
        context.OfficialStudentRecords.Add(new OfficialStudentRecord
        {
            SeatNumber = "SQL-TEST-0001",
            FullName = "SQL Server Integration Student",
            TotalScore = 390m,
            MaxScore = 410m,
            Percentage = 95.12m,
            EquivalentPercentage = 95.12m,
            IsEligible = true,
            ImportedAt = DateTime.UtcNow,
            ImportBatch = "sql-integration-test"
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = new OfficialRecordsMaintenanceService(
            context,
            NullLogger<OfficialRecordsMaintenanceService>.Instance);
        var outcome = await service.DeleteAllAsync(
            new OfficialRecordsMaintenanceActor("Integration Test", "127.0.0.1"),
            TestContext.Current.CancellationToken);

        Assert.Equal(OfficialRecordsDeleteStatus.Deleted, outcome.Status);
        Assert.Equal(1, outcome.DeletedRecords);
        Assert.Empty(await context.OfficialStudentRecords
            .AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken));
        Assert.True(await context.AuditLogs.AsNoTracking().AnyAsync(
            log => log.Action == "Delete Official Records",
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DatabaseReadinessCheckUsesMigratedSqlServerDatabase()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync(
            TestContext.Current.CancellationToken);
        var services = new ServiceCollection();
        services.AddDbContext<ANU_Admissions.Data.AppDbContext>(options =>
            options.UseSqlServer(database.ConnectionString));
        await using var provider = services.BuildServiceProvider();
        var check = new DatabaseHealthCheck(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<DatabaseHealthCheck>.Instance);

        var result = await check.CheckHealthAsync(
            new HealthCheckContext(),
            TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    private static ApplicationUser CreateUser(string key, string phone) => new()
    {
        Id = $"sql-test-{key}",
        UserName = $"{key}@sql.test",
        NormalizedUserName = $"{key.ToUpperInvariant()}@SQL.TEST",
        Email = $"{key}@sql.test",
        NormalizedEmail = $"{key.ToUpperInvariant()}@SQL.TEST",
        FullName = $"SQL Test {key}",
        PhoneNumber = phone,
        SecurityStamp = Guid.NewGuid().ToString("N"),
        ConcurrencyStamp = Guid.NewGuid().ToString("N")
    };
}
