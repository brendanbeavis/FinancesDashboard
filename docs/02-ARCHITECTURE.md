# Architecture

```text
FinanceReport.Web
        |
        v
FinanceReport.Api
        |
        v
FinanceReport.Application
       / \
      v   v
 Domain  Infrastructure
           |
           v
         SQLite
```

## Projects

- `FinanceReport.Domain` — entities, enums and domain rules.
- `FinanceReport.Application` — use cases, DTOs, interfaces, reporting, forecasting and import orchestration.
- `FinanceReport.Infrastructure` — EF Core, SQLite, migrations and repository implementations.
- `FinanceReport.Api` — HTTP endpoints and composition.
- `FinanceReport.Web` — Blazor UI.
- `FinanceReport.Tests` — unit/integration/acceptance tests.

## Principles

1. Domain must not depend on EF Core, ASP.NET Core or Blazor.
2. Keep financial calculations outside UI components.
3. Use DTOs at API boundaries.
4. Treat imported bank data as source data.
5. Prefer deterministic calculations.
6. Make forecast assumptions explicit.
7. Keep the first vertical slice small and testable.
