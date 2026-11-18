using FinancesDashboard.Application.Dto;

namespace FinancesDashboard.Application.Forecasting;

public interface IForecastStrategy
{
    IReadOnlyList<ForecastPoint> Forecast(
        IReadOnlyList<CashflowPoint> history,
        DateOnly startDate,
        int months);
}
