namespace FinancesDashboard.Web.Models;

public sealed record AccountModel(
    Guid Id,
    string Name,
    string Type,
    bool IsActive);
