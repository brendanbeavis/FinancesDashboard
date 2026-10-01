using FinancesDashboard.Application.Dto;
using FinancesDashboard.Domain.Models;
using FinancesDashboard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinancesDashboard.Application.Services;

public sealed class MortgageApplicationService(
    FinancesDashboardDbContext dbContext,
    MortgageAmortizationService amortizationService)
{
    public async Task<IReadOnlyList<MortgageDto>> GetMortgagesAsync(CancellationToken cancellationToken)
    {
        var mortgages = await dbContext.Mortgages
            .AsNoTracking()
            .OrderBy(mortgage => mortgage.Name)
            .ToListAsync(cancellationToken);

        var mortgageIds = mortgages.Select(mortgage => mortgage.Id).ToList();
        var rateHistory = await dbContext.InterestRatePeriods
            .AsNoTracking()
            .Where(period => mortgageIds.Contains(period.MortgageId))
            .OrderBy(period => period.StartDate)
            .ToListAsync(cancellationToken);

        return mortgages
            .Select(mortgage =>
            {
                var history = rateHistory
                    .Where(period => period.MortgageId == mortgage.Id)
                    .ToList();

                return MapMortgage(mortgage, history);
            })
            .ToList();
    }

    public async Task<MortgageDto> CreateMortgageAsync(CreateMortgageRequest request, CancellationToken cancellationToken)
    {
        var mortgage = new Mortgage
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            OriginalPrincipal = request.OriginalPrincipal,
            CurrentPrincipal = request.CurrentPrincipal,
            AnnualInterestRate = request.AnnualInterestRate,
            ScheduledRepayment = request.ScheduledRepayment,
            RepaymentsPerYear = request.RepaymentsPerYear,
            StartDate = request.StartDate,
            ExpectedEndDate = request.ExpectedEndDate
        };

        var history = request.InterestRateHistory.Count > 0
            ? request.InterestRateHistory
            : [new InterestRatePeriodDto(request.StartDate, null, request.AnnualInterestRate)];

        var rateHistory = history
            .OrderBy(period => period.StartDate)
            .Select(period => new InterestRatePeriod
            {
                MortgageId = mortgage.Id,
                StartDate = period.StartDate,
                EndDate = period.EndDate,
                AnnualInterestRate = period.AnnualInterestRate
            })
            .ToList();

        dbContext.Mortgages.Add(mortgage);
        dbContext.InterestRatePeriods.AddRange(rateHistory);
        await dbContext.SaveChangesAsync(cancellationToken);

        return MapMortgage(mortgage, rateHistory);
    }

    private MortgageDto MapMortgage(Mortgage mortgage, IReadOnlyList<InterestRatePeriod> rateHistory)
    {
        var rateHistoryDto = rateHistory
            .OrderBy(period => period.StartDate)
            .Select(period => new InterestRatePeriodDto(period.StartDate, period.EndDate, period.AnnualInterestRate))
            .ToList();

        var projection = amortizationService.BuildProjection(mortgage, rateHistory);

        return new MortgageDto(
            mortgage.Id,
            mortgage.Name,
            mortgage.OriginalPrincipal,
            mortgage.CurrentPrincipal,
            mortgage.AnnualInterestRate,
            mortgage.ScheduledRepayment,
            mortgage.RepaymentsPerYear,
            mortgage.StartDate,
            mortgage.ExpectedEndDate,
            rateHistoryDto,
            projection);
    }
}
