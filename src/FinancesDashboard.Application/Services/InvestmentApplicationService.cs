using FinancesDashboard.Application.Dto;
using FinancesDashboard.Domain.Enums;
using FinancesDashboard.Domain.Models;
using FinancesDashboard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinancesDashboard.Application.Services;

public sealed class InvestmentApplicationService(
    FinancesDashboardDbContext dbContext,
    PortfolioValuationService valuationService)
{
    public async Task<IReadOnlyList<InvestmentAccountDto>> GetInvestmentAccountsAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Accounts
            .AsNoTracking()
            .Where(account => account.Type == AccountType.Investment)
            .OrderBy(account => account.Name)
            .Select(account => new InvestmentAccountDto(
                account.Id,
                account.Name,
                account.AccountNumber,
                account.Type.ToString(),
                account.CurrencyCode,
                account.Provider,
                account.IsActive,
                account.IncludeInNetWorth,
                account.CreatedAt,
                account.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<InvestmentAccountDto> CreateInvestmentAccountAsync(CreateInvestmentAccountRequest request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Type = AccountType.Investment,
            IsActive = true,
            AccountNumber = request.AccountNumber,
            Provider = request.Provider,
            CurrencyCode = NormalizeCurrency(request.CurrencyCode),
            IncludeInNetWorth = request.IncludeInNetWorth,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.Accounts.Add(account);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new InvestmentAccountDto(
            account.Id,
            account.Name,
            account.AccountNumber,
            account.Type.ToString(),
            account.CurrencyCode,
            account.Provider,
            account.IsActive,
            account.IncludeInNetWorth,
            account.CreatedAt,
            account.UpdatedAt);
    }

    public async Task<InvestmentAccountDto> UpdateInvestmentAccountAsync(Guid accountId, UpdateInvestmentAccountRequest request, CancellationToken cancellationToken)
    {
        var account = await dbContext.Accounts.FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);
        if (account is null)
        {
            throw new KeyNotFoundException($"Investment account '{accountId}' not found.");
        }

        if (account.Type != AccountType.Investment)
        {
            throw new ArgumentException("Account is not an investment account.", nameof(accountId));
        }

        account.Name = request.Name;
        account.AccountNumber = request.AccountNumber;
        account.Provider = request.Provider;
        account.CurrencyCode = NormalizeCurrency(request.CurrencyCode);
        account.IsActive = request.IsActive;
        account.IncludeInNetWorth = request.IncludeInNetWorth;
        account.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new InvestmentAccountDto(
            account.Id,
            account.Name,
            account.AccountNumber,
            account.Type.ToString(),
            account.CurrencyCode,
            account.Provider,
            account.IsActive,
            account.IncludeInNetWorth,
            account.CreatedAt,
            account.UpdatedAt);
    }

    public async Task<InvestmentPortfolioSummaryDto?> GetPortfolioSummaryAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var account = await dbContext.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == accountId && a.Type == AccountType.Investment, cancellationToken);

        if (account is null)
        {
            return null;
        }

        var latestSnapshot = await dbContext.PortfolioSnapshots
            .AsNoTracking()
            .Where(snapshot => snapshot.InvestmentAccountId == accountId)
            .OrderByDescending(snapshot => snapshot.SnapshotDateTime)
            .ThenByDescending(snapshot => snapshot.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var holdingCount = await dbContext.PortfolioHoldings
            .AsNoTracking()
            .Where(holding => holding.InvestmentAccountId == accountId && !holding.IsClosed && holding.Units > 0m)
            .CountAsync(cancellationToken);

        if (latestSnapshot is null)
        {
            return new InvestmentPortfolioSummaryDto(
                accountId,
                account.Name,
                account.CurrencyCode,
                0m,
                0m,
                0m,
                0m,
                0m,
                holdingCount,
                null,
                true);
        }

        var stale = latestSnapshot.SnapshotDateTime < DateTime.UtcNow.AddDays(-3);

        return new InvestmentPortfolioSummaryDto(
            accountId,
            account.Name,
            latestSnapshot.CurrencyCode,
            latestSnapshot.TotalMarketValue,
            latestSnapshot.TotalCostBase,
            latestSnapshot.TotalUnrealisedProfitLoss,
            latestSnapshot.TotalUnrealisedProfitLossPercentage,
            latestSnapshot.TotalDailyValueChange,
            holdingCount,
            latestSnapshot.SnapshotDateTime,
            stale);
    }

    public async Task<IReadOnlyList<InvestmentHoldingDto>> GetHoldingsAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var rows = await (
            from holding in dbContext.PortfolioHoldings.AsNoTracking()
            join instrument in dbContext.InvestmentInstruments.AsNoTracking() on holding.InvestmentInstrumentId equals instrument.Id
            where holding.InvestmentAccountId == accountId
            orderby holding.CurrentMarketValue descending
            select new
            {
                Holding = holding,
                Instrument = instrument
            })
            .ToListAsync(cancellationToken);

        var totalMarketValue = rows.Where(row => !row.Holding.IsClosed).Sum(row => row.Holding.CurrentMarketValue);

        return rows.Select(row => new InvestmentHoldingDto(
                row.Holding.Id,
                row.Instrument.Code,
                row.Instrument.Name,
                row.Instrument.InstrumentType.ToString(),
                row.Instrument.Exchange,
                row.Holding.Units,
                row.Holding.AverageCostPerUnit,
                row.Holding.CurrentPrice,
                row.Holding.TotalCostBase,
                row.Holding.CurrentMarketValue,
                row.Holding.UnrealisedProfitLoss,
                row.Holding.UnrealisedProfitLossPercentage,
                valuationService.CalculateAllocationPercentage(row.Holding.CurrentMarketValue, totalMarketValue),
                row.Holding.CurrencyCode,
                row.Holding.LastUpdatedAt,
                row.Holding.IsClosed))
            .ToList();
    }

    public async Task<InvestmentHoldingDto?> GetHoldingByCodeAsync(Guid accountId, string instrumentCode, CancellationToken cancellationToken)
    {
        var row = await (
            from holding in dbContext.PortfolioHoldings.AsNoTracking()
            join instrument in dbContext.InvestmentInstruments.AsNoTracking() on holding.InvestmentInstrumentId equals instrument.Id
            where holding.InvestmentAccountId == accountId && instrument.Code == instrumentCode
            select new
            {
                Holding = holding,
                Instrument = instrument
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        var portfolioTotal = await dbContext.PortfolioHoldings
            .AsNoTracking()
            .Where(holding => holding.InvestmentAccountId == accountId && !holding.IsClosed)
            .SumAsync(holding => holding.CurrentMarketValue, cancellationToken);

        return new InvestmentHoldingDto(
            row.Holding.Id,
            row.Instrument.Code,
            row.Instrument.Name,
            row.Instrument.InstrumentType.ToString(),
            row.Instrument.Exchange,
            row.Holding.Units,
            row.Holding.AverageCostPerUnit,
            row.Holding.CurrentPrice,
            row.Holding.TotalCostBase,
            row.Holding.CurrentMarketValue,
            row.Holding.UnrealisedProfitLoss,
            row.Holding.UnrealisedProfitLossPercentage,
            valuationService.CalculateAllocationPercentage(row.Holding.CurrentMarketValue, portfolioTotal),
            row.Holding.CurrencyCode,
            row.Holding.LastUpdatedAt,
            row.Holding.IsClosed);
    }

    public async Task<IReadOnlyList<PortfolioSnapshotDto>> GetSnapshotsAsync(Guid accountId, CancellationToken cancellationToken)
    {
        return await dbContext.PortfolioSnapshots
            .AsNoTracking()
            .Where(snapshot => snapshot.InvestmentAccountId == accountId)
            .OrderBy(snapshot => snapshot.SnapshotDateTime)
            .Select(snapshot => new PortfolioSnapshotDto(
                snapshot.Id,
                snapshot.SnapshotDateTime,
                snapshot.CurrencyCode,
                snapshot.TotalMarketValue,
                snapshot.TotalCostBase,
                snapshot.TotalUnrealisedProfitLoss,
                snapshot.TotalUnrealisedProfitLossPercentage,
                snapshot.TotalDailyValueChange,
                snapshot.Source,
                snapshot.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PortfolioImportBatchDto>> GetImportHistoryAsync(Guid accountId, CancellationToken cancellationToken)
    {
        return await dbContext.PortfolioImportBatches
            .AsNoTracking()
            .Where(batch => batch.InvestmentAccountId == accountId)
            .OrderByDescending(batch => batch.ImportDateTime)
            .Select(batch => new PortfolioImportBatchDto(
                batch.Id,
                batch.FileName,
                batch.FileHash,
                batch.ImportDateTime,
                batch.SnapshotDateTime,
                batch.TotalRowsRead,
                batch.SuccessfulRows,
                batch.FailedRows,
                batch.TotalMarketValue,
                batch.TotalCostBase,
                batch.ImportStatus.ToString(),
                batch.ErrorSummary))
            .ToListAsync(cancellationToken);
    }

    private static string NormalizeCurrency(string currencyCode)
    {
        return string.IsNullOrWhiteSpace(currencyCode)
            ? "AUD"
            : currencyCode.Trim().ToUpperInvariant();
    }
}
