namespace FinancesDashboard.Application.Dto;

public sealed record AccountDto(Guid Id, string Name, string Type, bool IsActive);
