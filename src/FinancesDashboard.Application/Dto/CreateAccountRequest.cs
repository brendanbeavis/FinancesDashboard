namespace FinancesDashboard.Application.Dto;

public sealed record CreateAccountRequest(
    string Name,
    string Type,
    string? AccountNumber = null,
    string CurrencyCode = "AUD",
    string? Provider = null,
    bool IncludeInNetWorth = true);
