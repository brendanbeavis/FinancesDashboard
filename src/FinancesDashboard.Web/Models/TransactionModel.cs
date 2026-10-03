namespace FinancesDashboard.Web.Models;

public sealed record TransactionModel(
    Guid Id,
    Guid AccountId,
    DateOnly Date,
    decimal Amount,
    string Description,
    decimal BankBalance,
    DateTime ImportedUtc);
