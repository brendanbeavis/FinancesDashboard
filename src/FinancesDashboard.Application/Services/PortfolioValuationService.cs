namespace FinancesDashboard.Application.Services;

public sealed class PortfolioValuationService
{
    public HoldingValuation CalculateHolding(decimal units, decimal averageCostPerUnit, decimal currentPrice)
    {
        var totalCostBase = Round2(units * averageCostPerUnit);
        var currentMarketValue = Round2(units * currentPrice);
        var unrealisedProfitLoss = Round2(currentMarketValue - totalCostBase);
        var unrealisedProfitLossPercentage = totalCostBase == 0m
            ? 0m
            : Round6((unrealisedProfitLoss / totalCostBase) * 100m);

        return new HoldingValuation(
            units,
            averageCostPerUnit,
            totalCostBase,
            currentPrice,
            currentMarketValue,
            unrealisedProfitLoss,
            unrealisedProfitLossPercentage);
    }

    public PortfolioValuation CalculatePortfolio(decimal totalCostBase, decimal totalMarketValue, decimal totalDailyValueChange)
    {
        var totalUnrealisedProfitLoss = Round2(totalMarketValue - totalCostBase);
        var totalUnrealisedProfitLossPercentage = totalCostBase == 0m
            ? 0m
            : Round6((totalUnrealisedProfitLoss / totalCostBase) * 100m);

        return new PortfolioValuation(
            Round2(totalMarketValue),
            Round2(totalCostBase),
            totalUnrealisedProfitLoss,
            totalUnrealisedProfitLossPercentage,
            Round2(totalDailyValueChange));
    }

    public decimal CalculateAllocationPercentage(decimal holdingMarketValue, decimal totalPortfolioMarketValue)
    {
        if (totalPortfolioMarketValue == 0m)
        {
            return 0m;
        }

        return Round6((holdingMarketValue / totalPortfolioMarketValue) * 100m);
    }

    private static decimal Round2(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static decimal Round6(decimal value) => decimal.Round(value, 6, MidpointRounding.AwayFromZero);
}

public sealed record HoldingValuation(
    decimal Units,
    decimal AverageCostPerUnit,
    decimal TotalCostBase,
    decimal CurrentPrice,
    decimal CurrentMarketValue,
    decimal UnrealisedProfitLoss,
    decimal UnrealisedProfitLossPercentage);

public sealed record PortfolioValuation(
    decimal TotalMarketValue,
    decimal TotalCostBase,
    decimal TotalUnrealisedProfitLoss,
    decimal TotalUnrealisedProfitLossPercentage,
    decimal TotalDailyValueChange);
