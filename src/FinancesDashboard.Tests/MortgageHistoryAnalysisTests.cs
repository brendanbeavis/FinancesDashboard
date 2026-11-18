using FinancesDashboard.Application.Dto;
using FinancesDashboard.Application.Services;
using FinancesDashboard.Domain.Enums;
using FinancesDashboard.Domain.Models;
using FinancesDashboard.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FinancesDashboard.Tests;

public sealed class MortgageHistoryAnalysisTests
{
    [Fact]
    public async Task GetHistoryAsync_WithNormalHistory_ReturnsTransactionsSortedByDate()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var accountId = Guid.NewGuid();
        var account = new Account { Id = accountId, Name = "Home Loan", Type = AccountType.Mortgage, IsActive = true };
        dbContext.Accounts.Add(account);

        dbContext.Transactions.AddRange(
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, Date = new DateOnly(2026, 1, 1), Amount = -3200m, Description = "Payment", BankBalance = 447000m, ImportHash = "hash1", ImportedUtc = DateTime.UtcNow },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, Date = new DateOnly(2026, 2, 1), Amount = -3200m, Description = "Payment", BankBalance = 443800m, ImportHash = "hash2", ImportedUtc = DateTime.UtcNow },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, Date = new DateOnly(2026, 3, 1), Amount = -3200m, Description = "Payment", BankBalance = 440600m, ImportHash = "hash3", ImportedUtc = DateTime.UtcNow });

        await dbContext.SaveChangesAsync();

        var service = new MortgageHistoryAnalysisService(dbContext);
        var history = await service.GetHistoryAsync(accountId, cancellationToken: CancellationToken.None);

        Assert.Equal(3, history.Count);
        Assert.Equal(new DateOnly(2026, 1, 1), history[0].Date);
        Assert.Equal(new DateOnly(2026, 2, 1), history[1].Date);
        Assert.Equal(new DateOnly(2026, 3, 1), history[2].Date);
        Assert.Equal(447000m, history[0].Balance);
        Assert.Equal(443800m, history[1].Balance);
        Assert.Equal(440600m, history[2].Balance);
        Assert.All(history, h => Assert.Null(h.InterestAmount));
        Assert.All(history, h => Assert.Null(h.PrincipalAmount));
    }

    [Fact]
    public async Task GetHistoryAsync_WithDateRange_FiltersCorrectly()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var accountId = Guid.NewGuid();
        var account = new Account { Id = accountId, Name = "Home Loan", Type = AccountType.Mortgage, IsActive = true };
        dbContext.Accounts.Add(account);

        dbContext.Transactions.AddRange(
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, Date = new DateOnly(2026, 1, 1), Amount = -3200m, Description = "Payment", BankBalance = 447000m, ImportHash = "hash1", ImportedUtc = DateTime.UtcNow },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, Date = new DateOnly(2026, 2, 1), Amount = -3200m, Description = "Payment", BankBalance = 443800m, ImportHash = "hash2", ImportedUtc = DateTime.UtcNow },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, Date = new DateOnly(2026, 3, 1), Amount = -3200m, Description = "Payment", BankBalance = 440600m, ImportHash = "hash3", ImportedUtc = DateTime.UtcNow },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, Date = new DateOnly(2026, 4, 1), Amount = -3200m, Description = "Payment", BankBalance = 437400m, ImportHash = "hash4", ImportedUtc = DateTime.UtcNow });

        await dbContext.SaveChangesAsync();

        var service = new MortgageHistoryAnalysisService(dbContext);
        var history = await service.GetHistoryAsync(
            accountId,
            new DateOnly(2026, 2, 1),
            new DateOnly(2026, 3, 1),
            CancellationToken.None);

        Assert.Equal(2, history.Count);
        Assert.Equal(new DateOnly(2026, 2, 1), history[0].Date);
        Assert.Equal(new DateOnly(2026, 3, 1), history[1].Date);
    }

    [Fact]
    public async Task GetHistoryAsync_WithEmptyHistory_ReturnsEmptyList()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var accountId = Guid.NewGuid();
        var service = new MortgageHistoryAnalysisService(dbContext);
        var history = await service.GetHistoryAsync(accountId, cancellationToken: CancellationToken.None);

        Assert.Empty(history);
    }

    [Fact]
    public async Task GetHistoryAsync_WithSingleTransaction_ReturnsSinglePoint()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var accountId = Guid.NewGuid();
        var account = new Account { Id = accountId, Name = "Home Loan", Type = AccountType.Mortgage, IsActive = true };
        dbContext.Accounts.Add(account);

        dbContext.Transactions.Add(
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, Date = new DateOnly(2026, 1, 1), Amount = -3200m, Description = "Payment", BankBalance = 447000m, ImportHash = "hash1", ImportedUtc = DateTime.UtcNow });

        await dbContext.SaveChangesAsync();

        var service = new MortgageHistoryAnalysisService(dbContext);
        var history = await service.GetHistoryAsync(accountId, cancellationToken: CancellationToken.None);

        Assert.Single(history);
        Assert.Equal(new DateOnly(2026, 1, 1), history[0].Date);
        Assert.Equal(447000m, history[0].Balance);
        Assert.Equal(-3200m, history[0].PaymentAmount);
    }

    [Fact]
    public async Task GetHistoryAsync_WithIrregularDates_MaintainsOrder()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var accountId = Guid.NewGuid();
        var account = new Account { Id = accountId, Name = "Home Loan", Type = AccountType.Mortgage, IsActive = true };
        dbContext.Accounts.Add(account);

        dbContext.Transactions.AddRange(
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, Date = new DateOnly(2026, 3, 15), Amount = -3200m, Description = "Payment", BankBalance = 440600m, ImportHash = "hash1", ImportedUtc = DateTime.UtcNow },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, Date = new DateOnly(2026, 1, 10), Amount = -3200m, Description = "Payment", BankBalance = 447000m, ImportHash = "hash2", ImportedUtc = DateTime.UtcNow },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, Date = new DateOnly(2026, 2, 20), Amount = -3200m, Description = "Payment", BankBalance = 443800m, ImportHash = "hash3", ImportedUtc = DateTime.UtcNow });

        await dbContext.SaveChangesAsync();

        var service = new MortgageHistoryAnalysisService(dbContext);
        var history = await service.GetHistoryAsync(accountId, cancellationToken: CancellationToken.None);

        Assert.Equal(3, history.Count);
        Assert.Equal(new DateOnly(2026, 1, 10), history[0].Date);
        Assert.Equal(new DateOnly(2026, 2, 20), history[1].Date);
        Assert.Equal(new DateOnly(2026, 3, 15), history[2].Date);
    }

    [Fact]
    public async Task GetHistoryAsync_WithNegativeRepayments_IncludesNegativeAmounts()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var accountId = Guid.NewGuid();
        var account = new Account { Id = accountId, Name = "Home Loan", Type = AccountType.Mortgage, IsActive = true };
        dbContext.Accounts.Add(account);

        dbContext.Transactions.AddRange(
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, Date = new DateOnly(2026, 1, 1), Amount = 500m, Description = "Deposit", BankBalance = 450500m, ImportHash = "hash1", ImportedUtc = DateTime.UtcNow },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, Date = new DateOnly(2026, 1, 2), Amount = -3200m, Description = "Payment", BankBalance = 447300m, ImportHash = "hash2", ImportedUtc = DateTime.UtcNow });

        await dbContext.SaveChangesAsync();

        var service = new MortgageHistoryAnalysisService(dbContext);
        var history = await service.GetHistoryAsync(accountId, cancellationToken: CancellationToken.None);

        Assert.Equal(2, history.Count);
        Assert.Equal(500m, history[0].PaymentAmount);
        Assert.Equal(-3200m, history[1].PaymentAmount);
    }

    [Fact]
    public async Task GetHistoryAsync_WithNegativeMortgageBalances_ReturnsPositiveOutstandingPrincipal()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var accountId = Guid.NewGuid();
        var account = new Account { Id = accountId, Name = "Home Loan", Type = AccountType.Mortgage, IsActive = true };
        dbContext.Accounts.Add(account);

        dbContext.Transactions.AddRange(
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, Date = new DateOnly(2026, 1, 1), Amount = -3200m, Description = "Payment", BankBalance = -500000m, ImportHash = "hash1", ImportedUtc = DateTime.UtcNow },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, Date = new DateOnly(2026, 2, 1), Amount = -3200m, Description = "Payment", BankBalance = -498000m, ImportHash = "hash2", ImportedUtc = DateTime.UtcNow });

        await dbContext.SaveChangesAsync();

        var service = new MortgageHistoryAnalysisService(dbContext);
        var history = await service.GetHistoryAsync(accountId, cancellationToken: CancellationToken.None);

        Assert.Equal(2, history.Count);
        Assert.Equal(500000m, history[0].Balance);
        Assert.Equal(498000m, history[1].Balance);
    }

    [Fact]
    public async Task GetHistoryAsync_WithNegativeMortgageBalances_ShowsDebtReducingTowardZero()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var accountId = Guid.NewGuid();
        var account = new Account { Id = accountId, Name = "Home Loan", Type = AccountType.Mortgage, IsActive = true };
        dbContext.Accounts.Add(account);

        dbContext.Transactions.AddRange(
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, Date = new DateOnly(2026, 1, 1), Amount = -3200m, Description = "Payment", BankBalance = -500000m, ImportHash = "hash1", ImportedUtc = DateTime.UtcNow },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, Date = new DateOnly(2026, 2, 1), Amount = -3200m, Description = "Payment", BankBalance = -498000m, ImportHash = "hash2", ImportedUtc = DateTime.UtcNow });

        await dbContext.SaveChangesAsync();

        var service = new MortgageHistoryAnalysisService(dbContext);
        var balanceChange = await service.GetBalanceChangeAsync(accountId, cancellationToken: CancellationToken.None);

        Assert.Equal(2000m, balanceChange);
    }

    [Fact]
    public async Task GetCurrentBalanceAsync_ReturnsLatestBankBalance()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var accountId = Guid.NewGuid();
        var account = new Account { Id = accountId, Name = "Home Loan", Type = AccountType.Mortgage, IsActive = true };
        dbContext.Accounts.Add(account);

        dbContext.Transactions.AddRange(
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, Date = new DateOnly(2026, 1, 1), Amount = -3200m, Description = "Payment", BankBalance = 447000m, ImportHash = "hash1", ImportedUtc = DateTime.UtcNow },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, Date = new DateOnly(2026, 2, 1), Amount = -3200m, Description = "Payment", BankBalance = 443800m, ImportHash = "hash2", ImportedUtc = DateTime.UtcNow });

        await dbContext.SaveChangesAsync();

        var service = new MortgageHistoryAnalysisService(dbContext);
        var balance = await service.GetCurrentBalanceAsync(accountId, CancellationToken.None);

        Assert.Equal(443800m, balance);
    }

    [Fact]
    public async Task GetCurrentBalanceAsync_WithEmptyHistory_ReturnsZero()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var accountId = Guid.NewGuid();
        var service = new MortgageHistoryAnalysisService(dbContext);
        var balance = await service.GetCurrentBalanceAsync(accountId, CancellationToken.None);

        Assert.Equal(0m, balance);
    }

    [Fact]
    public async Task GetLatestDateAsync_ReturnsLatestTransactionDate()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var accountId = Guid.NewGuid();
        var account = new Account { Id = accountId, Name = "Home Loan", Type = AccountType.Mortgage, IsActive = true };
        dbContext.Accounts.Add(account);

        dbContext.Transactions.AddRange(
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, Date = new DateOnly(2026, 1, 1), Amount = -3200m, Description = "Payment", BankBalance = 447000m, ImportHash = "hash1", ImportedUtc = DateTime.UtcNow },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, Date = new DateOnly(2026, 3, 1), Amount = -3200m, Description = "Payment", BankBalance = 440600m, ImportHash = "hash2", ImportedUtc = DateTime.UtcNow });

        await dbContext.SaveChangesAsync();

        var service = new MortgageHistoryAnalysisService(dbContext);
        var latestDate = await service.GetLatestDateAsync(accountId, CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 3, 1), latestDate);
    }

    [Fact]
    public async Task GetLatestDateAsync_WithEmptyHistory_ReturnsNull()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var accountId = Guid.NewGuid();
        var service = new MortgageHistoryAnalysisService(dbContext);
        var latestDate = await service.GetLatestDateAsync(accountId, CancellationToken.None);

        Assert.Null(latestDate);
    }

    [Fact]
    public async Task GetTotalRepaymentsAsync_SumsNegativeTransactions()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var accountId = Guid.NewGuid();
        var account = new Account { Id = accountId, Name = "Home Loan", Type = AccountType.Mortgage, IsActive = true };
        dbContext.Accounts.Add(account);

        dbContext.Transactions.AddRange(
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, Date = new DateOnly(2026, 1, 1), Amount = -3200m, Description = "Payment", BankBalance = 447000m, ImportHash = "hash1", ImportedUtc = DateTime.UtcNow },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, Date = new DateOnly(2026, 2, 1), Amount = 500m, Description = "Deposit", BankBalance = 447500m, ImportHash = "hash2", ImportedUtc = DateTime.UtcNow },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, Date = new DateOnly(2026, 3, 1), Amount = -3200m, Description = "Payment", BankBalance = 444300m, ImportHash = "hash3", ImportedUtc = DateTime.UtcNow });

        await dbContext.SaveChangesAsync();

        var service = new MortgageHistoryAnalysisService(dbContext);
        var totalRepayments = await service.GetTotalRepaymentsAsync(accountId, cancellationToken: CancellationToken.None);

        Assert.Equal(6400m, totalRepayments);
    }

    [Fact]
    public async Task GetTotalRepaymentsAsync_WithDateRange_FiltersCorrectly()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var accountId = Guid.NewGuid();
        var account = new Account { Id = accountId, Name = "Home Loan", Type = AccountType.Mortgage, IsActive = true };
        dbContext.Accounts.Add(account);

        dbContext.Transactions.AddRange(
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, Date = new DateOnly(2026, 1, 1), Amount = -3200m, Description = "Payment", BankBalance = 447000m, ImportHash = "hash1", ImportedUtc = DateTime.UtcNow },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, Date = new DateOnly(2026, 2, 1), Amount = -3200m, Description = "Payment", BankBalance = 443800m, ImportHash = "hash2", ImportedUtc = DateTime.UtcNow },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, Date = new DateOnly(2026, 3, 1), Amount = -3200m, Description = "Payment", BankBalance = 440600m, ImportHash = "hash3", ImportedUtc = DateTime.UtcNow });

        await dbContext.SaveChangesAsync();

        var service = new MortgageHistoryAnalysisService(dbContext);
        var totalRepayments = await service.GetTotalRepaymentsAsync(
            accountId,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 2, 1),
            CancellationToken.None);

        Assert.Equal(6400m, totalRepayments);
    }

    [Fact]
    public async Task GetTotalRepaymentsAsync_WithEmptyHistory_ReturnsZero()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var accountId = Guid.NewGuid();
        var service = new MortgageHistoryAnalysisService(dbContext);
        var totalRepayments = await service.GetTotalRepaymentsAsync(accountId, cancellationToken: CancellationToken.None);

        Assert.Equal(0m, totalRepayments);
    }

    [Fact]
    public async Task GetBalanceChangeAsync_CalculatesDeltaBetweenFirstAndLastBalance()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var accountId = Guid.NewGuid();
        var account = new Account { Id = accountId, Name = "Home Loan", Type = AccountType.Mortgage, IsActive = true };
        dbContext.Accounts.Add(account);

        dbContext.Transactions.AddRange(
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, Date = new DateOnly(2026, 1, 1), Amount = -3200m, Description = "Payment", BankBalance = -500000m, ImportHash = "hash1", ImportedUtc = DateTime.UtcNow },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, Date = new DateOnly(2026, 2, 1), Amount = -3200m, Description = "Payment", BankBalance = -498000m, ImportHash = "hash2", ImportedUtc = DateTime.UtcNow });

        await dbContext.SaveChangesAsync();

        var service = new MortgageHistoryAnalysisService(dbContext);
        var balanceChange = await service.GetBalanceChangeAsync(accountId, cancellationToken: CancellationToken.None);

        Assert.Equal(2000m, balanceChange);
    }

    [Fact]
    public async Task GetBalanceChangeAsync_WithDateRange_CalculatesDeltaInRange()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var accountId = Guid.NewGuid();
        var account = new Account { Id = accountId, Name = "Home Loan", Type = AccountType.Mortgage, IsActive = true };
        dbContext.Accounts.Add(account);

        dbContext.Transactions.AddRange(
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, Date = new DateOnly(2026, 1, 1), Amount = -3200m, Description = "Payment", BankBalance = 447000m, ImportHash = "hash1", ImportedUtc = DateTime.UtcNow },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, Date = new DateOnly(2026, 2, 1), Amount = -3200m, Description = "Payment", BankBalance = 443800m, ImportHash = "hash2", ImportedUtc = DateTime.UtcNow },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, Date = new DateOnly(2026, 3, 1), Amount = -3200m, Description = "Payment", BankBalance = 440600m, ImportHash = "hash3", ImportedUtc = DateTime.UtcNow });

        await dbContext.SaveChangesAsync();

        var service = new MortgageHistoryAnalysisService(dbContext);
        var balanceChange = await service.GetBalanceChangeAsync(
            accountId,
            new DateOnly(2026, 2, 1),
            new DateOnly(2026, 3, 1),
            CancellationToken.None);

        Assert.Equal(3200m, balanceChange);
    }

    [Fact]
    public async Task GetBalanceChangeAsync_WithEmptyHistory_ReturnsZero()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var accountId = Guid.NewGuid();
        var service = new MortgageHistoryAnalysisService(dbContext);
        var balanceChange = await service.GetBalanceChangeAsync(accountId, cancellationToken: CancellationToken.None);

        Assert.Equal(0m, balanceChange);
    }

    [Fact]
    public async Task GetSummaryAsync_ReturnsCorrectStatistics()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var accountId = Guid.NewGuid();
        var account = new Account { Id = accountId, Name = "Home Loan", Type = AccountType.Mortgage, IsActive = true };
        dbContext.Accounts.Add(account);

        dbContext.Transactions.AddRange(
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, Date = new DateOnly(2026, 1, 1), Amount = -3200m, Description = "Payment", BankBalance = -500000m, ImportHash = "hash1", ImportedUtc = DateTime.UtcNow },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, Date = new DateOnly(2026, 2, 1), Amount = -3200m, Description = "Payment", BankBalance = -498000m, ImportHash = "hash2", ImportedUtc = DateTime.UtcNow },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, Date = new DateOnly(2026, 3, 1), Amount = -3200m, Description = "Payment", BankBalance = -496000m, ImportHash = "hash3", ImportedUtc = DateTime.UtcNow });

        await dbContext.SaveChangesAsync();

        var service = new MortgageHistoryAnalysisService(dbContext);
        var (transactionCount, currentBalance, totalRepayments, balanceChange) = await service.GetSummaryAsync(
            accountId,
            cancellationToken: CancellationToken.None);

        Assert.Equal(3, transactionCount);
        Assert.Equal(496000m, currentBalance);
        Assert.Equal(9600m, totalRepayments);
        Assert.Equal(4000m, balanceChange);
    }

    [Fact]
    public async Task GetSummaryAsync_WithEmptyHistory_ReturnsZeros()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var accountId = Guid.NewGuid();
        var service = new MortgageHistoryAnalysisService(dbContext);
        var (transactionCount, currentBalance, totalRepayments, balanceChange) = await service.GetSummaryAsync(
            accountId,
            cancellationToken: CancellationToken.None);

        Assert.Equal(0, transactionCount);
        Assert.Equal(0m, currentBalance);
        Assert.Equal(0m, totalRepayments);
        Assert.Equal(0m, balanceChange);
    }

    [Fact]
    public async Task EstimateAnnualInterestRateAsync_WithOffsetBalance_DerivesOffsetAwareAnnualRate()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var mortgageAccountId = Guid.NewGuid();
        var offsetAccountId = Guid.NewGuid();

        dbContext.Accounts.AddRange(
            new Account { Id = mortgageAccountId, Name = "Home Loan", Type = AccountType.Mortgage, IsActive = true },
            new Account { Id = offsetAccountId, Name = "Offset", Type = AccountType.Transaction, IsActive = true });

        dbContext.Transactions.AddRange(
            new Transaction { Id = Guid.NewGuid(), AccountId = offsetAccountId, Date = new DateOnly(2026, 1, 30), Amount = 0m, Description = "Balance snapshot", BankBalance = 50000m, ImportHash = "offset-1", ImportedUtc = DateTime.UtcNow },
            new Transaction { Id = Guid.NewGuid(), AccountId = mortgageAccountId, Date = new DateOnly(2026, 1, 31), Amount = 1500m, Description = "Interest charge", BankBalance = -501500m, ImportHash = "mortgage-interest-1", ImportedUtc = DateTime.UtcNow });

        await dbContext.SaveChangesAsync();

        var service = new MortgageHistoryAnalysisService(dbContext);
        var estimatedAnnualRate = await service.EstimateAnnualInterestRateAsync(
            mortgageAccountId,
            [offsetAccountId],
            cancellationToken: CancellationToken.None);

        Assert.Equal(0.04m, estimatedAnnualRate);
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
}
