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

### Mortgage Charting

- Selected library: `Blazor-ApexCharts`
- NuGet package: `Blazor-ApexCharts` version `7.0.0`
- Configuration required:
  - Add the package to `src/FinancesDashboard.Web/FinancesDashboard.Web.csproj`
  - Register `builder.Services.AddApexCharts()` in `src/FinancesDashboard.Web/Program.cs`
  - Add `@using ApexCharts` to `src/FinancesDashboard.Web/Components/_Imports.razor`
- Why selected:
  - Maintained Blazor-compatible package with strong adoption
  - Supports responsive line charts, datetime axes, shared tooltips, dashed forecast styling and annotations
  - Avoids custom SVG/canvas chart implementation while fitting the existing interactive server Blazor app
- Current mortgage chart behaviour:
  - Historical mortgage balance is displayed as a solid line
  - Forecast mortgage balance is displayed as a dashed line
  - Historical and projected data are clearly separated
  - A zero balance reference line is shown when supported by the chart options

## UI Principles

- Financial calculations belong in application services, not components.
- Clearly distinguish observed and projected data.
- Use consistent currency formatting.
- Provide loading, empty and error states.
- Confirm destructive actions.
