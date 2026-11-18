namespace FinancesDashboard.Web.Models;

public sealed record BalancePointModel(
    DateOnly Date,
    decimal Balance);
