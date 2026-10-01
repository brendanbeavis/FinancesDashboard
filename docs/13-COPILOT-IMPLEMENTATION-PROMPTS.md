# Copilot Implementation Prompts

Run these prompts sequentially. Each prompt should implement only its documented scope, add appropriate tests and keep the solution building.

## 1. Solution Foundation

> Read `docs/01-REQUIREMENTS.md` and `docs/02-ARCHITECTURE.md`. Create the .NET 10 solution with Domain, Application, Infrastructure, Api, Web and Tests projects. Configure references to respect the documented dependency direction. Do not implement business features yet. Build and fix compilation issues.

## 2. Domain

> Read `docs/03-DOMAIN-MODEL.md`. Implement the documented entities and enums without coupling the domain to EF Core, ASP.NET Core or Blazor. Add appropriate unit tests.

## 3. Persistence

> Read `docs/05-DATA-MODEL.md`. Implement EF Core SQLite persistence, DbContext, configurations, relationships, indexes and migrations. Use decimal for money and DateOnly for financial dates. Add SQLite integration tests.

## 4. CBA Import

> Read `docs/04-CBA-CSV-ETL.md`. Implement the headerless CBA importer with dd/MM/yyyy dates, invariant decimals, preserved descriptions/balances, deterministic SHA-256 hashes, duplicate detection and row-level errors. Add realistic fixture tests.

## 5. API

> Read `docs/06-API-DESIGN.md`. Implement the documented ASP.NET Core endpoints using application services and DTOs. Add validation, cancellation support and API integration tests.

## 6. Reporting

> Read `docs/08-FINANCIAL-REPORTING.md`. Implement account balance history, grouped positions, income, expenses, net cashflow, credit-card reporting and dashboard summary. Use observed BankBalance values for account positions. Add deterministic tests.

## 7. Forecasting

> Read `docs/09-FORECASTING.md`. Implement IForecastStrategy and the average-monthly-net-cashflow strategy with configurable lookback/horizon. Clearly mark results as projected. Add tests.

## 8. Mortgage

> Read `docs/10-MORTGAGE-MODEL.md`. Implement mortgage persistence, API and amortisation services, including rate history and remaining projected interest. Test repayment, interest, principal reduction, payoff and rate changes.

## 9. Blazor Dashboard

> Read `docs/07-BLAZOR-UI.md`. Implement navigation, accounts, import, dashboard and mortgages. Complete the first vertical slice: create account -> upload CBA CSV -> import -> view current balance -> view balance history. Keep calculations out of UI components.

## 10. Quality Review

> Review the whole solution against every document in `docs/`. Find missing requirements, dependency violations, financial calculation errors, validation gaps, missing tests and API/UI inconsistencies. Fix concrete issues, run the full test suite and build the entire solution. Do not add unrelated features.
