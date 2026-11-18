namespace FinancesDashboard.Domain.Models;

public sealed class PortfolioSnapshot
{
    public Guid Id { get; set; }

    public Guid InvestmentAccountId { get; set; }

    public DateTime SnapshotDateTime { get; set; }

    public required string CurrencyCode { get; set; }

    public decimal TotalMarketValue { get; set; }

    public decimal TotalCostBase { get; set; }

    public decimal TotalUnrealisedProfitLoss { get; set; }

    public decimal TotalUnrealisedProfitLossPercentage { get; set; }

    public decimal TotalDailyValueChange { get; set; }

    public required string Source { get; set; }

    public DateTime CreatedAt { get; set; }
}
