using ANU_Admissions.Services;
using Xunit;

namespace ANU_Admissions.UnitTests;

public class StudentVerificationRulesTests
{
    [Theory]
    [InlineData("علمي علوم")]
    [InlineData("علمي رياضة")]
    [InlineData("أدبي")]
    [InlineData("  أدبي  ")]
    public void AcceptsKnownStudentSections(string section)
    {
        Assert.True(StudentVerificationRules.IsAllowedSection(section));
    }

    [Theory]
    [InlineData("تجاري")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void RejectsUnknownStudentSections(string? section)
    {
        Assert.False(StudentVerificationRules.IsAllowedSection(section));
    }

    [Theory]
    [InlineData("100001", "100001", true)]
    [InlineData(" 100001 ", "100001", true)]
    [InlineData("100001", "100002", false)]
    [InlineData(null, "100001", false)]
    [InlineData("100001", "", false)]
    public void ComparesSeatNumbersExactlyAfterTrimming(
        string? officialSeatNumber,
        string? enteredSeatNumber,
        bool expected)
    {
        Assert.Equal(expected,
            StudentVerificationRules.SeatNumbersMatch(
                officialSeatNumber,
                enteredSeatNumber));
    }
}
