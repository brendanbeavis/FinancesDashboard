# Implementation Roadmap

## Phase 1 — Foundation
Create solution, projects, .NET 10 configuration, tests and dependency structure.

## Phase 2 — Persistence
EF Core, SQLite, entities, DbContext, configurations, migrations and indexes.

## Phase 3 — CBA Import
Parser, validation, deterministic hashing, duplicate handling and tests.

## Phase 4 — Reporting API
Accounts, import, dashboard summary, balances, cashflow and credit-card reporting.

## Phase 5 — Dashboard UI
Navigation, import page, dashboard cards and charts.

## Phase 6 — Account Management
Account editing, groups, grouped positions and detail views.

## Phase 7 — Mortgage
CRUD, rate history, amortisation, remaining interest and charts.

## Phase 8 — Transaction Intelligence
Categories, transfer matching, credit-card classification and recurring transactions.

## Phase 9 — Advanced Forecasting
Rolling/weighted averages, seasonality, budgets and scenarios.

## Phase 10 — Production Hardening
Backups, logging, error handling, performance, export and optional authentication.

## First Vertical Slice

```text
Create Account
 -> Upload CBA CSV
 -> Import Transactions
 -> View Current Balance
 -> View Balance History
```
