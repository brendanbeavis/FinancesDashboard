namespace FinancesDashboard.Application.Dto;

public sealed record CreateInvestmentAccountRequest(
    string Name,
    string? AccountNumber,
    string? Provider,
    string CurrencyCode,
    bool IncludeInNetWorth,
    string AccountType = "Brokerage");
