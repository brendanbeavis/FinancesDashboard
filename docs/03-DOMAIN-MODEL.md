# Domain Model

```csharp
public enum AccountType
{
    Transaction,
    Savings,
    CreditCard,
    Mortgage,
    Investment,
    Other
}

public sealed class Account
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public AccountType Type { get; set; }
    public bool IsActive { get; set; }
}

public sealed class AccountGroup
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
}

public sealed class AccountGroupMember
{
    public Guid AccountGroupId { get; set; }
    public Guid AccountId { get; set; }
}

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

public sealed class InterestRatePeriod
{
    public Guid MortgageId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public decimal AnnualInterestRate { get; set; }
}
```

Future concepts may include transaction categories, transfer matching, recurring transactions, budgets and scenarios.
