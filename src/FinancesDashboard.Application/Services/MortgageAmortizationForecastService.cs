using FinancesDashboard.Application.Dto;
using FinancesDashboard.Domain.Models;

namespace FinancesDashboard.Application.Services;

/// <summary>
/// Generates mortgage amortization forecasts from a starting balance.
/// Projects forward using fixed or variable interest rates.
/// </summary>
public sealed class MortgageAmortizationForecastService
{
    private const int MaxForecastYears = 100;
    private const string ForecastAssumption = "Approximation using periodic interest, selected offset balances, and scheduled repayments; actual lender calculations may differ.";

    public MortgageForecastProjectionResult GenerateForecast(
        decimal startingBalance,
        DateOnly startDate,
        decimal annualInterestRate,
        decimal repaymentAmount,
        int repaymentsPerYear,
        decimal offsetBalance = 0m,
        IReadOnlyList<InterestRatePeriod>? rateHistory = null)
    {
        if (startingBalance < 0m)
        {
            startingBalance = Math.Abs(startingBalance);
        }

        if (repaymentAmount < 0m)
        {
            throw new ArgumentException("Repayment amount cannot be negative.", nameof(repaymentAmount));
        }

        if (repaymentsPerYear <= 0)
        {
            throw new ArgumentException("Repayments per year must be greater than zero.", nameof(repaymentsPerYear));
        }

        if (offsetBalance < 0m)
        {
            throw new ArgumentException("Offset balance cannot be negative.", nameof(offsetBalance));
        }

        var roundedStartingBalance = decimal.Round(startingBalance, 2, MidpointRounding.AwayFromZero);
        var roundedOffsetBalance = decimal.Round(offsetBalance, 2, MidpointRounding.AwayFromZero);
        var effectivePrincipalAfterOffset = decimal.Round(Math.Max(0m, roundedStartingBalance - roundedOffsetBalance), 2, MidpointRounding.AwayFromZero);

        if (roundedStartingBalance <= 0m)
        {
            return new MortgageForecastProjectionResult(
                Status: "PaidOff",
                ProjectedPayoffDate: startDate,
                ProjectedClosingBalance: 0m,
                RemainingRepaymentCount: 0,
                TotalFutureInterest: 0m,
                TotalFutureRepayments: 0m,
                CurrentOutstandingPrincipal: 0m,
                CurrentOffsetBalance: roundedOffsetBalance,
                EffectivePrincipalAfterOffset: 0m,
                Schedule: [],
                ProjectedInterestFreeDate: startDate);
        }

        var schedule = new List<AmortisationPeriodDto>();
        var currentBalance = roundedStartingBalance;
        var currentDate = startDate;
        var currentEffectiveRate = annualInterestRate;
        var maxPeriods = Math.Max(repaymentsPerYear, repaymentsPerYear * MaxForecastYears);
        decimal totalFutureInterest = 0m;
        decimal totalFutureRepayments = 0m;
        DateOnly? interestFreeDate = null;

        for (var periodNumber = 1; periodNumber <= maxPeriods && currentBalance > 0m; periodNumber++)
        {
            var applicableRate = ResolveInterestRate(currentDate, currentEffectiveRate, rateHistory);
            currentEffectiveRate = applicableRate;

            var openingPrincipal = decimal.Round(currentBalance, 2, MidpointRounding.AwayFromZero);
            var effectivePrincipal = decimal.Round(Math.Max(0m, openingPrincipal - roundedOffsetBalance), 2, MidpointRounding.AwayFromZero);
            var periodicRate = applicableRate / repaymentsPerYear;
            var interestAmount = decimal.Round(effectivePrincipal * periodicRate, 2, MidpointRounding.AwayFromZero);

            // Track the first date when interest-bearing principal becomes zero
            if (interestFreeDate is null && effectivePrincipal <= 0m)
            {
                interestFreeDate = currentDate;
            }

            if (repaymentAmount <= interestAmount)
            {
                schedule.Add(new AmortisationPeriodDto(
                    periodNumber,
                    currentDate,
                    openingPrincipal,
                    roundedOffsetBalance,
                    effectivePrincipal,
                    applicableRate,
                    interestAmount,
                    0m,
                    decimal.Round(repaymentAmount, 2, MidpointRounding.AwayFromZero),
                    openingPrincipal,
                    true,
                    ForecastAssumption));

                totalFutureInterest += interestAmount;
                totalFutureRepayments += decimal.Round(repaymentAmount, 2, MidpointRounding.AwayFromZero);

                return new MortgageForecastProjectionResult(
                    Status: "NonAmortising",
                    ProjectedPayoffDate: null,
                    ProjectedClosingBalance: openingPrincipal,
                    RemainingRepaymentCount: schedule.Count,
                    TotalFutureInterest: decimal.Round(totalFutureInterest, 2, MidpointRounding.AwayFromZero),
                    TotalFutureRepayments: decimal.Round(totalFutureRepayments, 2, MidpointRounding.AwayFromZero),
                    CurrentOutstandingPrincipal: roundedStartingBalance,
                    CurrentOffsetBalance: roundedOffsetBalance,
                    EffectivePrincipalAfterOffset: effectivePrincipalAfterOffset,
                    Schedule: schedule,
                    ProjectedInterestFreeDate: interestFreeDate);
            }

            var principalRepaid = decimal.Round(repaymentAmount - interestAmount, 2, MidpointRounding.AwayFromZero);
            var payment = decimal.Round(repaymentAmount, 2, MidpointRounding.AwayFromZero);

            if (principalRepaid >= openingPrincipal)
            {
                principalRepaid = openingPrincipal;
                payment = decimal.Round(interestAmount + principalRepaid, 2, MidpointRounding.AwayFromZero);
            }

            var closingPrincipal = decimal.Round(openingPrincipal - principalRepaid, 2, MidpointRounding.AwayFromZero);
            if (closingPrincipal < 0m)
            {
                closingPrincipal = 0m;
            }

            schedule.Add(new AmortisationPeriodDto(
                periodNumber,
                currentDate,
                openingPrincipal,
                roundedOffsetBalance,
                effectivePrincipal,
                applicableRate,
                interestAmount,
                principalRepaid,
                payment,
                closingPrincipal,
                true,
                ForecastAssumption));

            totalFutureInterest += interestAmount;
            totalFutureRepayments += payment;

            if (closingPrincipal <= 0m)
            {
                return new MortgageForecastProjectionResult(
                    Status: "PaidOff",
                    ProjectedPayoffDate: currentDate,
                    ProjectedClosingBalance: 0m,
                    RemainingRepaymentCount: schedule.Count,
                    TotalFutureInterest: decimal.Round(totalFutureInterest, 2, MidpointRounding.AwayFromZero),
                    TotalFutureRepayments: decimal.Round(totalFutureRepayments, 2, MidpointRounding.AwayFromZero),
                    CurrentOutstandingPrincipal: roundedStartingBalance,
                    CurrentOffsetBalance: roundedOffsetBalance,
                    EffectivePrincipalAfterOffset: effectivePrincipalAfterOffset,
                    Schedule: schedule,
                    ProjectedInterestFreeDate: interestFreeDate);
            }

            currentBalance = closingPrincipal;
            currentDate = AdvancePeriodDate(currentDate, repaymentsPerYear);
        }

        return new MortgageForecastProjectionResult(
            Status: "SafetyLimitReached",
            ProjectedPayoffDate: null,
            ProjectedClosingBalance: decimal.Round(currentBalance, 2, MidpointRounding.AwayFromZero),
            RemainingRepaymentCount: schedule.Count,
            TotalFutureInterest: decimal.Round(totalFutureInterest, 2, MidpointRounding.AwayFromZero),
            TotalFutureRepayments: decimal.Round(totalFutureRepayments, 2, MidpointRounding.AwayFromZero),
            CurrentOutstandingPrincipal: roundedStartingBalance,
            CurrentOffsetBalance: roundedOffsetBalance,
            EffectivePrincipalAfterOffset: effectivePrincipalAfterOffset,
            Schedule: schedule,
            ProjectedInterestFreeDate: interestFreeDate);
    }

    private static DateOnly AdvancePeriodDate(DateOnly currentDate, int repaymentsPerYear)
    {
        if (repaymentsPerYear == 12)
        {
            return currentDate.AddMonths(1);
        }

        var daysPerPeriod = Math.Max(1, (int)Math.Round(365d / repaymentsPerYear, MidpointRounding.AwayFromZero));
        return currentDate.AddDays(daysPerPeriod);
    }

    private static decimal ResolveInterestRate(
        DateOnly periodDate,
        decimal defaultRate,
        IReadOnlyList<InterestRatePeriod>? rateHistory)
    {
        if (rateHistory is null || rateHistory.Count == 0)
        {
            return defaultRate;
        }

        var applicableRatePeriod = rateHistory
            .Where(r => r.StartDate <= periodDate && (r.EndDate is null || r.EndDate >= periodDate))
            .OrderByDescending(r => r.StartDate)
            .FirstOrDefault();

        return applicableRatePeriod?.AnnualInterestRate ?? defaultRate;
    }
}
