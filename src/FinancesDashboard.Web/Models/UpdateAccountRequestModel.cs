namespace FinancesDashboard.Web.Models;

public sealed record UpdateAccountRequestModel(
    string Name,
    bool IsActive);
