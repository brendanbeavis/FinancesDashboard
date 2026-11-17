namespace FinancesDashboard.Web.Models;

public sealed record InvestmentImportIssueModel(
    int RowNumber,
    string? InstrumentCode,
    string ErrorMessage,
    string RawRow);
