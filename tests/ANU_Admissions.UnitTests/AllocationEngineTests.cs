using ANU_Admissions.Services;
using Xunit;

namespace ANU_Admissions.UnitTests;

public class AllocationEngineTests
{
    private const string Science = "علمي علوم";
    private const string Literary = "أدبي";
    private readonly AllocationEngine _engine = new();

    [Fact]
    public void AssignsStudentToHighestRankedEligibleCollege()
    {
        var plan = Build(
            students: [Student(1, 95)],
            preferences: [Preference(1, 20, 2), Preference(1, 10, 1)],
            colleges: [College(10), College(20)]);

        Assert.Equal(new AllocationDecision(1, 10), Assert.Single(plan.Decisions));
        Assert.Equal(0, plan.RejectedCount);
    }

    [Fact]
    public void FallsBackToNextPreferenceWhenFirstCollegeIsFull()
    {
        var plan = Build(
            students: [Student(1, 95), Student(2, 90)],
            preferences:
            [
                Preference(1, 10, 1),
                Preference(2, 10, 1),
                Preference(2, 20, 2)
            ],
            colleges: [College(10, capacity: 1), College(20)]);

        Assert.Contains(new AllocationDecision(1, 10), plan.Decisions);
        Assert.Contains(new AllocationDecision(2, 20), plan.Decisions);
    }

    [Fact]
    public void RejectsStudentBelowMinimumScore()
    {
        var plan = Build(
            students: [Student(1, 79)],
            preferences: [Preference(1, 10, 1)],
            colleges: [College(10, minimumScore: 80)]);

        Assert.Empty(plan.Decisions);
        Assert.Equal(1, plan.RejectedCount);
    }

    [Fact]
    public void IgnoresInactiveCollege()
    {
        var plan = Build(
            students: [Student(1, 95)],
            preferences: [Preference(1, 10, 1)],
            colleges: [College(10, isActive: false)]);

        Assert.Empty(plan.Decisions);
    }

    [Fact]
    public void RejectsCollegeThatDoesNotAllowStudentSection()
    {
        var plan = Build(
            students: [Student(1, 95, section: Literary)],
            preferences: [Preference(1, 10, 1)],
            colleges: [College(10, allowedSections: Science)]);

        Assert.Empty(plan.Decisions);
        Assert.Equal(1, plan.RejectedCount);
    }

    [Fact]
    public void FinanceCollegeRemainsExcludedByBusinessRule()
    {
        var plan = Build(
            students: [Student(1, 95)],
            preferences: [Preference(1, 10, 1)],
            colleges: [College(10, code: "FIN")]);

        Assert.Empty(plan.Decisions);
    }

    [Fact]
    public void RejectsStudentWithoutPreferences()
    {
        var plan = Build(
            students: [Student(1, 95)],
            preferences: [],
            colleges: [College(10)]);

        Assert.Empty(plan.Decisions);
        Assert.Equal(1, plan.RejectedCount);
    }

    [Fact]
    public void EarlierApplicationWinsWhenScoresAreEqual()
    {
        var earlier = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var later = earlier.AddMinutes(1);

        var plan = Build(
            students:
            [
                Student(1, 90, applicationDate: later),
                Student(2, 90, applicationDate: earlier)
            ],
            preferences: [Preference(1, 10, 1), Preference(2, 10, 1)],
            colleges: [College(10, capacity: 1)]);

        Assert.Equal(new AllocationDecision(2, 10), Assert.Single(plan.Decisions));
    }

    [Fact]
    public void LowerStudentIdProvidesStableFinalTieBreaker()
    {
        var sameTime = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);

        var plan = Build(
            students:
            [
                Student(2, 90, applicationDate: sameTime),
                Student(1, 90, applicationDate: sameTime)
            ],
            preferences: [Preference(1, 10, 1), Preference(2, 10, 1)],
            colleges: [College(10, capacity: 1)]);

        Assert.Equal(new AllocationDecision(1, 10), Assert.Single(plan.Decisions));
    }

    [Fact]
    public void FinalCutoffIsLowestAcceptedScore()
    {
        var plan = Build(
            students: [Student(1, 95), Student(2, 88)],
            preferences: [Preference(1, 10, 1), Preference(2, 10, 1)],
            colleges: [College(10, capacity: 2, minimumScore: 80)]);

        Assert.Equal(2, plan.Decisions.Count);
        Assert.Equal(88m, plan.FinalCutoffs[10]);
    }

    private AllocationPlan Build(
        IReadOnlyCollection<AllocationStudentInput> students,
        IReadOnlyCollection<AllocationPreferenceInput> preferences,
        IReadOnlyCollection<AllocationCollegeInput> colleges)
        => _engine.BuildPlan(students, preferences, colleges);

    private static AllocationStudentInput Student(
        int id,
        decimal score,
        DateTime? applicationDate = null,
        string section = Science)
        => new(id, score, applicationDate ?? DateTime.UnixEpoch.AddMinutes(id), section);

    private static AllocationPreferenceInput Preference(int studentId, int collegeId, int rank)
        => new(studentId, collegeId, rank);

    private static AllocationCollegeInput College(
        int id,
        string code = "TEST",
        int capacity = 10,
        decimal minimumScore = 0,
        string allowedSections = Science,
        bool isActive = true)
        => new(id, code, capacity, minimumScore, allowedSections, isActive);
}
