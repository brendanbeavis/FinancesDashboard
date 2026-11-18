namespace FinancesDashboard.Web.Models;

public sealed record PortfolioSnapshotModel(
    Guid Id,
    DateTime SnapshotDateTime,
    string CurrencyCode,
    decimal TotalMarketValue,
    decimal TotalCostBase,
    decimal TotalUnrealisedProfitLoss,
    decimal TotalUnrealisedProfitLossPercentage,
    decimal TotalDailyValueChange,
    string Source,
    DateTime CreatedAt);
