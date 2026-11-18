namespace FinancesDashboard.Web.Models;

/// <summary>
/// Represents a single point in mortgage history (observed balance history).
/// </summary>
public sealed record MortgageHistoryPointModel(
    DateOnly Date,
    decimal Balance,
    decimal PaymentAmount,
    decimal? InterestAmount,
    decimal? PrincipalAmount);
