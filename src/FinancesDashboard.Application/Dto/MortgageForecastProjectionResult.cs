namespace FinancesDashboard.Application.Dto;

public sealed record MortgageForecastProjectionResult(
    string Status,
    DateOnly? ProjectedPayoffDate,
    decimal ProjectedClosingBalance,
    int RemainingRepaymentCount,
    decimal TotalFutureInterest,
    decimal TotalFutureRepayments,
    decimal CurrentOutstandingPrincipal,
    decimal CurrentOffsetBalance,
    decimal EffectivePrincipalAfterOffset,
    IReadOnlyList<AmortisationPeriodDto> Schedule,
    DateOnly? ProjectedInterestFreeDate = null);
