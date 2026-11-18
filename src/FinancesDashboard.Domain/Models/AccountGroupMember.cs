namespace FinancesDashboard.Domain.Models;

public sealed class AccountGroupMember
{
    public Guid AccountGroupId { get; set; }

    public Guid AccountId { get; set; }
}
