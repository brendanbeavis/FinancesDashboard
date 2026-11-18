namespace FinancesDashboard.Web.Models;

public sealed record InvestmentImportCommitResultModel(
    Guid ImportBatchId,
    Guid SnapshotId,
    int ImportedRows,
    int FailedRows,
    int ClosedHoldings,
    decimal TotalMarketValue,
    decimal TotalCostBase,
    decimal TotalUnrealisedProfitLoss,
    bool ReplacedExistingSnapshot,
    string ImportStatus,
    IReadOnlyList<string> Warnings);
