namespace FinancesDashboard.Web.Models;

/// <summary>
/// Current mortgage position data from historical transaction analysis.
/// </summary>
public sealed class MortgageCurrentPositionModel
{
    public decimal CurrentBalance { get; set; }
    public decimal TotalOffsetBalance { get; set; }
    public decimal InterestBearingBalance { get; set; }
    public decimal? EstimatedAnnualInterestRate { get; set; }
    public DateOnly LatestDate { get; set; }
    public decimal TotalRepayments { get; set; }
    public int TransactionCount { get; set; }
    public DateOnly? EarliestDate { get; set; }
    public IReadOnlyList<Guid> OffsetAccountIds { get; set; } = [];
}
