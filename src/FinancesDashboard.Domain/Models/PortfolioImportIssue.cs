namespace FinancesDashboard.Domain.Models;

public sealed class PortfolioImportIssue
{
    public Guid Id { get; set; }

    public Guid PortfolioImportBatchId { get; set; }

    public int RowNumber { get; set; }

    public string? InstrumentCode { get; set; }

    public required string ErrorMessage { get; set; }

    public required string RawRow { get; set; }
}
