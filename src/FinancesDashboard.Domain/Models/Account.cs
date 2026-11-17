using FinancesDashboard.Domain.Enums;

namespace FinancesDashboard.Domain.Models;

public sealed class Account
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public AccountType Type { get; set; }

    public bool IsActive { get; set; }

    public string? AccountNumber { get; set; }

    public string CurrencyCode { get; set; } = "AUD";

    public string? Provider { get; set; }

    public bool IncludeInNetWorth { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
