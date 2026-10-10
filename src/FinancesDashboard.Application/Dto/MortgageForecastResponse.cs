namespace FinancesDashboard.Application.Dto;

/// <summary>
/// Comprehensive mortgage forecast response combining historical observations and future projections.
/// Explicitly separates Observed (historical) data from Projected (forecast) data to avoid confusion.
/// </summary>
public sealed record MortgageForecastResponse(
    /// <summary>
    /// Historical mortgage positions from imported transactions.
    /// Each point is an observed (not projected) balance at a specific date.
    /// </summary>
    IReadOnlyList<MortgageHistoryPoint> HistoricalPoints,

    /// <summary>
    /// Future projected amortization schedule based on assumptions.
    /// Each period shows projected interest, principal, and balance.
    /// </summary>
    IReadOnlyList<AmortisationPeriodDto> ProjectedAmortisationSchedule,

    /// <summary>
    /// Summary statistics from historical analysis.
    /// </summary>
    MortgageSummary Summary,

    /// <summary>
    /// Forecast outcomes based on assumptions.
    /// </summary>
    MortgageForecastOutcome Forecast,

    /// <summary>
    /// The assumptions used to generate this forecast.
    /// </summary>
    MortgageForecastRequest Assumptions,

    /// <summary>
    /// Detected repayment pattern from historical transaction analysis.
    /// Null if no pattern could be detected from the transaction history.
    /// </summary>
    MortgageRepaymentPattern? DetectedRepayment = null);

/// <summary>
/// Historical summary statistics for a mortgage account.
/// </summary>
public sealed record MortgageSummary(
    int TransactionCount,
    decimal CurrentBalance,
    decimal TotalRepayments,
    decimal BalanceChange,
    DateOnly? EarliestDate,
    DateOnly? LatestDate);

/// <summary>
/// Forecast outcomes projected into the future.
/// </summary>
public sealed record MortgageForecastOutcome(
    string Status,
    DateOnly? ProjectedPayoffDate,
    int RemainingRepaymentCount,
    decimal TotalFutureInterest,
    decimal TotalFutureRepayments,
    decimal CurrentOutstandingPrincipal,
    decimal CurrentOffsetBalance,
    decimal EffectivePrincipalAfterOffset,
    DateOnly? ProjectedInterestFreeDate = null);
