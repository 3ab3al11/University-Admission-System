using System.Data;
using ANU_Admissions.Data;
using ANU_Admissions.Models;
using Microsoft.EntityFrameworkCore;

namespace ANU_Admissions.Services;

public interface IAllocationService
{
    Task<AllocationRunResult> RunAsync(CancellationToken cancellationToken = default);
}

public sealed record AllocationRunResult(
    int TotalProcessed,
    int Accepted,
    int Rejected,
    TimeSpan Duration);

/// <summary>
/// Runs the complete allocation workflow as one atomic database operation.
/// The controller is responsible for ensuring admissions are closed first.
/// </summary>
public sealed class AllocationService : IAllocationService
{
    private readonly AppDbContext _context;
    private readonly IAllocationEngine _engine;

    public AllocationService(AppDbContext context, IAllocationEngine engine)
    {
        _context = context;
        _engine = engine;
    }

    public async Task<AllocationRunResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var startedAt = DateTime.UtcNow;

        // Serializable gives this run a stable view of the data and prevents a
        // second allocation run from silently interleaving database changes.
        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var students = await _context.StudentProfiles
            .Where(student => student.EquivalentPercentage > 0)
            .ToListAsync(cancellationToken);

        if (students.Count == 0)
        {
            return new AllocationRunResult(0, 0, 0, DateTime.UtcNow - startedAt);
        }

        var studentIds = students.Select(student => student.Id).ToList();
        var preferences = await _context.Preferences
            .Where(preference => studentIds.Contains(preference.StudentProfileId))
            .ToListAsync(cancellationToken);

        var colleges = await _context.Colleges.ToListAsync(cancellationToken);

        var plan = _engine.BuildPlan(
            students.Select(student => new AllocationStudentInput(
                student.Id,
                student.EquivalentPercentage,
                student.ApplicationDate,
                student.Section)).ToList(),
            preferences.Select(preference => new AllocationPreferenceInput(
                preference.StudentProfileId,
                preference.CollegeId,
                preference.Rank)).ToList(),
            colleges.Select(college => new AllocationCollegeInput(
                college.Id,
                college.Code,
                college.Capacity,
                college.MinimumScore,
                college.AllowedSections,
                college.IsActive)).ToList());

        foreach (var college in colleges)
        {
            college.FinalCutoff = null;
        }

        // Delete immediately inside the transaction so the unique
        // StudentProfileId constraint cannot conflict with replacement rows.
        await _context.Allocations.ExecuteDeleteAsync(cancellationToken);

        foreach (var decision in plan.Decisions)
        {
            _context.Allocations.Add(new Allocation
            {
                StudentProfileId = decision.StudentProfileId,
                CollegeId = decision.CollegeId,
                AllocationDate = DateTime.UtcNow
            });
        }

        foreach (var college in colleges)
        {
            if (plan.FinalCutoffs.TryGetValue(college.Id, out var cutoff))
            {
                college.FinalCutoff = cutoff;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new AllocationRunResult(
            students.Count,
            plan.Decisions.Count,
            plan.RejectedCount,
            DateTime.UtcNow - startedAt);
    }
}
