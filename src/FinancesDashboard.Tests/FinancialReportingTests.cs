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

        dbContext.Transactions.AddRange(
            CreateTransaction(transaction.Id, new DateOnly(2026, 4, 1), 1200m, 1200m, new DateTime(2026, 4, 1, 9, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(savings.Id, new DateOnly(2026, 4, 1), 800m, 800m, new DateTime(2026, 4, 1, 9, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(creditCard.Id, new DateOnly(2026, 4, 1), -300m, -300m, new DateTime(2026, 4, 1, 9, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(investment.Id, new DateOnly(2026, 4, 1), 250m, 250m, new DateTime(2026, 4, 1, 9, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(other.Id, new DateOnly(2026, 4, 1), 60m, 60m, new DateTime(2026, 4, 1, 9, 0, 0, DateTimeKind.Utc)));

        dbContext.Mortgages.Add(new Mortgage
        {
            Id = Guid.NewGuid(),
            Name = "Home Loan",
            OriginalPrincipal = 500000m,
            CurrentPrincipal = 500000m,
            AnnualInterestRate = 0.06m,
            ScheduledRepayment = 3000m,
            RepaymentsPerYear = 12,
            StartDate = new DateOnly(2025, 1, 1)
        });

        await dbContext.SaveChangesAsync();

        var service = CreateDashboardService(dbContext);
        var summary = await service.GetSummaryAsync(CancellationToken.None);

        Assert.Equal(2000m, summary.CurrentCashPosition);
        Assert.Equal(-300m, summary.CurrentCreditCardPosition);
        Assert.Equal(-500000m, summary.CurrentMortgagePosition);
        Assert.Equal(-498110m, summary.CurrentNetPosition);
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
        return new DashboardApplicationService(dbContext, strategy, options);
    }
}
