using FinancesDashboard.Domain.Enums;

namespace FinancesDashboard.Domain.Models;

public sealed class InvestmentInstrument
{
    public Guid Id { get; set; }

    public required string Code { get; set; }

    public string? Name { get; set; }

    public InvestmentInstrumentType InstrumentType { get; set; }

    public required string Exchange { get; set; }

    public required string CurrencyCode { get; set; }

    public bool IsActive { get; set; }
}
