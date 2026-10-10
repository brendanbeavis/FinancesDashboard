namespace FinancesDashboard.Web.Models;

public sealed record MortgageBalanceChartPointModel(
    DateTime Timestamp,
    decimal Balance,
    bool IsForecast,
    string StatusLabel);
