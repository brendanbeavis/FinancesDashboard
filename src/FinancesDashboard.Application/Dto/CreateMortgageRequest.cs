namespace FinancesDashboard.Application.Dto;

public sealed record CreateMortgageRequest(
    string Name,
    decimal OriginalPrincipal,
    decimal CurrentPrincipal,
    decimal AnnualInterestRate,
    decimal ScheduledRepayment,
    int RepaymentsPerYear,
    DateOnly StartDate,
    DateOnly? ExpectedEndDate,
    IReadOnlyList<InterestRatePeriodDto> InterestRateHistory);
