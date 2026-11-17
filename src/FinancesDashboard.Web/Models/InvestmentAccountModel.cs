namespace FinancesDashboard.Web.Models;

public sealed record InvestmentAccountModel(
    Guid Id,
    string Name,
    string? AccountNumber,
    string AccountType,
    string CurrencyCode,
    string? Provider,
    bool IsActive,
    bool IncludeInNetWorth,
    DateTime CreatedAt,
    DateTime UpdatedAt);
