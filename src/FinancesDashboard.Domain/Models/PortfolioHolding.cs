namespace FinancesDashboard.Domain.Models;

public sealed class PortfolioHolding
{
    public Guid Id { get; set; }

    public Guid InvestmentAccountId { get; set; }

    public Guid InvestmentInstrumentId { get; set; }

    public decimal Units { get; set; }

    public decimal AverageCostPerUnit { get; set; }

    public decimal TotalCostBase { get; set; }

    public decimal CurrentPrice { get; set; }

    public decimal CurrentMarketValue { get; set; }

    public decimal UnrealisedProfitLoss { get; set; }

    public decimal UnrealisedProfitLossPercentage { get; set; }

    public required string CurrencyCode { get; set; }

    public DateTime LastUpdatedAt { get; set; }

    public bool IsClosed { get; set; }
}
