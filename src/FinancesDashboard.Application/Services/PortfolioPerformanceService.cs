using FinancesDashboard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinancesDashboard.Application.Services;

public sealed class PortfolioPerformanceService(FinancesDashboardDbContext dbContext)
{
    public async Task<IReadOnlyList<PortfolioHistoryPoint>> GetPortfolioHistoryAsync(
        Guid investmentAccountId,
        CancellationToken cancellationToken = default)
    {
        var snapshots = await dbContext.PortfolioSnapshots
            .AsNoTracking()
            .Where(snapshot => snapshot.InvestmentAccountId == investmentAccountId)
            .OrderBy(snapshot => snapshot.SnapshotDateTime)
            .Select(snapshot => new PortfolioHistoryPoint(
                snapshot.SnapshotDateTime,
                snapshot.TotalMarketValue,
                snapshot.TotalCostBase,
                snapshot.TotalUnrealisedProfitLoss,
                snapshot.TotalUnrealisedProfitLossPercentage,
                snapshot.TotalDailyValueChange))
            .ToListAsync(cancellationToken);

        return snapshots;
    }

    public async Task<IReadOnlyList<HoldingAllocationPoint>> GetAllocationAsync(
        Guid portfolioSnapshotId,
        CancellationToken cancellationToken = default)
    {
        var points = await (
            from holdingSnapshot in dbContext.HoldingSnapshots.AsNoTracking()
            join instrument in dbContext.InvestmentInstruments.AsNoTracking() on holdingSnapshot.InvestmentInstrumentId equals instrument.Id
            where holdingSnapshot.PortfolioSnapshotId == portfolioSnapshotId
            orderby holdingSnapshot.PortfolioWeightPercentage descending
            select new HoldingAllocationPoint(
                instrument.Code,
                instrument.Name,
                holdingSnapshot.MarketValue,
                holdingSnapshot.PortfolioWeightPercentage))
            .ToListAsync(cancellationToken);

        return points;
    }
}

public sealed record PortfolioHistoryPoint(
    DateTime SnapshotDateTime,
    decimal TotalMarketValue,
    decimal TotalCostBase,
    decimal TotalUnrealisedProfitLoss,
    decimal TotalUnrealisedProfitLossPercentage,
    decimal TotalDailyValueChange);

public sealed record HoldingAllocationPoint(
    string InstrumentCode,
    string? InstrumentName,
    decimal MarketValue,
    decimal WeightPercentage);
