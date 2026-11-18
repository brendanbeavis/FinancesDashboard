namespace FinancesDashboard.Web.Models;

public sealed record MortgageModel(
    Guid Id,
    Guid AccountId,
    string AccountName,
    decimal AnnualInterestRate,
    decimal? DetectedRepaymentAmount,
    int? DetectedRepaymentsPerYear,
    decimal LatestHistoricalBalance,
    decimal TotalOffsetBalance,
    decimal InterestBearingBalance,
    DateOnly LatestHistoricalDate,
    IReadOnlyList<InterestRatePeriodModel> InterestRateHistory,
    IReadOnlyList<Guid> OffsetAccountIds,
    MortgageProjectionModel Projection,
    decimal DetectionConfidence = 0m);
