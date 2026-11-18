namespace FinancesDashboard.Application.Dto;

/// <summary>
/// User assumptions for generating an ephemeral mortgage forecast.
/// These are transient assumptions that do not require a persisted Mortgage configuration.
/// </summary>
public sealed record MortgageForecastRequest(
    decimal AnnualInterestRate,
    decimal RepaymentAmount,
    int RepaymentsPerYear,
    IReadOnlyList<Guid> SelectedOffsetAccountIds);
