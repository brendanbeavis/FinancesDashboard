# API Design

## Endpoints

- `GET /api/health`
- `GET /api/accounts`
- `POST /api/accounts`
- `POST /api/import/cba/{accountId}`
- `GET /api/dashboard/summary`
- `GET /api/dashboard/balance?accountId=...`
- `GET /api/dashboard/cashflow?accountId=...`
- `GET /api/dashboard/credit-card-spend`
- `GET /api/dashboard/forecast?accountId=...&months=...`
- `GET /api/mortgages`
- `POST /api/mortgages`

## DTOs

```csharp
public sealed record AccountDto(Guid Id, string Name, string Type, bool IsActive);
public sealed record CreateAccountRequest(string Name, string Type);
public sealed record ImportResult(int Imported, int Skipped, IReadOnlyList<string> Errors);

public sealed record BalancePoint(DateOnly Date, decimal Balance);
public sealed record CashflowPoint(DateOnly Date, decimal Income, decimal Expenses, decimal Net);
public sealed record CreditCardPoint(DateOnly Date, decimal Spend);
public sealed record ForecastPoint(DateOnly Date, decimal ProjectedPosition);

public sealed record DashboardSummary(
    decimal CurrentNetPosition,
    decimal CurrentCashPosition,
    decimal CurrentCreditCardPosition,
    decimal CurrentMortgagePosition);
```

Validate requests at the boundary, support cancellation tokens and do not expose EF entities directly.
