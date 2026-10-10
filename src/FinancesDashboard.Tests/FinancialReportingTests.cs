using FinancesDashboard.Application.Services;
using FinancesDashboard.Application.Forecasting;
using FinancesDashboard.Domain.Enums;
using FinancesDashboard.Domain.Models;
using FinancesDashboard.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FinancesDashboard.Tests;

public sealed class FinancialReportingTests
{
    [Fact]
    public async Task GetBalanceAsync_UsesLatestObservedBankBalancePerDate()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var account = await CreateAccountAsync(dbContext, "Daily", AccountType.Transaction);

        dbContext.Transactions.AddRange(
            CreateTransaction(account.Id, new DateOnly(2026, 1, 1), 100m, 100m, new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(account.Id, new DateOnly(2026, 1, 1), -20m, 80m, new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(account.Id, new DateOnly(2026, 1, 3), 10m, 90m, new DateTime(2026, 1, 3, 9, 0, 0, DateTimeKind.Utc)));

        await dbContext.SaveChangesAsync();

        var service = CreateDashboardService(dbContext);
        var points = await service.GetBalanceAsync(account.Id, CancellationToken.None);

        Assert.Collection(points,
            point =>
            {
                Assert.Equal(new DateOnly(2026, 1, 1), point.Date);
                Assert.Equal(80m, point.Balance);
            },
            point =>
            {
                Assert.Equal(new DateOnly(2026, 1, 3), point.Date);
                Assert.Equal(90m, point.Balance);
            });
    }

    [Fact]
    public async Task GetGroupPositionAsync_SumsLatestAvailableBalancesOnOrBeforeDate()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var accountA = await CreateAccountAsync(dbContext, "A", AccountType.Transaction);
        var accountB = await CreateAccountAsync(dbContext, "B", AccountType.Savings);
        var accountC = await CreateAccountAsync(dbContext, "C", AccountType.Other);

        var group = new AccountGroup
        {
            Id = Guid.NewGuid(),
            Name = "Group 1"
        };

        dbContext.AccountGroups.Add(group);
        dbContext.AccountGroupMembers.AddRange(
            new AccountGroupMember { AccountGroupId = group.Id, AccountId = accountA.Id },
            new AccountGroupMember { AccountGroupId = group.Id, AccountId = accountB.Id },
            new AccountGroupMember { AccountGroupId = group.Id, AccountId = accountC.Id });

        dbContext.Transactions.AddRange(
            CreateTransaction(accountA.Id, new DateOnly(2026, 1, 1), 100m, 100m, new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(accountA.Id, new DateOnly(2026, 1, 3), 30m, 130m, new DateTime(2026, 1, 3, 9, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(accountB.Id, new DateOnly(2026, 1, 2), 200m, 200m, new DateTime(2026, 1, 2, 9, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(accountB.Id, new DateOnly(2026, 1, 4), -50m, 150m, new DateTime(2026, 1, 4, 9, 0, 0, DateTimeKind.Utc)));

        await dbContext.SaveChangesAsync();

        var service = CreateDashboardService(dbContext);
        var points = await service.GetGroupPositionAsync(group.Id, CancellationToken.None);

        Assert.Collection(points,
            point =>
            {
                Assert.Equal(new DateOnly(2026, 1, 1), point.Date);
                Assert.Equal(100m, point.Position);
            },
            point =>
            {
                Assert.Equal(new DateOnly(2026, 1, 2), point.Date);
                Assert.Equal(300m, point.Position);
            },
            point =>
            {
                Assert.Equal(new DateOnly(2026, 1, 3), point.Date);
                Assert.Equal(330m, point.Position);
            },
            point =>
            {
                Assert.Equal(new DateOnly(2026, 1, 4), point.Date);
                Assert.Equal(280m, point.Position);
            });
    }

    [Fact]
    public async Task GetCashflowAsync_ComputesIncomeExpensesAndNetByDate()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var account = await CreateAccountAsync(dbContext, "Cashflow", AccountType.Transaction);

        dbContext.Transactions.AddRange(
            CreateTransaction(account.Id, new DateOnly(2026, 2, 1), 100m, 500m, new DateTime(2026, 2, 1, 8, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(account.Id, new DateOnly(2026, 2, 1), -30m, 470m, new DateTime(2026, 2, 1, 9, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(account.Id, new DateOnly(2026, 2, 1), -20m, 450m, new DateTime(2026, 2, 1, 10, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(account.Id, new DateOnly(2026, 2, 2), -10m, 440m, new DateTime(2026, 2, 2, 9, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(account.Id, new DateOnly(2026, 2, 2), 5m, 445m, new DateTime(2026, 2, 2, 10, 0, 0, DateTimeKind.Utc)));

        await dbContext.SaveChangesAsync();

        var service = CreateDashboardService(dbContext);
        var points = await service.GetCashflowAsync(account.Id, CancellationToken.None);

        Assert.Collection(points,
            point =>
            {
                Assert.Equal(new DateOnly(2026, 2, 1), point.Date);
                Assert.Equal(100m, point.Income);
                Assert.Equal(50m, point.Expenses);
                Assert.Equal(50m, point.Net);
            },
            point =>
            {
                Assert.Equal(new DateOnly(2026, 2, 2), point.Date);
                Assert.Equal(5m, point.Income);
                Assert.Equal(10m, point.Expenses);
                Assert.Equal(-5m, point.Net);
            });
    }

    [Fact]
    public async Task GetCreditCardSpendAsync_UsesNegativeCreditCardTransactionsOnly()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var creditCard = await CreateAccountAsync(dbContext, "Card", AccountType.CreditCard);
        var transactionAccount = await CreateAccountAsync(dbContext, "Daily", AccountType.Transaction);

        dbContext.Transactions.AddRange(
            CreateTransaction(creditCard.Id, new DateOnly(2026, 3, 1), -40m, -40m, new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(creditCard.Id, new DateOnly(2026, 3, 1), 10m, -30m, new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(creditCard.Id, new DateOnly(2026, 3, 1), -15m, -45m, new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(creditCard.Id, new DateOnly(2026, 3, 2), -5m, -50m, new DateTime(2026, 3, 2, 9, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(transactionAccount.Id, new DateOnly(2026, 3, 1), -100m, 900m, new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc)));

        await dbContext.SaveChangesAsync();

        var service = CreateDashboardService(dbContext);
        var points = await service.GetCreditCardSpendAsync(CancellationToken.None);

        Assert.Collection(points,
            point =>
            {
                Assert.Equal(new DateOnly(2026, 3, 1), point.Date);
                Assert.Equal(55m, point.Spend);
            },
            point =>
            {
                Assert.Equal(new DateOnly(2026, 3, 2), point.Date);
                Assert.Equal(5m, point.Spend);
            });
    }

    [Fact]
    public async Task GetSummaryAsync_CreditCard_OneCardWithNegativeBalance()
    {
        // Arrange: Single credit card with spending (negative amounts)
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var creditCard = await CreateAccountAsync(dbContext, "Test Card", AccountType.CreditCard);

        dbContext.Transactions.AddRange(
            CreateTransaction(creditCard.Id, new DateOnly(2026, 1, 1), -50m, 0m, new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(creditCard.Id, new DateOnly(2026, 1, 5), -75m, 0m, new DateTime(2026, 1, 5, 9, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(creditCard.Id, new DateOnly(2026, 1, 10), -25m, 0m, new DateTime(2026, 1, 10, 9, 0, 0, DateTimeKind.Utc)));

        await dbContext.SaveChangesAsync();

        var service = CreateDashboardService(dbContext);
        var summary = await service.GetSummaryAsync(CancellationToken.None);

        // Accrued balance: -50 + -75 + -25 = -150m
        // User display (positive owing): 150m
        Assert.Equal(150m, summary.CurrentCreditCardPosition);
        Assert.Equal(-150m, summary.CurrentNetPosition);
    }

    [Fact]
    public async Task GetSummaryAsync_CreditCard_MultipleCardsWithDifferentBalances()
    {
        // Arrange: Multiple credit cards with different balances
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var card1 = await CreateAccountAsync(dbContext, "Card 1", AccountType.CreditCard);
        var card2 = await CreateAccountAsync(dbContext, "Card 2", AccountType.CreditCard);

        dbContext.Transactions.AddRange(
            CreateTransaction(card1.Id, new DateOnly(2026, 1, 1), -100m, 0m, new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(card1.Id, new DateOnly(2026, 1, 5), -50m, 0m, new DateTime(2026, 1, 5, 9, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(card2.Id, new DateOnly(2026, 1, 2), -75m, 0m, new DateTime(2026, 1, 2, 9, 0, 0, DateTimeKind.Utc)));

        await dbContext.SaveChangesAsync();

        var service = CreateDashboardService(dbContext);
        var summary = await service.GetSummaryAsync(CancellationToken.None);

        // Card1 balance: -100 + -50 = -150m
        // Card2 balance: -75m
        // Total accrued: -225m
        // User display: 225m
        Assert.Equal(225m, summary.CurrentCreditCardPosition);
        Assert.Equal(-225m, summary.CurrentNetPosition);
    }

    [Fact]
    public async Task GetSummaryAsync_CreditCard_ZeroBalance()
    {
        // Arrange: Credit card with no transactions
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var creditCard = await CreateAccountAsync(dbContext, "Unused Card", AccountType.CreditCard);
        var cashAccount = await CreateAccountAsync(dbContext, "Cash", AccountType.Transaction);

        dbContext.Transactions.Add(
            CreateTransaction(cashAccount.Id, new DateOnly(2026, 1, 1), 1000m, 1000m, new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc)));

        await dbContext.SaveChangesAsync();

        var service = CreateDashboardService(dbContext);
        var summary = await service.GetSummaryAsync(CancellationToken.None);

        Assert.Equal(0m, summary.CurrentCreditCardPosition);
        Assert.Equal(1000m, summary.CurrentNetPosition);
        Assert.Equal(1000m, summary.CurrentCashPosition);
    }

    [Fact]
    public async Task GetSummaryAsync_CreditCard_NoTransactions()
    {
        // Arrange: No accounts at all
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var service = CreateDashboardService(dbContext);
        var summary = await service.GetSummaryAsync(CancellationToken.None);

        Assert.Equal(0m, summary.CurrentCreditCardPosition);
        Assert.Equal(0m, summary.CurrentNetPosition);
        Assert.Equal(0m, summary.CurrentCashPosition);
        Assert.Equal(0m, summary.CurrentMortgagePosition);
    }

    [Fact]
    public async Task GetSummaryAsync_CreditCard_MixedDebitCredit()
    {
        // Arrange: Credit card with both purchases (negative) and repayments (positive)
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var creditCard = await CreateAccountAsync(dbContext, "Mixed Card", AccountType.CreditCard);

        dbContext.Transactions.AddRange(
            CreateTransaction(creditCard.Id, new DateOnly(2026, 1, 1), -150m, 0m, new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(creditCard.Id, new DateOnly(2026, 1, 5), 100m, 0m, new DateTime(2026, 1, 5, 9, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(creditCard.Id, new DateOnly(2026, 1, 10), -50m, 0m, new DateTime(2026, 1, 10, 9, 0, 0, DateTimeKind.Utc)));

        await dbContext.SaveChangesAsync();

        var service = CreateDashboardService(dbContext);
        var summary = await service.GetSummaryAsync(CancellationToken.None);

        // Accrued balance: -150 + 100 + -50 = -100m (still owing)
        // User display: 100m
        Assert.Equal(100m, summary.CurrentCreditCardPosition);
        Assert.Equal(-100m, summary.CurrentNetPosition);
    }

    [Fact]
    public async Task GetSummaryAsync_CreditCard_LatestBankBalanceNotUsedForCBA()
    {
        // Arrange: Credit card where latest BankBalance differs from sum of amounts
        // (This tests that we use SUM(Amount) not latest BankBalance)
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var creditCard = await CreateAccountAsync(dbContext, "BankBalance Test", AccountType.CreditCard);

        dbContext.Transactions.AddRange(
            CreateTransaction(creditCard.Id, new DateOnly(2026, 1, 1), -100m, 0m, new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(creditCard.Id, new DateOnly(2026, 1, 5), -100m, 0m, new DateTime(2026, 1, 5, 9, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(creditCard.Id, new DateOnly(2026, 1, 10), -100m, -250m, new DateTime(2026, 1, 10, 9, 0, 0, DateTimeKind.Utc)));

        await dbContext.SaveChangesAsync();

        var service = CreateDashboardService(dbContext);
        var summary = await service.GetSummaryAsync(CancellationToken.None);

        // SUM of amounts: -100 + -100 + -100 = -300m
        // Latest BankBalance: -250m (if we were using it incorrectly)
        // We should use SUM (300m) not latest BankBalance (250m)
        Assert.Equal(300m, summary.CurrentCreditCardPosition);
    }

    [Fact]
    public async Task GetSummaryAsync_CreditCard_WithOtherAccountTypes()
    {
        // Arrange: Mixed account types including credit card
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var transaction = await CreateAccountAsync(dbContext, "Txn", AccountType.Transaction);
        var savings = await CreateAccountAsync(dbContext, "Savings", AccountType.Savings);
        var creditCard = await CreateAccountAsync(dbContext, "Card", AccountType.CreditCard);

        dbContext.Transactions.AddRange(
            CreateTransaction(transaction.Id, new DateOnly(2026, 1, 1), 500m, 500m, new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(savings.Id, new DateOnly(2026, 1, 1), 300m, 300m, new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(creditCard.Id, new DateOnly(2026, 1, 1), -100m, 0m, new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(creditCard.Id, new DateOnly(2026, 1, 5), -50m, 0m, new DateTime(2026, 1, 5, 9, 0, 0, DateTimeKind.Utc)));

        await dbContext.SaveChangesAsync();

        var service = CreateDashboardService(dbContext);
        var summary = await service.GetSummaryAsync(CancellationToken.None);

        // Cash: 500 + 300 = 800m
        // Credit Card owing: 150m
        // Net: 500 + 300 - 150 = 650m
        Assert.Equal(800m, summary.CurrentCashPosition);
        Assert.Equal(150m, summary.CurrentCreditCardPosition);
        Assert.Equal(650m, summary.CurrentNetPosition);
    }

    [Fact]
    public async Task GetSummaryAsync_ComputesComponentAndNetPositionsDeterministically()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var transaction = await CreateAccountAsync(dbContext, "Txn", AccountType.Transaction);
        var savings = await CreateAccountAsync(dbContext, "Savings", AccountType.Savings);
        var creditCard = await CreateAccountAsync(dbContext, "Card", AccountType.CreditCard);
        var investment = await CreateAccountAsync(dbContext, "Invest", AccountType.Investment);
        var other = await CreateAccountAsync(dbContext, "Other", AccountType.Other);
        var mortgage = await CreateAccountAsync(dbContext, "Home Loan", AccountType.Mortgage);

        dbContext.Transactions.AddRange(
            CreateTransaction(transaction.Id, new DateOnly(2026, 4, 1), 1200m, 1200m, new DateTime(2026, 4, 1, 9, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(savings.Id, new DateOnly(2026, 4, 1), 800m, 800m, new DateTime(2026, 4, 1, 9, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(creditCard.Id, new DateOnly(2026, 4, 1), -300m, -300m, new DateTime(2026, 4, 1, 9, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(investment.Id, new DateOnly(2026, 4, 1), 250m, 250m, new DateTime(2026, 4, 1, 9, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(other.Id, new DateOnly(2026, 4, 1), 60m, 60m, new DateTime(2026, 4, 1, 9, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(mortgage.Id, new DateOnly(2026, 4, 1), -3200m, -500000m, new DateTime(2026, 4, 1, 9, 0, 0, DateTimeKind.Utc)));

        // Create a mortgage configuration linked to the mortgage account
        dbContext.Mortgages.Add(new Mortgage
        {
            Id = Guid.NewGuid(),
            AccountId = mortgage.Id,
            AnnualInterestRate = 0.06m,
            ScheduledRepayment = 3000m,
            RepaymentsPerYear = 12
        });

        await dbContext.SaveChangesAsync();

        var service = CreateDashboardService(dbContext);
        var summary = await service.GetSummaryAsync(CancellationToken.None);

        Assert.Equal(2000m, summary.CurrentCashPosition);
        Assert.Equal(300m, summary.CurrentCreditCardPosition);
        Assert.Equal(-500000m, summary.CurrentMortgagePosition);
        Assert.Equal(-498110m, summary.CurrentNetPosition);
    }

    [Fact]
    public async Task GetAccountSummariesAsync_TransactionAccount_ReturnsCorrectPosition()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var account = await CreateAccountAsync(dbContext, "Checking", AccountType.Transaction);

        dbContext.Transactions.AddRange(
            CreateTransaction(account.Id, new DateOnly(2026, 1, 15), 1000m, 1000m, new DateTime(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(account.Id, new DateOnly(2026, 1, 20), -200m, 800m, new DateTime(2026, 1, 20, 9, 0, 0, DateTimeKind.Utc)));

        await dbContext.SaveChangesAsync();

        var service = CreateDashboardService(dbContext);
        var summaries = await service.GetAccountSummariesAsync(CancellationToken.None);

        Assert.Single(summaries);
        var summary = summaries[0];
        Assert.Equal(account.Id, summary.AccountId);
        Assert.Equal("Checking", summary.AccountName);
        Assert.Equal("Transaction", summary.AccountType);
        Assert.Equal(800m, summary.CurrentPosition);
        Assert.False(summary.IsLiability);
        Assert.Equal(new DateOnly(2026, 1, 20), summary.LastTransactionDate);
        Assert.Equal(2, summary.TransactionCount);
    }

    [Fact]
    public async Task GetAccountSummariesAsync_SavingsAccount_ReturnsCurrentBankBalance()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var account = await CreateAccountAsync(dbContext, "Savings", AccountType.Savings);

        dbContext.Transactions.AddRange(
            CreateTransaction(account.Id, new DateOnly(2026, 1, 1), 5000m, 5000m, new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(account.Id, new DateOnly(2026, 2, 1), 200m, 5200m, new DateTime(2026, 2, 1, 9, 0, 0, DateTimeKind.Utc)));

        await dbContext.SaveChangesAsync();

        var service = CreateDashboardService(dbContext);
        var summaries = await service.GetAccountSummariesAsync(CancellationToken.None);

        var summary = summaries[0];
        Assert.Equal(5200m, summary.CurrentPosition);
        Assert.False(summary.IsLiability);
    }

    [Fact]
    public async Task GetAccountSummariesAsync_CreditCard_ReturnsPositiveOwing()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var account = await CreateAccountAsync(dbContext, "Visa", AccountType.CreditCard);

        dbContext.Transactions.AddRange(
            CreateTransaction(account.Id, new DateOnly(2026, 1, 10), -150m, 0m, new DateTime(2026, 1, 10, 9, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(account.Id, new DateOnly(2026, 2, 5), -75m, 0m, new DateTime(2026, 2, 5, 9, 0, 0, DateTimeKind.Utc)));

        await dbContext.SaveChangesAsync();

        var service = CreateDashboardService(dbContext);
        var summaries = await service.GetAccountSummariesAsync(CancellationToken.None);

        var summary = summaries[0];
        // SUM(-150, -75) = -225, ABS = 225
        Assert.Equal(225m, summary.CurrentPosition);
        Assert.True(summary.IsLiability);
    }

    [Fact]
    public async Task GetAccountSummariesAsync_Mortgage_ReturnsPositiveOutstandingPrincipal()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var account = await CreateAccountAsync(dbContext, "Home Loan", AccountType.Mortgage);

        dbContext.Transactions.AddRange(
            CreateTransaction(account.Id, new DateOnly(2026, 1, 1), -3000m, -500000m, new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(account.Id, new DateOnly(2026, 2, 1), -3000m, -497000m, new DateTime(2026, 2, 1, 9, 0, 0, DateTimeKind.Utc)));

        await dbContext.SaveChangesAsync();

        var service = CreateDashboardService(dbContext);
        var summaries = await service.GetAccountSummariesAsync(CancellationToken.None);

        var summary = summaries[0];
        // Latest BankBalance is -497000, ABS = 497000
        Assert.Equal(497000m, summary.CurrentPosition);
        Assert.True(summary.IsLiability);
    }

    [Fact]
    public async Task GetAccountSummariesAsync_PreviousMonthCalculations_UsesCalendarMonth()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var account = await CreateAccountAsync(dbContext, "Test", AccountType.Transaction);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var firstDayOfCurrentMonth = new DateOnly(today.Year, today.Month, 1);
        var lastDayOfPreviousMonth = firstDayOfCurrentMonth.AddDays(-1);
        var firstDayOfPreviousMonth = new DateOnly(lastDayOfPreviousMonth.Year, lastDayOfPreviousMonth.Month, 1);

        dbContext.Transactions.AddRange(
            // Previous month transactions
            CreateTransaction(account.Id, firstDayOfPreviousMonth, 1000m, 1000m, DateTime.UtcNow),
            CreateTransaction(account.Id, firstDayOfPreviousMonth.AddDays(5), -200m, 800m, DateTime.UtcNow),
            CreateTransaction(account.Id, lastDayOfPreviousMonth, -50m, 750m, DateTime.UtcNow),
            // Current month transactions (should not be included)
            CreateTransaction(account.Id, firstDayOfCurrentMonth, 500m, 1250m, DateTime.UtcNow));

        await dbContext.SaveChangesAsync();

        var service = CreateDashboardService(dbContext);
        var summaries = await service.GetAccountSummariesAsync(CancellationToken.None);

        var summary = summaries[0];
        // Previous month: 1000 - 200 - 50 = 750
        Assert.Equal(750m, summary.PreviousMonthNet);
        Assert.Equal(1000m, summary.PreviousMonthIncome);
        Assert.Equal(250m, summary.PreviousMonthExpenses);
        Assert.Equal(3, summary.PreviousMonthTransactionCount);
    }

    [Fact]
    public async Task GetAccountSummariesAsync_MultipleAccounts_ReturnsSortedByName()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var zAccount = await CreateAccountAsync(dbContext, "Zebra Savings", AccountType.Savings);
        var aAccount = await CreateAccountAsync(dbContext, "Apple Checking", AccountType.Transaction);
        var mAccount = await CreateAccountAsync(dbContext, "Middle Card", AccountType.CreditCard);

        dbContext.Transactions.AddRange(
            CreateTransaction(aAccount.Id, new DateOnly(2026, 1, 1), 100m, 100m, DateTime.UtcNow),
            CreateTransaction(mAccount.Id, new DateOnly(2026, 1, 1), -50m, 0m, DateTime.UtcNow),
            CreateTransaction(zAccount.Id, new DateOnly(2026, 1, 1), 1000m, 1000m, DateTime.UtcNow));

        await dbContext.SaveChangesAsync();

        var service = CreateDashboardService(dbContext);
        var summaries = await service.GetAccountSummariesAsync(CancellationToken.None);

        Assert.Equal(3, summaries.Count);
        Assert.Equal("Apple Checking", summaries[0].AccountName);
        Assert.Equal("Middle Card", summaries[1].AccountName);
        Assert.Equal("Zebra Savings", summaries[2].AccountName);
    }

    [Fact]
    public async Task GetAccountSummariesAsync_NoTransactions_ReturnsZeroValues()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var account = await CreateAccountAsync(dbContext, "Unused", AccountType.Transaction);

        await dbContext.SaveChangesAsync();

        var service = CreateDashboardService(dbContext);
        var summaries = await service.GetAccountSummariesAsync(CancellationToken.None);

        var summary = summaries[0];
        Assert.Equal(0m, summary.CurrentPosition);
        Assert.Equal(0m, summary.PreviousMonthIncome);
        Assert.Equal(0m, summary.PreviousMonthExpenses);
        Assert.Equal(0m, summary.PreviousMonthNet);
        Assert.Null(summary.LastTransactionDate);
        Assert.Equal(0, summary.TransactionCount);
    }

    [Fact]
    public async Task GetAccountSummariesAsync_InactiveAccounts_NotIncluded()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var activeAccount = await CreateAccountAsync(dbContext, "Active", AccountType.Transaction);
        var inactiveAccount = new Account
        {
            Id = Guid.NewGuid(),
            Name = "Inactive",
            Type = AccountType.Savings,
            IsActive = false
        };
        dbContext.Accounts.Add(inactiveAccount);

        dbContext.Transactions.Add(CreateTransaction(activeAccount.Id, new DateOnly(2026, 1, 1), 100m, 100m, DateTime.UtcNow));
        dbContext.Transactions.Add(CreateTransaction(inactiveAccount.Id, new DateOnly(2026, 1, 1), 200m, 200m, DateTime.UtcNow));

        await dbContext.SaveChangesAsync();

        var service = CreateDashboardService(dbContext);
        var summaries = await service.GetAccountSummariesAsync(CancellationToken.None);

        Assert.Single(summaries);
        Assert.Equal(activeAccount.Id, summaries[0].AccountId);
    }

    [Fact]
    public async Task GetAccountSummariesAsync_NoAccounts_ReturnsEmptyList()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var service = CreateDashboardService(dbContext);
        var summaries = await service.GetAccountSummariesAsync(CancellationToken.None);

        Assert.Empty(summaries);
    }

    private static SqliteConnection CreateOpenSqliteConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    private static FinancesDashboardDbContext CreateDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<FinancesDashboardDbContext>()
            .UseSqlite(connection)
            .Options;

        return new FinancesDashboardDbContext(options);
    }

    private static async Task<Account> CreateAccountAsync(FinancesDashboardDbContext dbContext, string name, AccountType type)
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = type,
            IsActive = true
        };

        dbContext.Accounts.Add(account);
        await dbContext.SaveChangesAsync();

        return account;
    }

    private static Transaction CreateTransaction(
        Guid accountId,
        DateOnly date,
        decimal amount,
        decimal bankBalance,
        DateTime importedUtc)
    {
        return new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Date = date,
            Amount = amount,
            Description = "Seeded",
            BankBalance = bankBalance,
            ImportHash = Guid.NewGuid().ToString("N"),
            ImportedUtc = importedUtc
        };
    }

    private static DashboardApplicationService CreateDashboardService(FinancesDashboardDbContext dbContext)
    {
        var options = new ForecastOptions
        {
            LookbackMonths = 6,
            MaxHorizonMonths = 36
        };

        var strategy = new AverageMonthlyNetCashflowForecastStrategy(options);
        var creditCardPositionService = new CreditCardPositionService(dbContext);
        return new DashboardApplicationService(dbContext, strategy, options, creditCardPositionService);
    }

    [Fact]
    public async Task GetAccountSummariesAsync_CreditCard_NoBankBalance_WithNegativeTransactions_CalculatesExpenses()
    {
        // Test 1: Credit card with no BankBalance has negative transactions -> expenses calculated correctly
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var creditCard = await CreateAccountAsync(dbContext, "CreditCard-NoBankBalance", AccountType.CreditCard);

        // Calculate previous month relative to today
        var today = DateOnly.FromDateTime(DateTime.Today);
        var firstDayOfCurrentMonth = new DateOnly(today.Year, today.Month, 1);
        var lastDayOfPreviousMonth = firstDayOfCurrentMonth.AddDays(-1);
        var firstDayOfPreviousMonth = new DateOnly(lastDayOfPreviousMonth.Year, lastDayOfPreviousMonth.Month, 1);
        var midOfPreviousMonth = firstDayOfPreviousMonth.AddDays(4);
        var anotherDayInPreviousMonth = firstDayOfPreviousMonth.AddDays(9);
        var lastDayInPreviousMonth = firstDayOfPreviousMonth.AddDays(14);

        dbContext.Transactions.AddRange(
            CreateTransaction(creditCard.Id, midOfPreviousMonth, -120m, 0m, midOfPreviousMonth.ToDateTime(TimeOnly.MinValue).AddHours(9).ToUniversalTime()),
            CreateTransaction(creditCard.Id, anotherDayInPreviousMonth, -80m, 0m, anotherDayInPreviousMonth.ToDateTime(TimeOnly.MinValue).AddHours(9).ToUniversalTime()),
            CreateTransaction(creditCard.Id, lastDayInPreviousMonth, -50m, 0m, lastDayInPreviousMonth.ToDateTime(TimeOnly.MinValue).AddHours(9).ToUniversalTime()));

        await dbContext.SaveChangesAsync();

        var service = CreateDashboardService(dbContext);
        var summaries = await service.GetAccountSummariesAsync(CancellationToken.None);

        Assert.Single(summaries);
        var summary = summaries[0];
        Assert.Equal(0m, summary.PreviousMonthIncome);
        Assert.Equal(250m, summary.PreviousMonthExpenses);  // 120 + 80 + 50
        Assert.Equal(-250m, summary.PreviousMonthNet);  // Income - Expenses = 0 - 250
    }

    [Fact]
    public async Task GetAccountSummariesAsync_CreditCard_MixedTransactions_CalculatesCorrectly()
    {
        // Test 2: Credit card with positive and negative transactions
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var creditCard = await CreateAccountAsync(dbContext, "CreditCard-Mixed", AccountType.CreditCard);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var firstDayOfCurrentMonth = new DateOnly(today.Year, today.Month, 1);
        var lastDayOfPreviousMonth = firstDayOfCurrentMonth.AddDays(-1);
        var firstDayOfPreviousMonth = new DateOnly(lastDayOfPreviousMonth.Year, lastDayOfPreviousMonth.Month, 1);

        dbContext.Transactions.AddRange(
            CreateTransaction(creditCard.Id, firstDayOfPreviousMonth, 500m, 0m, firstDayOfPreviousMonth.ToDateTime(TimeOnly.MinValue).AddHours(9).ToUniversalTime()),      // Payment/refund
            CreateTransaction(creditCard.Id, firstDayOfPreviousMonth.AddDays(4), -120m, 0m, firstDayOfPreviousMonth.AddDays(4).ToDateTime(TimeOnly.MinValue).AddHours(9).ToUniversalTime()),    // Supermarket
            CreateTransaction(creditCard.Id, firstDayOfPreviousMonth.AddDays(9), -80m, 0m, firstDayOfPreviousMonth.AddDays(9).ToDateTime(TimeOnly.MinValue).AddHours(9).ToUniversalTime()),   // Fuel
            CreateTransaction(creditCard.Id, firstDayOfPreviousMonth.AddDays(14), -50m, 0m, firstDayOfPreviousMonth.AddDays(14).ToDateTime(TimeOnly.MinValue).AddHours(9).ToUniversalTime()));  // Restaurant

        await dbContext.SaveChangesAsync();

        var service = CreateDashboardService(dbContext);
        var summaries = await service.GetAccountSummariesAsync(CancellationToken.None);

        Assert.Single(summaries);
        var summary = summaries[0];
        Assert.Equal(500m, summary.PreviousMonthIncome);
        Assert.Equal(250m, summary.PreviousMonthExpenses);
        Assert.Equal(250m, summary.PreviousMonthNet);  // 500 - 250
    }

    [Fact]
    public async Task GetAccountSummariesAsync_CreditCard_BankBalanceZero_WithTransactions_CalculatesActivity()
    {
        // Test 3: Credit card with BankBalance = 0 still returns activity
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var creditCard = await CreateAccountAsync(dbContext, "CreditCard-ZeroBalance", AccountType.CreditCard);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var firstDayOfCurrentMonth = new DateOnly(today.Year, today.Month, 1);
        var lastDayOfPreviousMonth = firstDayOfCurrentMonth.AddDays(-1);
        var firstDayOfPreviousMonth = new DateOnly(lastDayOfPreviousMonth.Year, lastDayOfPreviousMonth.Month, 1);

        dbContext.Transactions.AddRange(
            CreateTransaction(creditCard.Id, firstDayOfPreviousMonth.AddDays(2), -75m, 0m, firstDayOfPreviousMonth.AddDays(2).ToDateTime(TimeOnly.MinValue).AddHours(9).ToUniversalTime()),
            CreateTransaction(creditCard.Id, firstDayOfPreviousMonth.AddDays(7), -45m, 0m, firstDayOfPreviousMonth.AddDays(7).ToDateTime(TimeOnly.MinValue).AddHours(9).ToUniversalTime()));

        await dbContext.SaveChangesAsync();

        var service = CreateDashboardService(dbContext);
        var summaries = await service.GetAccountSummariesAsync(CancellationToken.None);

        Assert.Single(summaries);
        var summary = summaries[0];
        Assert.Equal(0m, summary.PreviousMonthIncome);
        Assert.Equal(120m, summary.PreviousMonthExpenses);  // 75 + 45
        Assert.Equal(-120m, summary.PreviousMonthNet);
        Assert.Equal(2, summary.PreviousMonthTransactionCount);
    }

    [Fact]
    public async Task GetAccountSummariesAsync_CreditCard_NoTransactions_ReturnsZeroActivity()
    {
        // Test 4: Credit card with no transactions returns zero activity
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var creditCard = await CreateAccountAsync(dbContext, "CreditCard-Empty", AccountType.CreditCard);

        await dbContext.SaveChangesAsync();

        var service = CreateDashboardService(dbContext);
        var summaries = await service.GetAccountSummariesAsync(CancellationToken.None);

        Assert.Single(summaries);
        var summary = summaries[0];
        Assert.Equal(0m, summary.PreviousMonthIncome);
        Assert.Equal(0m, summary.PreviousMonthExpenses);
        Assert.Equal(0m, summary.PreviousMonthNet);
        Assert.Equal(0, summary.PreviousMonthTransactionCount);
    }

    [Fact]
    public async Task GetAccountSummariesAsync_PreviousMonth_ExcludesTransactionsOutsideRange()
    {
        // Test 5 & 6: Previous-month filtering excludes transactions outside the month
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var creditCard = await CreateAccountAsync(dbContext, "CreditCard-DateFilter", AccountType.CreditCard);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var firstDayOfCurrentMonth = new DateOnly(today.Year, today.Month, 1);
        var lastDayOfPreviousMonth = firstDayOfCurrentMonth.AddDays(-1);
        var firstDayOfPreviousMonth = new DateOnly(lastDayOfPreviousMonth.Year, lastDayOfPreviousMonth.Month, 1);
        var dayBeforePreviousMonth = firstDayOfPreviousMonth.AddDays(-1);

        // Transactions in and out of previous month
        dbContext.Transactions.AddRange(
            CreateTransaction(creditCard.Id, dayBeforePreviousMonth, -100m, 0m, dayBeforePreviousMonth.ToDateTime(TimeOnly.MinValue).AddHours(9).ToUniversalTime()),  // Previous previous month
            CreateTransaction(creditCard.Id, firstDayOfPreviousMonth, -50m, 0m, firstDayOfPreviousMonth.ToDateTime(TimeOnly.MinValue).AddHours(9).ToUniversalTime()),     // Previous month start
            CreateTransaction(creditCard.Id, firstDayOfPreviousMonth.AddDays(14), -75m, 0m, firstDayOfPreviousMonth.AddDays(14).ToDateTime(TimeOnly.MinValue).AddHours(9).ToUniversalTime()),   // Previous month middle
            CreateTransaction(creditCard.Id, lastDayOfPreviousMonth, -25m, 0m, lastDayOfPreviousMonth.ToDateTime(TimeOnly.MinValue).AddHours(9).ToUniversalTime()),   // Previous month end
            CreateTransaction(creditCard.Id, firstDayOfCurrentMonth, -200m, 0m, firstDayOfCurrentMonth.ToDateTime(TimeOnly.MinValue).AddHours(9).ToUniversalTime()));   // Current month

        await dbContext.SaveChangesAsync();

        var service = CreateDashboardService(dbContext);
        var summaries = await service.GetAccountSummariesAsync(CancellationToken.None);

        Assert.Single(summaries);
        var summary = summaries[0];
        // Should only include previous month transactions: 50 + 75 + 25 = 150
        Assert.Equal(0m, summary.PreviousMonthIncome);
        Assert.Equal(150m, summary.PreviousMonthExpenses);
        Assert.Equal(-150m, summary.PreviousMonthNet);
        Assert.Equal(3, summary.PreviousMonthTransactionCount);
    }

    [Fact]
    public async Task GetAccountSummariesAsync_AllAccountTypes_ContinueWorking()
    {
        // Test 7: Normal transaction/savings accounts continue to work correctly
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var transaction = await CreateAccountAsync(dbContext, "Checking", AccountType.Transaction);
        var savings = await CreateAccountAsync(dbContext, "Savings", AccountType.Savings);
        var creditCard = await CreateAccountAsync(dbContext, "Visa", AccountType.CreditCard);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var firstDayOfCurrentMonth = new DateOnly(today.Year, today.Month, 1);
        var lastDayOfPreviousMonth = firstDayOfCurrentMonth.AddDays(-1);
        var firstDayOfPreviousMonth = new DateOnly(lastDayOfPreviousMonth.Year, lastDayOfPreviousMonth.Month, 1);

        dbContext.Transactions.AddRange(
            // Transaction account: previous month activity
            CreateTransaction(transaction.Id, firstDayOfPreviousMonth.AddDays(4), 1000m, 5000m, firstDayOfPreviousMonth.AddDays(4).ToDateTime(TimeOnly.MinValue).AddHours(9).ToUniversalTime()),
            CreateTransaction(transaction.Id, firstDayOfPreviousMonth.AddDays(9), -200m, 4800m, firstDayOfPreviousMonth.AddDays(9).ToDateTime(TimeOnly.MinValue).AddHours(9).ToUniversalTime()),
            // Savings account: previous month activity
            CreateTransaction(savings.Id, firstDayOfPreviousMonth, 500m, 10500m, firstDayOfPreviousMonth.ToDateTime(TimeOnly.MinValue).AddHours(9).ToUniversalTime()),
            CreateTransaction(savings.Id, firstDayOfPreviousMonth.AddDays(14), -100m, 10400m, firstDayOfPreviousMonth.AddDays(14).ToDateTime(TimeOnly.MinValue).AddHours(9).ToUniversalTime()),
            // Credit card: previous month activity (zero BankBalance is valid for CC)
            CreateTransaction(creditCard.Id, firstDayOfPreviousMonth.AddDays(2), -300m, 0m, firstDayOfPreviousMonth.AddDays(2).ToDateTime(TimeOnly.MinValue).AddHours(9).ToUniversalTime()),
            CreateTransaction(creditCard.Id, firstDayOfPreviousMonth.AddDays(19), 100m, 0m, firstDayOfPreviousMonth.AddDays(19).ToDateTime(TimeOnly.MinValue).AddHours(9).ToUniversalTime()));

        await dbContext.SaveChangesAsync();

        var service = CreateDashboardService(dbContext);
        var summaries = await service.GetAccountSummariesAsync(CancellationToken.None);

        Assert.Equal(3, summaries.Count);

        // Transaction account
        var txSummary = summaries.First(s => s.AccountType == "Transaction");
        Assert.Equal(1000m, txSummary.PreviousMonthIncome);
        Assert.Equal(200m, txSummary.PreviousMonthExpenses);
        Assert.Equal(800m, txSummary.PreviousMonthNet);

        // Savings account
        var savingSummary = summaries.First(s => s.AccountType == "Savings");
        Assert.Equal(500m, savingSummary.PreviousMonthIncome);
        Assert.Equal(100m, savingSummary.PreviousMonthExpenses);
        Assert.Equal(400m, savingSummary.PreviousMonthNet);

        // Credit card
        var ccSummary = summaries.First(s => s.AccountType == "CreditCard");
        Assert.Equal(100m, ccSummary.PreviousMonthIncome);
        Assert.Equal(300m, ccSummary.PreviousMonthExpenses);
        Assert.Equal(-200m, ccSummary.PreviousMonthNet);
    }
}
