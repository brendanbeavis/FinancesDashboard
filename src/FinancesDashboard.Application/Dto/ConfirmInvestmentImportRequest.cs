namespace FinancesDashboard.Application.Dto;

public sealed record ConfirmInvestmentImportRequest(
    Guid InvestmentAccountId,
    string FileName,
    string FileHash,
    DateTime SnapshotDateTime,
    string CurrencyCode,
    bool ReplaceExistingSnapshot,
    IReadOnlyList<InvestmentImportPreviewHoldingDto> Holdings,
    IReadOnlyList<InvestmentImportIssueDto> Issues);
