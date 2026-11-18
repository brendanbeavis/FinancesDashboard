namespace FinancesDashboard.Web.Models;

public sealed record MortgageProjectionModel(
    decimal ProjectedBalance,
    decimal TotalRemainingInterest,
    DateOnly? EstimatedPayoffDate,
    bool IsProjected,
    string Assumption,
    IReadOnlyList<AmortisationPeriodModel> AmortisationSchedule);
