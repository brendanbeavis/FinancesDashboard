namespace FinancesDashboard.Web.Models;

public sealed record DetectedRepaymentPatternModel(
    decimal EstimatedAmount,
    int EstimatedFrequency,
    decimal Confidence,
    int NumberOfObservations,
    DateOnly LastObservedDate,
    string FrequencyLabel,
    int AverageGapDays);
