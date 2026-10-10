namespace FinancesDashboard.Application.Dto;

public sealed record AmortisationPeriodDto(
    int PeriodNumber,
    DateOnly PeriodDate,
    decimal OpeningPrincipal,
    decimal OffsetBalance,
    decimal EffectivePrincipal,
    decimal AnnualInterestRate,
    decimal InterestAmount,
    decimal PrincipalRepaid,
    decimal Payment,
    decimal ClosingPrincipal,
    bool IsProjected,
    string Assumption);
