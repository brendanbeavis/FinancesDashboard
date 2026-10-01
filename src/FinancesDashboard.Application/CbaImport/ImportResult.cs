namespace FinancesDashboard.Application.CbaImport;

public sealed record ImportResult(
    int Imported,
    int Skipped,
    IReadOnlyList<string> Errors);
