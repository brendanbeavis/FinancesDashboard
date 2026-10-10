using FinancesDashboard.Application.Dto;
using FinancesDashboard.Domain.Models;

namespace FinancesDashboard.Application.Services;

public sealed class MortgageAmortizationService(MortgageProjectionOptions options)
{
    private const string ProjectionAssumption = "Approximation using periodic interest and scheduled repayments; actual lender calculations may differ.";

    /// <summary>
    /// Build a mortgage amortization projection from explicitly provided parameters.
    /// </summary>
    public MortgageProjectionDto BuildProjection(
        decimal currentPrincipal,
        DateOnly startDate,
        decimal annualInterestRate,
        decimal scheduledRepayment,
        int repaymentsPerYear,
        IReadOnlyList<InterestRatePeriod> rateHistory)
    {
        var horizon = Math.Max(1, options.HorizonPeriods);
        var annualRatePeriods = rateHistory
            .OrderBy(period => period.StartDate)
            .ToList();

        var schedule = new List<AmortisationPeriodDto>(horizon);
        var openingPrincipal = decimal.Round(Math.Max(0m, currentPrincipal), 2, MidpointRounding.AwayFromZero);
        var periodDate = startDate;
        decimal totalRemainingInterest = 0m;
        DateOnly? payoffDate = null;

        for (var periodNumber = 1; periodNumber <= horizon; periodNumber++)
        {
            if (openingPrincipal <= 0m)
            {
                payoffDate ??= periodDate;
                break;
            }

            var annualRate = ResolveAnnualRate(periodDate, annualInterestRate, annualRatePeriods);
            var periodicRate = annualRate / repaymentsPerYear;
            var interestAmount = decimal.Round(openingPrincipal * periodicRate, 2, MidpointRounding.AwayFromZero);
            var scheduledPayment = decimal.Round(Math.Max(0m, scheduledRepayment), 2, MidpointRounding.AwayFromZero);

            var rawPrincipalRepaid = scheduledPayment - interestAmount;
            var principalRepaid = rawPrincipalRepaid;
            var effectivePayment = scheduledPayment;

            if (rawPrincipalRepaid > openingPrincipal)
            {
                principalRepaid = openingPrincipal;
                effectivePayment = decimal.Round(interestAmount + principalRepaid, 2, MidpointRounding.AwayFromZero);
            }

            var closingPrincipal = decimal.Round(openingPrincipal - principalRepaid, 2, MidpointRounding.AwayFromZero);
            if (closingPrincipal <= 0m)
            {
                closingPrincipal = 0m;
                payoffDate ??= periodDate;
            }

            totalRemainingInterest += interestAmount;

            schedule.Add(new AmortisationPeriodDto(
                periodNumber,
                periodDate,
                openingPrincipal,
                0m,
                openingPrincipal,
                annualRate,
                interestAmount,
                decimal.Round(principalRepaid, 2, MidpointRounding.AwayFromZero),
                effectivePayment,
                closingPrincipal,
                true,
                ProjectionAssumption));

            openingPrincipal = closingPrincipal;
            periodDate = AdvancePeriodDate(periodDate, repaymentsPerYear);
        }

        return new MortgageProjectionDto(
            decimal.Round(openingPrincipal, 2, MidpointRounding.AwayFromZero),
            decimal.Round(totalRemainingInterest, 2, MidpointRounding.AwayFromZero),
            payoffDate,
            true,
            ProjectionAssumption,
            schedule);
    }

    private static decimal ResolveAnnualRate(
        DateOnly periodDate,
        decimal fallbackAnnualRate,
        IReadOnlyList<InterestRatePeriod> annualRatePeriods)
    {
        var matched = annualRatePeriods
            .LastOrDefault(ratePeriod =>
                ratePeriod.StartDate <= periodDate &&
                (ratePeriod.EndDate is null || ratePeriod.EndDate.Value >= periodDate));

        return matched?.AnnualInterestRate ?? fallbackAnnualRate;
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
}
