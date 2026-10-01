# Mortgage Model

## Inputs

- Original principal.
- Current principal.
- Annual interest rate.
- Scheduled repayment.
- Repayments per year.
- Start date.
- Expected end date.
- Interest-rate history.

## Periodic Model

```text
Periodic Rate = Annual Rate / Repayments Per Year
Interest = Opening Principal * Periodic Rate
Principal Repaid = Payment - Interest
Closing Principal = Opening Principal - Principal Repaid
```

Repeat until the balance reaches zero or the forecast horizon ends.

## Outputs

- Projected balance.
- Principal repayment.
- Interest amount.
- Total remaining interest.
- Estimated payoff date.
- Amortisation schedule.

## Important Limitation

This is an approximation. Actual Australian lender calculations can differ because of daily interest, offset balances, redraws, extra repayments, rate changes, fees and payment timing.

The UI must label projections accordingly.
