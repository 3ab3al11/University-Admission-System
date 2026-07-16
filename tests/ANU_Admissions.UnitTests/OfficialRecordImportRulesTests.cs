using ANU_Admissions.Services;
using Xunit;

namespace ANU_Admissions.UnitTests;

public class OfficialRecordImportRulesTests
{
    [Theory]
    [InlineData("ناجح", true)]
    [InlineData("  ناجح   دور أول  ", true)]
    [InlineData("غير ناجح", false)]
    [InlineData("راسب", false)]
    [InlineData("غياب", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void EligibilityRequiresPositiveSuccessStatus(string? status, bool expected)
    {
        Assert.Equal(expected, OfficialRecordImportRules.IsEligibleStatus(status));
    }

    [Theory]
    [InlineData("650.5", 650.5)]
    [InlineData(500, 500)]
    [InlineData(99.25, 99.25)]
    public void ParsesSupportedScoreValues(object value, double expected)
    {
        var parsed = OfficialRecordImportRules.TryParseScore(value, out var result);

        Assert.True(parsed);
        Assert.Equal((decimal)expected, result);
    }

    [Fact]
    public void RejectsInvalidScoreValue()
    {
        Assert.False(OfficialRecordImportRules.TryParseScore("not-a-score", out _));
    }
}
