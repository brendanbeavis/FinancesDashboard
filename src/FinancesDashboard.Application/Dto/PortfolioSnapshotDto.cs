namespace FinancesDashboard.Application.Dto;

public sealed record PortfolioSnapshotDto(
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
