namespace FinancesDashboard.Web.Models;

public sealed record MortgageBalanceChartDataModel(
    IReadOnlyList<MortgageBalanceChartPointModel> HistoricalPoints,
    IReadOnlyList<MortgageBalanceChartPointModel> ForecastPoints,
    IReadOnlyList<MortgageBalanceChartPointModel> OffsetAdjustedHistoricalPoints,
    IReadOnlyList<MortgageBalanceChartPointModel> OffsetAdjustedForecastPoints)
{
    public bool HasData => HistoricalPoints.Count > 0
        || ForecastPoints.Count > 0
        || OffsetAdjustedHistoricalPoints.Count > 0
        || OffsetAdjustedForecastPoints.Count > 0;

    public bool HasOffsetAdjustedBalance => OffsetAdjustedHistoricalPoints.Count > 0 || OffsetAdjustedForecastPoints.Count > 0;
}
