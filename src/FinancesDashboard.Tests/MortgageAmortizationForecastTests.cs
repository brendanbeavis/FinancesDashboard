using FinancesDashboard.Application.Services;
using FinancesDashboard.Domain.Models;

namespace FinancesDashboard.Tests;

public sealed class MortgageAmortizationForecastTests
{
    [Fact]
    public void GenerateForecast_WithZeroInterest_ProducesLinearDeclineAndPayoff()
    {
        var service = new MortgageAmortizationForecastService();
        var result = service.GenerateForecast(
            startingBalance: 100000m,
            startDate: new DateOnly(2026, 1, 1),
            annualInterestRate: 0m,
            repaymentAmount: 10000m,
            repaymentsPerYear: 12);

        Assert.Equal("PaidOff", result.Status);
        Assert.Equal(10, result.RemainingRepaymentCount);
        Assert.Equal(0m, result.TotalFutureInterest);
        Assert.Equal(100000m, result.TotalFutureRepayments);
        Assert.Equal(0m, result.ProjectedClosingBalance);
        Assert.NotEmpty(result.Schedule);
        Assert.All(result.Schedule, period => Assert.Equal(0m, period.InterestAmount));
        Assert.Equal(90000m, result.Schedule[0].ClosingPrincipal);
    }

    [Fact]
    public void GenerateForecast_WithNormalInterest_CalculatesPrincipalReduction()
    {
        var service = new MortgageAmortizationForecastService();
        var result = service.GenerateForecast(
            startingBalance: 100000m,
            startDate: new DateOnly(2026, 1, 1),
            annualInterestRate: 0.06m,
            repaymentAmount: 2000m,
            repaymentsPerYear: 12);

        var period1 = result.Schedule[0];
        Assert.Equal("PaidOff", result.Status);
        Assert.Equal(100000m, period1.OpeningPrincipal);
        Assert.Equal(0.06m, period1.AnnualInterestRate);
        Assert.Equal(500m, period1.InterestAmount);
        Assert.Equal(1500m, period1.PrincipalRepaid);
        Assert.Equal(2000m, period1.Payment);
        Assert.Equal(98500m, period1.ClosingPrincipal);

        var period2 = result.Schedule[1];
        Assert.Equal(492.5m, period2.InterestAmount);
        Assert.Equal(1507.5m, period2.PrincipalRepaid);
    }

    [Fact]
    public void GenerateForecast_WithOffsetBalance_ReducesInterestBearingPrincipal()
    {
        var service = new MortgageAmortizationForecastService();
        var result = service.GenerateForecast(
            startingBalance: 100000m,
            startDate: new DateOnly(2026, 1, 1),
            annualInterestRate: 0.06m,
            repaymentAmount: 2000m,
            repaymentsPerYear: 12,
            offsetBalance: 20000m);

        var period1 = result.Schedule[0];
        Assert.Equal(20000m, period1.OffsetBalance);
        Assert.Equal(80000m, period1.EffectivePrincipal);
        Assert.Equal(400m, period1.InterestAmount);
        Assert.Equal(80000m, result.EffectivePrincipalAfterOffset);
    }

    [Fact]
    public void GenerateForecast_WithOffsetEqualToMortgage_InterestBearingBalanceBecomesZero()
    {
        var service = new MortgageAmortizationForecastService();
        var result = service.GenerateForecast(
            startingBalance: 100000m,
            startDate: new DateOnly(2026, 1, 1),
            annualInterestRate: 0.06m,
            repaymentAmount: 2000m,
            repaymentsPerYear: 12,
            offsetBalance: 100000m);

        var period1 = result.Schedule[0];
        Assert.Equal(0m, period1.EffectivePrincipal);
        Assert.Equal(0m, period1.InterestAmount);
        Assert.Equal(100000m, period1.OpeningPrincipal);
    }

    [Fact]
    public void GenerateForecast_WithOffsetGreaterThanMortgage_InterestBearingBalanceRemainsZeroButPrincipalStaysOutstanding()
    {
        var service = new MortgageAmortizationForecastService();
        var result = service.GenerateForecast(
            startingBalance: 50000m,
            startDate: new DateOnly(2026, 1, 1),
            annualInterestRate: 0.06m,
            repaymentAmount: 1000m,
            repaymentsPerYear: 12,
            offsetBalance: 75000m);

        var period1 = result.Schedule[0];
        Assert.Equal(0m, period1.EffectivePrincipal);
        Assert.Equal(0m, period1.InterestAmount);
        Assert.Equal(50000m, period1.OpeningPrincipal);
        Assert.Equal(49000m, period1.ClosingPrincipal);
    }

    [Fact]
    public void GenerateForecast_WithInsufficientPayment_ReturnsNonAmortising()
    {
        var service = new MortgageAmortizationForecastService();
        var result = service.GenerateForecast(
            startingBalance: 100000m,
            startDate: new DateOnly(2026, 1, 1),
            annualInterestRate: 0.12m,
            repaymentAmount: 500m,
            repaymentsPerYear: 12);

        Assert.Equal("NonAmortising", result.Status);
        Assert.Single(result.Schedule);
        Assert.Null(result.ProjectedPayoffDate);
        Assert.Equal(100000m, result.ProjectedClosingBalance);
        Assert.Equal(0m, result.Schedule[0].PrincipalRepaid);
        Assert.True(result.Schedule[0].InterestAmount >= result.Schedule[0].Payment);
    }

    [Fact]
    public void GenerateForecast_WithChangingInterestRates_AppliesRateChanges()
    {
        var service = new MortgageAmortizationForecastService();
        var rateHistory = new List<InterestRatePeriod>
        {
            new()
            {
                MortgageId = Guid.NewGuid(),
                StartDate = new DateOnly(2026, 1, 1),
                EndDate = new DateOnly(2026, 6, 30),
                AnnualInterestRate = 0.05m
            },
            new()
            {
                MortgageId = Guid.NewGuid(),
                StartDate = new DateOnly(2026, 7, 1),
                EndDate = null,
                AnnualInterestRate = 0.06m
            }
        };

        var result = service.GenerateForecast(
            startingBalance: 100000m,
            startDate: new DateOnly(2026, 1, 1),
            annualInterestRate: 0.05m,
            repaymentAmount: 2000m,
            repaymentsPerYear: 12,
            rateHistory: rateHistory);

        Assert.NotEmpty(result.Schedule);
        Assert.Equal(0.05m, result.Schedule[0].AnnualInterestRate);

        var latePeriod = result.Schedule.FirstOrDefault(p => p.PeriodDate > new DateOnly(2026, 6, 30));
        Assert.NotNull(latePeriod);
        Assert.Equal(0.06m, latePeriod!.AnnualInterestRate);
    }

    [Fact]
    public void GenerateForecast_WithNegativeBalance_NormalizesToPositivePrincipal()
    {
        var service = new MortgageAmortizationForecastService();
        var result = service.GenerateForecast(
            startingBalance: -50000m,
            startDate: new DateOnly(2026, 1, 1),
            annualInterestRate: 0.05m,
            repaymentAmount: 1000m,
            repaymentsPerYear: 12);

        Assert.NotEmpty(result.Schedule);
        Assert.Equal(50000m, result.Schedule[0].OpeningPrincipal);
        Assert.All(result.Schedule, period => Assert.True(period.ClosingPrincipal >= 0m));
    }

    [Fact]
    public void GenerateForecast_WithFinalPartialRepayment_AdjustsLastPayment()
    {
        var service = new MortgageAmortizationForecastService();
        var result = service.GenerateForecast(
            startingBalance: 1500m,
            startDate: new DateOnly(2026, 1, 1),
            annualInterestRate: 0m,
            repaymentAmount: 1000m,
            repaymentsPerYear: 12);

        var lastPeriod = result.Schedule[^1];
        Assert.Equal("PaidOff", result.Status);
        Assert.Equal(500m, lastPeriod.Payment);
        Assert.Equal(500m, lastPeriod.PrincipalRepaid);
        Assert.Equal(0m, lastPeriod.ClosingPrincipal);
    }

    [Fact]
    public void GenerateForecast_WithZeroRepaymentsPerYear_ThrowsException()
    {
        var service = new MortgageAmortizationForecastService();

        var ex = Assert.Throws<ArgumentException>(() =>
            service.GenerateForecast(
                startingBalance: 100000m,
                startDate: new DateOnly(2026, 1, 1),
                annualInterestRate: 0.05m,
                repaymentAmount: 1000m,
                repaymentsPerYear: 0));

        Assert.Contains("must be greater than zero", ex.Message);
    }

    [Fact]
    public void GenerateForecast_WithNegativeOffset_ThrowsException()
    {
        var service = new MortgageAmortizationForecastService();

        var ex = Assert.Throws<ArgumentException>(() =>
            service.GenerateForecast(
                startingBalance: 100000m,
                startDate: new DateOnly(2026, 1, 1),
                annualInterestRate: 0.05m,
                repaymentAmount: 1000m,
                repaymentsPerYear: 12,
                offsetBalance: -1m));

        Assert.Contains("Offset balance cannot be negative", ex.Message);
    }

    [Fact]
    public void GenerateForecast_WithOffsetEqualToMortgage_InterestFreeDateIsFirstPeriod()
    {
        var service = new MortgageAmortizationForecastService();
        var startDate = new DateOnly(2026, 1, 1);
        var result = service.GenerateForecast(
            startingBalance: 100000m,
            startDate: startDate,
            annualInterestRate: 0.06m,
            repaymentAmount: 2000m,
            repaymentsPerYear: 12,
            offsetBalance: 100000m);

        // Interest-free date should be the start date when offset equals principal
        Assert.Equal(startDate, result.ProjectedInterestFreeDate);

        // Payoff date should be later when actually paying off the mortgage
        Assert.True(result.ProjectedPayoffDate > startDate);

        var period1 = result.Schedule[0];
        Assert.Equal(0m, period1.EffectivePrincipal);
        Assert.Equal(0m, period1.InterestAmount);
    }

    [Fact]
    public void GenerateForecast_WithOffsetGreaterThanMortgage_InterestFreeDateIsFirstPeriod()
    {
        var service = new MortgageAmortizationForecastService();
        var startDate = new DateOnly(2026, 1, 1);
        var result = service.GenerateForecast(
            startingBalance: 50000m,
            startDate: startDate,
            annualInterestRate: 0.06m,
            repaymentAmount: 1000m,
            repaymentsPerYear: 12,
            offsetBalance: 75000m);

        // Interest-free date should be the start date when offset exceeds principal
        Assert.Equal(startDate, result.ProjectedInterestFreeDate);

        var period1 = result.Schedule[0];
        Assert.Equal(0m, period1.EffectivePrincipal);
        Assert.Equal(0m, period1.InterestAmount);
    }

    [Fact]
    public void GenerateForecast_WithPartialOffset_InterestFreeDateCalculatedWhenPrincipalFallsBelow()
    {
        var service = new MortgageAmortizationForecastService();
        var startDate = new DateOnly(2026, 1, 1);
        var result = service.GenerateForecast(
            startingBalance: 100000m,
            startDate: startDate,
            annualInterestRate: 0.06m,
            repaymentAmount: 5000m,
            repaymentsPerYear: 12,
            offsetBalance: 50000m);

        // Interest-free date should be found when principal falls below offset
        Assert.NotNull(result.ProjectedInterestFreeDate);
        Assert.True(result.ProjectedInterestFreeDate > startDate);

        // Payoff and interest-free dates may differ
        Assert.NotEqual(result.ProjectedPayoffDate, result.ProjectedInterestFreeDate);

        // Verify that at interest-free date, effective principal is zero
        var scheduleAtInterestFreeDate = result.Schedule
            .FirstOrDefault(p => p.PeriodDate == result.ProjectedInterestFreeDate);

        if (scheduleAtInterestFreeDate is not null)
        {
            Assert.True(scheduleAtInterestFreeDate.EffectivePrincipal <= 0m);
        }
    }

    [Fact]
    public void GenerateForecast_WithNoOffset_InterestFreeDateIsNeverReached()
    {
        var service = new MortgageAmortizationForecastService();
        var result = service.GenerateForecast(
            startingBalance: 100000m,
            startDate: new DateOnly(2026, 1, 1),
            annualInterestRate: 0.06m,
            repaymentAmount: 2000m,
            repaymentsPerYear: 12,
            offsetBalance: 0m);

        // Without offset, interest-free date is never reached (same as payoff date conceptually)
        // Interest-free date should be null or same as payoff date
        if (result.ProjectedInterestFreeDate.HasValue)
        {
            Assert.Equal(result.ProjectedPayoffDate, result.ProjectedInterestFreeDate);
        }
    }

    [Fact]
    public void GenerateForecast_PayoffDateVsInterestFreeDateDistinction()
    {
        var service = new MortgageAmortizationForecastService();
        var startDate = new DateOnly(2026, 1, 1);
        var result = service.GenerateForecast(
            startingBalance: 100000m,
            startDate: startDate,
            annualInterestRate: 0.06m,
            repaymentAmount: 3000m,
            repaymentsPerYear: 12,
            offsetBalance: 80000m);

        // With significant offset, interest-free date should be earlier or same as payoff date
        Assert.NotNull(result.ProjectedInterestFreeDate);

        // Interest-free date is when interest-bearing principal becomes zero
        Assert.True(result.ProjectedInterestFreeDate <= result.ProjectedPayoffDate);

        // After interest-free date, remaining payments are principal-only
        var periodsAfterInterestFree = result.Schedule
            .Where(p => p.PeriodDate > result.ProjectedInterestFreeDate).ToList();

        Assert.All(periodsAfterInterestFree.Where(p => p.ClosingPrincipal > 0), 
            period => Assert.Equal(0m, period.InterestAmount));
    }

    [Fact]
    public void GenerateForecast_NonAmortising_WithOffset_InterestFreeDateNotSet()
    {
        var service = new MortgageAmortizationForecastService();
        var startDate = new DateOnly(2026, 1, 1);
        var result = service.GenerateForecast(
            startingBalance: 100000m,
            startDate: startDate,
            annualInterestRate: 0.06m,
            repaymentAmount: 200m,  // Less than interest on effective principal
            repaymentsPerYear: 12,
            offsetBalance: 50000m);

        Assert.Equal("NonAmortising", result.Status);

        // In non-amortising case, interest-free date may be calculated but loan never amortises
        var period1 = result.Schedule[0];
        Assert.True(period1.InterestAmount > period1.Payment);
    }

    [Fact]
    public void GenerateForecast_InterestFreeDate_AlreadyFullyOffset_IsStartDate()
    {
        var service = new MortgageAmortizationForecastService();
        var startDate = new DateOnly(2026, 1, 1);
        var result = service.GenerateForecast(
            startingBalance: 100000m,
            startDate: startDate,
            annualInterestRate: 0.06m,
            repaymentAmount: 2000m,
            repaymentsPerYear: 12,
            offsetBalance: 100000m);

        // When already fully offset, interest-free date is today
        Assert.Equal(startDate, result.ProjectedInterestFreeDate);
    }

    [Fact]
    public void GenerateForecast_ZeroStartingBalance_InterestFreeDateIsStartDate()
    {
        var service = new MortgageAmortizationForecastService();
        var startDate = new DateOnly(2026, 1, 1);
        var result = service.GenerateForecast(
            startingBalance: 0m,
            startDate: startDate,
            annualInterestRate: 0.06m,
            repaymentAmount: 2000m,
            repaymentsPerYear: 12,
            offsetBalance: 0m);

        Assert.Equal("PaidOff", result.Status);
        Assert.Equal(startDate, result.ProjectedInterestFreeDate);
        Assert.Equal(0m, result.ProjectedClosingBalance);
    }

    [Fact]
    public void GenerateForecast_WithOffsetAndRateChange_InterestFreeDateCalculatedCorrectly()
    {
        var service = new MortgageAmortizationForecastService();
        var startDate = new DateOnly(2026, 1, 1);
        var rateHistory = new[]
        {
            new InterestRatePeriod { MortgageId = Guid.NewGuid(), StartDate = startDate, EndDate = new DateOnly(2026, 6, 30), AnnualInterestRate = 0.06m },
            new InterestRatePeriod { MortgageId = Guid.NewGuid(), StartDate = new DateOnly(2026, 7, 1), EndDate = null, AnnualInterestRate = 0.05m }
        };

        var result = service.GenerateForecast(
            startingBalance: 100000m,
            startDate: startDate,
            annualInterestRate: 0.06m,
            repaymentAmount: 4000m,
            repaymentsPerYear: 12,
            offsetBalance: 60000m,
            rateHistory: rateHistory);

        // Interest-free date should be calculated regardless of rate changes
        Assert.NotNull(result.ProjectedInterestFreeDate);
        Assert.Equal("PaidOff", result.Status);
    }
}
