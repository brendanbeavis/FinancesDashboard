namespace FinancesDashboard.Web.Models;

public sealed record UpdateAccountRequestModel(
    string Name,
    bool IsActive,
    string? AccountNumber = null,
    string CurrencyCode = "AUD",
    string? Provider = null,
    bool IncludeInNetWorth = true);
