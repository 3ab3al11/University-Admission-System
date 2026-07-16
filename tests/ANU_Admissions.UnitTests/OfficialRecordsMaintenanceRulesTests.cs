using ANU_Admissions.Services;
using Xunit;

namespace ANU_Admissions.UnitTests;

public class OfficialRecordsMaintenanceRulesTests
{
    [Fact]
    public void DeleteIsAllowedOnlyWithoutLinkedProfiles()
    {
        Assert.True(OfficialRecordsMaintenanceRules.CanDelete(0));
        Assert.False(OfficialRecordsMaintenanceRules.CanDelete(1));
        Assert.False(OfficialRecordsMaintenanceRules.CanDelete(500));
    }

    [Fact]
    public void NegativeLinkedProfileCountIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            OfficialRecordsMaintenanceRules.CanDelete(-1));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(-1, false)]
    [InlineData(-2, false)]
    [InlineData(-3, false)]
    [InlineData(-999, false)]
    public void InterpretsSqlApplicationLockResult(int result, bool expected)
    {
        Assert.Equal(expected, OfficialRecordsDatabaseLock.IsAcquiredResult(result));
    }

    [Fact]
    public void ApplicationLockIsExclusiveAndOwnedByTransaction()
    {
        var sql = OfficialRecordsDatabaseLock.AcquireLockSql;

        Assert.Contains("sp_getapplock", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("'Exclusive'", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("'Transaction'", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(nameof(OfficialRecordsDatabaseLock.OfficialRecordsRangeLockSql))]
    [InlineData(nameof(OfficialRecordsDatabaseLock.LinkedProfilesRangeLockSql))]
    public void GuardQueriesUseUpdateAndSerializableRangeLocks(string queryName)
    {
        var sql = queryName == nameof(OfficialRecordsDatabaseLock.OfficialRecordsRangeLockSql)
            ? OfficialRecordsDatabaseLock.OfficialRecordsRangeLockSql
            : OfficialRecordsDatabaseLock.LinkedProfilesRangeLockSql;

        Assert.Contains("UPDLOCK", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("HOLDLOCK", sql, StringComparison.OrdinalIgnoreCase);
    }
}
