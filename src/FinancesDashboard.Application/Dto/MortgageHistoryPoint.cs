namespace FinancesDashboard.Application.Dto;

/// <summary>
/// Represents a single observed point in mortgage transaction history.
/// Based on actual CBA imported transaction data.
/// </summary>
public sealed record MortgageHistoryPoint(
    /// <summary>
    /// Transaction date from the bank statement.
    /// </summary>
    DateOnly Date,
    /// <summary>
    /// Observed account balance at this transaction (from BankBalance).
    /// </summary>
    decimal Balance,
    /// <summary>
    /// Transaction amount. Negative values indicate a payment/repayment.
    /// </summary>
    decimal PaymentAmount,
    /// <summary>
    /// Interest component of the transaction (if reliably determined from data).
    /// Null if insufficient data to calculate reliably.
    /// </summary>
    decimal? InterestAmount,
    /// <summary>
    /// Principal component of the transaction (if reliably determined from data).
    /// Null if insufficient data to calculate reliably.
    /// </summary>
    decimal? PrincipalAmount);
