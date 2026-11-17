namespace FinancesDashboard.Application.Dto;

public sealed record InvestmentImportIssueDto(
    int RowNumber,
    string? InstrumentCode,
    string ErrorMessage,
    string RawRow);
