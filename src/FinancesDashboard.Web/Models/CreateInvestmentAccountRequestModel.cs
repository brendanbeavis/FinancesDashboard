namespace FinancesDashboard.Web.Models;

public sealed record CreateInvestmentAccountRequestModel(
    string Name,
    string? AccountNumber,
    string? Provider,
    string CurrencyCode,
    bool IncludeInNetWorth,
    string AccountType = "Brokerage");
