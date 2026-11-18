using FinancesDashboard.Application.Dto;
using FinancesDashboard.Domain.Enums;
using FinancesDashboard.Domain.Models;
using FinancesDashboard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinancesDashboard.Application.Services;

public sealed class MortgageApplicationService(
    FinancesDashboardDbContext dbContext,
    RepaymentAnalysisService repaymentAnalysisService,
    MortgageAmortizationForecastService forecastService,
    MortgageHistoryAnalysisService historyAnalysisService)
{
    public async Task<IReadOnlyList<MortgageDto>> GetMortgagesAsync(CancellationToken cancellationToken)
    {
        var mortgages = await dbContext.Mortgages
            .AsNoTracking()
            .OrderBy(mortgage => mortgage.AccountId)
            .ToListAsync(cancellationToken);

        var accountIds = mortgages.Select(mortgage => mortgage.AccountId).Distinct().ToList();

        var accounts = await dbContext.Accounts
            .AsNoTracking()
            .Where(a => accountIds.Contains(a.Id))
            .ToListAsync(cancellationToken);

        var rateHistory = await dbContext.InterestRatePeriods
            .AsNoTracking()
            .Where(period => mortgages.Select(m => m.Id).Contains(period.MortgageId))
            .OrderBy(period => period.StartDate)
            .ToListAsync(cancellationToken);

        var result = new List<MortgageDto>();

        foreach (var mortgage in mortgages)
        {
            var account = accounts.FirstOrDefault(a => a.Id == mortgage.AccountId);
            if (account is null)
            {
                continue;
            }

            var mortgageRateHistory = rateHistory
                .Where(period => period.MortgageId == mortgage.Id)
                .ToList();

            var dto = await MapMortgageAsync(mortgage, account, mortgageRateHistory, cancellationToken);
            result.Add(dto);
        }

        return result;
    }

    public async Task<MortgageDto> CreateMortgageAsync(CreateMortgageRequest request, CancellationToken cancellationToken)
    {
        var account = await dbContext.Accounts.FirstOrDefaultAsync(
            a => a.Id == request.AccountId,
            cancellationToken);

        if (account is null)
        {
            throw new KeyNotFoundException($"Account with ID {request.AccountId} not found.");
        }

        var validatedOffsetAccountIds = await ValidateOffsetAccountIdsAsync(
            request.AccountId,
            request.OffsetAccountIds,
            cancellationToken);

        var mortgage = new Mortgage
        {
            Id = Guid.NewGuid(),
            AccountId = request.AccountId,
            AnnualInterestRate = request.AnnualInterestRate,
            ScheduledRepayment = 0m,
            RepaymentsPerYear = 0
        };

        // Get first transaction date from account
        var firstTransaction = await dbContext.Transactions
            .AsNoTracking()
            .Where(t => t.AccountId == request.AccountId)
            .OrderBy(t => t.Date)
            .FirstOrDefaultAsync(cancellationToken);

        var history = request.InterestRateHistory.Count > 0
            ? request.InterestRateHistory
            : [new InterestRatePeriodDto(
                firstTransaction?.Date ?? DateOnly.FromDateTime(DateTime.Today),
                null,
                request.AnnualInterestRate)];

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
        await ReplaceOffsetAccountLinksAsync(mortgage.AccountId, validatedOffsetAccountIds, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await MapMortgageAsync(mortgage, account, rateHistory, cancellationToken);
    }

    public async Task<MortgageCurrentPositionDto> UpdateMortgageOffsetAccountsAsync(
        Guid mortgageAccountId,
        UpdateMortgageOffsetAccountsRequest request,
        CancellationToken cancellationToken)
    {
        var mortgageAccount = await dbContext.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == mortgageAccountId, cancellationToken);

        if (mortgageAccount is null)
        {
            throw new KeyNotFoundException($"Mortgage account with ID {mortgageAccountId} not found.");
        }

        if (mortgageAccount.Type != AccountType.Mortgage)
        {
            throw new ArgumentException("Offset accounts can only be linked to mortgage accounts.", nameof(mortgageAccountId));
        }

        var validatedOffsetAccountIds = await ValidateOffsetAccountIdsAsync(
            mortgageAccountId,
            request.OffsetAccountIds,
            cancellationToken);

        await ReplaceOffsetAccountLinksAsync(mortgageAccountId, validatedOffsetAccountIds, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetMortgageCurrentPositionAsync(mortgageAccountId, null, null, cancellationToken);
    }

    public async Task<MortgageCurrentPositionDto> GetMortgageCurrentPositionAsync(
        Guid accountId,
        DateOnly? startDate,
        DateOnly? endDate,
        CancellationToken cancellationToken)
    {
        var account = await dbContext.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);

        if (account is null)
        {
            throw new KeyNotFoundException($"Mortgage account with ID {accountId} not found.");
        }

        var (transactionCount, currentBalance, totalRepayments, balanceChange) =
            await historyAnalysisService.GetSummaryAsync(accountId, startDate, endDate, cancellationToken);

        var historyPoints = await historyAnalysisService.GetHistoryAsync(accountId, startDate, endDate, cancellationToken);
        var offsetAccountIds = await GetPersistedOffsetAccountIdsAsync(accountId, cancellationToken);
        var totalOffsetBalance = await GetSelectedOffsetBalanceAsync(offsetAccountIds, cancellationToken);
        var interestBearingBalance = decimal.Round(Math.Max(0m, currentBalance - totalOffsetBalance), 2, MidpointRounding.AwayFromZero);
        var estimatedAnnualInterestRate = await historyAnalysisService.EstimateAnnualInterestRateAsync(
            accountId,
            offsetAccountIds,
            startDate,
            endDate,
            cancellationToken);

        return new MortgageCurrentPositionDto(
            transactionCount,
            currentBalance,
            totalOffsetBalance,
            interestBearingBalance,
            totalRepayments,
            balanceChange,
            estimatedAnnualInterestRate,
            historyPoints.Any() ? historyPoints.Min(point => point.Date) : null,
            historyPoints.Any() ? historyPoints.Max(point => point.Date) : null,
            offsetAccountIds);
    }

    /// <summary>
    /// Generate an ephemeral mortgage forecast without persisting a Mortgage configuration.
    /// This method is used to analyze existing mortgage accounts and project future outcomes.
    /// </summary>
    /// <param name="accountId">The mortgage account ID to analyze.</param>
    /// <param name="request">User assumptions for the forecast.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Comprehensive forecast response combining history and projections.</returns>
    public async Task<MortgageForecastResponse> GenerateEphemeralForecastAsync(
        Guid accountId,
        MortgageForecastRequest request,
        CancellationToken cancellationToken)
    {
        // Get historical data
        var historyPoints = await historyAnalysisService.GetHistoryAsync(accountId, null, null, cancellationToken);

        var (transactionCount, currentBalance, totalRepayments, balanceChange) =
            await historyAnalysisService.GetSummaryAsync(accountId, null, null, cancellationToken);

        var latestDate = historyPoints.Any() ? historyPoints.Max(h => h.Date) : DateOnly.FromDateTime(DateTime.Today);
        var earliestDate = historyPoints.Any() ? historyPoints.Min(h => h.Date) : (DateOnly?)null;

        // Detect repayment pattern from historical transactions
        var transactions = await dbContext.Transactions
            .AsNoTracking()
            .Where(t => t.AccountId == accountId)
            .OrderBy(t => t.Date)
            .ToListAsync(cancellationToken);

        var detectedRepayment = transactions.Count > 0
            ? repaymentAnalysisService.AnalyzeRepaymentPatternDetailed(transactions)
            : null;

        var effectiveRepaymentAmount = request.RepaymentAmount > 0m
            ? request.RepaymentAmount
            : detectedRepayment?.EstimatedAmount ?? 0m;

        var effectiveRepaymentsPerYear = request.RepaymentsPerYear > 0
            ? request.RepaymentsPerYear
            : detectedRepayment?.EstimatedFrequency ?? 0;

        var selectedOffsetAccountIds = request.SelectedOffsetAccountIds.Count > 0
            ? await ValidateOffsetAccountIdsAsync(accountId, request.SelectedOffsetAccountIds, cancellationToken)
            : await GetPersistedOffsetAccountIdsAsync(accountId, cancellationToken);
        var offsetBalance = await GetSelectedOffsetBalanceAsync(selectedOffsetAccountIds, cancellationToken);

        MortgageForecastProjectionResult projection;

        if (effectiveRepaymentAmount > 0m && effectiveRepaymentsPerYear > 0)
        {
            // Generate forecast using user assumptions (or detected repayment details when not overridden)
            projection = forecastService.GenerateForecast(
                startingBalance: currentBalance,
                startDate: latestDate,
                annualInterestRate: request.AnnualInterestRate,
                repaymentAmount: effectiveRepaymentAmount,
                repaymentsPerYear: effectiveRepaymentsPerYear,
                offsetBalance: offsetBalance,
                rateHistory: []); // No rate history changes for ephemeral forecast
        }
        else
        {
            projection = new MortgageForecastProjectionResult(
                Status: "Unavailable",
                ProjectedPayoffDate: null,
                ProjectedClosingBalance: currentBalance,
                RemainingRepaymentCount: 0,
                TotalFutureInterest: 0m,
                TotalFutureRepayments: 0m,
                CurrentOutstandingPrincipal: currentBalance,
                CurrentOffsetBalance: offsetBalance,
                EffectivePrincipalAfterOffset: decimal.Round(Math.Max(0m, currentBalance - offsetBalance), 2, MidpointRounding.AwayFromZero),
                Schedule: []);
        }

        var summary = new MortgageSummary(
            transactionCount,
            currentBalance,
            totalRepayments,
            balanceChange,
            earliestDate,
            latestDate
        );

        var forecast = new MortgageForecastOutcome(
            projection.Status,
            projection.ProjectedPayoffDate,
            projection.RemainingRepaymentCount,
            projection.TotalFutureInterest,
            projection.TotalFutureRepayments,
            projection.CurrentOutstandingPrincipal,
            projection.CurrentOffsetBalance,
            projection.EffectivePrincipalAfterOffset,
            projection.ProjectedInterestFreeDate
        );

        return new MortgageForecastResponse(
            historyPoints,
            projection.Schedule,
            summary,
            forecast,
            request with { SelectedOffsetAccountIds = selectedOffsetAccountIds },
            detectedRepayment);
    }

    private async Task<MortgageDto> MapMortgageAsync(
        Mortgage mortgage,
        Account account,
        IReadOnlyList<InterestRatePeriod> rateHistory,
        CancellationToken cancellationToken)
    {
        // Get historical balance from latest transaction
        var latestTransaction = await dbContext.Transactions
            .AsNoTracking()
            .Where(t => t.AccountId == mortgage.AccountId)
            .OrderByDescending(t => t.Date)
            .FirstOrDefaultAsync(cancellationToken);

        var latestBalance = Math.Abs(latestTransaction?.BankBalance ?? 0m);
        var latestDate = latestTransaction?.Date ?? DateOnly.FromDateTime(DateTime.Today);

        // Analyze historical transactions to detect repayment patterns
        var transactions = await dbContext.Transactions
            .AsNoTracking()
            .Where(t => t.AccountId == mortgage.AccountId)
            .OrderBy(t => t.Date)
            .ToListAsync(cancellationToken);

        var detectedPattern = transactions.Count > 0
            ? repaymentAnalysisService.AnalyzeRepaymentPatternDetailed(transactions)
            : null;

        var offsetAccountIds = await dbContext.MortgageOffsetAccounts
            .AsNoTracking()
            .Where(link => link.MortgageAccountId == mortgage.AccountId)
            .Select(link => link.OffsetAccountId)
            .ToListAsync(cancellationToken);

        var offsetBalance = await GetSelectedOffsetBalanceAsync(offsetAccountIds, cancellationToken);
        var interestBearingBalance = decimal.Round(Math.Max(0m, latestBalance - offsetBalance), 2, MidpointRounding.AwayFromZero);
        var detectionConfidence = detectedPattern?.Confidence ?? 0m;
        var detectedRepaymentAmount = detectedPattern?.EstimatedAmount;
        var detectedRepaymentsPerYear = detectedPattern?.EstimatedFrequency;

        var effectiveRepaymentAmount = detectedRepaymentAmount;
        var effectiveRepaymentsPerYear = detectedRepaymentsPerYear;

        if ((!effectiveRepaymentAmount.HasValue || effectiveRepaymentAmount.Value <= 0m) && mortgage.ScheduledRepayment > 0m)
        {
            effectiveRepaymentAmount = mortgage.ScheduledRepayment;
        }

        if ((!effectiveRepaymentsPerYear.HasValue || effectiveRepaymentsPerYear.Value <= 0) && mortgage.RepaymentsPerYear > 0)
        {
            effectiveRepaymentsPerYear = mortgage.RepaymentsPerYear;
        }

        MortgageProjectionDto projection;

        if (effectiveRepaymentAmount.GetValueOrDefault() > 0m && effectiveRepaymentsPerYear.GetValueOrDefault() > 0)
        {
            var projectionResult = forecastService.GenerateForecast(
                startingBalance: latestBalance,
                startDate: latestDate,
                annualInterestRate: mortgage.AnnualInterestRate,
                repaymentAmount: effectiveRepaymentAmount.Value,
                repaymentsPerYear: effectiveRepaymentsPerYear.Value,
                offsetBalance: offsetBalance,
                rateHistory: rateHistory);

            projection = new MortgageProjectionDto(
                projectionResult.ProjectedClosingBalance,
                projectionResult.TotalFutureInterest,
                projectionResult.ProjectedPayoffDate,
                true,
                "Forecast from observed balance using detected repayment behaviour and selected offsets.",
                projectionResult.Schedule);
        }
        else
        {
            projection = new MortgageProjectionDto(
                latestBalance,
                0m,
                null,
                false,
                "Forecast unavailable until a repayment amount and frequency can be detected from imported transactions or provided as transient forecast overrides.",
                []);
        }

        var rateHistoryDto = rateHistory
            .OrderBy(period => period.StartDate)
            .Select(period => new InterestRatePeriodDto(period.StartDate, period.EndDate, period.AnnualInterestRate))
            .ToList();

        return new MortgageDto(
            mortgage.Id,
            mortgage.AccountId,
            account.Name,
            mortgage.AnnualInterestRate,
            detectedRepaymentAmount,
            detectedRepaymentsPerYear,
            latestBalance,
            offsetBalance,
            interestBearingBalance,
            latestDate,
            rateHistoryDto,
            offsetAccountIds,
            projection,
            detectionConfidence);
    }

    private async Task<IReadOnlyList<Guid>> GetPersistedOffsetAccountIdsAsync(
        Guid mortgageAccountId,
        CancellationToken cancellationToken)
    {
        return await dbContext.MortgageOffsetAccounts
            .AsNoTracking()
            .Where(link => link.MortgageAccountId == mortgageAccountId)
            .Select(link => link.OffsetAccountId)
            .ToListAsync(cancellationToken);
    }

    private async Task ReplaceOffsetAccountLinksAsync(
        Guid mortgageAccountId,
        IReadOnlyList<Guid> offsetAccountIds,
        CancellationToken cancellationToken)
    {
        var existingLinks = await dbContext.MortgageOffsetAccounts
            .Where(link => link.MortgageAccountId == mortgageAccountId)
            .ToListAsync(cancellationToken);

        dbContext.MortgageOffsetAccounts.RemoveRange(existingLinks);

        if (offsetAccountIds.Count > 0)
        {
            dbContext.MortgageOffsetAccounts.AddRange(offsetAccountIds.Select(offsetAccountId => new MortgageOffsetAccount
            {
                MortgageAccountId = mortgageAccountId,
                OffsetAccountId = offsetAccountId
            }));
        }
    }

    private async Task<IReadOnlyList<Guid>> ValidateOffsetAccountIdsAsync(
        Guid mortgageAccountId,
        IReadOnlyList<Guid> requestedOffsetAccountIds,
        CancellationToken cancellationToken)
    {
        var distinctOffsetAccountIds = requestedOffsetAccountIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (distinctOffsetAccountIds.Count == 0)
        {
            return [];
        }

        if (distinctOffsetAccountIds.Contains(mortgageAccountId))
        {
            throw new ArgumentException("The mortgage account itself cannot be selected as an offset account.", nameof(requestedOffsetAccountIds));
        }

        var accounts = await dbContext.Accounts
            .AsNoTracking()
            .Where(account => distinctOffsetAccountIds.Contains(account.Id))
            .ToListAsync(cancellationToken);

        if (accounts.Count != distinctOffsetAccountIds.Count)
        {
            throw new ArgumentException("One or more offset accounts do not exist.", nameof(requestedOffsetAccountIds));
        }

        var invalidAccounts = accounts
            .Where(account => !account.IsActive || account.Type is AccountType.Mortgage or AccountType.CreditCard)
            .ToList();

        if (invalidAccounts.Count > 0)
        {
            throw new ArgumentException("One or more offset accounts are inactive or not eligible for offset selection.", nameof(requestedOffsetAccountIds));
        }

        return distinctOffsetAccountIds;
    }

    private async Task<decimal> GetSelectedOffsetBalanceAsync(
        IReadOnlyList<Guid> selectedOffsetAccountIds,
        CancellationToken cancellationToken)
    {
        if (selectedOffsetAccountIds.Count == 0)
        {
            return 0m;
        }

        var latestBalances = await dbContext.Transactions
            .AsNoTracking()
            .Where(t => selectedOffsetAccountIds.Contains(t.AccountId))
            .GroupBy(t => t.AccountId)
            .Select(group => group
                .OrderByDescending(t => t.Date)
                .ThenByDescending(t => t.ImportedUtc)
                .Select(t => t.BankBalance)
                .FirstOrDefault())
            .ToListAsync(cancellationToken);

        return decimal.Round(
            latestBalances.Sum(balance => Math.Max(0m, balance)),
            2,
            MidpointRounding.AwayFromZero);
    }
}
