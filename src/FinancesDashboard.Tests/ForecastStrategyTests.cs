using FinancesDashboard.Application.Dto;
using FinancesDashboard.Application.Forecasting;
using FinancesDashboard.Application.Services;
using FinancesDashboard.Domain.Enums;
using FinancesDashboard.Domain.Models;
using FinancesDashboard.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FinancesDashboard.Tests;

public sealed class ForecastStrategyTests
{
    [Fact]
    public void Forecast_UsesAverageMonthlyNetCashflowFromConfiguredLookback()
    {
        var options = new ForecastOptions
        {
            LookbackMonths = 2,
            MaxHorizonMonths = 12
        };

        var strategy = new AverageMonthlyNetCashflowForecastStrategy(options);

        var history = new List<CashflowPoint>
        {
            new(new DateOnly(2026, 1, 15), 1000m, 0m, 1000m),
            new(new DateOnly(2026, 2, 15), 2000m, 0m, 2000m),
            new(new DateOnly(2026, 3, 15), 3000m, 0m, 3000m),
            new(new DateOnly(2026, 4, 15), 4000m, 0m, 4000m)
        };

        var points = strategy.Forecast(history, new DateOnly(2026, 4, 30), 3);

        Assert.Collection(points,
            point =>
            {
                Assert.Equal(new DateOnly(2026, 5, 30), point.Date);
                Assert.Equal(3500m, point.ProjectedPosition);
                Assert.True(point.IsProjected);
                Assert.Equal("Average monthly net cashflow assumption", point.Assumption);
            },
            point =>
            {
                Assert.Equal(new DateOnly(2026, 6, 30), point.Date);
                Assert.Equal(7000m, point.ProjectedPosition);
                Assert.True(point.IsProjected);
            },
            point =>
            {
                Assert.Equal(new DateOnly(2026, 7, 30), point.Date);
                Assert.Equal(10500m, point.ProjectedPosition);
                Assert.True(point.IsProjected);
            });
    }

    [Fact]
    public void Forecast_RespectsConfiguredMaxHorizon()
    {
        var options = new ForecastOptions
        {
            LookbackMonths = 6,
            MaxHorizonMonths = 2
        };

        var strategy = new AverageMonthlyNetCashflowForecastStrategy(options);

        var history = new List<CashflowPoint>
        {
            new(new DateOnly(2026, 1, 15), 100m, 0m, 100m)
        };

        var points = strategy.Forecast(history, new DateOnly(2026, 1, 31), 5);

        Assert.Equal(2, points.Count);
        Assert.Equal(new DateOnly(2026, 2, 28), points[0].Date);
        Assert.Equal(new DateOnly(2026, 3, 28), points[1].Date);
    }

    [Fact]
    public void Forecast_WithEmptyHistory_ReturnsZeroProjectedDeltas()
    {
        var options = new ForecastOptions
        {
            LookbackMonths = 6,
            MaxHorizonMonths = 3
        };

        var strategy = new AverageMonthlyNetCashflowForecastStrategy(options);

        var points = strategy.Forecast([], new DateOnly(2026, 1, 31), 3);

        Assert.Collection(points,
            point => Assert.Equal(0m, point.ProjectedPosition),
            point => Assert.Equal(0m, point.ProjectedPosition),
            point => Assert.Equal(0m, point.ProjectedPosition));
    }

    [Fact]
    public void Forecast_WithZeroCashflow_RemainsFlat()
    {
        var options = new ForecastOptions
        {
            LookbackMonths = 3,
            MaxHorizonMonths = 3
        };

        var strategy = new AverageMonthlyNetCashflowForecastStrategy(options);

        var history = new List<CashflowPoint>
        {
            new(new DateOnly(2026, 1, 10), 0m, 0m, 0m),
            new(new DateOnly(2026, 2, 10), 0m, 0m, 0m),
            new(new DateOnly(2026, 3, 10), 0m, 0m, 0m)
        };

        var points = strategy.Forecast(history, new DateOnly(2026, 3, 31), 3);

        Assert.All(points, point => Assert.Equal(0m, point.ProjectedPosition));
    }

    [Fact]
    public async Task DashboardService_GetForecastAsync_UsesStrategyAndMarksProjectedResults()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = "Forecast Account",
            Type = AccountType.Transaction,
            IsActive = true
        };

        dbContext.Accounts.Add(account);

        dbContext.Transactions.AddRange(
            CreateTransaction(account.Id, new DateOnly(2026, 1, 31), 100m, 100m, new DateTime(2026, 1, 31, 9, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(account.Id, new DateOnly(2026, 2, 28), 200m, 300m, new DateTime(2026, 2, 28, 9, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(account.Id, new DateOnly(2026, 3, 31), 300m, 600m, new DateTime(2026, 3, 31, 9, 0, 0, DateTimeKind.Utc)));

        await dbContext.SaveChangesAsync();

        var options = new ForecastOptions
        {
            LookbackMonths = 2,
            MaxHorizonMonths = 4
        };

        var strategy = new AverageMonthlyNetCashflowForecastStrategy(options);
        var service = new DashboardApplicationService(dbContext, strategy, options);

        var points = await service.GetForecastAsync(account.Id, 3, CancellationToken.None);

        Assert.Collection(points,
            point =>
            {
                Assert.Equal(new DateOnly(2026, 4, 30), point.Date);
                Assert.Equal(850m, point.ProjectedPosition);
                Assert.True(point.IsProjected);
                Assert.Equal("Average monthly net cashflow assumption", point.Assumption);
            },
            point =>
            {
                Assert.Equal(new DateOnly(2026, 5, 30), point.Date);
                Assert.Equal(1100m, point.ProjectedPosition);
                Assert.True(point.IsProjected);
            },
            point =>
            {
                Assert.Equal(new DateOnly(2026, 6, 30), point.Date);
                Assert.Equal(1350m, point.ProjectedPosition);
                Assert.True(point.IsProjected);
            });
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
            Description = "Forecast Seed",
            BankBalance = bankBalance,
            ImportHash = Guid.NewGuid().ToString("N"),
            ImportedUtc = importedUtc
        };
    }
}
