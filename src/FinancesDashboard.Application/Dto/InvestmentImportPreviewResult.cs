namespace FinancesDashboard.Application.Dto;

public sealed record InvestmentImportPreviewResult(
    Guid InvestmentAccountId,
    string FileName,
    string FileHash,
    string? SourceAccountNumber,
    DateTime? SnapshotDateTime,
    string CurrencyCode,
    decimal Tolerance,
    int TotalRowsRead,
    int SuccessfulRows,
    int FailedRows,
    bool MissingRequiredColumns,
    IReadOnlyList<string> MissingColumns,
    bool DuplicateFileDetected,
    bool DuplicateSnapshotDetected,
    bool HasBlockingErrors,
    decimal CalculatedTotalMarketValue,
    decimal CalculatedTotalCostBase,
    decimal CalculatedTotalUnrealisedProfitLoss,
    IReadOnlyList<InvestmentImportPreviewHoldingDto> Holdings,
    IReadOnlyList<InvestmentImportIssueDto> Issues);
