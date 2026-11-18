namespace FinancesDashboard.Web.Models;

public sealed record DashboardActivityChartPointModel(
    string AccountName,
    decimal Income,
    decimal Expenses,
    decimal Net);