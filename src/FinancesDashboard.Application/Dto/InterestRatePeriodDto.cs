namespace FinancesDashboard.Application.Dto;

public sealed record InterestRatePeriodDto(
    DateOnly StartDate,
    DateOnly? EndDate,
    decimal AnnualInterestRate);
