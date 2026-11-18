# Forecasting

## Initial Strategy

Use a deterministic average monthly net-cashflow model.

1. Collect historical cashflow.
2. Apply configurable lookback.
3. Calculate average monthly net cashflow.
4. Apply it to future periods.
5. Return projected position points.

```csharp
public interface IForecastStrategy
{
    IReadOnlyList<ForecastPoint> Forecast(
        IReadOnlyList<CashflowPoint> history,
        DateOnly startDate,
        int months);
}
```

Example:

```text
Current Position = $500,000
Average Monthly Net Cashflow = $3,000

Month 1 = $503,000
Month 2 = $506,000
Month 3 = $509,000
```

The forecast assumes historical behaviour remains representative. These assumptions must be visible.

Future strategies may include rolling averages, weighted averages, seasonality, budgets, scenarios and Monte Carlo.

Forecast charts must clearly identify the boundary between observed and projected data.
