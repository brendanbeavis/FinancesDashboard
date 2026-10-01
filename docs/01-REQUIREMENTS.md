# Requirements

## Purpose

FinanceReport imports Commonwealth Bank transaction exports and provides financial reporting, historical trends, forecasts and mortgage analysis.

## Functional Requirements

### Accounts
- Create and manage accounts.
- Support Transaction, Savings, CreditCard, Mortgage, Investment and Other account types.
- View individual account positions.
- Group multiple account positions.

### CBA Import
- Upload a CBA CSV through Blazor.
- Associate the import with an account.
- Parse the four-column headerless format: TIMESTAMP, AMOUNT, DESCRIPTION, NET_ACCOUNT_TOTAL.
- Preserve the bank-provided running balance.
- Make imports idempotent.
- Report imported, skipped and invalid rows.

### Reporting
Provide account positions over time, grouped positions, credit-card expenditure, income versus expenses, net cashflow, future position forecasts and mortgage reporting.

### Mortgage
Track original/current principal, interest rate, repayment, repayment frequency, start date, expected end date, rate history, amortisation and remaining projected interest.

### Non-Functional
- .NET 10 / C#.
- Blazor frontend.
- ASP.NET Core API.
- EF Core + SQLite.
- Clear Domain/Application/Infrastructure/API/UI separation.
- Deterministic calculations.
- Unit and integration tests.
- Imported source data must not be silently modified.
- Forecasts must be clearly distinguished from observed data.

## Financial Semantics

Store CBA `NET_ACCOUNT_TOTAL` as `BankBalance` on every transaction. Use the observed bank balance for account-position reporting.

Initial classification:
- Amount > 0 = income.
- Amount < 0 = expense.
- Expense display uses absolute value.

Internal transfers and credit-card repayments can make this simplistic classification misleading; transaction intelligence is a later phase.
