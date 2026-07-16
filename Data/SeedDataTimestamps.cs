using ANU_Admissions.Models;

namespace ANU_Admissions.Data;

/// <summary>
/// Stable timestamps for EF Core seed records. Runtime-created entities still
/// use their normal current-time defaults; only model metadata uses these values.
/// </summary>
public static class SeedDataTimestamps
{
    private static readonly DateTime SnapshotBase =
        new(2026, 6, 21, 20, 54, 10, 817, DateTimeKind.Utc);

    public static DateTime AdmissionsSettingLastModified =>
        SnapshotBase.AddTicks(9566);

    public static DateTime CollegeCreatedAt(int collegeId) =>
        SnapshotBase.AddTicks(collegeId switch
        {
            1 => 2949,
            2 => 2963,
            3 => 2966,
            4 => 2968,
            5 => 2970,
            6 => 2972,
            7 => 2974,
            8 => 2976,
            _ => throw new ArgumentOutOfRangeException(
                nameof(collegeId),
                collegeId,
                "Unknown seeded college.")
        });

    public static College[] ApplyToSeededColleges(params College[] colleges)
    {
        foreach (var college in colleges)
        {
            college.CreatedAt = CollegeCreatedAt(college.Id);
        }

        return colleges;
    }
}
