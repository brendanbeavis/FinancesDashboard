namespace FinancesDashboard.Application.Dto;

/// <summary>
/// Data transfer object for mortgage configuration and analysis.
/// Combines persisted mortgage settings with observed account history and derived repayment signals.
/// </summary>
public sealed record MortgageDto(
    Guid Id,
    Guid AccountId,
    string AccountName,
    decimal AnnualInterestRate,
    decimal? DetectedRepaymentAmount,
    int? DetectedRepaymentsPerYear,
    /// <summary>
    /// Latest historical balance derived from the Account's most recent transaction.
    /// </summary>
    decimal LatestHistoricalBalance,
    /// <summary>
    /// Total latest balance across linked offset accounts.
    /// </summary>
    decimal TotalOffsetBalance,
    /// <summary>
    /// Mortgage principal on which interest is calculated after offsets.
    /// </summary>
    decimal InterestBearingBalance,
    /// <summary>
    /// Date of the latest transaction reflecting current position.
    /// </summary>
    DateOnly LatestHistoricalDate,
    /// <summary>
    /// Interest rate history for varying rates over time.
    /// </summary>
    IReadOnlyList<InterestRatePeriodDto> InterestRateHistory,
    /// <summary>
    /// Accounts linked as offsets for this mortgage.
    /// </summary>
    IReadOnlyList<Guid> OffsetAccountIds,
    /// <summary>
    /// Amortization and forecasting projection based on current assumptions.
    /// </summary>
    MortgageProjectionDto Projection,
    /// <summary>
    /// Confidence level (0.0-1.0) in the detected repayment amount and frequency, if available.
    /// </summary>
    decimal DetectionConfidence = 0m);

