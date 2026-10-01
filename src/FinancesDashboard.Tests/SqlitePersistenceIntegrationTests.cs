using FinancesDashboard.Domain.Enums;
using FinancesDashboard.Domain.Models;
using FinancesDashboard.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FinancesDashboard.Tests;

public sealed class SqlitePersistenceIntegrationTests
{
    [Fact]
    public async Task Migrate_CreatesExpectedTablesAndTransactionIndexes()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);

        await dbContext.Database.MigrateAsync();

        var tableNames = await GetTableNamesAsync(connection);

        Assert.Contains("Accounts", tableNames);
        Assert.Contains("Transactions", tableNames);
        Assert.Contains("AccountGroups", tableNames);
        Assert.Contains("AccountGroupMembers", tableNames);
        Assert.Contains("Mortgages", tableNames);
        Assert.Contains("InterestRatePeriods", tableNames);

        var indexRows = await GetTransactionIndexesAsync(connection);

        Assert.Contains(indexRows, index => index.Name == "IX_Transactions_AccountId");
        Assert.Contains(indexRows, index => index.Name == "IX_Transactions_Date");
        Assert.Contains(indexRows, index => index.Name == "IX_Transactions_AccountId_Date");

        var importHashIndex = Assert.Single(indexRows.Where(index => index.Name == "IX_Transactions_ImportHash"));
        Assert.Equal(1, importHashIndex.IsUnique);
    }

    [Fact]
    public async Task SaveChanges_EnforcesUniqueTransactionImportHash()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);

        await dbContext.Database.MigrateAsync();

        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = "Offset",
            Type = AccountType.Transaction,
            IsActive = true
        };

        dbContext.Accounts.Add(account);
        await dbContext.SaveChangesAsync();

        var first = CreateTransaction(account.Id, "hash-001", new DateOnly(2026, 1, 1), 100m, 200m);
        var second = CreateTransaction(account.Id, "hash-001", new DateOnly(2026, 1, 2), -25m, 175m);

        dbContext.Transactions.Add(first);
        await dbContext.SaveChangesAsync();

        dbContext.Transactions.Add(second);

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task SaveChanges_PersistsDateOnlyAndDecimalFinancialValues()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);

        await dbContext.Database.MigrateAsync();

        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = "Daily",
            Type = AccountType.Transaction,
            IsActive = true
        };

        dbContext.Accounts.Add(account);

        var expectedDate = new DateOnly(2026, 2, 10);
        var expectedAmount = 1234.56m;
        var expectedBankBalance = 7890.12m;

        var transaction = CreateTransaction(account.Id, "hash-roundtrip", expectedDate, expectedAmount, expectedBankBalance);
        dbContext.Transactions.Add(transaction);

        await dbContext.SaveChangesAsync();

        var saved = await dbContext.Transactions.SingleAsync(transactionRow => transactionRow.Id == transaction.Id);

        Assert.Equal(expectedDate, saved.Date);
        Assert.Equal(expectedAmount, saved.Amount);
        Assert.Equal(expectedBankBalance, saved.BankBalance);
    }

    [Fact]
    public async Task SaveChanges_EnforcesForeignKeyRelationships()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);

        await dbContext.Database.MigrateAsync();

        var transactionWithUnknownAccount = CreateTransaction(Guid.NewGuid(), "fk-hash", new DateOnly(2026, 3, 5), -20m, 100m);
        dbContext.Transactions.Add(transactionWithUnknownAccount);

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
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

    private static Transaction CreateTransaction(Guid accountId, string importHash, DateOnly date, decimal amount, decimal bankBalance)
    {
        return new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Date = date,
            Amount = amount,
            Description = "Sample",
            BankBalance = bankBalance,
            ImportHash = importHash,
            ImportedUtc = DateTime.UtcNow
        };
    }

    private static async Task<List<string>> GetTableNamesAsync(SqliteConnection connection)
    {
        var tableNames = new List<string>();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table';";

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tableNames.Add(reader.GetString(0));
        }

        return tableNames;
    }

    private static async Task<List<IndexInfo>> GetTransactionIndexesAsync(SqliteConnection connection)
    {
        var indexes = new List<IndexInfo>();

        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA index_list('Transactions');";

        await using var reader = await command.ExecuteReaderAsync();
        var nameOrdinal = reader.GetOrdinal("name");
        var uniqueOrdinal = reader.GetOrdinal("unique");

        while (await reader.ReadAsync())
        {
            indexes.Add(new IndexInfo
            {
                Name = reader.GetString(nameOrdinal),
                IsUnique = reader.GetInt32(uniqueOrdinal)
            });
        }

        return indexes;
    }

    private sealed class IndexInfo
    {
        public string Name { get; set; } = string.Empty;

        public int IsUnique { get; set; }
    }
}
