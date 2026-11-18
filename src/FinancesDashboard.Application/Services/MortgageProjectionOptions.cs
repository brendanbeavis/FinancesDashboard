namespace FinancesDashboard.Application.Services;

public sealed class MortgageProjectionOptions
{
    public const string SectionName = "MortgageProjection";

    public int HorizonPeriods { get; set; } = 360;
}
