using FinancesDashboard.Domain.Enums;

namespace FinancesDashboard.Domain.Models;

public sealed class PortfolioImportBatch
{
    public Guid Id { get; set; }

    public Guid InvestmentAccountId { get; set; }

    public required string FileName { get; set; }

    public required string FileHash { get; set; }

    public DateTime ImportDateTime { get; set; }

    public DateTime SnapshotDateTime { get; set; }

    public int TotalRowsRead { get; set; }

    public int SuccessfulRows { get; set; }

    public int FailedRows { get; set; }

    public decimal TotalMarketValue { get; set; }

    public decimal TotalCostBase { get; set; }

    public PortfolioImportStatus ImportStatus { get; set; }

    public string? ErrorSummary { get; set; }
}
