namespace FinancesDashboard.Web.Models;

public sealed record UpdateInvestmentAccountRequestModel(
    string Name,
    string? AccountNumber,
    string? Provider,
    string CurrencyCode,
    bool IsActive,
    bool IncludeInNetWorth,
    string AccountType = "Brokerage");
