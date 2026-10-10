namespace FinancesDashboard.Web.Models;

public sealed record UpdateMortgageOffsetAccountsRequestModel(
    IReadOnlyList<Guid> OffsetAccountIds);
