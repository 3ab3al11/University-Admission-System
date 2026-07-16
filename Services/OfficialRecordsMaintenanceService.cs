using System.Data;
using System.Globalization;
using ANU_Admissions.Data;
using ANU_Admissions.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ANU_Admissions.Services;

public sealed record OfficialRecordsMaintenanceActor(string Name, string IpAddress);

public enum OfficialRecordsDeleteStatus
{
    Deleted,
    NoRecords,
    LinkedProfilesExist,
    Busy,
    Failed
}

public sealed record OfficialRecordsDeleteOutcome(
    OfficialRecordsDeleteStatus Status,
    int DeletedRecords = 0,
    int LinkedProfiles = 0);

public enum VerifiedApplicationsResetStatus
{
    Reset,
    NoLinkedProfiles,
    Busy,
    Failed
}

public sealed record VerifiedApplicationsResetOutcome(
    VerifiedApplicationsResetStatus Status,
    int ResetProfiles = 0,
    int DeletedPreferences = 0,
    int DeletedAllocations = 0);

public interface IOfficialRecordsMaintenanceService
{
    Task<OfficialRecordsDeleteOutcome> DeleteAllAsync(
        OfficialRecordsMaintenanceActor actor,
        CancellationToken cancellationToken = default);

    Task<VerifiedApplicationsResetOutcome> ResetVerifiedApplicationsAsync(
        OfficialRecordsMaintenanceActor actor,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Serializes destructive official-record operations across application
/// instances. Range locks prevent a verification link from appearing between
/// the linked-student guard and the final delete.
/// </summary>
public sealed class OfficialRecordsMaintenanceService
    : IOfficialRecordsMaintenanceService
{
    private readonly AppDbContext _context;
    private readonly ILogger<OfficialRecordsMaintenanceService> _logger;

    public OfficialRecordsMaintenanceService(
        AppDbContext context,
        ILogger<OfficialRecordsMaintenanceService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<OfficialRecordsDeleteOutcome> DeleteAllAsync(
        OfficialRecordsMaintenanceActor actor,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var transaction = await _context.Database
                .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var (connection, sqlTransaction) = GetSqlTransaction(transaction);

            if (!await OfficialRecordsDatabaseLock.TryAcquireAsync(
                    connection, sqlTransaction, cancellationToken))
            {
                return new OfficialRecordsDeleteOutcome(OfficialRecordsDeleteStatus.Busy);
            }

            var linkedProfiles = await OfficialRecordsDatabaseLock
                .CountLinkedProfilesWithRangeLockAsync(
                    connection,
                    sqlTransaction,
                    cancellationToken);

            if (!OfficialRecordsMaintenanceRules.CanDelete(linkedProfiles))
            {
                return new OfficialRecordsDeleteOutcome(
                    OfficialRecordsDeleteStatus.LinkedProfilesExist,
                    LinkedProfiles: linkedProfiles);
            }

            var deleted = await _context.OfficialStudentRecords
                .ExecuteDeleteAsync(cancellationToken);
            if (deleted == 0)
            {
                return new OfficialRecordsDeleteOutcome(
                    OfficialRecordsDeleteStatus.NoRecords);
            }

            AddAudit(
                actor,
                "Delete Official Records",
                $"Deleted {deleted} imported official record(s)");
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new OfficialRecordsDeleteOutcome(
                OfficialRecordsDeleteStatus.Deleted,
                DeletedRecords: deleted);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to delete official records atomically");
            return new OfficialRecordsDeleteOutcome(OfficialRecordsDeleteStatus.Failed);
        }
    }

    public async Task<VerifiedApplicationsResetOutcome> ResetVerifiedApplicationsAsync(
        OfficialRecordsMaintenanceActor actor,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var transaction = await _context.Database
                .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var (connection, sqlTransaction) = GetSqlTransaction(transaction);

            if (!await OfficialRecordsDatabaseLock.TryAcquireAsync(
                    connection, sqlTransaction, cancellationToken))
            {
                return new VerifiedApplicationsResetOutcome(
                    VerifiedApplicationsResetStatus.Busy);
            }

            var linkedProfiles = await OfficialRecordsDatabaseLock
                .CountLinkedProfilesWithRangeLockAsync(
                    connection,
                    sqlTransaction,
                    cancellationToken);
            if (linkedProfiles == 0)
            {
                return new VerifiedApplicationsResetOutcome(
                    VerifiedApplicationsResetStatus.NoLinkedProfiles);
            }

            var preferencesDeleted = await _context.Preferences
                .Where(preference => preference.StudentProfile.OfficialRecordId != null)
                .ExecuteDeleteAsync(cancellationToken);

            var allocationsDeleted = await _context.Allocations
                .Where(allocation => allocation.StudentProfile.OfficialRecordId != null)
                .ExecuteDeleteAsync(cancellationToken);

            var resetProfiles = await _context.StudentProfiles
                .Where(profile => profile.OfficialRecordId != null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(profile => profile.OfficialRecordId, (int?)null)
                    .SetProperty(profile => profile.NationalId, (string?)null)
                    .SetProperty(profile => profile.SeatNumber, (string?)null)
                    .SetProperty(profile => profile.TotalScore, 0m)
                    .SetProperty(profile => profile.Percentage, 0m)
                    .SetProperty(profile => profile.EquivalentPercentage, 0m),
                    cancellationToken);

            AddAudit(
                actor,
                "Reset verified applications",
                $"Reset {resetProfiles} profile(s), deleted " +
                $"{preferencesDeleted} preference(s) and {allocationsDeleted} allocation(s)");
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new VerifiedApplicationsResetOutcome(
                VerifiedApplicationsResetStatus.Reset,
                resetProfiles,
                preferencesDeleted,
                allocationsDeleted);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to reset verified applications atomically");
            return new VerifiedApplicationsResetOutcome(
                VerifiedApplicationsResetStatus.Failed);
        }
    }

    private static (SqlConnection Connection, SqlTransaction Transaction) GetSqlTransaction(
        IDbContextTransaction transaction) =>
        ((SqlConnection)transaction.GetDbTransaction().Connection!,
         (SqlTransaction)transaction.GetDbTransaction());

    private void AddAudit(
        OfficialRecordsMaintenanceActor actor,
        string action,
        string details)
    {
        _context.AuditLogs.Add(new AuditLog
        {
            Action = action,
            PerformedBy = string.IsNullOrWhiteSpace(actor.Name) ? "Admin" : actor.Name,
            PerformedAt = DateTime.UtcNow,
            Details = details,
            IpAddress = string.IsNullOrWhiteSpace(actor.IpAddress)
                ? "Unknown"
                : actor.IpAddress
        });
    }
}

public static class OfficialRecordsDatabaseLock
{
    public const string ResourceName = "ANU_Admissions:OfficialRecordsMaintenance";
    public const int TimeoutMilliseconds = 5_000;
    public const string AcquireLockSql = """
        DECLARE @result int;
        EXEC @result = sys.sp_getapplock
            @Resource = @resource,
            @LockMode = 'Exclusive',
            @LockOwner = 'Transaction',
            @LockTimeout = @timeout;
        SELECT @result;
        """;
    public const string OfficialRecordsRangeLockSql = """
        SELECT CASE WHEN EXISTS
        (
            SELECT 1
            FROM OfficialStudentRecords WITH (UPDLOCK, HOLDLOCK)
        ) THEN 1 ELSE 0 END;
        """;
    public const string LinkedProfilesRangeLockSql = """
        SELECT COUNT_BIG(*)
        FROM StudentProfiles WITH (UPDLOCK, HOLDLOCK)
        WHERE OfficialRecordId IS NOT NULL;
        """;

    public static bool IsAcquiredResult(int result) => result >= 0;

    public static async Task<bool> TryAcquireAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = AcquireLockSql;
        command.Parameters.AddWithValue("@resource", ResourceName);
        command.Parameters.AddWithValue("@timeout", TimeoutMilliseconds);

        var result = Convert.ToInt32(
            await command.ExecuteScalarAsync(cancellationToken),
            CultureInfo.InvariantCulture);
        return IsAcquiredResult(result);
    }

    public static async Task<bool> AnyOfficialRecordsWithRangeLockAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = OfficialRecordsRangeLockSql;

        return Convert.ToInt32(
            await command.ExecuteScalarAsync(cancellationToken),
            CultureInfo.InvariantCulture) == 1;
    }

    public static async Task<int> CountLinkedProfilesWithRangeLockAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = LinkedProfilesRangeLockSql;

        return checked((int)Convert.ToInt64(
            await command.ExecuteScalarAsync(cancellationToken),
            CultureInfo.InvariantCulture));
    }
}

public static class OfficialRecordsMaintenanceRules
{
    public static bool CanDelete(int linkedProfiles)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(linkedProfiles);
        return linkedProfiles == 0;
    }
}
