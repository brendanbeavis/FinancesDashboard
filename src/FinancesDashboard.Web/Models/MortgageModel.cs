namespace FinancesDashboard.Web.Models;

public sealed record MortgageModel(
    Guid Id,
    string Name,
    decimal OriginalPrincipal,
    decimal CurrentPrincipal,
    decimal AnnualInterestRate,
    decimal ScheduledRepayment,
    int RepaymentsPerYear,
    DateOnly StartDate,
    DateOnly? ExpectedEndDate,
    IReadOnlyList<InterestRatePeriodModel> InterestRateHistory,
    MortgageProjectionModel Projection);
