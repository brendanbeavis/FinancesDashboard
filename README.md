# FinancesDashboard

FinancesDashboard is a .NET 10 personal finances dashboard for importing bank/credit-card CSVs, modelling transactions and accounts, running forecasts (including mortgage amortisation), and generating financial reports.

**Key Features**
- CSV importers and ETL for common exports (CBA, Coles, etc.)
- Clean domain model for accounts, transactions, forecasts and mortgages
- REST API service and host runtime
- Blazor web UI (frontend)
- Persistence layer and EF Core migrations
- Unit and integration test coverage for core domain behaviours

**Repository Layout**
- `FinancesDashboard.slnx` — solution file
- `src/FinancesDashboard.ApiService` — ASP.NET API project
- `src/FinancesDashboard.AppHost` — host/runtime and composition root
- `src/FinancesDashboard.Application` — application services, DTOs, use-cases
- `src/FinancesDashboard.Domain` — domain entities, enums, value objects
- `src/FinancesDashboard.Infrastructure` — persistence, EF Core migrations, repositories
- `src/FinancesDashboard.Web` — Blazor UI project
- `src/FinancesDashboard.Tests` — unit & integration tests
- `docs/` — requirements, architecture, domain model, ETL and implementation notes
- `data/` — sample CSVs used for tests and demos

Getting started
---------------

Prerequisites
- .NET 10 SDK
- (Optional) Visual Studio 2022/2023 or Visual Studio Code

Quick CLI setup

Restore, build and run tests from the repo root:

```powershell
dotnet restore
dotnet build --no-restore
dotnet test
```

Run the API locally:

```powershell
dotnet run --project src/FinancesDashboard.ApiService
```

Run the host (composition / background services):

```powershell
dotnet run --project src/FinancesDashboard.AppHost
```

Database / Migrations
- The `Infrastructure` project contains EF Core migrations. Apply migrations (or update the local DB) using the standard `dotnet ef` commands pointed at the Infrastructure project.

Example (from solution root):

```powershell
dotnet tool install --global dotnet-ef --version 8.*
dotnet ef database update --project src/FinancesDashboard.Infrastructure --startup-project src/FinancesDashboard.AppHost
```

Importing sample CSVs
- Sample CSV files are in the `data/` folder. See `docs/04-CBA-CSV-ETL.md` for file format, expected columns, and the import workflow.

Configuration
- Each runnable project contains `appsettings.json` and `appsettings.Development.json` for local development. Common settings include connection strings and logging.

Testing
- Unit and integration tests live in `src/FinancesDashboard.Tests`. Run `dotnet test` to execute the suite.

Development notes
- Follow domain-driven boundaries: prefer existing domain concepts in `src/FinancesDashboard.Domain`.
- Place new domain types in `src/FinancesDashboard.Domain/Models` and one class per file.
- Add unit tests for domain behaviour and integration tests for persistence and API flows.

Contributing
- Read `docs/` for design constraints and implementation guidelines before adding major features.
- Open a feature branch named `feature/<short-desc>` and include focused tests.

CI / Recommended next steps
- Add a GitHub Actions workflow for build/test on push and PRs.
- Add a `CONTRIBUTING.md` with local setup steps and branch/PR rules.

License
- This repository currently has no license file. Add a `LICENSE` if you plan to publish.

Contact / Questions
- If you'd like, I can add a CONTRIBUTING.md, a GitHub Actions CI workflow, or VS Code launch/tasks for local debugging.

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
