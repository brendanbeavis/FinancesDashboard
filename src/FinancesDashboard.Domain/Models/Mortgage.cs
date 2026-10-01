namespace FinancesDashboard.Domain.Models;

public sealed class Mortgage
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public decimal OriginalPrincipal { get; set; }

    public decimal CurrentPrincipal { get; set; }

    public decimal AnnualInterestRate { get; set; }

    public decimal ScheduledRepayment { get; set; }

    public int RepaymentsPerYear { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly? ExpectedEndDate { get; set; }
}
