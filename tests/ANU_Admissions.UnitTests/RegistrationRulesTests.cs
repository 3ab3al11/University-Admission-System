using ANU_Admissions.Data;
using ANU_Admissions.Models;
using ANU_Admissions.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ANU_Admissions.UnitTests;

public class RegistrationRulesTests
{
    [Theory]
    [InlineData(" 01012345678 ", "01012345678")]
    [InlineData("01012345678", "01012345678")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void NormalizesPhoneValues(string? input, string? expected)
    {
        Assert.Equal(expected, RegistrationRules.NormalizePhone(input));
    }

    [Fact]
    public void RecognizesPhoneIndexConflictThroughInnerExceptions()
    {
        var exception = new DbUpdateException(
            "Saving failed",
            new InvalidOperationException(
                $"Duplicate key in {RegistrationRules.StudentPhoneIndexName}"));

        Assert.True(RegistrationRules.IsStudentPhoneConflict(exception));
    }

    [Fact]
    public void DoesNotTreatUnrelatedDatabaseErrorsAsPhoneConflicts()
    {
        var exception = new DbUpdateException(
            "Saving failed",
            new InvalidOperationException("Connection timeout"));

        Assert.False(RegistrationRules.IsStudentPhoneConflict(exception));
    }

    [Fact]
    public void UserPhoneIndexIsUniqueAndFiltered()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(ApplicationUser))!;
        var property = entity.FindProperty(nameof(ApplicationUser.PhoneNumber))!;
        var index = entity.GetIndexes().Single(candidate =>
            candidate.Properties.Count == 1
            && candidate.Properties[0].Name == nameof(ApplicationUser.PhoneNumber));

        Assert.Equal(32, property.GetMaxLength());
        Assert.True(index.IsUnique);
        Assert.Equal(RegistrationRules.StudentPhoneIndexName, index.GetDatabaseName());
        Assert.Equal(
            "[PhoneNumber] IS NOT NULL AND [PhoneNumber] <> ''",
            index.GetFilter());
    }

    [Fact]
    public void ParentPhoneIndexSupportsSerializableCountCheck()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(ApplicationUser))!;
        var property = entity.FindProperty(nameof(ApplicationUser.ParentPhoneNumber))!;
        var index = entity.GetIndexes().Single(candidate =>
            candidate.Properties.Count == 1
            && candidate.Properties[0].Name == nameof(ApplicationUser.ParentPhoneNumber));

        Assert.Equal(32, property.GetMaxLength());
        Assert.False(index.IsUnique);
        Assert.Equal("IX_AspNetUsers_ParentPhoneNumber", index.GetDatabaseName());
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=ModelOnly;Trusted_Connection=True")
            .Options;

        return new AppDbContext(options);
    }
}
