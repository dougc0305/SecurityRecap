namespace SecurityRecap.Core.Interfaces;

/// <summary>
/// A period recap for a board meeting. Every count here is computed from the database, never
/// from the model — a figure in a board packet has to be exact. The model is given these
/// numbers and asked only for the reading of them.
/// </summary>
public record RecapCoverage(
    DateOnly From,
    DateOnly To,
    int NightsInPeriod,
    int ReportsReceived,
    int UsableReports,
    IReadOnlyList<DateOnly> MissingDates,
    IReadOnlyList<DateOnly> EmptyReportDates,
    IReadOnlyList<OfficerShifts> Officers,
    double AveragePatrolsPerShift,
    int MinPatrols,
    int MaxPatrols);

public record OfficerShifts(string Officer, int Shifts);

public record RecapCategoryCount(string Category, int High, int Medium, int Low, int Total);

public record RecapEntry(
    DateOnly ReportDate,
    DateTime? LocalTime,
    string IncidentType,
    string Severity,
    string? Location,
    string Description);

public record RecapViolation(
    DateOnly ReportDate,
    DateTime? LocalTime,
    string? PlateNumber,
    string? PlateState,
    string? Vehicle,
    string? Location,
    string? ViolationType,
    string Severity,
    bool NoticeIssued,
    bool TowNotified,
    int PlateViolationsAllTime,
    DateTime? PlateFirstSeen,
    int PlateViolationsInPeriod);

public record RecapRecurringItem(string Description, string? Location, int Occurrences, DateOnly First, DateOnly Last);

public record RecapData(
    string PropertyName,
    string TimeZone,
    RecapCoverage Coverage,
    int TotalEntries,
    int RoutineEntries,
    int SubstantiveEntries,
    IReadOnlyList<RecapCategoryCount> ByCategory,
    IReadOnlyList<RecapEntry> SubstantiveDetail,
    IReadOnlyList<RecapEntry> HighSeverity,
    IReadOnlyList<RecapViolation> Violations,
    IReadOnlyList<RecapRecurringItem> RecurringMaintenance,
    IReadOnlyList<RecapRecurringItem> RepeatLocations);

/// <summary>The model's reading of the numbers, plus the narrative for the packet.</summary>
public record RecapNarrative(
    string Headline,
    IReadOnlyList<RecapAttentionItem> AttentionItems,
    IReadOnlyList<string> DataNotes,
    string MarkdownSummary);

public record RecapAttentionItem(
    string Title,
    string? When,
    string What,
    string WhyItMatters,
    string Significance);

public record RecapResult(RecapData Data, RecapNarrative? Narrative, string? NarrativeError);

public interface IRecapService
{
    /// <summary>Computes the period's figures. No model involved.</summary>
    Task<RecapData> BuildDataAsync(
        Guid tenantId, Guid userId, Enums.UserRole userRole,
        Guid propertyId, DateOnly from, DateOnly to, CancellationToken ct = default);

    /// <summary>
    /// Computes the figures and asks Claude to read them. A narrative failure is returned
    /// rather than thrown: the numbers are the part the board cannot do without.
    /// </summary>
    Task<RecapResult> BuildAsync(
        Guid tenantId, Guid userId, Enums.UserRole userRole,
        Guid propertyId, DateOnly from, DateOnly to, CancellationToken ct = default);
}
