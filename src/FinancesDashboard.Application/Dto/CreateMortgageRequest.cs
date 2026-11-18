namespace FinancesDashboard.Application.Dto;

/// <summary>
/// Request to create a mortgage configuration for an existing mortgage Account.
/// Historical balances and repayment behavior are derived from the Account's imported transactions.
/// </summary>
public sealed record CreateMortgageRequest(
    /// <summary>
    /// Account ID of the mortgage Account (must be AccountType.Mortgage).
    /// </summary>
    Guid AccountId,
    /// <summary>
    /// Current annual interest rate for forecasting.
    /// </summary>
    decimal AnnualInterestRate,
    /// <summary>
    /// Optional interest rate history for varying rates over time.
    /// </summary>
    IReadOnlyList<InterestRatePeriodDto> InterestRateHistory,
    /// <summary>
    /// Existing imported accounts linked as offsets for this mortgage.
    /// </summary>
    IReadOnlyList<Guid> OffsetAccountIds);

