namespace ANU_Admissions.Services;

public sealed record AllocationStudentInput(
    int Id,
    decimal EquivalentPercentage,
    DateTime ApplicationDate,
    string? Section);

public sealed record AllocationPreferenceInput(
    int StudentProfileId,
    int CollegeId,
    int Rank);

public sealed record AllocationCollegeInput(
    int Id,
    string Code,
    int Capacity,
    decimal MinimumScore,
    string? AllowedSections,
    bool IsActive);

public sealed record AllocationDecision(int StudentProfileId, int CollegeId);

public sealed record AllocationPlan(
    IReadOnlyList<AllocationDecision> Decisions,
    IReadOnlyDictionary<int, decimal> FinalCutoffs,
    int RejectedCount);

public interface IAllocationEngine
{
    AllocationPlan BuildPlan(
        IReadOnlyCollection<AllocationStudentInput> students,
        IReadOnlyCollection<AllocationPreferenceInput> preferences,
        IReadOnlyCollection<AllocationCollegeInput> colleges);
}

/// <summary>
/// Pure, deterministic allocation algorithm. It has no database or web
/// dependencies, so every business rule can be covered by fast unit tests.
/// </summary>
public sealed class AllocationEngine : IAllocationEngine
{
    public AllocationPlan BuildPlan(
        IReadOnlyCollection<AllocationStudentInput> students,
        IReadOnlyCollection<AllocationPreferenceInput> preferences,
        IReadOnlyCollection<AllocationCollegeInput> colleges)
    {
        var collegesById = colleges.ToDictionary(college => college.Id);
        var preferencesByStudent = preferences
            .GroupBy(preference => preference.StudentProfileId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(preference => preference.Rank).ToList());

        var acceptedScores = colleges.ToDictionary(
            college => college.Id,
            _ => new List<decimal>());
        var decisions = new List<AllocationDecision>();
        var rejected = 0;

        foreach (var student in students
                     .OrderByDescending(student => student.EquivalentPercentage)
                     .ThenBy(student => student.ApplicationDate)
                     .ThenBy(student => student.Id))
        {
            if (!preferencesByStudent.TryGetValue(student.Id, out var studentPreferences))
            {
                rejected++;
                continue;
            }

            AllocationCollegeInput? selectedCollege = null;
            foreach (var preference in studentPreferences)
            {
                if (!collegesById.TryGetValue(preference.CollegeId, out var college))
                    continue;

                if (college.IsActive
                    && acceptedScores[college.Id].Count < college.Capacity
                    && student.EquivalentPercentage >= college.MinimumScore
                    && IsAllowedForSection(college, student.Section))
                {
                    selectedCollege = college;
                    break;
                }
            }

            if (selectedCollege == null)
            {
                rejected++;
                continue;
            }

            acceptedScores[selectedCollege.Id].Add(student.EquivalentPercentage);
            decisions.Add(new AllocationDecision(student.Id, selectedCollege.Id));
        }

        var cutoffs = acceptedScores
            .Where(pair => pair.Value.Count > 0)
            .ToDictionary(pair => pair.Key, pair => pair.Value.Min());

        return new AllocationPlan(decisions, cutoffs, rejected);
    }

    private static bool IsAllowedForSection(AllocationCollegeInput college, string? section)
    {
        if (college.Code == "FIN") return false;
        if (string.IsNullOrWhiteSpace(section) || string.IsNullOrWhiteSpace(college.AllowedSections))
            return false;

        return college.AllowedSections
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Contains(section, StringComparer.Ordinal);
    }
}
