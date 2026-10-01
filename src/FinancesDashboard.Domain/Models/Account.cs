using FinancesDashboard.Domain.Enums;

namespace FinancesDashboard.Domain.Models;

public sealed class Account
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public AccountType Type { get; set; }

    public bool IsActive { get; set; }
}
