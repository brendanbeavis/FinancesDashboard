namespace FinancesDashboard.Web.Models;

public sealed record DashboardAccountTrendChartPointModel(
    DateTime Timestamp,
    decimal Balance,
    string AccountName);