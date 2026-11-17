using FinancesDashboard.Domain.Models;
using FinancesDashboard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinancesDashboard.Application.Services;

public sealed class InvestmentPortfolioService(FinancesDashboardDbContext dbContext)
{
    public async Task<PortfolioSnapshot?> GetLatestSnapshotAsync(Guid investmentAccountId, CancellationToken cancellationToken = default)
    {
        return await dbContext.PortfolioSnapshots
            .AsNoTracking()
            .Where(snapshot => snapshot.InvestmentAccountId == investmentAccountId)
            .OrderByDescending(snapshot => snapshot.SnapshotDateTime)
            .ThenByDescending(snapshot => snapshot.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Dictionary<Guid, PortfolioSnapshot>> GetLatestSnapshotsAsync(
        IReadOnlyCollection<Guid> investmentAccountIds,
        CancellationToken cancellationToken = default)
    {
        if (investmentAccountIds.Count == 0)
        {
            return [];
        }

        var snapshots = await dbContext.PortfolioSnapshots
            .AsNoTracking()
            .Where(snapshot => investmentAccountIds.Contains(snapshot.InvestmentAccountId))
            .ToListAsync(cancellationToken);

        return snapshots
            .GroupBy(snapshot => snapshot.InvestmentAccountId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(snapshot => snapshot.SnapshotDateTime)
                    .ThenByDescending(snapshot => snapshot.CreatedAt)
                    .First());
    }

    public async Task<IReadOnlyList<PortfolioHolding>> GetCurrentHoldingsAsync(
        Guid investmentAccountId,
        bool includeClosed,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.PortfolioHoldings
            .AsNoTracking()
            .Where(holding => holding.InvestmentAccountId == investmentAccountId);

        if (!includeClosed)
        {
            query = query.Where(holding => !holding.IsClosed && holding.Units > 0m);
        }

        return await query
            .OrderByDescending(holding => holding.CurrentMarketValue)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PortfolioSnapshot>> GetSnapshotHistoryAsync(
        Guid investmentAccountId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.PortfolioSnapshots
            .AsNoTracking()
            .Where(snapshot => snapshot.InvestmentAccountId == investmentAccountId)
            .OrderBy(snapshot => snapshot.SnapshotDateTime)
            .ToListAsync(cancellationToken);
    }
}
