namespace FinancesDashboard.Application.Dto;

public sealed record UpdateAccountRequest(
    string Name,
    bool IsActive,
    string? AccountNumber = null,
    string CurrencyCode = "AUD",
    string? Provider = null,
    bool IncludeInNetWorth = true);
