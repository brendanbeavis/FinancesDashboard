namespace FinancesDashboard.Application.Dto;

public sealed record CashflowPoint(DateOnly Date, decimal Income, decimal Expenses, decimal Net);
