using FinancesDashboard.Application.Dto;
using FinancesDashboard.Application.Forecasting;
using FinancesDashboard.Domain.Enums;
using FinancesDashboard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinancesDashboard.Application.Services;

public sealed class DashboardApplicationService(
    FinancesDashboardDbContext dbContext,
    IForecastStrategy forecastStrategy,
    ForecastOptions forecastOptions)
{
    public async Task<DashboardSummary> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var latestAccountBalances = await GetLatestAccountBalancesAsync(cancellationToken);

        var accountNetPosition = latestAccountBalances
            .Sum(item => GetNetContribution(item.Type, item.Balance));

        var currentCashPosition = latestAccountBalances
            .Where(item => item.Type is AccountType.Transaction or AccountType.Savings)
            .Sum(item => item.Balance);

        var currentCreditCardPosition = latestAccountBalances
            .Where(item => item.Type == AccountType.CreditCard)
            .Sum(item => item.Balance);

        var currentMortgagePosition = -await dbContext.Mortgages
            .AsNoTracking()
            .SumAsync(mortgage => (decimal?)mortgage.CurrentPrincipal, cancellationToken) ?? 0m;

        return new DashboardSummary(
            accountNetPosition + currentMortgagePosition,
            currentCashPosition,
            currentCreditCardPosition,
            currentMortgagePosition);
    }

    public async Task<IReadOnlyList<BalancePoint>> GetBalanceAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var transactions = await dbContext.Transactions
            .AsNoTracking()
            .Where(transaction => transaction.AccountId == accountId)
            .Select(transaction => new
            {
                transaction.Date,
                transaction.BankBalance,
                transaction.ImportedUtc
            })
            .ToListAsync(cancellationToken);

        return transactions
            .GroupBy(transaction => transaction.Date)
            .Select(group => group
                .OrderByDescending(item => item.ImportedUtc)
                .First())
            .Select(item => new BalancePoint(item.Date, item.BankBalance))
            .OrderBy(point => point.Date)
            .ToList();
    }

    public async Task<IReadOnlyList<GroupPositionPoint>> GetGroupPositionAsync(Guid accountGroupId, CancellationToken cancellationToken)
    {
        var accountIds = await dbContext.AccountGroupMembers
            .AsNoTracking()
            .Where(member => member.AccountGroupId == accountGroupId)
            .Select(member => member.AccountId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (accountIds.Count == 0)
        {
            return [];
        }

        var transactionSnapshots = await dbContext.Transactions
            .AsNoTracking()
            .Where(transaction => accountIds.Contains(transaction.AccountId))
            .Select(transaction => new
            {
                transaction.AccountId,
                transaction.Date,
                transaction.BankBalance,
                transaction.ImportedUtc
            })
            .ToListAsync(cancellationToken);

        if (transactionSnapshots.Count == 0)
        {
            return [];
        }

        var latestByAccountAndDate = transactionSnapshots
            .GroupBy(item => new { item.AccountId, item.Date })
            .Select(group => group
                .OrderByDescending(item => item.ImportedUtc)
                .First())
            .ToList();

        var reportDates = latestByAccountAndDate
            .Select(item => item.Date)
            .Distinct()
            .OrderBy(date => date)
            .ToList();

        var points = new List<GroupPositionPoint>(reportDates.Count);
        foreach (var reportDate in reportDates)
        {
            var position = latestByAccountAndDate
                .GroupBy(item => item.AccountId)
                .Select(group => group
                    .Where(item => item.Date <= reportDate)
                    .OrderByDescending(item => item.Date)
                    .FirstOrDefault())
                .Where(item => item is not null)
                .Sum(item => item!.BankBalance);

            points.Add(new GroupPositionPoint(reportDate, position));
        }

        return points;
    }

    public async Task<IReadOnlyList<CashflowPoint>> GetCashflowAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var grouped = await dbContext.Transactions
            .AsNoTracking()
            .Where(transaction => transaction.AccountId == accountId)
            .GroupBy(transaction => transaction.Date)
            .Select(group => new
            {
                Date = group.Key,
                Income = group.Where(item => item.Amount > 0m).Sum(item => item.Amount),
                Expenses = group.Where(item => item.Amount < 0m).Sum(item => -item.Amount)
            })
            .OrderBy(item => item.Date)
            .ToListAsync(cancellationToken);

        return grouped
            .Select(item => new CashflowPoint(item.Date, item.Income, item.Expenses, item.Income - item.Expenses))
            .ToList();
    }

    private static decimal GetNetContribution(AccountType accountType, decimal balance)
    {
        return accountType switch
        {
            AccountType.Transaction => balance,
            AccountType.Savings => balance,
            AccountType.Investment => balance,
            AccountType.CreditCard => -Math.Abs(balance),
            AccountType.Mortgage => -Math.Abs(balance),
            AccountType.Other => -Math.Abs(balance),
            _ => balance
        };
    }

    public async Task<IReadOnlyList<CreditCardPoint>> GetCreditCardSpendAsync(CancellationToken cancellationToken)
    {
        var creditCardAccountIds = await dbContext.Accounts
            .AsNoTracking()
            .Where(account => account.Type == AccountType.CreditCard)
            .Select(account => account.Id)
            .ToListAsync(cancellationToken);

        if (creditCardAccountIds.Count == 0)
        {
            return [];
        }

        var transactions = await dbContext.Transactions
            .AsNoTracking()
            .Where(transaction => creditCardAccountIds.Contains(transaction.AccountId) && transaction.Amount < 0m)
            .Select(transaction => new
            {
                transaction.Date,
                transaction.Amount
            })
            .ToListAsync(cancellationToken);

        var spend = transactions
            .GroupBy(transaction => transaction.Date)
            .Select(group => new CreditCardPoint(group.Key, group.Sum(item => -item.Amount)))
            .OrderBy(point => point.Date)
            .ToList();

        return spend;
    }

    public async Task<IReadOnlyList<ForecastPoint>> GetForecastAsync(Guid accountId, int months, CancellationToken cancellationToken)
    {
        if (months <= 0)
        {
            return [];
        }

        var boundedMonths = Math.Min(months, Math.Max(1, forecastOptions.MaxHorizonMonths));

        var observedHistory = await GetBalanceAsync(accountId, cancellationToken);
        if (observedHistory.Count == 0)
        {
            return [];
        }

        var startPoint = observedHistory[^1];

        var cashflowHistory = await GetCashflowAsync(accountId, cancellationToken);
        var projectedDeltas = forecastStrategy.Forecast(cashflowHistory, startPoint.Date, boundedMonths);

        return projectedDeltas
            .Select(point => point with { ProjectedPosition = decimal.Round(startPoint.Balance + point.ProjectedPosition, 2, MidpointRounding.AwayFromZero) })
            .ToList();
    }

    private async Task<List<AccountBalance>> GetLatestAccountBalancesAsync(CancellationToken cancellationToken)
    {
        var latestPerAccount = await dbContext.Transactions
            .AsNoTracking()
            .GroupBy(transaction => transaction.AccountId)
            .Select(group => new
            {
                AccountId = group.Key,
                MaxDate = group.Max(item => item.Date)
            })
            .ToListAsync(cancellationToken);

        var balances = new List<AccountBalance>();
        foreach (var latest in latestPerAccount)
        {
            var snapshot = await (
                from transaction in dbContext.Transactions.AsNoTracking()
                join account in dbContext.Accounts.AsNoTracking() on transaction.AccountId equals account.Id
                where transaction.AccountId == latest.AccountId && transaction.Date == latest.MaxDate
                orderby transaction.ImportedUtc descending
                select new AccountBalance(account.Type, transaction.BankBalance))
                .FirstOrDefaultAsync(cancellationToken);

            if (snapshot is not null)
            {
                balances.Add(snapshot);
            }
        }

        return balances;
    }

    private sealed record AccountBalance(AccountType Type, decimal Balance);
}
