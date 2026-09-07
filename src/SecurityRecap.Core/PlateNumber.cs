namespace SecurityRecap.Core;

/// <summary>
/// Rules for deciding whether an extracted licence plate is a real, trackable tag.
/// </summary>
public static class PlateNumber
{
    /// <summary>
    /// Values a report uses to mean "no readable plate". Tracking these as plates produces
    /// a single fictitious vehicle that accumulates every unreadable tag on the property,
    /// which then reads as a serious repeat offender.
    /// </summary>
    private static readonly HashSet<string> Placeholders = new(StringComparer.OrdinalIgnoreCase)
    {
        "UNKNOWN", "UNK", "N/A", "NA", "NONE", "NO PLATE", "NOPLATE", "NO TAG", "NOTAG",
        "UNREADABLE", "ILLEGIBLE", "OBSCURED", "NOT VISIBLE", "NOTVISIBLE",
        "TEMP", "TEMPORARY", "PAPER TAG", "PAPERTAG", "N-A", "-", "--", "?", "??", "???"
    };

    public static string Normalize(string? plate) =>
        (plate ?? string.Empty).Trim().ToUpperInvariant();

    /// <summary>
    /// True when the value looks like an actual plate worth building history against.
    /// Rejects placeholders and anything too short to identify a vehicle.
    /// </summary>
    public static bool IsTrackable(string? plate)
    {
        var normalized = Normalize(plate);

        if (normalized.Length < 2) return false;
        if (Placeholders.Contains(normalized)) return false;

        // Must contain at least one letter or digit; punctuation alone is not a plate.
        return normalized.Any(char.IsLetterOrDigit);
    }
}
