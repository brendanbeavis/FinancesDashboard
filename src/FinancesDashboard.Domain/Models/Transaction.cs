namespace FinancesDashboard.Domain.Models;

public sealed class Transaction
{
    public Guid Id { get; set; }

    public Guid AccountId { get; set; }

    public DateOnly Date { get; set; }

    public decimal Amount { get; set; }

    public required string Description { get; set; }

    public decimal BankBalance { get; set; }

    public required string ImportHash { get; set; }

    public DateTime ImportedUtc { get; set; }
}
