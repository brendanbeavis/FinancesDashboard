namespace FinancesDashboard.Web.Models;

public sealed record AmortisationPeriodModel(
    int PeriodNumber,
    DateOnly PeriodDate,
    decimal OpeningPrincipal,
    decimal AnnualInterestRate,
    decimal InterestAmount,
    decimal PrincipalRepaid,
    decimal Payment,
    decimal ClosingPrincipal,
    bool IsProjected,
    string Assumption);
