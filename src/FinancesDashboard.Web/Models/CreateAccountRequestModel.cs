namespace FinancesDashboard.Web.Models;

public sealed record CreateAccountRequestModel(
    string Name,
    string Type,
    string? AccountNumber = null,
    string CurrencyCode = "AUD",
    string? Provider = null,
    bool IncludeInNetWorth = true);
