namespace FinancesDashboard.Web.Models;

public sealed record CreateMortgageRequestModel(
    string Name,
    decimal OriginalPrincipal,
    decimal CurrentPrincipal,
    decimal AnnualInterestRate,
    decimal ScheduledRepayment,
    int RepaymentsPerYear,
    DateOnly StartDate,
    DateOnly? ExpectedEndDate,
    IReadOnlyList<InterestRatePeriodModel> InterestRateHistory);
