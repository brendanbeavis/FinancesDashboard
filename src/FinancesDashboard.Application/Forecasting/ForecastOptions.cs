namespace FinancesDashboard.Application.Forecasting;

public sealed class ForecastOptions
{
    public const string SectionName = "Forecasting";

    public int LookbackMonths { get; set; } = 6;

    public int MaxHorizonMonths { get; set; } = 36;
}
