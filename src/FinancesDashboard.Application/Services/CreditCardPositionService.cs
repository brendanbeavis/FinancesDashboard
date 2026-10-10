using FinancesDashboard.Domain.Enums;
using FinancesDashboard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinancesDashboard.Application.Services;

/// <summary>
/// Calculates credit card account position from signed transaction history.
/// 
/// Sign Convention:
/// - CSV imports use: negative amounts = purchases/debits, positive amounts = payments/credits
/// - These are stored as-is in the Transaction.Amount field
/// - Position calculation: starting from opening balance (default $0), sum all signed amounts
/// - Display: absolute value with liability flag to indicate "amount owing"
/// 
/// Baseline Handling:
/// - Default assumption: account starts at $0 opening balance when not explicitly configured
/// - Partial histories are acceptable; flagged with disclaimer when opening balance not confirmed
/// </summary>
public sealed class CreditCardPositionService(FinancesDashboardDbContext dbContext)
{
    /// <summary>
    /// Calculates the current credit card position for an account.
    /// </summary>
    /// <param name="accountId">The credit card account ID</param>
    /// <param name="openingBalance">Opening balance baseline (default $0). Positive = amount owing, negative = credit balance</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Position calculation result with metadata about reliability</returns>
    public async Task<CreditCardPositionResult> CalculatePositionAsync(
        Guid accountId, 
        decimal openingBalance = 0m,
        CancellationToken cancellationToken = default)
    {
        // Verify account exists and is a credit card
        var account = await dbContext.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);

        if (account is null)
        {
            throw new KeyNotFoundException($"Account '{accountId}' not found.");
        }

        if (account.Type != AccountType.CreditCard)
        {
            throw new ArgumentException($"Account '{accountId}' is not a credit card account.", nameof(accountId));
        }

        // Get all transactions for the account ordered by date
        var transactions = await dbContext.Transactions
            .AsNoTracking()
            .Where(t => t.AccountId == accountId)
            .OrderBy(t => t.Date)
            .ThenBy(t => t.ImportedUtc)
            .ToListAsync(cancellationToken);

        // Determine if this is a credit (positive position = overpayment/credit) or owing (negative = debt)
        // When opening balance is the default $0 (not explicitly confirmed), include disclaimer
        var isOpeningBalanceConfirmed = openingBalance != 0m;
        var disclaimerText = openingBalance == 0m 
            ? "Position based on imported transaction history (opening balance assumed to be $0)"
            : null;

        // If no transactions, position is the opening balance
        if (transactions.Count == 0)
        {
            return new CreditCardPositionResult(
                Position: openingBalance,
                AbsolutePosition: Math.Abs(openingBalance),
                IsCredit: openingBalance > 0m,
                TransactionCount: 0,
                HasTransactions: false,
                OpeningBalance: openingBalance,
                IsOpeningBalanceConfirmed: isOpeningBalanceConfirmed,
                Disclaimer: disclaimerText);
        }

        // Sum all transaction amounts starting from opening balance
        var summedAmount = transactions.Sum(t => t.Amount);
        var currentPosition = openingBalance + summedAmount;

        // Determine if this is a credit (positive position = overpayment/credit) or owing (negative = debt)
        var isCredit = currentPosition > 0m;

        // For display, use absolute value
        var absolutePosition = Math.Abs(currentPosition);

        return new CreditCardPositionResult(
            Position: currentPosition,
            AbsolutePosition: absolutePosition,
            IsCredit: isCredit,
            TransactionCount: transactions.Count,
            HasTransactions: true,
            OpeningBalance: openingBalance,
            IsOpeningBalanceConfirmed: isOpeningBalanceConfirmed,
            Disclaimer: disclaimerText);
    }

    /// <summary>
    /// Calculates the total credit card position across all credit card accounts.
    /// </summary>
    public async Task<decimal> GetTotalCreditCardPositionAsync(CancellationToken cancellationToken = default)
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
}

/// <summary>
/// Result of a credit card position calculation, including metadata about reliability.
/// </summary>
public sealed record CreditCardPositionResult(
    /// <summary>Signed position: positive = owing, negative = credit balance</summary>
    decimal Position,

    /// <summary>Absolute value for display (always positive)</summary>
    decimal AbsolutePosition,

    /// <summary>True if account has a credit balance (negative position, overpaid)</summary>
    bool IsCredit,

    /// <summary>Number of transactions included in calculation</summary>
    int TransactionCount,

    /// <summary>Whether the calculation includes any transactions</summary>
    bool HasTransactions,

    /// <summary>The opening balance used for calculation (typically $0 unless configured)</summary>
    decimal OpeningBalance,

    /// <summary>True if opening balance was explicitly set, false if using default $0</summary>
    bool IsOpeningBalanceConfirmed,

    /// <summary>Disclaimer text if data is incomplete or based on assumptions (null if none)</summary>
    string? Disclaimer);
