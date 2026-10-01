namespace FinancesDashboard.Domain.Models;

public sealed class AccountGroup
{
    public Guid Id { get; set; }

    public required string Name { get; set; }
}
