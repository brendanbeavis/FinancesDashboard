# FinancesDashboard

FinancesDashboard is a .NET 10-based personal finances dashboard: import bank/credit card CSVs, model transactions, run forecasts, and generate reports.

**Key Features**
- Importers for CSV exports (CBA, Coles, etc.)
- Domain model for accounts, transactions, forecasts and mortgages
- API service, app host, and Blazor web UI
- Test suite covering importers, forecasting, and reporting

**Repository Structure**
- FinancesDashboard.slnx — solution file
- src/FinancesDashboard.ApiService — web API project
- src/FinancesDashboard.AppHost — host and runtime configuration
- src/FinancesDashboard.Application — application services, DTOs, and use-cases
- src/FinancesDashboard.Domain — domain models and enums
- src/FinancesDashboard.Infrastructure — persistence and migrations
- src/FinancesDashboard.Web — (UI) Blazor frontend
- docs/ — design, API, and implementation docs
- data/ — sample CSV imports for testing and demos
- src/FinancesDashboard.Tests — unit & integration tests

Getting started

Prerequisites
- .NET 10 SDK
- (Optional) Visual Studio 2022/2023 or Visual Studio Code

Quick start (CLI)

Restore, build and run tests:

```powershell
dotnet restore
dotnet build --no-restore
dotnet test
```

Run the API locally (from the solution root):

```powershell
dotnet run --project src/FinancesDashboard.ApiService
```

Run the AppHost:

```powershell
dotnet run --project src/FinancesDashboard.AppHost
```

Import sample data
- Sample CSV files are in the `data/` directory. See docs/04-CBA-CSV-ETL.md for the import process and format expectations.

Documentation
- See the `docs/` folder for requirements, architecture, domain model, API design, and implementation notes.

Testing
- Unit and integration tests live in `src/FinancesDashboard.Tests`. Use `dotnet test` to run them.

Contributing
- Follow the coding guidelines in the `docs/` folder. Add focused unit tests for new domain behaviour and run the test suite before submitting PRs.

License
- This repository currently has no license file. Add a `LICENSE` if you intend to publish or share.

Questions or next steps
- Want a short developer setup guide (VS Code launch tasks, local DB seed scripts), CI config, or a CONTRIBUTING.md? I can add one.
