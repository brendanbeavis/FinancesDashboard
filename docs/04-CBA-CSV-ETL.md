# CBA CSV ETL

## Source Format

The CBA export is headerless and contains four columns:

```text
TIMESTAMP,AMOUNT,DESCRIPTION,NET_ACCOUNT_TOTAL
```

Example:

```text
31/08/2026,-1560,Loan Repayment LN REPAY 564572311,524482.92
27/08/2026,-413.25,Direct Debit 613602 QP Haven Early L DT.5v7aba QKP_Have,526042.92
27/08/2026,-51.17,KANDO ROWVILLE,526456.17
```

## Parsing

- Date: `dd/MM/yyyy`.
- Decimal: invariant culture.
- Preserve description.
- Preserve signed amount.
- Preserve `NET_ACCOUNT_TOTAL` as `BankBalance`.
- Do not reconstruct the balance when the source provides it.

CsvHelper configuration should use `HasHeaderRecord = false` and trimming.

## Idempotency

Calculate a deterministic SHA-256 hash from:

```text
AccountId + Date + Amount + Description + BankBalance
```

Store it in `ImportHash` with a unique database index.

Also maintain an in-memory `HashSet<string>` during a single import.

## Result

```csharp
public sealed record ImportResult(
    int Imported,
    int Skipped,
    IReadOnlyList<string> Errors);
```

Errors should identify the row and reason.

## Workflow

Upload -> select account -> validate -> parse -> hash -> duplicate check -> persist -> return summary.

Source transaction amounts, descriptions and balances must never be silently rewritten.
