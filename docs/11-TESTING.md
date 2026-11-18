# Testing

## Unit Tests

### Import
- Valid rows.
- Headerless CSV.
- Date and decimal parsing.
- Invalid values.
- Duplicate rows.
- Duplicate rows within one file.
- Deterministic hashes.

### Reporting
- Latest balance selection.
- Date boundaries.
- Account grouping.
- Income/expense/net cashflow.
- Empty data.
- Credit-card filtering.

### Forecasting
- Monthly average.
- Lookback.
- Starting position.
- Multiple months.
- Empty history.
- Zero cashflow.

### Mortgage
- Interest.
- Principal reduction.
- Payoff.
- Rate changes.
- Remaining interest.

## Integration

Test:

```text
CSV -> Import API -> EF Core -> SQLite -> Dashboard API
```

## UI Acceptance

Verify:
- Dashboard loads.
- Account creation.
- CSV import.
- Re-import produces no duplicates.
- Charts render.
- Mortgage creation.
- Forecast is distinct from history.

Maintain deterministic CSV fixtures for normal transactions, duplicates, transfers, credit-card activity and mortgage repayments.
