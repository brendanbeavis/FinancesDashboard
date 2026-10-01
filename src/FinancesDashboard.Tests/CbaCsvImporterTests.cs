using FinancesDashboard.Application.CbaImport;
using FinancesDashboard.Domain.Enums;
using FinancesDashboard.Domain.Models;
using FinancesDashboard.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FinancesDashboard.Tests;

public sealed class CbaCsvImporterTests
{
    [Fact]
    public async Task ImportAsync_ValidFixture_ImportsRowsAndPreservesSourceValues()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var accountId = await CreateAccountAsync(dbContext);
        var importer = new CbaCsvImporter(dbContext);

        await using var csvStream = OpenFixtureStream("cba-valid.csv");

        var result = await importer.ImportAsync(accountId, csvStream);

        Assert.Equal(3, result.Imported);
        Assert.Equal(0, result.Skipped);
        Assert.Empty(result.Errors);

        var transactions = await dbContext.Transactions
            .OrderBy(transaction => transaction.Date)
            .ThenBy(transaction => transaction.Amount)
            .ToListAsync();

        Assert.Equal(3, transactions.Count);

        var loanRepayment = Assert.Single(transactions.Where(transaction => transaction.Description == "Loan Repayment LN REPAY 564572311"));
        Assert.Equal(-1560m, loanRepayment.Amount);
        Assert.Equal(524482.92m, loanRepayment.BankBalance);

        var expectedHash = CbaCsvImporter.CreateImportHash(
            accountId,
            new DateOnly(2026, 8, 31),
            -1560m,
            "Loan Repayment LN REPAY 564572311",
            524482.92m);

        Assert.Equal(expectedHash, loanRepayment.ImportHash);
    }

    [Fact]
    public async Task ImportAsync_MixedFixture_ReportsRowErrorsAndSkipsInFileDuplicate()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var accountId = await CreateAccountAsync(dbContext);
        var importer = new CbaCsvImporter(dbContext);

        await using var csvStream = OpenFixtureStream("cba-mixed-invalid-and-duplicates.csv");

        var result = await importer.ImportAsync(accountId, csvStream);

        Assert.Equal(2, result.Imported);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(5, result.Errors.Count);

        Assert.Contains(result.Errors, error => error.StartsWith("Row 3: Invalid date"));
        Assert.Contains(result.Errors, error => error.StartsWith("Row 4: Invalid amount"));
        Assert.Contains(result.Errors, error => error.StartsWith("Row 5: Description is required."));
        Assert.Contains(result.Errors, error => error.StartsWith("Row 6: Invalid NET_ACCOUNT_TOTAL"));
        Assert.Contains(result.Errors, error => error.StartsWith("Row 7: Expected 4 columns"));
    }

    [Fact]
    public async Task ImportAsync_SecondRun_SkipsExistingDatabaseDuplicates()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var accountId = await CreateAccountAsync(dbContext);
        var importer = new CbaCsvImporter(dbContext);

        await using var firstRunStream = OpenFixtureStream("cba-valid.csv");
        var firstResult = await importer.ImportAsync(accountId, firstRunStream);

        Assert.Equal(3, firstResult.Imported);
        Assert.Equal(0, firstResult.Skipped);
        Assert.Empty(firstResult.Errors);

        await using var secondRunStream = OpenFixtureStream("cba-valid.csv");
        var secondResult = await importer.ImportAsync(accountId, secondRunStream);

        Assert.Equal(0, secondResult.Imported);
        Assert.Equal(3, secondResult.Skipped);
        Assert.Empty(secondResult.Errors);
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

    private static async Task<Guid> CreateAccountAsync(FinancesDashboardDbContext dbContext)
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = "CBA Everyday",
            Type = AccountType.Transaction,
            IsActive = true
        };

        dbContext.Accounts.Add(account);
        await dbContext.SaveChangesAsync();

        return account.Id;
    }

    private static FileStream OpenFixtureStream(string fixtureName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureName);
        return File.OpenRead(path);
    }
}
