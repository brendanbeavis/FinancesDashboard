namespace FinancesDashboard.Application.Dto;

public sealed record AccountDashboardSummary(
    Guid AccountId,
    string AccountName,
    string AccountType,
    decimal CurrentPosition,
    bool IsLiability,
    decimal PreviousMonthIncome,
    decimal PreviousMonthExpenses,
    decimal PreviousMonthNet,
    DateOnly? LastTransactionDate,
    int TransactionCount,
    int PreviousMonthTransactionCount,
    bool IsActive);
