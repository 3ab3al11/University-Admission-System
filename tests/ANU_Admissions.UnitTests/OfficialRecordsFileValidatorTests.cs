using System.IO.Compression;
using System.Text;
using ANU_Admissions.Services;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace ANU_Admissions.UnitTests;

public class OfficialRecordsFileValidatorTests
{
    private readonly OfficialRecordsFileValidator _validator = new();

    [Fact]
    public async Task AcceptsXlsCompoundFileSignature()
    {
        var bytes = new byte[]
        {
            0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0x00
        };

        var result = await ValidateAsync(CreateFile(bytes, "results.xls"));

        Assert.Equal(OfficialRecordsFileValidationStatus.Valid, result.Status);
    }

    [Fact]
    public async Task RejectsXlsWithForgedExtension()
    {
        var result = await ValidateAsync(
            CreateFile(Encoding.UTF8.GetBytes("not an xls workbook"), "results.xls"));

        Assert.Equal(
            OfficialRecordsFileValidationStatus.InvalidSignature,
            result.Status);
    }

    [Fact]
    public async Task AcceptsXlsxWithRequiredWorkbookEntries()
    {
        var result = await ValidateAsync(
            CreateFile(CreateXlsx(), "results.xlsx"));

        Assert.Equal(OfficialRecordsFileValidationStatus.Valid, result.Status);
    }

    [Fact]
    public async Task RejectsZipThatIsNotAnXlsxWorkbook()
    {
        var bytes = CreateZip(("random.txt", "hello"));

        var result = await ValidateAsync(CreateFile(bytes, "results.xlsx"));

        Assert.Equal(
            OfficialRecordsFileValidationStatus.InvalidWorkbook,
            result.Status);
    }

    [Fact]
    public async Task RejectsXlsxWithForgedExtension()
    {
        var result = await ValidateAsync(
            CreateFile(Encoding.UTF8.GetBytes("not a zip"), "results.xlsx"));

        Assert.Equal(
            OfficialRecordsFileValidationStatus.InvalidSignature,
            result.Status);
    }

    [Fact]
    public async Task RejectsEmptyFile()
    {
        var result = await ValidateAsync(CreateFile([], "results.xlsx"));

        Assert.Equal(OfficialRecordsFileValidationStatus.Empty, result.Status);
    }

    [Fact]
    public async Task RejectsFileAboveConfiguredLimitBeforeOpeningIt()
    {
        var file = new FormFile(
            Stream.Null,
            0,
            OfficialRecordsFileLimits.MaxFileSizeBytes + 1,
            "ExcelFile",
            "results.xlsx");

        var result = await ValidateAsync(file);

        Assert.Equal(OfficialRecordsFileValidationStatus.TooLarge, result.Status);
    }

    [Fact]
    public async Task RejectsUnsupportedExtensionEvenWithValidZipContent()
    {
        var result = await ValidateAsync(
            CreateFile(CreateXlsx(), "results.zip"));

        Assert.Equal(
            OfficialRecordsFileValidationStatus.InvalidExtension,
            result.Status);
    }

    [Fact]
    public async Task RejectsSuspiciousArchiveExpansionRatio()
    {
        var repeatedContent = new string(
            'A',
            (int)OfficialRecordsFileLimits.CompressionRatioCheckMinimumBytes * 2);
        var bytes = CreateZip(
            ("[Content_Types].xml", "<Types />"),
            ("xl/workbook.xml", "<workbook />"),
            ("xl/worksheets/sheet1.xml", repeatedContent));

        var result = await ValidateAsync(CreateFile(bytes, "results.xlsx"));

        Assert.Equal(
            OfficialRecordsFileValidationStatus.UnsafeArchive,
            result.Status);
    }

    private static IFormFile CreateFile(byte[] bytes, string fileName)
    {
        var stream = new MemoryStream(bytes, writable: false);
        return new FormFile(stream, 0, bytes.Length, "ExcelFile", fileName);
    }

    private Task<OfficialRecordsFileValidationResult> ValidateAsync(IFormFile file) =>
        _validator.ValidateAsync(file, TestContext.Current.CancellationToken);

    private static byte[] CreateXlsx() => CreateZip(
        ("[Content_Types].xml", "<Types />"),
        ("xl/workbook.xml", "<workbook />"),
        ("xl/worksheets/sheet1.xml", "<worksheet />"));

    private static byte[] CreateZip(params (string Name, string Content)[] entries)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
                using var writer = new StreamWriter(
                    entry.Open(),
                    Encoding.UTF8,
                    bufferSize: 1024,
                    leaveOpen: false);
                writer.Write(content);
            }
        }

        return stream.ToArray();
    }
}
