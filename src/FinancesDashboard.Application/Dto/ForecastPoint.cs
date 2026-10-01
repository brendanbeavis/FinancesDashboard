namespace FinancesDashboard.Application.Dto;

public sealed record ForecastPoint(
    DateOnly Date,
    decimal ProjectedPosition,
    bool IsProjected,
    string Assumption);
