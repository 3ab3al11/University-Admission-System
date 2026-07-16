using System.IO.Compression;
using Microsoft.AspNetCore.Http;

namespace ANU_Admissions.Services;

public static class OfficialRecordsFileLimits
{
    public const long MaxFileSizeBytes = 100L * 1024 * 1024;
    public const long MaxRequestSizeBytes = 105L * 1024 * 1024;
    public const int MaxArchiveEntries = 10_000;
    public const long MaxUncompressedArchiveBytes = 1024L * 1024 * 1024;
    public const long CompressionRatioCheckMinimumBytes = 1024L * 1024;
    public const double MaxCompressionRatio = 250d;
}

public enum OfficialRecordsFileValidationStatus
{
    Valid,
    Empty,
    TooLarge,
    InvalidExtension,
    InvalidSignature,
    InvalidWorkbook,
    UnsafeArchive,
    Unreadable
}

public sealed record OfficialRecordsFileValidationResult(
    OfficialRecordsFileValidationStatus Status)
{
    public bool IsValid => Status == OfficialRecordsFileValidationStatus.Valid;
}

public interface IOfficialRecordsFileValidator
{
    Task<OfficialRecordsFileValidationResult> ValidateAsync(
        IFormFile file,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Performs bounded, streaming validation before ExcelDataReader sees a file.
/// XLS uses its compound-file signature; XLSX additionally validates its ZIP
/// structure and rejects suspicious expansion ratios.
/// </summary>
public sealed class OfficialRecordsFileValidator : IOfficialRecordsFileValidator
{
    private static readonly byte[] XlsSignature =
        [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];
    private static readonly byte[][] ZipSignatures =
    [
        [0x50, 0x4B, 0x03, 0x04],
        [0x50, 0x4B, 0x05, 0x06],
        [0x50, 0x4B, 0x07, 0x08]
    ];

    public async Task<OfficialRecordsFileValidationResult> ValidateAsync(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        if (file.Length <= 0)
        {
            return Result(OfficialRecordsFileValidationStatus.Empty);
        }

        if (file.Length > OfficialRecordsFileLimits.MaxFileSizeBytes)
        {
            return Result(OfficialRecordsFileValidationStatus.TooLarge);
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".xlsx" or ".xls"))
        {
            return Result(OfficialRecordsFileValidationStatus.InvalidExtension);
        }

        try
        {
            await using var stream = file.OpenReadStream();
            if (!stream.CanRead || !stream.CanSeek)
            {
                return Result(OfficialRecordsFileValidationStatus.Unreadable);
            }

            var header = new byte[8];
            var bytesRead = await ReadHeaderAsync(stream, header, cancellationToken);
            stream.Position = 0;

            if (extension == ".xls")
            {
                return bytesRead >= XlsSignature.Length
                    && header.AsSpan(0, XlsSignature.Length).SequenceEqual(XlsSignature)
                    ? Result(OfficialRecordsFileValidationStatus.Valid)
                    : Result(OfficialRecordsFileValidationStatus.InvalidSignature);
            }

            if (bytesRead < 4 || !ZipSignatures.Any(signature =>
                    header.AsSpan(0, 4).SequenceEqual(signature)))
            {
                return Result(OfficialRecordsFileValidationStatus.InvalidSignature);
            }

            return ValidateXlsxArchive(stream);
        }
        catch (InvalidDataException)
        {
            return Result(OfficialRecordsFileValidationStatus.InvalidWorkbook);
        }
        catch (IOException)
        {
            return Result(OfficialRecordsFileValidationStatus.Unreadable);
        }
        catch (NotSupportedException)
        {
            return Result(OfficialRecordsFileValidationStatus.Unreadable);
        }
    }

    private static OfficialRecordsFileValidationResult ValidateXlsxArchive(Stream stream)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        if (archive.Entries.Count == 0
            || archive.Entries.Count > OfficialRecordsFileLimits.MaxArchiveEntries)
        {
            return Result(OfficialRecordsFileValidationStatus.UnsafeArchive);
        }

        var hasContentTypes = false;
        var hasWorkbook = false;
        var hasWorksheet = false;
        long totalExpanded = 0;
        long totalCompressed = 0;

        foreach (var entry in archive.Entries)
        {
            hasContentTypes |= entry.FullName.Equals(
                "[Content_Types].xml",
                StringComparison.OrdinalIgnoreCase);
            hasWorkbook |= entry.FullName.Equals(
                "xl/workbook.xml",
                StringComparison.OrdinalIgnoreCase);
            hasWorksheet |= entry.FullName.StartsWith(
                    "xl/worksheets/",
                    StringComparison.OrdinalIgnoreCase)
                && entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);

            if (entry.Length > OfficialRecordsFileLimits.MaxUncompressedArchiveBytes
                || totalExpanded > OfficialRecordsFileLimits.MaxUncompressedArchiveBytes - entry.Length)
            {
                return Result(OfficialRecordsFileValidationStatus.UnsafeArchive);
            }

            if (totalCompressed > long.MaxValue - entry.CompressedLength)
            {
                return Result(OfficialRecordsFileValidationStatus.UnsafeArchive);
            }

            totalExpanded += entry.Length;
            totalCompressed += entry.CompressedLength;

            if (entry.Length >= OfficialRecordsFileLimits.CompressionRatioCheckMinimumBytes
                && (entry.CompressedLength == 0
                    || entry.Length / (double)entry.CompressedLength
                        > OfficialRecordsFileLimits.MaxCompressionRatio))
            {
                return Result(OfficialRecordsFileValidationStatus.UnsafeArchive);
            }
        }

        if (totalExpanded >= OfficialRecordsFileLimits.CompressionRatioCheckMinimumBytes
            && (totalCompressed == 0
                || totalExpanded / (double)totalCompressed
                    > OfficialRecordsFileLimits.MaxCompressionRatio))
        {
            return Result(OfficialRecordsFileValidationStatus.UnsafeArchive);
        }

        return hasContentTypes && hasWorkbook && hasWorksheet
            ? Result(OfficialRecordsFileValidationStatus.Valid)
            : Result(OfficialRecordsFileValidationStatus.InvalidWorkbook);
    }

    private static async Task<int> ReadHeaderAsync(
        Stream stream,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(
                buffer.AsMemory(totalRead, buffer.Length - totalRead),
                cancellationToken);
            if (read == 0)
            {
                break;
            }

            totalRead += read;
        }

        return totalRead;
    }

    private static OfficialRecordsFileValidationResult Result(
        OfficialRecordsFileValidationStatus status) => new(status);
}
