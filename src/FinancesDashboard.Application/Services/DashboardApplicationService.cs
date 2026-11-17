using FinancesDashboard.Application.Dto;
using FinancesDashboard.Application.Forecasting;
using FinancesDashboard.Domain.Enums;
using FinancesDashboard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinancesDashboard.Application.Services;

public sealed class DashboardApplicationService(
    FinancesDashboardDbContext dbContext,
    IForecastStrategy forecastStrategy,
    ForecastOptions forecastOptions,
    CreditCardPositionService creditCardPositionService)
{
    public async Task<DashboardSummary> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var latestAccountBalances = await GetLatestAccountBalancesAsync(cancellationToken);

        // Credit card position is calculated from the sum of all transaction amounts.
        // Get the total accrued balance across all credit card accounts
        var creditCardAccruedBalance = await creditCardPositionService.GetTotalCreditCardPositionAsync(cancellationToken);
        var currentCreditCardPosition = Math.Abs(creditCardAccruedBalance);

        // Build net position: include all non-credit-card accounts via GetNetContribution,
        // then apply the credit card accrued balance (which will be negative for debt)
        var accountNetPosition = latestAccountBalances
            .Where(item => item.Type != AccountType.CreditCard && item.IncludeInNetWorth)
            .Sum(item => GetNetContribution(item.Type, item.Balance))
            + GetNetContribution(AccountType.CreditCard, creditCardAccruedBalance);

        var currentCashPosition = latestAccountBalances
            .Where(item => item.Type is AccountType.Transaction or AccountType.Savings)
            .Sum(item => item.Balance);

        var currentMortgagePosition = latestAccountBalances
            .Where(item => item.Type == AccountType.Mortgage)
            .Sum(item => -Math.Abs(item.Balance));

        var currentInvestmentPosition = latestAccountBalances
            .Where(item => item.Type == AccountType.Investment)
            .Sum(item => item.Balance);

        var latestInvestmentSnapshotDateTime = await dbContext.PortfolioSnapshots
            .AsNoTracking()
            .OrderByDescending(snapshot => snapshot.SnapshotDateTime)
            .Select(snapshot => (DateTime?)snapshot.SnapshotDateTime)
            .FirstOrDefaultAsync(cancellationToken);

        return new DashboardSummary(
            accountNetPosition,
            currentCashPosition,
            currentCreditCardPosition,
            currentMortgagePosition,
            currentInvestmentPosition,
            latestInvestmentSnapshotDateTime);
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

    public async Task<IReadOnlyList<AccountDashboardSummary>> GetAccountSummariesAsync(CancellationToken cancellationToken)
    {
        var accounts = await dbContext.Accounts
            .AsNoTracking()
            .Where(a => a.IsActive)
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken);

        if (accounts.Count == 0)
        {
            return [];
        }

        var summaries = new List<AccountDashboardSummary>(accounts.Count);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var firstDayOfCurrentMonth = new DateOnly(today.Year, today.Month, 1);
        var lastDayOfPreviousMonth = firstDayOfCurrentMonth.AddDays(-1);
        var firstDayOfPreviousMonth = new DateOnly(lastDayOfPreviousMonth.Year, lastDayOfPreviousMonth.Month, 1);

        var transactionsByAccount = await dbContext.Transactions
            .AsNoTracking()
            .GroupBy(t => t.AccountId)
            .Select(g => new
            {
                AccountId = g.Key,
                AllTransactions = g.ToList(),
                LatestTransaction = g.OrderByDescending(t => t.Date).ThenByDescending(t => t.ImportedUtc).First(),
                PreviousMonthTransactions = g.Where(t => t.Date >= firstDayOfPreviousMonth && t.Date <= lastDayOfPreviousMonth).ToList()
            })
            .ToDictionaryAsync(x => x.AccountId, cancellationToken);

        var investmentAccountIds = accounts
            .Where(account => account.Type == AccountType.Investment)
            .Select(account => account.Id)
            .ToList();

        var latestInvestmentSnapshots = await dbContext.PortfolioSnapshots
            .AsNoTracking()
            .Where(snapshot => investmentAccountIds.Contains(snapshot.InvestmentAccountId))
            .ToListAsync(cancellationToken);

        var latestInvestmentSnapshotByAccount = latestInvestmentSnapshots
            .GroupBy(snapshot => snapshot.InvestmentAccountId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(snapshot => snapshot.SnapshotDateTime)
                    .ThenByDescending(snapshot => snapshot.CreatedAt)
                    .First());

        var openHoldingCountByAccount = await dbContext.PortfolioHoldings
            .AsNoTracking()
            .Where(holding => investmentAccountIds.Contains(holding.InvestmentAccountId) && !holding.IsClosed && holding.Units > 0m)
            .GroupBy(holding => holding.InvestmentAccountId)
            .Select(group => new { AccountId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.AccountId, item => item.Count, cancellationToken);

        foreach (var account in accounts)
        {
            decimal currentPosition = 0m;
            bool isLiability = false;
            DateOnly? lastTransactionDate = null;
            int transactionCount = 0;
            decimal previousMonthIncome = 0m;
            decimal previousMonthExpenses = 0m;
            int previousMonthTransactionCount = 0;

            if (account.Type == AccountType.Investment)
            {
                if (latestInvestmentSnapshotByAccount.TryGetValue(account.Id, out var latestSnapshot))
                {
                    currentPosition = latestSnapshot.TotalMarketValue;
                    lastTransactionDate = DateOnly.FromDateTime(latestSnapshot.SnapshotDateTime);
                }

                if (openHoldingCountByAccount.TryGetValue(account.Id, out var holdingCount))
                {
                    transactionCount = holdingCount;
                }
            }
            else if (transactionsByAccount.TryGetValue(account.Id, out var transactionData) && transactionData is not null)
            {
                lastTransactionDate = transactionData.LatestTransaction.Date;
                transactionCount = transactionData.AllTransactions.Count;

                currentPosition = account.Type switch
                {
                    AccountType.CreditCard => Math.Abs(transactionData.AllTransactions.Sum(t => t.Amount)),
                    AccountType.Mortgage => Math.Abs(transactionData.LatestTransaction.BankBalance),
                    _ => transactionData.LatestTransaction.BankBalance
                };

                isLiability = account.Type is AccountType.CreditCard or AccountType.Mortgage;

                if (transactionData.PreviousMonthTransactions.Count > 0)
                {
                    previousMonthIncome = transactionData.PreviousMonthTransactions
                        .Where(t => t.Amount > 0m)
                        .Sum(t => t.Amount);

                    previousMonthExpenses = transactionData.PreviousMonthTransactions
                        .Where(t => t.Amount < 0m)
                        .Sum(t => -t.Amount);

                    previousMonthTransactionCount = transactionData.PreviousMonthTransactions.Count;
                }
            }

            var previousMonthNet = previousMonthIncome - previousMonthExpenses;

            summaries.Add(new AccountDashboardSummary(
                account.Id,
                account.Name,
                account.Type.ToString(),
                currentPosition,
                isLiability,
                previousMonthIncome,
                previousMonthExpenses,
                previousMonthNet,
                lastTransactionDate,
                transactionCount,
                previousMonthTransactionCount,
                account.IsActive));
        }

        return summaries;
    }

    private async Task<decimal> GetCreditCardAccruedBalanceAsync(CancellationToken cancellationToken)
    {
        var creditCardBalance = await dbContext.Transactions
            .AsNoTracking()
            .Where(transaction => 
                dbContext.Accounts
                    .AsNoTracking()
                    .Where(account => account.Type == AccountType.CreditCard)
                    .Select(account => account.Id)
                    .Contains(transaction.AccountId))
            .GroupBy(transaction => transaction.AccountId)
            .Select(group => group.Sum(item => item.Amount))
            .SumAsync(cancellationToken);

        return creditCardBalance;
    }

    private async Task<List<AccountBalance>> GetLatestAccountBalancesAsync(CancellationToken cancellationToken)
    {
        var activeAccounts = await dbContext.Accounts
            .AsNoTracking()
            .Where(account => account.IsActive)
            .Select(account => new { account.Id, account.Type, account.IncludeInNetWorth })
            .ToListAsync(cancellationToken);

        var balances = new List<AccountBalance>(activeAccounts.Count);

        var nonInvestmentAccountIds = activeAccounts
            .Where(account => account.Type != AccountType.Investment)
            .Select(account => account.Id)
            .ToList();

        if (nonInvestmentAccountIds.Count > 0)
        {
            var latestPerAccount = await dbContext.Transactions
                .AsNoTracking()
                .Where(transaction => nonInvestmentAccountIds.Contains(transaction.AccountId))
                .GroupBy(transaction => transaction.AccountId)
                .Select(group => new
                {
                    AccountId = group.Key,
                    MaxDate = group.Max(item => item.Date)
                })
                .ToListAsync(cancellationToken);

            foreach (var latest in latestPerAccount)
            {
                var snapshot = await (
                    from transaction in dbContext.Transactions.AsNoTracking()
                    join account in dbContext.Accounts.AsNoTracking() on transaction.AccountId equals account.Id
                    where transaction.AccountId == latest.AccountId && transaction.Date == latest.MaxDate
                    orderby transaction.ImportedUtc descending
                    select new
                    {
                        account.Type,
                        transaction.BankBalance,
                        account.IncludeInNetWorth
                    })
                    .FirstOrDefaultAsync(cancellationToken);

                if (snapshot is not null)
                {
                    balances.Add(new AccountBalance(snapshot.Type, snapshot.BankBalance, snapshot.IncludeInNetWorth));
                }
            }
        }

        var investmentAccountIds = activeAccounts
            .Where(account => account.Type == AccountType.Investment)
            .Select(account => account.Id)
            .ToList();

        if (investmentAccountIds.Count > 0)
        {
            var latestInvestmentSnapshots = await dbContext.PortfolioSnapshots
                .AsNoTracking()
                .Where(snapshot => investmentAccountIds.Contains(snapshot.InvestmentAccountId))
                .ToListAsync(cancellationToken);

            var latestByAccount = latestInvestmentSnapshots
                .GroupBy(snapshot => snapshot.InvestmentAccountId)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .OrderByDescending(snapshot => snapshot.SnapshotDateTime)
                        .ThenByDescending(snapshot => snapshot.CreatedAt)
                        .First());

            foreach (var account in activeAccounts.Where(account => account.Type == AccountType.Investment))
            {
                if (latestByAccount.TryGetValue(account.Id, out var latestSnapshot))
                {
                    balances.Add(new AccountBalance(account.Type, latestSnapshot.TotalMarketValue, account.IncludeInNetWorth));
                    continue;
                }

                var latestTransactionBalance = await dbContext.Transactions
                    .AsNoTracking()
                    .Where(transaction => transaction.AccountId == account.Id)
                    .OrderByDescending(transaction => transaction.Date)
                    .ThenByDescending(transaction => transaction.ImportedUtc)
                    .Select(transaction => (decimal?)transaction.BankBalance)
                    .FirstOrDefaultAsync(cancellationToken);

                if (latestTransactionBalance.HasValue)
                {
                    balances.Add(new AccountBalance(account.Type, latestTransactionBalance.Value, account.IncludeInNetWorth));
                }
            }
        }

        return balances;
    }

    private sealed record AccountBalance(AccountType Type, decimal Balance, bool IncludeInNetWorth);
}
