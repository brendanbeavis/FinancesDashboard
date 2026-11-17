namespace FinancesDashboard.Domain.Models;

public sealed class HoldingSnapshot
{
    public Guid Id { get; set; }

    public Guid PortfolioSnapshotId { get; set; }

    public Guid InvestmentInstrumentId { get; set; }

    public decimal Units { get; set; }

    public decimal AverageCostPerUnit { get; set; }

    public decimal TotalCostBase { get; set; }

    public decimal MarketPrice { get; set; }

    public decimal MarketValue { get; set; }

    public decimal DailyPriceChange { get; set; }

    public decimal DailyPriceChangePercentage { get; set; }

    public decimal UnrealisedProfitLoss { get; set; }

    public decimal UnrealisedProfitLossPercentage { get; set; }

    public decimal PortfolioWeightPercentage { get; set; }
}
