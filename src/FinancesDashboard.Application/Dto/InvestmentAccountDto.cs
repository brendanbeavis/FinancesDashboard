namespace FinancesDashboard.Application.Dto;

public sealed record InvestmentAccountDto(
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
