namespace FinancesDashboard.Domain.Models;

public sealed class MortgageOffsetAccount
{
    public Guid MortgageAccountId { get; set; }

    public Guid OffsetAccountId { get; set; }
}
