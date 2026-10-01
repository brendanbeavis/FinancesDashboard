namespace FinancesDashboard.Application.Dto;

public sealed record MortgageProjectionDto(
    decimal ProjectedBalance,
    decimal TotalRemainingInterest,
    DateOnly? EstimatedPayoffDate,
    bool IsProjected,
    string Assumption,
    IReadOnlyList<AmortisationPeriodDto> AmortisationSchedule);
