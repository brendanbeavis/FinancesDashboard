namespace FinancesDashboard.Application.Dto;

public sealed record MortgageDto(
    Guid Id,
    string Name,
    decimal OriginalPrincipal,
    decimal CurrentPrincipal,
    decimal AnnualInterestRate,
    decimal ScheduledRepayment,
    int RepaymentsPerYear,
    DateOnly StartDate,
    DateOnly? ExpectedEndDate,
    IReadOnlyList<InterestRatePeriodDto> InterestRateHistory,
    MortgageProjectionDto Projection);
