namespace FinancesDashboard.Web.Models;

/// <summary>
/// Comprehensive mortgage forecast response combining historical observations and future projections.
/// Explicitly separates Observed (historical) data from Projected (forecast) data.
/// </summary>
public sealed record MortgageForecastResponseModel(
    /// <summary>
    /// Historical mortgage positions from imported transactions.
    /// Each point is an observed (not projected) balance at a specific date.
    /// </summary>
    IReadOnlyList<MortgageHistoryPointModel> HistoricalPoints,

    /// <summary>
    /// Future projected amortization schedule based on assumptions.
    /// Each period shows projected interest, principal, and balance.
    /// </summary>
    IReadOnlyList<AmortisationPeriodModel> ProjectedAmortisationSchedule,

    /// <summary>
    /// Summary statistics from historical analysis.
    /// </summary>
    MortgageSummaryModel Summary,

    /// <summary>
    /// Forecast outcomes based on assumptions.
    /// </summary>
    MortgageForecastOutcomeModel Forecast,

    /// <summary>
    /// The assumptions used to generate this forecast.
    /// </summary>
    MortgageForecastRequestModel Assumptions,

    /// <summary>
    /// Detected repayment pattern from historical transaction analysis.
    /// Null if no pattern could be detected from the transaction history.
    /// </summary>
    DetectedRepaymentPatternModel? DetectedRepayment = null);

/// <summary>
/// Historical summary statistics for a mortgage account.
/// </summary>
public sealed record MortgageSummaryModel(
    int TransactionCount,
    decimal CurrentBalance,
    decimal TotalRepayments,
    decimal BalanceChange,
    DateOnly? EarliestDate,
    DateOnly? LatestDate);

/// <summary>
/// Forecast outcomes projected into the future.
/// </summary>
public sealed record MortgageForecastOutcomeModel(
    string Status,
    DateOnly? ProjectedPayoffDate,
    int RemainingRepaymentCount,
    decimal TotalFutureInterest,
    decimal TotalFutureRepayments,
    decimal CurrentOutstandingPrincipal,
    decimal CurrentOffsetBalance,
    decimal EffectivePrincipalAfterOffset,
    DateOnly? ProjectedInterestFreeDate = null);
