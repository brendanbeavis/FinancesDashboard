namespace FinancesDashboard.Web.Models;

public sealed record CreateMortgageRequestModel(
    Guid AccountId,
    decimal AnnualInterestRate,
    IReadOnlyList<InterestRatePeriodModel> InterestRateHistory,
    IReadOnlyList<Guid> OffsetAccountIds);
