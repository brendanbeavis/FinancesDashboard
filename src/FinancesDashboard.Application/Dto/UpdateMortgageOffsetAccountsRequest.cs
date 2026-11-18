namespace FinancesDashboard.Application.Dto;

public sealed record UpdateMortgageOffsetAccountsRequest(
    IReadOnlyList<Guid> OffsetAccountIds);
