using FinancesDashboard.Application.Services;
using FinancesDashboard.Domain.Enums;
using FinancesDashboard.Domain.Models;
using FinancesDashboard.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FinancesDashboard.Tests;

public sealed class CreditCardPositionServiceTests
{
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

    [Fact]
    public async Task CalculatePositionAsync_NoTransactions_ReturnsOpeningBalance()
    {
        // Arrange
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = "Test Credit Card",
            Type = AccountType.CreditCard,
            IsActive = true
        };
        dbContext.Accounts.Add(account);
        await dbContext.SaveChangesAsync();

        var service = new CreditCardPositionService(dbContext);

        // Act
        var result = await service.CalculatePositionAsync(account.Id, openingBalance: 500m, CancellationToken.None);

        // Assert
        Assert.Equal(500m, result.Position);
        Assert.Equal(500m, result.AbsolutePosition);
        Assert.True(result.IsCredit);  // positive position = credit
        Assert.Equal(0, result.TransactionCount);
        Assert.False(result.HasTransactions);
        Assert.Equal(500m, result.OpeningBalance);
        Assert.True(result.IsOpeningBalanceConfirmed);
        Assert.Null(result.Disclaimer);
    }

    [Fact]
    public async Task CalculatePositionAsync_DefaultOpeningBalance_IncludesDisclaimer()
    {
        // Arrange
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = "Test Credit Card",
            Type = AccountType.CreditCard,
            IsActive = true
        };
        dbContext.Accounts.Add(account);
        await dbContext.SaveChangesAsync();

        var service = new CreditCardPositionService(dbContext);

        // Act - using default opening balance (0)
        var result = await service.CalculatePositionAsync(account.Id, cancellationToken: CancellationToken.None);

        // Assert
        Assert.Equal(0m, result.Position);
        Assert.Equal(0m, result.AbsolutePosition);
        Assert.False(result.IsCredit);  // zero position is not a credit
        Assert.False(result.IsOpeningBalanceConfirmed);
        Assert.NotNull(result.Disclaimer);
        Assert.Contains("opening balance assumed to be $0", result.Disclaimer);
    }

    [Fact]
    public async Task CalculatePositionAsync_SinglePurchaseTransaction_CalculatesOwing()
    {
        // Arrange
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = "Test Credit Card",
            Type = AccountType.CreditCard,
            IsActive = true
        };
        dbContext.Accounts.Add(account);
        await dbContext.SaveChangesAsync();

        // Purchase (negative amount)
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = account.Id,
            Date = DateOnly.FromDateTime(DateTime.Today),
            Amount = -150m,
            Description = "Grocery Store",
            BankBalance = 0m,
            ImportHash = "hash1",
            ImportedUtc = DateTime.UtcNow
        };
        dbContext.Transactions.Add(transaction);
        await dbContext.SaveChangesAsync();

        var service = new CreditCardPositionService(dbContext);

        // Act
        var result = await service.CalculatePositionAsync(account.Id, openingBalance: 0m, CancellationToken.None);

        // Assert
        Assert.Equal(-150m, result.Position);  // negative = owing
        Assert.Equal(150m, result.AbsolutePosition);  // display positive
        Assert.False(result.IsCredit);  // not a credit when owing
        Assert.Equal(1, result.TransactionCount);
        Assert.True(result.HasTransactions);
    }

    [Fact]
    public async Task CalculatePositionAsync_PurchaseAndRepayment_CalculatesNetPosition()
    {
        // Arrange
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = "Test Credit Card",
            Type = AccountType.CreditCard,
            IsActive = true
        };
        dbContext.Accounts.Add(account);
        await dbContext.SaveChangesAsync();

        // Purchase (negative)
        var purchase = new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = account.Id,
            Date = new DateOnly(2026, 1, 15),
            Amount = -200m,
            Description = "Restaurant",
            BankBalance = 0m,
            ImportHash = "hash1",
            ImportedUtc = DateTime.UtcNow
        };

        // Repayment (positive)
        var repayment = new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = account.Id,
            Date = new DateOnly(2026, 1, 25),
            Amount = 100m,  // Paid $100 toward balance
            Description = "Payment",
            BankBalance = 0m,
            ImportHash = "hash2",
            ImportedUtc = DateTime.UtcNow.AddSeconds(1)
        };

        dbContext.Transactions.AddRange(purchase, repayment);
        await dbContext.SaveChangesAsync();

        var service = new CreditCardPositionService(dbContext);

        // Act
        var result = await service.CalculatePositionAsync(account.Id, openingBalance: 0m, CancellationToken.None);

        // Assert
        // -200 + 100 = -100 owing
        Assert.Equal(-100m, result.Position);
        Assert.Equal(100m, result.AbsolutePosition);
        Assert.False(result.IsCredit);  // still owing
        Assert.Equal(2, result.TransactionCount);
    }

    [Fact]
    public async Task CalculatePositionAsync_OverpaymentCreatesCredit()
    {
        // Arrange
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = "Test Credit Card",
            Type = AccountType.CreditCard,
            IsActive = true
        };
        dbContext.Accounts.Add(account);
        await dbContext.SaveChangesAsync();

        // Purchase
        var purchase = new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = account.Id,
            Date = new DateOnly(2026, 1, 15),
            Amount = -100m,
            Description = "Uber",
            BankBalance = 0m,
            ImportHash = "hash1",
            ImportedUtc = DateTime.UtcNow
        };

        // Overpayment
        var overpayment = new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = account.Id,
            Date = new DateOnly(2026, 1, 25),
            Amount = 200m,  // Paid more than owing
            Description = "Payment",
            BankBalance = 0m,
            ImportHash = "hash2",
            ImportedUtc = DateTime.UtcNow.AddSeconds(1)
        };

        dbContext.Transactions.AddRange(purchase, overpayment);
        await dbContext.SaveChangesAsync();

        var service = new CreditCardPositionService(dbContext);

        // Act
        var result = await service.CalculatePositionAsync(account.Id, openingBalance: 0m, CancellationToken.None);

        // Assert
        // -100 + 200 = +100 credit (positive balance, account in credit)
        Assert.Equal(100m, result.Position);
        Assert.Equal(100m, result.AbsolutePosition);
        Assert.True(result.IsCredit);  // account is in credit (positive position)
        Assert.Equal(2, result.TransactionCount);
    }

    [Fact]
    public async Task CalculatePositionAsync_MultipleTransactionsOutOfOrder_IgnoresDateOrder()
    {
        // Arrange
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = "Test Credit Card",
            Type = AccountType.CreditCard,
            IsActive = true
        };
        dbContext.Accounts.Add(account);
        await dbContext.SaveChangesAsync();

        // Import transactions in reverse chronological order
        var t1 = new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = account.Id,
            Date = new DateOnly(2026, 3, 15),
            Amount = -50m,
            Description = "Late import",
            BankBalance = 0m,
            ImportHash = "hash3",
            ImportedUtc = DateTime.UtcNow.AddSeconds(3)
        };

        var t2 = new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = account.Id,
            Date = new DateOnly(2026, 1, 15),
            Amount = -100m,
            Description = "Early transaction",
            BankBalance = 0m,
            ImportHash = "hash1",
            ImportedUtc = DateTime.UtcNow.AddSeconds(1)
        };

        var t3 = new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = account.Id,
            Date = new DateOnly(2026, 2, 15),
            Amount = -75m,
            Description = "Middle transaction",
            BankBalance = 0m,
            ImportHash = "hash2",
            ImportedUtc = DateTime.UtcNow.AddSeconds(2)
        };

        dbContext.Transactions.AddRange(t1, t2, t3);
        await dbContext.SaveChangesAsync();

        var service = new CreditCardPositionService(dbContext);

        // Act
        var result = await service.CalculatePositionAsync(account.Id, openingBalance: 0m, CancellationToken.None);

        // Assert
        // Sum should be: -100 + -75 + -50 = -225 (order independent)
        Assert.Equal(-225m, result.Position);
        Assert.Equal(225m, result.AbsolutePosition);
        Assert.Equal(3, result.TransactionCount);
    }

    [Fact]
    public async Task CalculatePositionAsync_WithNonZeroOpeningBalance()
    {
        // Arrange
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = "Test Credit Card",
            Type = AccountType.CreditCard,
            IsActive = true
        };
        dbContext.Accounts.Add(account);
        await dbContext.SaveChangesAsync();

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = account.Id,
            Date = DateOnly.FromDateTime(DateTime.Today),
            Amount = -100m,
            Description = "Purchase",
            BankBalance = 0m,
            ImportHash = "hash1",
            ImportedUtc = DateTime.UtcNow
        };
        dbContext.Transactions.Add(transaction);
        await dbContext.SaveChangesAsync();

        var service = new CreditCardPositionService(dbContext);

        // Act - opening balance of $50 owed (negative)
        var result = await service.CalculatePositionAsync(account.Id, openingBalance: -50m, CancellationToken.None);

        // Assert
        // -50 (opening) + -100 (purchase) = -150
        Assert.Equal(-150m, result.Position);
        Assert.Equal(150m, result.AbsolutePosition);
        Assert.Equal(-50m, result.OpeningBalance);
        Assert.True(result.IsOpeningBalanceConfirmed);
        Assert.Null(result.Disclaimer);  // No disclaimer when opening balance is confirmed
    }

    [Fact]
    public async Task CalculatePositionAsync_InvalidAccount_ThrowsKeyNotFoundException()
    {
        // Arrange
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var service = new CreditCardPositionService(dbContext);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.CalculatePositionAsync(Guid.NewGuid(), cancellationToken: CancellationToken.None));

        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public async Task CalculatePositionAsync_NonCreditCardAccount_ThrowsArgumentException()
    {
        // Arrange
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = "Savings Account",
            Type = AccountType.Savings,  // Not a credit card
            IsActive = true
        };
        dbContext.Accounts.Add(account);
        await dbContext.SaveChangesAsync();

        var service = new CreditCardPositionService(dbContext);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => service.CalculatePositionAsync(account.Id, cancellationToken: CancellationToken.None));

        Assert.Contains("not a credit card account", exception.Message);
    }

    [Fact]
    public async Task CalculatePositionAsync_WithFeesAndInterest_IncludesAllDebitsCredits()
    {
        // Arrange
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = "Test Credit Card",
            Type = AccountType.CreditCard,
            IsActive = true
        };
        dbContext.Accounts.Add(account);
        await dbContext.SaveChangesAsync();

        var transactions = new[]
        {
            new Transaction
            {
                Id = Guid.NewGuid(),
                AccountId = account.Id,
                Date = new DateOnly(2026, 1, 10),
                Amount = -500m,  // Purchase
                Description = "Online Shopping",
                BankBalance = 0m,
                ImportHash = "hash1",
                ImportedUtc = DateTime.UtcNow
            },
            new Transaction
            {
                Id = Guid.NewGuid(),
                AccountId = account.Id,
                Date = new DateOnly(2026, 1, 20),
                Amount = -5.50m,  // Interest charge
                Description = "Interest Charge",
                BankBalance = 0m,
                ImportHash = "hash2",
                ImportedUtc = DateTime.UtcNow.AddSeconds(1)
            },
            new Transaction
            {
                Id = Guid.NewGuid(),
                AccountId = account.Id,
                Date = new DateOnly(2026, 1, 22),
                Amount = 200m,  // Partial payment
                Description = "Payment",
                BankBalance = 0m,
                ImportHash = "hash3",
                ImportedUtc = DateTime.UtcNow.AddSeconds(2)
            },
            new Transaction
            {
                Id = Guid.NewGuid(),
                AccountId = account.Id,
                Date = new DateOnly(2026, 1, 25),
                Amount = 25m,  // Refund/credit
                Description = "Refund",
                BankBalance = 0m,
                ImportHash = "hash4",
                ImportedUtc = DateTime.UtcNow.AddSeconds(3)
            }
        };

        dbContext.Transactions.AddRange(transactions);
        await dbContext.SaveChangesAsync();

        var service = new CreditCardPositionService(dbContext);

        // Act
        var result = await service.CalculatePositionAsync(account.Id, openingBalance: 0m, CancellationToken.None);

        // Assert
        // -500 + -5.50 + 200 + 25 = -280.50 owing
        Assert.Equal(-280.50m, result.Position);
        Assert.Equal(280.50m, result.AbsolutePosition);
        Assert.Equal(4, result.TransactionCount);
    }

    [Fact]
    public async Task GetTotalCreditCardPositionAsync_MultipleAccounts_SumsAllTransactions()
    {
        // Arrange
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var account1 = new Account
        {
            Id = Guid.NewGuid(),
            Name = "Credit Card 1",
            Type = AccountType.CreditCard,
            IsActive = true
        };

        var account2 = new Account
        {
            Id = Guid.NewGuid(),
            Name = "Credit Card 2",
            Type = AccountType.CreditCard,
            IsActive = true
        };

        var savingsAccount = new Account
        {
            Id = Guid.NewGuid(),
            Name = "Savings",
            Type = AccountType.Savings,
            IsActive = true
        };

        dbContext.Accounts.AddRange(account1, account2, savingsAccount);
        await dbContext.SaveChangesAsync();

        var transactions = new[]
        {
            new Transaction
            {
                Id = Guid.NewGuid(),
                AccountId = account1.Id,
                Date = DateOnly.FromDateTime(DateTime.Today),
                Amount = -100m,
                Description = "Card 1 purchase",
                BankBalance = 0m,
                ImportHash = "hash1",
                ImportedUtc = DateTime.UtcNow
            },
            new Transaction
            {
                Id = Guid.NewGuid(),
                AccountId = account2.Id,
                Date = DateOnly.FromDateTime(DateTime.Today),
                Amount = -200m,
                Description = "Card 2 purchase",
                BankBalance = 0m,
                ImportHash = "hash2",
                ImportedUtc = DateTime.UtcNow.AddSeconds(1)
            },
            new Transaction
            {
                Id = Guid.NewGuid(),
                AccountId = savingsAccount.Id,
                Date = DateOnly.FromDateTime(DateTime.Today),
                Amount = 1000m,
                Description = "Savings deposit",
                BankBalance = 1000m,
                ImportHash = "hash3",
                ImportedUtc = DateTime.UtcNow.AddSeconds(2)
            }
        };

        dbContext.Transactions.AddRange(transactions);
        await dbContext.SaveChangesAsync();

        var service = new CreditCardPositionService(dbContext);

        // Act
        var result = await service.GetTotalCreditCardPositionAsync(CancellationToken.None);

        // Assert
        // Should sum only credit card transactions: -100 + -200 = -300
        // Savings account transaction should not be included
        Assert.Equal(-300m, result);
    }
}
