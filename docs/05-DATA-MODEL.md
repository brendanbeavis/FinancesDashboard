# Data Model

Use SQLite with EF Core migrations.

## Tables

### Accounts
`Id`, `Name`, `Type`, `IsActive`

### Transactions
`Id`, `AccountId`, `Date`, `Amount`, `Description`, `BankBalance`, `ImportHash`, `ImportedUtc`

### AccountGroups
`Id`, `Name`

### AccountGroupMembers
Composite key: `AccountGroupId + AccountId`

### Mortgages
Current mortgage configuration.

### InterestRatePeriods
Mortgage rate history.

## Relationships

```text
Account 1 ---- * Transaction
Account * ---- * AccountGroup
Mortgage 1 ---- * InterestRatePeriod
```

## Indexes

Transactions:
- `AccountId`
- `Date`
- `AccountId + Date`
- unique `ImportHash`

Use `decimal` for money and `DateOnly` for transaction dates. Import timestamps are UTC.

Use EF Core migrations rather than `EnsureCreated()` for the normal database lifecycle.
