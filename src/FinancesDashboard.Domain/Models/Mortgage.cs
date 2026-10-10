namespace FinancesDashboard.Domain.Models;

/// <summary>
/// Mortgage configuration and forecasting assumptions.
/// Historical data is derived from the associated mortgage Account and its Transactions.
/// </summary>
public sealed class Mortgage
{
    /// <summary>
    /// Unique identifier for this mortgage configuration.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Reference to the mortgage Account (AccountType.Mortgage).
    /// Historical balance and transaction data come from this Account.
    /// </summary>
    public Guid AccountId { get; set; }

    /// <summary>
    /// Current annual interest rate for forecasting projections.
    /// </summary>
    public decimal AnnualInterestRate { get; set; }

    /// <summary>
    /// Scheduled repayment amount per period.
    /// </summary>
    public decimal ScheduledRepayment { get; set; }

    /// <summary>
    /// Number of repayments per year (e.g., 12 for monthly).
    /// </summary>
    public int RepaymentsPerYear { get; set; }

    }
