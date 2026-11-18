namespace FinancesDashboard.Web.Models;

public sealed record InvestmentPortfolioSummaryModel(
    Guid InvestmentAccountId,
    string AccountName,
    string CurrencyCode,
    decimal TotalMarketValue,
    decimal TotalCostBase,
    decimal TotalUnrealisedProfitLoss,
    decimal TotalUnrealisedProfitLossPercentage,
    decimal TotalDailyValueChange,
    int HoldingCount,
    DateTime? LatestSnapshotDateTime,
    bool IsStale);
