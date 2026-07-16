using System.Data;
using System.Diagnostics;
using ANU_Admissions.Data;
using ANU_Admissions.ViewModels;
using ExcelDataReader;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ANU_Admissions.Services;

public enum OfficialRecordsImportStatus
{
    Completed,
    RecordsAlreadyExist,
    InvalidFileType,
    EmptyFile,
    FileTooLarge,
    InvalidFileContent,
    UnsafeArchive,
    Busy
}

public sealed record OfficialRecordsImportOutcome(
    OfficialRecordsImportStatus Status,
    ImportOfficialRecordsResultViewModel? Result = null);

public interface IOfficialRecordsImportService
{
    Task<OfficialRecordsImportOutcome> ImportAsync(
        IFormFile excelFile,
        decimal maxScore,
        string? importBatch,
        bool abortOnAnyOverflow,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Reads, validates, and bulk-inserts official student results. The complete
/// import uses a SQL transaction, so an aborted file never leaves partial rows.
/// </summary>
public sealed class OfficialRecordsImportService : IOfficialRecordsImportService
{
    private const int BulkBatchSize = 5000;
    private readonly AppDbContext _context;
    private readonly IOfficialRecordsFileValidator _fileValidator;
    private readonly ILogger<OfficialRecordsImportService> _logger;

    public OfficialRecordsImportService(
        AppDbContext context,
        IOfficialRecordsFileValidator fileValidator,
        ILogger<OfficialRecordsImportService> logger)
    {
        _context = context;
        _fileValidator = fileValidator;
        _logger = logger;
    }

    public async Task<OfficialRecordsImportOutcome> ImportAsync(
        IFormFile excelFile,
        decimal maxScore,
        string? importBatch,
        bool abortOnAnyOverflow,
        CancellationToken cancellationToken = default)
    {
        var fileValidation = await _fileValidator.ValidateAsync(
            excelFile,
            cancellationToken);
        if (!fileValidation.IsValid)
        {
            return new OfficialRecordsImportOutcome(
                MapFileValidationStatus(fileValidation.Status));
        }

        var stopwatch = Stopwatch.StartNew();
        var result = new ImportOfficialRecordsResultViewModel
        {
            MaxScoreUsed = maxScore,
            ImportBatch = string.IsNullOrWhiteSpace(importBatch)
                ? $"Import-{DateTime.UtcNow:yyyyMMdd-HHmmss}"
                : importBatch.Trim()
        };

        var seenInFile = new HashSet<string>(StringComparer.Ordinal);

        var connectionString = _context.Database.GetConnectionString()
            ?? throw new InvalidOperationException("Connection string not configured.");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            if (!await OfficialRecordsDatabaseLock.TryAcquireAsync(
                    connection,
                    transaction,
                    cancellationToken))
            {
                return new OfficialRecordsImportOutcome(
                    OfficialRecordsImportStatus.Busy);
            }

            // Check inside the same locked transaction as the bulk copy. Two
            // simultaneous imports can no longer both observe an empty table.
            if (await OfficialRecordsDatabaseLock.AnyOfficialRecordsWithRangeLockAsync(
                    connection,
                    transaction,
                    cancellationToken))
            {
                return new OfficialRecordsImportOutcome(
                    OfficialRecordsImportStatus.RecordsAlreadyExist);
            }

            using var bulkCopy = new SqlBulkCopy(
                connection,
                SqlBulkCopyOptions.CheckConstraints | SqlBulkCopyOptions.TableLock,
                transaction)
            {
                DestinationTableName = "OfficialStudentRecords",
                BatchSize = BulkBatchSize,
                BulkCopyTimeout = 600
            };
            ConfigureBulkCopyMappings(bulkCopy);

            var batch = CreateOfficialRecordsTable();
            using var stream = excelFile.OpenReadStream();
            using var reader = ExcelReaderFactory.CreateReader(stream);

            var rowNumber = 0;
            var headerConsumed = false;

            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                rowNumber++;

                if (!headerConsumed)
                {
                    headerConsumed = true;
                    continue;
                }

                result.TotalRowsRead++;

                var seatNumber = reader.GetValue(0)?.ToString()?.Trim();
                var fullName = reader.GetValue(1)?.ToString()?.Trim();
                var scoreRaw = reader.GetValue(2);
                var statusDescription = reader.FieldCount > 4
                    ? reader.GetValue(4)?.ToString()?.Trim()
                    : null;

                if (string.IsNullOrEmpty(seatNumber) || string.IsNullOrEmpty(fullName))
                {
                    result.SkippedMissingData++;
                    AddError(result, rowNumber, seatNumber, "رقم جلوس أو اسم مفقود");
                    continue;
                }

                if (!OfficialRecordImportRules.TryParseScore(scoreRaw, out var totalScore)
                    || totalScore < 0)
                {
                    result.SkippedMissingData++;
                    AddError(result, rowNumber, seatNumber, "الدرجة مفقودة أو غير صحيحة");
                    continue;
                }

                if (!seenInFile.Add(seatNumber))
                {
                    result.SkippedDuplicateInFile++;
                    AddError(result, rowNumber, seatNumber, "رقم جلوس مكرر في نفس الملف");
                    continue;
                }

                var percentage = Math.Round(totalScore / maxScore * 100m, 2);
                if (percentage > 100m)
                {
                    if (abortOnAnyOverflow)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        result.Aborted = true;
                        result.AbortReason =
                            $"النهاية العظمى المُدخلة ({maxScore}) غير مناسبة لهذا الملف. " +
                            $"رقم الجلوس {seatNumber} درجته {totalScore} ونسبته {percentage}% (> 100%). " +
                            "تم إلغاء الاستيراد بالكامل ولم تُحفظ أي بيانات.";
                        result.Duration = stopwatch.Elapsed;
                        return new OfficialRecordsImportOutcome(
                            OfficialRecordsImportStatus.Completed,
                            result);
                    }

                    result.SkippedOverMaxScore++;
                    AddError(result, rowNumber, seatNumber,
                        $"النسبة {percentage}% > 100% (MaxScore غير مناسب)");
                    continue;
                }

                var isEligible = OfficialRecordImportRules
                    .IsEligibleStatus(statusDescription);

                var row = batch.NewRow();
                row["SeatNumber"] = seatNumber;
                row["FullName"] = fullName;
                row["TotalScore"] = totalScore;
                row["MaxScore"] = maxScore;
                row["Percentage"] = percentage;
                row["EquivalentPercentage"] = percentage;
                row["StatusDescription"] = (object?)statusDescription ?? DBNull.Value;
                row["IsEligible"] = isEligible;
                row["NationalId"] = DBNull.Value;
                row["ImportedAt"] = DateTime.UtcNow;
                row["ImportBatch"] = result.ImportBatch!;
                batch.Rows.Add(row);

                if (isEligible) result.Imported++;
                else result.NotEligibleImported++;

                if (batch.Rows.Count >= BulkBatchSize)
                {
                    await bulkCopy.WriteToServerAsync(batch, cancellationToken);
                    batch.Clear();
                }
            }

            if (batch.Rows.Count > 0)
            {
                await bulkCopy.WriteToServerAsync(batch, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            result.Duration = stopwatch.Elapsed;
        }
        catch (OperationCanceledException)
        {
            try { await transaction.RollbackAsync(CancellationToken.None); } catch { }
            throw;
        }
        catch (Exception exception)
        {
            try { await transaction.RollbackAsync(CancellationToken.None); } catch { }
            _logger.LogError(exception, "Failed to import official records");
            result.Aborted = true;
            result.AbortReason = "حدث خطأ أثناء تنفيذ العملية. يرجى المحاولة مرة أخرى.";
            result.Duration = stopwatch.Elapsed;
        }

        return new OfficialRecordsImportOutcome(
            OfficialRecordsImportStatus.Completed,
            result);
    }

    private static OfficialRecordsImportStatus MapFileValidationStatus(
        OfficialRecordsFileValidationStatus status) => status switch
        {
            OfficialRecordsFileValidationStatus.Empty =>
                OfficialRecordsImportStatus.EmptyFile,
            OfficialRecordsFileValidationStatus.TooLarge =>
                OfficialRecordsImportStatus.FileTooLarge,
            OfficialRecordsFileValidationStatus.InvalidExtension =>
                OfficialRecordsImportStatus.InvalidFileType,
            OfficialRecordsFileValidationStatus.UnsafeArchive =>
                OfficialRecordsImportStatus.UnsafeArchive,
            _ => OfficialRecordsImportStatus.InvalidFileContent
        };

    private static void AddError(
        ImportOfficialRecordsResultViewModel result,
        int rowNumber,
        string? seatNumber,
        string reason)
    {
        if (result.FirstErrors.Count >= 50) return;

        result.FirstErrors.Add(new ImportRowError
        {
            RowNumber = rowNumber,
            SeatNumber = seatNumber,
            Reason = reason
        });
    }

    private static DataTable CreateOfficialRecordsTable()
    {
        var table = new DataTable();
        table.Columns.Add("SeatNumber", typeof(string));
        table.Columns.Add("FullName", typeof(string));
        table.Columns.Add("TotalScore", typeof(decimal));
        table.Columns.Add("MaxScore", typeof(decimal));
        table.Columns.Add("Percentage", typeof(decimal));
        table.Columns.Add("EquivalentPercentage", typeof(decimal));
        table.Columns.Add("StatusDescription", typeof(string));
        table.Columns.Add("IsEligible", typeof(bool));
        table.Columns.Add("NationalId", typeof(string));
        table.Columns.Add("ImportedAt", typeof(DateTime));
        table.Columns.Add("ImportBatch", typeof(string));
        return table;
    }

    private static void ConfigureBulkCopyMappings(SqlBulkCopy bulkCopy)
    {
        bulkCopy.ColumnMappings.Add("SeatNumber", "SeatNumber");
        bulkCopy.ColumnMappings.Add("FullName", "FullName");
        bulkCopy.ColumnMappings.Add("TotalScore", "TotalScore");
        bulkCopy.ColumnMappings.Add("MaxScore", "MaxScore");
        bulkCopy.ColumnMappings.Add("Percentage", "Percentage");
        bulkCopy.ColumnMappings.Add("EquivalentPercentage", "EquivalentPercentage");
        bulkCopy.ColumnMappings.Add("StatusDescription", "StatusDescription");
        bulkCopy.ColumnMappings.Add("IsEligible", "IsEligible");
        bulkCopy.ColumnMappings.Add("NationalId", "NationalId");
        bulkCopy.ColumnMappings.Add("ImportedAt", "ImportedAt");
        bulkCopy.ColumnMappings.Add("ImportBatch", "ImportBatch");
    }
}

/// <summary>Pure import rules shared by the importer and its unit tests.</summary>
public static class OfficialRecordImportRules
{
    public static bool IsEligibleStatus(string? statusDescription)
    {
        if (string.IsNullOrWhiteSpace(statusDescription)) return false;

        var normalized = string.Join(' ', statusDescription
            .Split((char[]?)null,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return normalized.Equals("ناجح", StringComparison.Ordinal)
            || normalized.StartsWith("ناجح ", StringComparison.Ordinal);
    }

    public static bool TryParseScore(object? value, out decimal result)
    {
        result = 0m;
        if (value is null) return false;
        if (value is decimal decimalValue) { result = decimalValue; return true; }
        if (value is double doubleValue) { result = (decimal)doubleValue; return true; }
        if (value is float floatValue) { result = (decimal)floatValue; return true; }
        if (value is int intValue) { result = intValue; return true; }
        if (value is long longValue) { result = longValue; return true; }

        return decimal.TryParse(
            value.ToString(),
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out result);
    }
}
