using Microsoft.EntityFrameworkCore;
using ANU_Admissions.Data;

namespace ANU_Admissions.Services;

/// <summary>
/// Resolves whether admissions are currently open based on three SystemSettings
/// rows (no schema change): the manual <c>AdmissionsOpen</c> master switch and
/// the optional <c>AdmissionsStartAt</c> / <c>AdmissionsEndAt</c> window.
/// All times are server local time (<see cref="DateTime.Now"/>) because the
/// project is a single-tenant demo without timezone handling.
/// </summary>
public enum AdmissionsGateState
{
    /// <summary>The admin flipped AdmissionsOpen = false. Hard close.</summary>
    ManualClosed,
    /// <summary>AdmissionsOpen = true, no schedule rows configured.</summary>
    ManualOpen,
    /// <summary>Schedule is set, now is before StartAt.</summary>
    NotStarted,
    /// <summary>Schedule is set, now is between StartAt and EndAt.</summary>
    Open,
    /// <summary>Schedule is set, now is past EndAt.</summary>
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

public class AdmissionsGateService : IAdmissionsGate
{
    public const string KeyOpen    = "AdmissionsOpen";
    public const string KeyStartAt = "AdmissionsStartAt";
    public const string KeyEndAt   = "AdmissionsEndAt";

    private readonly AppDbContext _context;

    public AdmissionsGateService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<AdmissionsGateStatus> GetStatusAsync()
    {
        // Single round-trip for all three rows.
        var settings = await _context.SystemSettings
            .Where(s => s.Key == KeyOpen || s.Key == KeyStartAt || s.Key == KeyEndAt)
            .ToDictionaryAsync(s => s.Key, s => s.Value);

        var manualOpen = settings.TryGetValue(KeyOpen, out var openVal) && openVal == "true";
        var startAt    = TryParseDate(settings.GetValueOrDefault(KeyStartAt));
        var endAt      = TryParseDate(settings.GetValueOrDefault(KeyEndAt));

        // 1. Manual master switch wins. A scheduled window cannot override an
        //    explicit close by the admin.
        if (!manualOpen)
        {
            return new AdmissionsGateStatus
            {
                IsOpen = false,
                State = AdmissionsGateState.ManualClosed,
                StartAt = startAt,
                EndAt = endAt
            };
        }

        // 2. No window configured → behave like the old boolean toggle.
        if (startAt == null || endAt == null)
        {
            return new AdmissionsGateStatus
            {
                IsOpen = true,
                State = AdmissionsGateState.ManualOpen,
                StartAt = startAt,
                EndAt = endAt
            };
        }

        // 3. Window configured → time decides.
        var now = DateTime.Now;
        AdmissionsGateState state;
        bool isOpen;
        if (now < startAt)        { state = AdmissionsGateState.NotStarted; isOpen = false; }
        else if (now > endAt)     { state = AdmissionsGateState.Expired;    isOpen = false; }
        else                      { state = AdmissionsGateState.Open;       isOpen = true;  }

        return new AdmissionsGateStatus
        {
            IsOpen = isOpen,
            State = state,
            StartAt = startAt,
            EndAt = endAt
        };
    }

    // Parses an "o" (round-trip) or local datetime string. Returns null for
    // empty/malformed values so an absent/garbled setting is treated as "not set".
    private static DateTime? TryParseDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return DateTime.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeLocal, out var dt)
            ? dt
            : null;
    }
}
