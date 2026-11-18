# Financial Reporting

## Account Position

For a reporting date, select the latest `BankBalance` on or before that date for the account.

## Group Position

For each member account, select its latest balance on or before the date and sum available positions. Initially, accounts without history are excluded rather than assumed to be zero.

## Cashflow

```text
Income = SUM(Amount where Amount > 0)
Expenses = ABS(SUM(Amount where Amount < 0))
Net = SUM(Amount)
```

Aggregate by date/month as required.

## Credit Card

Initially report negative transactions on accounts typed `CreditCard`. Later classification should distinguish purchases, repayments, refunds, fees and interest.

## Net Position

Conceptually:

```text
Net Position = Assets - Liabilities
```

The implementation should make the treatment of each account type explicit.

## Transfers

Internal transfers can look like income/expense. A future classification layer should detect and exclude matched transfers from income/expense analytics.

Historical observations must remain distinct from forecasts.
