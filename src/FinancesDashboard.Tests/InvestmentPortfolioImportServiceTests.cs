using System.Text;
using FinancesDashboard.Application.Services;
using FinancesDashboard.Domain.Enums;
using FinancesDashboard.Domain.Models;
using FinancesDashboard.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinancesDashboard.Tests;

public sealed class InvestmentPortfolioImportServiceTests
{
    [Fact]
    public async Task PreviewImportAsync_ParsesSampleSnapshotAndTotals()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<FinancesDashboardDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new FinancesDashboardDbContext(options);
        await dbContext.Database.MigrateAsync();

        var accountId = Guid.NewGuid();
        dbContext.Accounts.Add(new Account
        {
            Id = accountId,
            Name = "Broker",
            Type = AccountType.Investment,
            IsActive = true,
            CurrencyCode = "AUD",
            IncludeInNetWorth = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync();

        const string csv = "Account Number: 4899988,,,,,,,,,,\n\"Share Holdings    As of 8:21:26 pm Sydney Time, 02 Nov. 2026\",,,,,,,,,,\nCode,Avail Units,Purchase $,Last $,Change $,Chg %,Profit/Loss $,P/L %,Mkt Value $,Wgt %,Value Chg $\nCHESS,,,,,,,,,,\nAMX,1000,0.6,0.31,0.003,0.98,-290,-48.33,310,0.7,3\nNDQ,185,46.772,59.29,-0.38,-0.64,2315.83,26.76,10968.65,24.65,-70.3\nSubtotal,,,,,,2025.83,0,11278.65,0,0\nTotal,,,,,,2025.83,0,11278.65,0,0\n";

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var service = new InvestmentPortfolioImportService(dbContext, NullLogger<InvestmentPortfolioImportService>.Instance);

        var preview = await service.PreviewImportAsync(accountId, stream, "sample.csv");

        Assert.False(preview.HasBlockingErrors);
        Assert.Equal(2, preview.SuccessfulRows);
        Assert.Equal(11278.65m, preview.CalculatedTotalMarketValue);
        Assert.Equal(9252.82m, preview.CalculatedTotalCostBase);
        Assert.Equal(2025.83m, preview.CalculatedTotalUnrealisedProfitLoss);
    }

    [Fact]
    public void CalculateHolding_ComputesExpectedValues()
    {
        var service = new PortfolioValuationService();

        var value = service.CalculateHolding(185m, 46.772m, 59.29m);

        Assert.Equal(8652.82m, value.TotalCostBase);
        Assert.Equal(10968.65m, value.CurrentMarketValue);
        Assert.Equal(2315.83m, value.UnrealisedProfitLoss);
        Assert.Equal(26.763876m, value.UnrealisedProfitLossPercentage);
    }
}
