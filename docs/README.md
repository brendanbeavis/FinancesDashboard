# FinanceReport Documentation

FinanceReport is a .NET 10 / C# personal finance reporting application using Blazor, ASP.NET Core, EF Core and SQLite.

## Documentation

- [01 Requirements](01-REQUIREMENTS.md)
- [02 Architecture](02-ARCHITECTURE.md)
- [03 Domain Model](03-DOMAIN-MODEL.md)
- [04 CBA CSV ETL](04-CBA-CSV-ETL.md)
- [05 Data Model](05-DATA-MODEL.md)
- [06 API Design](06-API-DESIGN.md)
- [07 Blazor UI](07-BLAZOR-UI.md)
- [08 Financial Reporting](08-FINANCIAL-REPORTING.md)
- [09 Forecasting](09-FORECASTING.md)
- [10 Mortgage Model](10-MORTGAGE-MODEL.md)
- [11 Testing](11-TESTING.md)
- [12 Implementation Roadmap](12-IMPLEMENTATION-ROADMAP.md)
- [13 Copilot Implementation Prompts](13-COPILOT-IMPLEMENTATION-PROMPTS.md)

## First Vertical Slice

Create Account -> Upload CBA CSV -> Import Transactions -> View Current Balance -> View Balance History.

## Charting Notes

The mortgage UI uses `Blazor-ApexCharts` (`Blazor-ApexCharts` NuGet package) for the interactive mortgage balance chart. The package is registered through `builder.Services.AddApexCharts()` in the Blazor web app and imported via `@using ApexCharts` in component imports. It was selected because it is a maintained Blazor charting library that supports responsive datetime line charts, dashed forecast styling, tooltips, and annotations without requiring a custom manual SVG/canvas chart implementation.
