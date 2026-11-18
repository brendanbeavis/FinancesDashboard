namespace FinancesDashboard.Application.Dto;

public sealed record UpdateInvestmentAccountRequest(
    string Name,
    string? AccountNumber,
    string? Provider,
    string CurrencyCode,
    bool IsActive,
    bool IncludeInNetWorth,
    string AccountType = "Brokerage");
