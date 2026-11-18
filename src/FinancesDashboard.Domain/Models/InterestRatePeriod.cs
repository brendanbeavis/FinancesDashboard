namespace FinancesDashboard.Domain.Models;

public sealed class InterestRatePeriod
{
    public Guid MortgageId { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public decimal AnnualInterestRate { get; set; }
}
