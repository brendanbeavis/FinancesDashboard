using FinancesDashboard.Application.Dto;
using FinancesDashboard.Application.Services;
using FinancesDashboard.Domain.Models;

namespace FinancesDashboard.Tests;

public sealed class MortgageAmortizationTests
{
    [Fact]
    public void BuildProjection_ComputesPeriodicInterestAndPrincipalReduction()
    {
        var service = new MortgageAmortizationService(new MortgageProjectionOptions { HorizonPeriods = 12 });

        var mortgage = CreateMortgage(currentPrincipal: 1000m, annualInterestRate: 0.12m, scheduledRepayment: 200m);

        var projection = service.BuildProjection(mortgage, []);

        var first = projection.AmortisationSchedule[0];
        Assert.Equal(10m, first.InterestAmount);
        Assert.Equal(190m, first.PrincipalRepaid);
        Assert.Equal(810m, first.ClosingPrincipal);
        Assert.True(first.IsProjected);
    }

    [Fact]
    public void BuildProjection_DetectsPayoffAndCapsFinalPayment()
    {
        var service = new MortgageAmortizationService(new MortgageProjectionOptions { HorizonPeriods = 12 });

        var mortgage = CreateMortgage(currentPrincipal: 1000m, annualInterestRate: 0.12m, scheduledRepayment: 600m);

        var projection = service.BuildProjection(mortgage, []);

        Assert.Equal(2, projection.AmortisationSchedule.Count);
        Assert.Equal(new DateOnly(2026, 2, 1), projection.EstimatedPayoffDate);
        Assert.Equal(0m, projection.ProjectedBalance);

        var finalPeriod = projection.AmortisationSchedule[^1];
        Assert.Equal(410m, finalPeriod.PrincipalRepaid);
        Assert.Equal(414.10m, finalPeriod.Payment);
        Assert.Equal(0m, finalPeriod.ClosingPrincipal);
    }

    [Fact]
    public void BuildProjection_AppliesInterestRateHistoryChanges()
    {
        var service = new MortgageAmortizationService(new MortgageProjectionOptions { HorizonPeriods = 3 });

        var mortgage = CreateMortgage(currentPrincipal: 1000m, annualInterestRate: 0.05m, scheduledRepayment: 100m);
        var rateHistory = new List<InterestRatePeriod>
        {
            new()
            {
                MortgageId = mortgage.Id,
                StartDate = new DateOnly(2026, 1, 1),
                EndDate = new DateOnly(2026, 1, 31),
                AnnualInterestRate = 0.12m
            },
            new()
            {
                MortgageId = mortgage.Id,
                StartDate = new DateOnly(2026, 2, 1),
                EndDate = null,
                AnnualInterestRate = 0.24m
            }
        };

        var projection = service.BuildProjection(mortgage, rateHistory);

        Assert.Equal(0.12m, projection.AmortisationSchedule[0].AnnualInterestRate);
        Assert.Equal(0.24m, projection.AmortisationSchedule[1].AnnualInterestRate);
        Assert.Equal(0.24m, projection.AmortisationSchedule[2].AnnualInterestRate);

        Assert.Equal(10m, projection.AmortisationSchedule[0].InterestAmount);
        Assert.Equal(18.20m, projection.AmortisationSchedule[1].InterestAmount);
        Assert.Equal(16.56m, projection.AmortisationSchedule[2].InterestAmount);
        Assert.Equal(44.76m, projection.TotalRemainingInterest);
    }

    private static Mortgage CreateMortgage(decimal currentPrincipal, decimal annualInterestRate, decimal scheduledRepayment)
    {
        return new Mortgage
        {
            Id = Guid.NewGuid(),
            Name = "Mortgage",
            OriginalPrincipal = currentPrincipal,
            CurrentPrincipal = currentPrincipal,
            AnnualInterestRate = annualInterestRate,
            ScheduledRepayment = scheduledRepayment,
            RepaymentsPerYear = 12,
            StartDate = new DateOnly(2026, 1, 1),
            ExpectedEndDate = null
        };
    }
}
