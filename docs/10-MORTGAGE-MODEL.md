# Mortgage Model

## Source of Truth

- Imported mortgage-account transactions are the source of truth for historical balances.
- The latest imported mortgage `BankBalance` represents the current outstanding principal.
- Imported CBA mortgage balances may be negative and must be presented as a positive amount owing in mortgage-specific views.
- Offset accounts are linked to the mortgage configuration, but their balances remain sourced from their own imported account history.

## Persisted Mortgage Configuration

- Mortgage account.
- Annual interest rate.
- Interest-rate history.
- Linked offset accounts.

Persisted mortgage configuration must not require manual original principal, manual current principal, manual scheduled repayment, or a manual forecast horizon.

## Forecast Inputs

- Current outstanding principal derived from imported history.
- Annual interest rate.
- Detected repayment amount derived from observed repayment transactions.
- Detected repayment frequency derived from observed repayment timing.
- Selected offset-account balances.
- Optional transient manual repayment amount override.
- Optional transient manual repayment frequency override.

## Periodic Model

```text
Periodic Rate = Annual Rate / Repayments Per Year
Interest = Max(0, Opening Principal - Offset Balance) * Periodic Rate
Principal Repaid = Payment - Interest
Closing Principal = Opening Principal - Principal Repaid
```

Repeat until the balance reaches zero, the repayment pattern is non-amortising, or a safety limit is reached.

## Outputs

- Projected balance.
- Principal repayment.
- Interest amount.
- Total remaining interest.
- Estimated payoff date.
- Projected interest-free date.
- Amortisation schedule.
- Forecast status.

## Interest-Free Date

When an offset account balance is linked to a mortgage, the effective principal is reduced:

```text
Effective Principal = Max(0, Opening Principal - Total Offset Balance)
```

The **Projected Interest-Free Date** is the first forecast period where the effective principal reaches zero (or below), meaning no further interest will be charged even though the outstanding principal may not yet be fully repaid.

### Key Semantics

- **Projected Payoff Date**: The forecast period when the actual outstanding principal balance reaches zero. This is the date the loan is fully repaid.
- **Projected Interest-Free Date**: The forecast period when the effective principal (after subtracting offset) reaches zero. After this date, no additional interest accrues, but the outstanding principal may continue to exist until fully repaid.
- **Fully Offset from Start**: If the offset balance exceeds or equals the opening principal, the interest-free date is the first forecast period (the start date), and interest is zero throughout.
- **Non-Amortising Loan**: If the loan is non-amortising (repayment amount is zero or the balance never decreases), the interest-free date is not reached and is not set.

### Interaction with Rate Changes

Interest-free date calculations continue after interest-rate changes. The effective principal calculation remains consistent, and the first period where effective principal ≤ 0 is recorded as the interest-free date.

## Important Limitation

This is an approximation. Actual Australian lender calculations can differ because of daily interest, offset balances, redraws, extra repayments, rate changes, fees and payment timing.

The UI must label projections accordingly and clearly distinguish observed history from forecast data.
