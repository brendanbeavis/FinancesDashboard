namespace FinancesDashboard.Application.Dto;

/// <summary>
/// Detected mortgage repayment pattern from historical transaction analysis.
/// Represents observed recurring payment behavior with confidence metrics and supporting data.
/// All values are derived from observed transactions, not user configuration.
/// </summary>
public sealed record MortgageRepaymentPattern(
    /// <summary>
    /// The estimated typical repayment amount.
    /// </summary>
    decimal EstimatedAmount,

    /// <summary>
    /// The estimated repayment frequency as repayments per year.
    /// Examples: 26 (fortnightly), 24 (bi-weekly), 12 (monthly), 52 (weekly), 4 (quarterly)
    /// </summary>
    int EstimatedFrequency,

    /// <summary>
    /// Confidence score (0.0-1.0) indicating quality of detected pattern.
    /// Combines amount consistency and frequency consistency.
    /// 0.7+ suggests high confidence pattern suitable for forecasting.
    /// </summary>
    decimal Confidence,

    /// <summary>
    /// Number of repayment transactions observed that match the pattern.
    /// Higher numbers increase confidence in the detection.
    /// </summary>
    int NumberOfObservations,

    /// <summary>
    /// The most recent date a matching repayment was observed.
    /// </summary>
    DateOnly LastObservedDate,

    /// <summary>
    /// Human-readable frequency label (e.g., "Fortnightly", "Monthly", "Weekly").
    /// </summary>
    string FrequencyLabel,

    /// <summary>
    /// Average gap in days between observed repayments.
    /// Useful for understanding consistency of timing.
    /// </summary>
    int AverageGapDays,

    /// <summary>
    /// Percentage (0.0-1.0) of payment transactions that match the estimated amount.
    /// Indicates amount consistency. 1.0 = all payments same amount.
    /// </summary>
    decimal AmountConsistency,

    /// <summary>
    /// Standard deviation of gaps between payments in days.
    /// Lower values indicate more regular timing. High values suggest irregular spacing.
    /// </summary>
    decimal GapStandardDeviation,

    /// <summary>
    /// The earliest date a matching repayment was observed.
    /// Combined with LastObservedDate, shows historical period covered.
    /// </summary>
    DateOnly FirstObservedDate);
