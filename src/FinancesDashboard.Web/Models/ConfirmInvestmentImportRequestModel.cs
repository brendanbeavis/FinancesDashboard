namespace FinancesDashboard.Web.Models;

public sealed record ConfirmInvestmentImportRequestModel(
    Guid InvestmentAccountId,
    string FileName,
    string FileHash,
    DateTime SnapshotDateTime,
    string CurrencyCode,
    bool ReplaceExistingSnapshot,
    IReadOnlyList<InvestmentImportPreviewHoldingModel> Holdings,
    IReadOnlyList<InvestmentImportIssueModel> Issues);
