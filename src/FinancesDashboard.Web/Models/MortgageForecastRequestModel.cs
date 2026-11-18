namespace FinancesDashboard.Web.Models;

public sealed record MortgageForecastRequestModel(
    decimal AnnualInterestRate,
    decimal RepaymentAmount,
    int RepaymentsPerYear,
    IReadOnlyList<Guid> SelectedOffsetAccountIds);
