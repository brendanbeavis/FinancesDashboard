namespace FinancesDashboard.Web.Models;

public sealed record PortfolioImportBatchModel(
    Guid Id,
    string FileName,
    string FileHash,
    DateTime ImportDateTime,
    DateTime SnapshotDateTime,
    int TotalRowsRead,
    int SuccessfulRows,
    int FailedRows,
    decimal TotalMarketValue,
    decimal TotalCostBase,
    string ImportStatus,
    string? ErrorSummary);
