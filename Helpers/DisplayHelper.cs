using System.Globalization;
using Microsoft.Extensions.Localization;
using ANU_Admissions.Resources;

namespace ANU_Admissions.Helpers;

/// <summary>
/// Pure display helpers for culture-aware rendering. These NEVER change stored
/// data — the canonical Arabic section values and college codes are untouched;
/// only what the user sees is localized.
/// </summary>
public static class DisplayHelper
{
    private static bool IsEnglish =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en";

    /// <summary>
    /// College name for the current culture: English uses NameEn when present,
    /// otherwise falls back to the Arabic name. Arabic always uses NameAr.
    /// </summary>
    public static string CollegeName(string? nameAr, string? nameEn)
    {
        if (IsEnglish && !string.IsNullOrWhiteSpace(nameEn))
            return nameEn!;
        return nameAr ?? string.Empty;
    }

    /// <summary>
    /// Localized LABEL for a stored section value. The input value stays the
    /// canonical Arabic string (used by allocation / preference logic); this
    /// only maps it to a translated label for display. Unknown values are
    /// returned unchanged.
    /// </summary>
    public static string SectionLabel(IStringLocalizer localizer, string? sectionValue)
    {
        var key = sectionValue switch
        {
            "علمي علوم" => "Section_Science",
            "علمي رياضة" => "Section_Math",
            "أدبي" => "Section_Literary",
            _ => null
        };
        return key == null ? (sectionValue ?? string.Empty) : localizer[key].Value;
    }

    /// <summary>
    /// Localized label for the stored allocation status. The DB value (e.g.
    /// "Allocated", "Confirmed", "Rejected", "Pending") stays unchanged — only
    /// what the user reads in the UI flips with the current culture. Unknown
    /// values render verbatim so we don't hide a real status.
    /// </summary>
    public static string AllocationStatusLabel(IStringLocalizer localizer, string? statusValue)
    {
        if (string.IsNullOrWhiteSpace(statusValue)) return string.Empty;
        var key = statusValue.Trim() switch
        {
            "Allocated" => "AS_Allocated",
            "Confirmed" => "AS_Confirmed",
            "Rejected" => "AS_Rejected",
            "Pending" => "AS_Pending",
            _ => null
        };
        return key == null ? statusValue : localizer[key].Value;
    }

    /// <summary>
    /// Maps a comma-separated AllowedSections string to localized labels for
    /// display only (e.g. "علمي علوم,أدبي" -> "Scientific - Biology, Literary").
    /// The stored value is never modified.
    /// </summary>
    public static string AllowedSectionsLabel(IStringLocalizer localizer, string? allowedSections)
    {
        if (string.IsNullOrWhiteSpace(allowedSections)) return "—";
        var parts = allowedSections
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(s => SectionLabel(localizer, s));
        return string.Join("، ", parts);
    }
}
