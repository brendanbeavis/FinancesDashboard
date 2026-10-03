namespace FinancesDashboard.Application.Dto;

public sealed record UpdateAccountRequest(
    string Name,
    bool IsActive);
