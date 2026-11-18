namespace FinancesDashboard.Application.Dto;

public sealed record AccountDto(
    Guid Id,
    string Name,
    string Type,
    bool IsActive,
    string? AccountNumber = null,
    string CurrencyCode = "AUD",
    string? Provider = null,
    bool IncludeInNetWorth = true,
    DateTime? CreatedAt = null,
    DateTime? UpdatedAt = null);
