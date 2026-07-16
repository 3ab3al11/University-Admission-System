using ANU_Admissions.Data;
using ANU_Admissions.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace ANU_Admissions.UnitTests;

public class SeedDataTimestampsTests
{
    [Fact]
    public void CollegeSeedTimestampsAreStableUtcValues()
    {
        var firstRead = Enumerable.Range(1, 8)
            .Select(SeedDataTimestamps.CollegeCreatedAt)
            .ToArray();
        var secondRead = Enumerable.Range(1, 8)
            .Select(SeedDataTimestamps.CollegeCreatedAt)
            .ToArray();

        Assert.Equal(firstRead, secondRead);
        Assert.All(firstRead, value => Assert.Equal(DateTimeKind.Utc, value.Kind));
    }

    [Fact]
    public void StableTimestampHelperOverridesRuntimeEntityDefaults()
    {
        var colleges = Enumerable.Range(1, 8)
            .Select(id => new College { Id = id })
            .ToArray();

        var result = SeedDataTimestamps.ApplyToSeededColleges(colleges);

        Assert.Same(colleges, result);
        Assert.All(result, college =>
            Assert.Equal(
                SeedDataTimestamps.CollegeCreatedAt(college.Id),
                college.CreatedAt));
    }

    [Fact]
    public void EfModelContainsOnlyTheStableSeedTimestamps()
    {
        using var context = CreateContext();
        var designTimeModel = context.GetService<IDesignTimeModel>().Model;
        var collegeSeeds = designTimeModel.FindEntityType(typeof(College))!
            .GetSeedData()
            .ToDictionary(
                row => (int)row[nameof(College.Id)]!,
                row => (DateTime)row[nameof(College.CreatedAt)]!);

        Assert.Equal(8, collegeSeeds.Count);
        foreach (var collegeId in Enumerable.Range(1, 8))
        {
            Assert.Equal(
                SeedDataTimestamps.CollegeCreatedAt(collegeId),
                collegeSeeds[collegeId]);
        }

        var settingSeed = Assert.Single(
            designTimeModel.FindEntityType(typeof(SystemSetting))!.GetSeedData());
        Assert.Equal(
            SeedDataTimestamps.AdmissionsSettingLastModified,
            (DateTime)settingSeed[nameof(SystemSetting.LastModified)]!);
    }

    [Fact]
    public void UnknownSeededCollegeIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SeedDataTimestamps.CollegeCreatedAt(99));
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=ModelOnly;Trusted_Connection=True")
            .Options;

        return new AppDbContext(options);
    }
}
