using ANU_Admissions.Data;
using Microsoft.EntityFrameworkCore;

namespace ANU_Admissions.Services;

/// <summary>
/// Resolves whether admissions are currently open from the manual master switch
/// and the optional start/end settings. Times use the server's local timezone.
/// </summary>
public enum AdmissionsGateState
{
    /// <summary>The admin explicitly closed admissions.</summary>
    ManualClosed,
    /// <summary>The admin opened admissions without a scheduled bound.</summary>
    ManualOpen,
    /// <summary>The current time is before the configured start.</summary>
    NotStarted,
    /// <summary>At least one schedule bound is set and the current time is allowed.</summary>
    Open,
    /// <summary>The current time is past the configured end.</summary>
    Expired
}

public class AdmissionsGateStatus
{
    public bool IsOpen { get; init; }
    public AdmissionsGateState State { get; init; }
    public DateTime? StartAt { get; init; }
    public DateTime? EndAt { get; init; }
}

public interface IAdmissionsGate
{
    Task<AdmissionsGateStatus> GetStatusAsync();
}

/// <summary>
/// Pure admission-window rules. Start and end bounds are independently optional
/// and inclusive: a start-only schedule opens from that instant onward, while an
/// end-only schedule stays open through the configured end instant.
/// </summary>
public static class AdmissionsScheduleRules
{
    public static AdmissionsGateStatus Evaluate(
        bool manualOpen,
        DateTime now,
        DateTime? startAt,
        DateTime? endAt)
    {
        if (!manualOpen)
        {
            return CreateStatus(false, AdmissionsGateState.ManualClosed, startAt, endAt);
        }

        if (startAt.HasValue && now < startAt.Value)
        {
            return CreateStatus(false, AdmissionsGateState.NotStarted, startAt, endAt);
        }

        if (endAt.HasValue && now > endAt.Value)
        {
            return CreateStatus(false, AdmissionsGateState.Expired, startAt, endAt);
        }

        var state = startAt.HasValue || endAt.HasValue
            ? AdmissionsGateState.Open
            : AdmissionsGateState.ManualOpen;

        return CreateStatus(true, state, startAt, endAt);
    }

    private static AdmissionsGateStatus CreateStatus(
        bool isOpen,
        AdmissionsGateState state,
        DateTime? startAt,
        DateTime? endAt) => new()
        {
            IsOpen = isOpen,
            State = state,
            StartAt = startAt,
            EndAt = endAt
        };
}

public class AdmissionsGateService : IAdmissionsGate
{
    public const string KeyOpen = "AdmissionsOpen";
    public const string KeyStartAt = "AdmissionsStartAt";
    public const string KeyEndAt = "AdmissionsEndAt";

    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;

    public AdmissionsGateService(AppDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<AdmissionsGateStatus> GetStatusAsync()
    {
        var settings = await _context.SystemSettings
            .Where(s => s.Key == KeyOpen || s.Key == KeyStartAt || s.Key == KeyEndAt)
            .ToDictionaryAsync(s => s.Key, s => s.Value);

        var manualOpen = settings.TryGetValue(KeyOpen, out var openValue)
            && string.Equals(openValue, "true", StringComparison.OrdinalIgnoreCase);
        var startAt = TryParseDate(settings.GetValueOrDefault(KeyStartAt));
        var endAt = TryParseDate(settings.GetValueOrDefault(KeyEndAt));

        return AdmissionsScheduleRules.Evaluate(
            manualOpen,
            _timeProvider.GetLocalNow().DateTime,
            startAt,
            endAt);
    }

    private static DateTime? TryParseDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return DateTime.TryParse(
            raw,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeLocal,
            out var date)
            ? date
            : null;
    }
}
