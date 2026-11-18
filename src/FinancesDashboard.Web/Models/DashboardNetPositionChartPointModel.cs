namespace FinancesDashboard.Web.Models;

public sealed record DashboardNetPositionChartPointModel(
    DateTime Timestamp,
    decimal NetPosition);