namespace FinancesDashboard.Application.Dto;

public sealed record DashboardSummary(
    decimal CurrentNetPosition,
    decimal CurrentCashPosition,
    decimal CurrentCreditCardPosition,
    decimal CurrentMortgagePosition);
