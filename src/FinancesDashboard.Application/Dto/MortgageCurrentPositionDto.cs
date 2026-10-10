namespace FinancesDashboard.Application.Dto;

public sealed record MortgageCurrentPositionDto(
    int TransactionCount,
    decimal CurrentBalance,
    decimal TotalOffsetBalance,
    decimal InterestBearingBalance,
    decimal TotalRepayments,
    decimal BalanceChange,
    decimal? EstimatedAnnualInterestRate,
    DateOnly? EarliestDate,
    DateOnly? LatestDate,
    IReadOnlyList<Guid> OffsetAccountIds);
