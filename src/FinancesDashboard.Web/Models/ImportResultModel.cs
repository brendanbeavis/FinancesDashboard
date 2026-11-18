namespace FinancesDashboard.Web.Models;

public sealed record ImportResultModel(
    int Imported,
    int Skipped,
    IReadOnlyList<string> Errors);
