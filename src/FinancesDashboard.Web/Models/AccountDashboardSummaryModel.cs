namespace FinancesDashboard.Web.Models;

public sealed record AccountDashboardSummaryModel(
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
