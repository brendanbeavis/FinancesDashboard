namespace FinancesDashboard.Web.Models;

public sealed record InvestmentImportPreviewResultModel(
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
    IReadOnlyList<InvestmentImportPreviewHoldingModel> Holdings,
    IReadOnlyList<InvestmentImportIssueModel> Issues);
