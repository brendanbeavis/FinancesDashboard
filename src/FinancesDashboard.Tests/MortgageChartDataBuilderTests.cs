using FinancesDashboard.Web.Models;
using FinancesDashboard.Web.Services;

namespace FinancesDashboard.Tests;

public sealed class MortgageChartDataBuilderTests
{
    [Fact]
    public void Build_WithNullResponse_ReturnsEmptyData()
    {
        var result = MortgageChartDataBuilder.Build(null);

        Assert.Empty(result.HistoricalPoints);
        Assert.Empty(result.ForecastPoints);
        Assert.Empty(result.OffsetAdjustedHistoricalPoints);
        Assert.Empty(result.OffsetAdjustedForecastPoints);
        Assert.False(result.HasData);
        Assert.False(result.HasOffsetAdjustedBalance);
    }

    [Fact]
    public void Build_UsesAbsoluteHistoricalBalancesAndForecastClosingPoints()
    {
        var response = new MortgageForecastResponseModel(
            HistoricalPoints:
            [
                new MortgageHistoryPointModel(new DateOnly(2026, 1, 1), -500000m, 0m, null, null),
                new MortgageHistoryPointModel(new DateOnly(2026, 2, 1), -498000m, 0m, null, null)
            ],
            ProjectedAmortisationSchedule:
            [
                new AmortisationPeriodModel(1, new DateOnly(2026, 2, 1), 498000m, 50000m, 448000m, 0.06m, 2240m, 960m, 3200m, 497040m, true, "Forecast"),
                new AmortisationPeriodModel(2, new DateOnly(2026, 3, 1), 497040m, 50000m, 447040m, 0.06m, 2235.20m, 964.80m, 3200m, 496075.20m, true, "Forecast")
            ],
            Summary: new MortgageSummaryModel(2, 498000m, 6400m, 2000m, new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1)),
            Forecast: new MortgageForecastOutcomeModel("PaidOff", new DateOnly(2045, 1, 1), 100, 123456m, 320000m, 498000m, 50000m, 448000m),
            Assumptions: new MortgageForecastRequestModel(0.06m, 3200m, 12, []),
            DetectedRepayment: null);

        var result = MortgageChartDataBuilder.Build(response);

        Assert.Equal(2, result.HistoricalPoints.Count);
        Assert.Equal(500000m, result.HistoricalPoints[0].Balance);
        Assert.Equal(498000m, result.HistoricalPoints[1].Balance);
        Assert.All(result.HistoricalPoints, point => Assert.False(point.IsForecast));

        Assert.Equal(3, result.ForecastPoints.Count);
        Assert.Equal(498000m, result.ForecastPoints[0].Balance);
        Assert.Equal(497040m, result.ForecastPoints[1].Balance);
        Assert.Equal(496075.20m, result.ForecastPoints[2].Balance);
        Assert.All(result.ForecastPoints, point => Assert.True(point.IsForecast));

        Assert.Equal(2, result.OffsetAdjustedHistoricalPoints.Count);
        Assert.Equal(450000m, result.OffsetAdjustedHistoricalPoints[0].Balance);
        Assert.Equal(448000m, result.OffsetAdjustedHistoricalPoints[1].Balance);
        Assert.All(result.OffsetAdjustedHistoricalPoints, point => Assert.False(point.IsForecast));

        Assert.Equal(3, result.OffsetAdjustedForecastPoints.Count);
        Assert.Equal(448000m, result.OffsetAdjustedForecastPoints[0].Balance);
        Assert.Equal(447040m, result.OffsetAdjustedForecastPoints[1].Balance);
        Assert.Equal(446075.20m, result.OffsetAdjustedForecastPoints[2].Balance);
        Assert.All(result.OffsetAdjustedForecastPoints, point => Assert.True(point.IsForecast));
        Assert.True(result.HasOffsetAdjustedBalance);
    }

    [Fact]
    public void Build_DownsamplesLargeHistoricalSeries_WhileKeepingLastPoint()
    {
        var history = Enumerable.Range(0, 10)
            .Select(index => new MortgageHistoryPointModel(
                new DateOnly(2026, 1, 1).AddDays(index),
                -(500000m - index),
                0m,
                null,
                null))
            .ToList();

        var response = new MortgageForecastResponseModel(
            HistoricalPoints: history,
            ProjectedAmortisationSchedule: [],
            Summary: new MortgageSummaryModel(history.Count, 499991m, 0m, 0m, history[0].Date, history[^1].Date),
            Forecast: new MortgageForecastOutcomeModel("PaidOff", null, 0, 0m, 0m, 499991m, 0m, 499991m),
            Assumptions: new MortgageForecastRequestModel(0.06m, 0m, 0, []),
            DetectedRepayment: null);

        var result = MortgageChartDataBuilder.Build(response, maxHistoricalPoints: 3);

        Assert.True(result.HistoricalPoints.Count <= 4);
        Assert.Equal(history[^1].Date.ToDateTime(TimeOnly.MinValue), result.HistoricalPoints[^1].Timestamp);
        Assert.Equal(Math.Abs(history[^1].Balance), result.HistoricalPoints[^1].Balance);
        Assert.Empty(result.OffsetAdjustedHistoricalPoints);
        Assert.Empty(result.OffsetAdjustedForecastPoints);
        Assert.False(result.HasOffsetAdjustedBalance);
    }
}
