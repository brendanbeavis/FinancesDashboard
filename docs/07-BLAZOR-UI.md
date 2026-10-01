# Blazor UI

## Navigation

- Dashboard
- Accounts
- Import
- Mortgages
- Settings

## Dashboard

Cards:
- Net Position
- Cash
- Credit Card
- Mortgage

Charts:
- Net position over time
- Account balance history
- Income versus expenses
- Credit-card spending
- Forecast
- Mortgage balance
- Mortgage interest versus principal

## Import

Provide:
1. Account selector.
2. File picker.
3. Import action.
4. Progress/status.
5. Imported/skipped/error summary.

## Accounts

Provide account list, account type, active state, creation/editing, account history and groups.

## Mortgages

Provide mortgage CRUD, rate history, projected payoff, remaining interest and amortisation chart.

## UI Principles

- Financial calculations belong in application services, not components.
- Clearly distinguish observed and projected data.
- Use consistent currency formatting.
- Provide loading, empty and error states.
- Confirm destructive actions.
