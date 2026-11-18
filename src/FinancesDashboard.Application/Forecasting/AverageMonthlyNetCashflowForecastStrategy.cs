using FinancesDashboard.Application.Dto;

namespace FinancesDashboard.Application.Forecasting;

public sealed class AverageMonthlyNetCashflowForecastStrategy(ForecastOptions options) : IForecastStrategy
{
    public IReadOnlyList<ForecastPoint> Forecast(
        IReadOnlyList<CashflowPoint> history,
        DateOnly startDate,
        int months)
    {
        if (months <= 0)
        {
            return [];
        }

        var horizon = Math.Min(months, Math.Max(1, options.MaxHorizonMonths));

        var lookbackMonths = Math.Max(1, options.LookbackMonths);
        var startMonth = new DateOnly(startDate.Year, startDate.Month, 1);
        var lookbackStart = startMonth.AddMonths(-(lookbackMonths - 1));
        var lookbackHistory = history
            .Where(point => point.Date >= lookbackStart && point.Date <= startDate)
            .ToList();

        var monthlyGroups = lookbackHistory
            .GroupBy(point => new { point.Date.Year, point.Date.Month })
            .Select(group => group.Sum(point => point.Net))
            .ToList();

        var averageMonthlyNet = monthlyGroups.Count == 0
            ? 0m
            : monthlyGroups.Average();

        var points = new List<ForecastPoint>(horizon);
        var projectedPosition = 0m;
        var projectedDate = startDate;

        for (var index = 0; index < horizon; index++)
        {
            projectedDate = projectedDate.AddMonths(1);
            projectedPosition += averageMonthlyNet;

            points.Add(new ForecastPoint(
                projectedDate,
                decimal.Round(projectedPosition, 2, MidpointRounding.AwayFromZero),
                true,
                "Average monthly net cashflow assumption"));
        }

        return points;
    }
}
