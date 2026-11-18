using FinancesDashboard.Application.Dto;
using FinancesDashboard.Domain.Models;
using FinancesDashboard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinancesDashboard.Application.Services;

/// <summary>
/// Analyzes historical mortgage account data from imported CBA transactions.
/// Produces time-series data showing observed mortgage position over time.
/// </summary>
public sealed class MortgageHistoryAnalysisService(FinancesDashboardDbContext dbContext)
{
    /// <summary>
    /// Get the complete transaction history for a mortgage account.
    /// </summary>
    /// <param name="accountId">The mortgage account ID.</param>
    /// <param name="startDate">Optional start date filter (inclusive).</param>
    /// <param name="endDate">Optional end date filter (inclusive).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Time series of mortgage history points ordered by date.</returns>
    public async Task<IReadOnlyList<MortgageHistoryPoint>> GetHistoryAsync(
        Guid accountId,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Transactions
            .AsNoTracking()
            .Where(t => t.AccountId == accountId);

        if (startDate.HasValue)
        {
            query = query.Where(t => t.Date >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(t => t.Date <= endDate.Value);
        }

        var transactions = await query
            .OrderBy(t => t.Date)
            .ToListAsync(cancellationToken);

        return transactions
            .Select(t => new MortgageHistoryPoint(
                t.Date,
                Math.Abs(t.BankBalance),
                t.Amount,
                null, // InterestAmount: cannot be reliably determined from transaction data alone
                null)) // PrincipalAmount: cannot be reliably determined from transaction data alone
            .ToList();
    }

    /// <summary>
    /// Get the current (latest) observed balance for a mortgage account.
    /// </summary>
    /// <param name="accountId">The mortgage account ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The latest BankBalance, or 0 if no transactions exist.</returns>
    public async Task<decimal> GetCurrentBalanceAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var latestTransaction = await dbContext.Transactions
            .AsNoTracking()
            .Where(t => t.AccountId == accountId)
            .OrderByDescending(t => t.Date)
            .FirstOrDefaultAsync(cancellationToken);

        return Math.Abs(latestTransaction?.BankBalance ?? 0m);
    }

    /// <summary>
    /// Get the latest transaction date for a mortgage account.
    /// </summary>
    /// <param name="accountId">The mortgage account ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The latest transaction date, or null if no transactions exist.</returns>
    public async Task<DateOnly?> GetLatestDateAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var latestTransaction = await dbContext.Transactions
            .AsNoTracking()
            .Where(t => t.AccountId == accountId)
            .OrderByDescending(t => t.Date)
            .Select(t => t.Date)
            .FirstOrDefaultAsync(cancellationToken);

        return latestTransaction == default ? null : latestTransaction;
    }

    /// <summary>
    /// Get the total amount paid (sum of negative transactions) for a mortgage account.
    /// </summary>
    /// <param name="accountId">The mortgage account ID.</param>
    /// <param name="startDate">Optional start date filter (inclusive).</param>
    /// <param name="endDate">Optional end date filter (inclusive).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The sum of all negative transaction amounts (absolute value).</returns>
    public async Task<decimal> GetTotalRepaymentsAsync(
        Guid accountId,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Transactions
            .AsNoTracking()
            .Where(t => t.AccountId == accountId && t.Amount < 0m);

        if (startDate.HasValue)
        {
            query = query.Where(t => t.Date >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(t => t.Date <= endDate.Value);
        }

        var totalRepayments = await query
            .SumAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0m;

        // Return absolute value since amounts are negative
        return Math.Abs(totalRepayments);
    }

    /// <summary>
    /// Get the net balance change over a date range.
    /// </summary>
    /// <param name="accountId">The mortgage account ID.</param>
    /// <param name="startDate">Optional start date filter (inclusive).</param>
    /// <param name="endDate">Optional end date filter (inclusive).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The change in balance (latest balance - starting balance) in the date range.</returns>
    public async Task<decimal> GetBalanceChangeAsync(
        Guid accountId,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Transactions
            .AsNoTracking()
            .Where(t => t.AccountId == accountId);

        if (startDate.HasValue)
        {
            query = query.Where(t => t.Date >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(t => t.Date <= endDate.Value);
        }

        var transactions = await query
            .OrderBy(t => t.Date)
            .Select(t => new { t.Date, t.BankBalance })
            .ToListAsync(cancellationToken);

        if (transactions.Count == 0)
        {
            return 0m;
        }

        var startingBalance = Math.Abs(transactions.FirstOrDefault()?.BankBalance ?? 0m);
        var endingBalance = Math.Abs(transactions.LastOrDefault()?.BankBalance ?? 0m);

        return startingBalance - endingBalance;
    }

    /// <summary>
    /// Get summary statistics for a mortgage account history.
    /// </summary>
    /// <param name="accountId">The mortgage account ID.</param>
    /// <param name="startDate">Optional start date filter (inclusive).</param>
    /// <param name="endDate">Optional end date filter (inclusive).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple containing (TransactionCount, CurrentBalance, TotalRepayments, BalanceChange).</returns>
    public async Task<(int TransactionCount, decimal CurrentBalance, decimal TotalRepayments, decimal BalanceChange)> GetSummaryAsync(
        Guid accountId,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Transactions
            .AsNoTracking()
            .Where(t => t.AccountId == accountId);

        if (startDate.HasValue)
        {
            query = query.Where(t => t.Date >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(t => t.Date <= endDate.Value);
        }

        var transactions = await query
            .OrderBy(t => t.Date)
            .ToListAsync(cancellationToken);
        var transactionCount = transactions.Count;
        var currentBalance = Math.Abs(transactions.LastOrDefault()?.BankBalance ?? 0m);
        var totalRepayments = Math.Abs(transactions.Where(t => t.Amount < 0m).Sum(t => t.Amount));
        var balanceChange = transactionCount > 0
            ? (Math.Abs(transactions.First().BankBalance) - Math.Abs(transactions.Last().BankBalance))
            : 0m;

        return (transactionCount, currentBalance, totalRepayments, balanceChange);
    }

    public async Task<decimal?> EstimateAnnualInterestRateAsync(
        Guid accountId,
        IReadOnlyList<Guid> offsetAccountIds,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var mortgageQuery = dbContext.Transactions
            .AsNoTracking()
            .Where(t => t.AccountId == accountId);

        if (startDate.HasValue)
        {
            mortgageQuery = mortgageQuery.Where(t => t.Date >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            mortgageQuery = mortgageQuery.Where(t => t.Date <= endDate.Value);
        }

        var mortgageTransactions = await mortgageQuery
            .OrderBy(t => t.Date)
            .ToListAsync(cancellationToken);

        var interestCharges = mortgageTransactions
            .Where(IsInterestCharge)
            .ToList();

        if (interestCharges.Count == 0)
        {
            return null;
        }

        var offsetTransactionsByAccount = new Dictionary<Guid, List<Transaction>>();

        if (offsetAccountIds.Count > 0)
        {
            var latestInterestDate = interestCharges.Max(t => t.Date);
            var offsetTransactions = await dbContext.Transactions
                .AsNoTracking()
                .Where(t => offsetAccountIds.Contains(t.AccountId) && t.Date <= latestInterestDate)
                .OrderBy(t => t.Date)
                .ToListAsync(cancellationToken);

            offsetTransactionsByAccount = offsetTransactions
                .GroupBy(t => t.AccountId)
                .ToDictionary(group => group.Key, group => group.OrderBy(t => t.Date).ToList());
        }

        var annualRates = new List<decimal>();

        foreach (var interestCharge in interestCharges)
        {
            var interestAmount = decimal.Round(Math.Abs(interestCharge.Amount), 2, MidpointRounding.AwayFromZero);
            if (interestAmount <= 0m)
            {
                continue;
            }

            var balanceBeforeInterest = decimal.Round(
                Math.Max(0m, Math.Abs(interestCharge.BankBalance) - interestAmount),
                2,
                MidpointRounding.AwayFromZero);

            var offsetBalanceAtChargeDate = decimal.Round(
                GetOffsetBalanceAtDate(offsetTransactionsByAccount, interestCharge.Date),
                2,
                MidpointRounding.AwayFromZero);

            var effectivePrincipal = decimal.Round(
                Math.Max(0m, balanceBeforeInterest - offsetBalanceAtChargeDate),
                2,
                MidpointRounding.AwayFromZero);

            if (effectivePrincipal <= 0m)
            {
                continue;
            }

            annualRates.Add(decimal.Round((interestAmount / effectivePrincipal) * 12m, 6, MidpointRounding.AwayFromZero));
        }

        if (annualRates.Count == 0)
        {
            return null;
        }

        return decimal.Round(annualRates.Average(), 6, MidpointRounding.AwayFromZero);
    }

    private static bool IsInterestCharge(Transaction transaction)
    {
        return transaction.Amount > 0m
            && !string.IsNullOrWhiteSpace(transaction.Description)
            && transaction.Description.Contains("interest", StringComparison.OrdinalIgnoreCase);
    }

    private static decimal GetOffsetBalanceAtDate(
        IReadOnlyDictionary<Guid, List<Transaction>> offsetTransactionsByAccount,
        DateOnly date)
    {
        decimal totalOffsetBalance = 0m;

        foreach (var transactions in offsetTransactionsByAccount.Values)
        {
            var latestTransaction = transactions.LastOrDefault(t => t.Date <= date);
            if (latestTransaction is null)
            {
                continue;
            }

            totalOffsetBalance += Math.Max(0m, latestTransaction.BankBalance);
        }

        return totalOffsetBalance;
    }
}
