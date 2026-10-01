namespace FinancesDashboard.Web.Models;

public sealed record DashboardSummaryModel(
    decimal CurrentNetPosition,
    decimal CurrentCashPosition,
    decimal CurrentCreditCardPosition,
    decimal CurrentMortgagePosition);
