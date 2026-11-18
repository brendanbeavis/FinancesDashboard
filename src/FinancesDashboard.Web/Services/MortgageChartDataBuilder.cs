using FinancesDashboard.Web.Models;

namespace FinancesDashboard.Web.Services;

public static class MortgageChartDataBuilder
{
    private const int DefaultMaxHistoricalPoints = 1500;

    public static MortgageBalanceChartDataModel Build(
        MortgageForecastResponseModel? response,
        int maxHistoricalPoints = DefaultMaxHistoricalPoints)
    {
        if (response is null)
        {
            return new MortgageBalanceChartDataModel([], [], [], []);
        }

        var historicalPoints = BuildHistoricalPoints(response.HistoricalPoints, maxHistoricalPoints);
        var forecastPoints = BuildForecastPoints(response.ProjectedAmortisationSchedule);
        var offsetAdjustedHistoricalPoints = BuildOffsetAdjustedHistoricalPoints(
            response.HistoricalPoints,
            response.Forecast.CurrentOffsetBalance,
            maxHistoricalPoints);
        var offsetAdjustedForecastPoints = BuildOffsetAdjustedForecastPoints(response.ProjectedAmortisationSchedule);

        return new MortgageBalanceChartDataModel(
            historicalPoints,
            forecastPoints,
            offsetAdjustedHistoricalPoints,
            offsetAdjustedForecastPoints);
    }

    private static IReadOnlyList<MortgageBalanceChartPointModel> BuildHistoricalPoints(
        IReadOnlyList<MortgageHistoryPointModel> history,
        int maxHistoricalPoints)
    {
        if (history.Count == 0)
        {
            return [];
        }

        var ordered = history
            .OrderBy(point => point.Date)
            .ToList();

        var reduced = ordered.Count <= maxHistoricalPoints
            ? ordered
            : Downsample(ordered, maxHistoricalPoints);

        return reduced
            .Select(point => new MortgageBalanceChartPointModel(
                point.Date.ToDateTime(TimeOnly.MinValue),
                decimal.Round(Math.Abs(point.Balance), 2, MidpointRounding.AwayFromZero),
                false,
                "Historical"))
            .ToList();
    }

    private static IReadOnlyList<MortgageBalanceChartPointModel> BuildForecastPoints(
        IReadOnlyList<AmortisationPeriodModel> schedule)
    {
        if (schedule.Count == 0)
        {
            return [];
        }

        var ordered = schedule
            .OrderBy(period => period.PeriodDate)
            .ThenBy(period => period.PeriodNumber)
            .ToList();

        var points = new List<MortgageBalanceChartPointModel>(ordered.Count + 1);
        var firstPeriod = ordered[0];
        var openingTimestamp = firstPeriod.PeriodDate.ToDateTime(TimeOnly.MinValue);

        points.Add(new MortgageBalanceChartPointModel(
            openingTimestamp,
            decimal.Round(firstPeriod.OpeningPrincipal, 2, MidpointRounding.AwayFromZero),
            true,
            "Forecast"));

        foreach (var period in ordered)
        {
            var timestamp = period.PeriodDate.ToDateTime(TimeOnly.MinValue);
            if (points[^1].Timestamp == timestamp)
            {
                timestamp = timestamp.AddHours(12);
            }

            points.Add(new MortgageBalanceChartPointModel(
                timestamp,
                decimal.Round(period.ClosingPrincipal, 2, MidpointRounding.AwayFromZero),
                true,
                "Forecast"));
        }

        return points;
    }

    private static IReadOnlyList<MortgageBalanceChartPointModel> BuildOffsetAdjustedHistoricalPoints(
        IReadOnlyList<MortgageHistoryPointModel> history,
        decimal currentOffsetBalance,
        int maxHistoricalPoints)
    {
        if (history.Count == 0 || currentOffsetBalance <= 0m)
        {
            return [];
        }

        var ordered = history
            .OrderBy(point => point.Date)
            .ToList();

        var reduced = ordered.Count <= maxHistoricalPoints
            ? ordered
            : Downsample(ordered, maxHistoricalPoints);

        return reduced
            .Select(point => new MortgageBalanceChartPointModel(
                point.Date.ToDateTime(TimeOnly.MinValue),
                decimal.Round(Math.Max(0m, Math.Abs(point.Balance) - currentOffsetBalance), 2, MidpointRounding.AwayFromZero),
                false,
                "Historical (after offsets)"))
            .ToList();
    }

    private static IReadOnlyList<MortgageBalanceChartPointModel> BuildOffsetAdjustedForecastPoints(
        IReadOnlyList<AmortisationPeriodModel> schedule)
    {
        if (schedule.Count == 0 || schedule.All(period => period.OffsetBalance <= 0m))
        {
            return [];
        }

        var ordered = schedule
            .OrderBy(period => period.PeriodDate)
            .ThenBy(period => period.PeriodNumber)
            .ToList();

        var points = new List<MortgageBalanceChartPointModel>(ordered.Count + 1);
        var firstPeriod = ordered[0];
        var openingTimestamp = firstPeriod.PeriodDate.ToDateTime(TimeOnly.MinValue);

        points.Add(new MortgageBalanceChartPointModel(
            openingTimestamp,
            decimal.Round(firstPeriod.EffectivePrincipal, 2, MidpointRounding.AwayFromZero),
            true,
            "Forecast (after offsets)"));

        foreach (var period in ordered)
        {
            var timestamp = period.PeriodDate.ToDateTime(TimeOnly.MinValue);
            if (points[^1].Timestamp == timestamp)
            {
                timestamp = timestamp.AddHours(12);
            }

            points.Add(new MortgageBalanceChartPointModel(
                timestamp,
                decimal.Round(Math.Max(0m, period.ClosingPrincipal - period.OffsetBalance), 2, MidpointRounding.AwayFromZero),
                true,
                "Forecast (after offsets)"));
        }

        return points;
    }

    private static List<MortgageHistoryPointModel> Downsample(
        IReadOnlyList<MortgageHistoryPointModel> ordered,
        int maxHistoricalPoints)
    {
        var step = Math.Max(1, (int)Math.Ceiling(ordered.Count / (double)maxHistoricalPoints));
        var reduced = new List<MortgageHistoryPointModel>(maxHistoricalPoints);

        for (var index = 0; index < ordered.Count; index += step)
        {
            reduced.Add(ordered[index]);
        }

        if (reduced[^1] != ordered[^1])
        {
            reduced.Add(ordered[^1]);
        }

        return reduced;
    }
}
