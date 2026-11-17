namespace FinancesDashboard.Web.Models;

public sealed record InvestmentHoldingModel(
    Guid Id,
    string InstrumentCode,
    string? InstrumentName,
    string InstrumentType,
    string Exchange,
    decimal Units,
    decimal AverageCostPerUnit,
    decimal CurrentPrice,
    decimal TotalCostBase,
    decimal CurrentMarketValue,
    decimal UnrealisedProfitLoss,
    decimal UnrealisedProfitLossPercentage,
    decimal PortfolioWeightPercentage,
    string CurrencyCode,
    DateTime LastUpdatedAt,
    bool IsClosed);
