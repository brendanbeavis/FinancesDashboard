namespace FinancesDashboard.Web.Models;

public sealed record AccountModel(
    Guid Id,
    string Name,
    string Type,
    bool IsActive,
    string? AccountNumber = null,
    string CurrencyCode = "AUD",
    string? Provider = null,
    bool IncludeInNetWorth = true,
    DateTime? CreatedAt = null,
    DateTime? UpdatedAt = null);
