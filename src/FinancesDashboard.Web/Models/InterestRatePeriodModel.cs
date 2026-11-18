namespace FinancesDashboard.Web.Models;

public sealed record InterestRatePeriodModel(
    DateOnly StartDate,
    DateOnly? EndDate,
    decimal AnnualInterestRate);
