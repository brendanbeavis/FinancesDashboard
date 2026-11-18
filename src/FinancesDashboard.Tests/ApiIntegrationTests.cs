extern alias ApiService;

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FinancesDashboard.Application.CbaImport;
using FinancesDashboard.Application.Dto;
using FinancesDashboard.Domain.Enums;
using FinancesDashboard.Domain.Models;
using FinancesDashboard.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FinancesDashboard.Tests;

public sealed class ApiIntegrationTests
{
    [Fact]
    public async Task GetHealth_ReturnsOk()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var factory = new ApiTestFactory(connection);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostAccounts_WithInvalidType_ReturnsValidationProblem()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var factory = new ApiTestFactory(connection);
        using var client = factory.CreateClient();

        var request = new CreateAccountRequest("Daily", "NotAType");
        var response = await client.PostAsJsonAsync("/api/accounts", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostMortgages_WithImportedHistoryAndDetectedRepayments_ReturnsProjectedAmortisation()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var factory = new ApiTestFactory(connection);
        using var client = factory.CreateClient();

        var accountRequest = new CreateAccountRequest("Home Loan", nameof(AccountType.Mortgage));
        var accountResponse = await client.PostAsJsonAsync("/api/accounts", accountRequest);
        accountResponse.EnsureSuccessStatusCode();

        var account = await accountResponse.Content.ReadFromJsonAsync<AccountDto>();
        Assert.NotNull(account);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FinancesDashboardDbContext>();
            dbContext.Transactions.AddRange(
                new Transaction { Id = Guid.NewGuid(), AccountId = account.Id, Date = new DateOnly(2026, 1, 1), Amount = -3200m, Description = "Mortgage repayment", BankBalance = -500000m, ImportHash = "mort-1", ImportedUtc = DateTime.UtcNow },
                new Transaction { Id = Guid.NewGuid(), AccountId = account.Id, Date = new DateOnly(2026, 2, 1), Amount = -3200m, Description = "Mortgage repayment", BankBalance = -498000m, ImportHash = "mort-2", ImportedUtc = DateTime.UtcNow },
                new Transaction { Id = Guid.NewGuid(), AccountId = account.Id, Date = new DateOnly(2026, 3, 1), Amount = -3200m, Description = "Mortgage repayment", BankBalance = -496000m, ImportHash = "mort-3", ImportedUtc = DateTime.UtcNow });
            await dbContext.SaveChangesAsync();
        }

        var mortgageRequest = new CreateMortgageRequest(
            account.Id,
            0.0615m,
            [new InterestRatePeriodDto(new DateOnly(2024, 7, 1), null, 0.0615m)],
            []);

        var response = await client.PostAsJsonAsync("/api/mortgages", mortgageRequest);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var mortgage = await response.Content.ReadFromJsonAsync<MortgageDto>();
        Assert.NotNull(mortgage);
        Assert.Equal(account.Id, mortgage.AccountId);
        Assert.Equal(account.Name, mortgage.AccountName);
        Assert.Single(mortgage.InterestRateHistory);
        Assert.True(mortgage.Projection.IsProjected);
        Assert.NotEmpty(mortgage.Projection.Assumption);
        Assert.NotNull(mortgage.DetectedRepaymentAmount);
        Assert.NotNull(mortgage.DetectedRepaymentsPerYear);

        var listedResponse = await client.GetAsync("/api/mortgages");
        listedResponse.EnsureSuccessStatusCode();

        var mortgages = await listedResponse.Content.ReadFromJsonAsync<List<MortgageDto>>();
        Assert.NotNull(mortgages);
        var stored = Assert.Single(mortgages.Where(item => item.Id == mortgage.Id));
        Assert.Single(stored.InterestRateHistory);
        Assert.Equal(account.Id, stored.AccountId);
        Assert.NotNull(stored.DetectedRepaymentAmount);
    }

    [Fact]
    public async Task PostAccounts_ThenGetAccounts_ReturnsCreatedAccount()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var factory = new ApiTestFactory(connection);
        using var client = factory.CreateClient();

        var request = new CreateAccountRequest("Daily", "Transaction");
        var postResponse = await client.PostAsJsonAsync("/api/accounts", request);

        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);

        var created = await postResponse.Content.ReadFromJsonAsync<AccountDto>();
        Assert.NotNull(created);
        Assert.Equal("Daily", created.Name);
        Assert.Equal("Transaction", created.Type);
        Assert.True(created.IsActive);

        var getResponse = await client.GetAsync("/api/accounts");
        getResponse.EnsureSuccessStatusCode();

        var accounts = await getResponse.Content.ReadFromJsonAsync<List<AccountDto>>();
        Assert.NotNull(accounts);
        Assert.Contains(accounts, account => account.Id == created.Id);
    }

    [Fact]
    public async Task PostImportCba_ImportsFixtureTransactions()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var factory = new ApiTestFactory(connection);
        using var client = factory.CreateClient();

        var accountResponse = await client.PostAsJsonAsync("/api/accounts", new CreateAccountRequest("CBA Everyday", "Transaction"));
        accountResponse.EnsureSuccessStatusCode();
        var account = await accountResponse.Content.ReadFromJsonAsync<AccountDto>();
        Assert.NotNull(account);

        await using var fixtureStream = OpenFixtureStream("cba-valid.csv");
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(fixtureStream), "file", "cba-valid.csv");

        var importResponse = await client.PostAsync($"/api/import/cba/{account.Id}", content);
        var importBody = await importResponse.Content.ReadAsStringAsync();
        Assert.True(importResponse.IsSuccessStatusCode, $"Expected success but got {(int)importResponse.StatusCode}: {importBody}");

        var result = JsonSerializer.Deserialize<ImportResult>(importBody, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(result);
        Assert.Equal(3, result.Imported);
        Assert.Equal(0, result.Skipped);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ImportWorkflow_ThenDashboardBalance_ReturnsObservedBankBalanceHistory()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var factory = new ApiTestFactory(connection);
        using var client = factory.CreateClient();

        var accountResponse = await client.PostAsJsonAsync("/api/accounts", new CreateAccountRequest("Slice Account", "Transaction"));
        accountResponse.EnsureSuccessStatusCode();
        var account = await accountResponse.Content.ReadFromJsonAsync<AccountDto>();
        Assert.NotNull(account);

        await using var fixtureStream = OpenFixtureStream("cba-valid.csv");
        using (var content = new MultipartFormDataContent())
        {
            content.Add(new StreamContent(fixtureStream), "file", "cba-valid.csv");
            var importResponse = await client.PostAsync($"/api/import/cba/{account.Id}", content);
            importResponse.EnsureSuccessStatusCode();
        }

        var balanceResponse = await client.GetAsync($"/api/dashboard/balance?accountId={account.Id}");
        balanceResponse.EnsureSuccessStatusCode();

        var balancePoints = await balanceResponse.Content.ReadFromJsonAsync<List<BalancePoint>>();
        Assert.NotNull(balancePoints);
        Assert.Equal(2, balancePoints.Count);
        Assert.Equal(new DateOnly(2026, 8, 27), balancePoints[0].Date);
        Assert.Equal(526456.17m, balancePoints[0].Balance);
        Assert.Equal(new DateOnly(2026, 8, 31), balancePoints[1].Date);
        Assert.Equal(524482.92m, balancePoints[1].Balance);
    }

    [Fact]
    public async Task GetDashboardForecast_WithInvalidMonths_ReturnsValidationProblem()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var factory = new ApiTestFactory(connection);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/dashboard/forecast?accountId={Guid.NewGuid()}&months=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetMortgagesAccounts_WithMortgageAccounts_ReturnsOnlyMortgages()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var factory = new ApiTestFactory(connection);
        using var client = factory.CreateClient();

        // Create mixed account types
        await client.PostAsJsonAsync("/api/accounts", new CreateAccountRequest("Checking", nameof(AccountType.Transaction)));
        await client.PostAsJsonAsync("/api/accounts", new CreateAccountRequest("Mortgage 1", nameof(AccountType.Mortgage)));
        await client.PostAsJsonAsync("/api/accounts", new CreateAccountRequest("Savings", nameof(AccountType.Savings)));
        await client.PostAsJsonAsync("/api/accounts", new CreateAccountRequest("Mortgage 2", nameof(AccountType.Mortgage)));

        var response = await client.GetAsync("/api/mortgages/accounts");
        response.EnsureSuccessStatusCode();

        var accounts = await response.Content.ReadFromJsonAsync<List<AccountDto>>();
        Assert.NotNull(accounts);
        Assert.Equal(2, accounts.Count);
        Assert.All(accounts, account => Assert.Equal(nameof(AccountType.Mortgage), account.Type));
    }

    [Fact]
    public async Task PostMortgagesForecast_WithValidAssumptions_ReturnsComprehensiveForecast()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var factory = new ApiTestFactory(connection);
        using var client = factory.CreateClient();

        // Create a mortgage account
        var accountRequest = new CreateAccountRequest("Home Loan", nameof(AccountType.Mortgage));
        var accountResponse = await client.PostAsJsonAsync("/api/accounts", accountRequest);
        accountResponse.EnsureSuccessStatusCode();

        var account = await accountResponse.Content.ReadFromJsonAsync<AccountDto>();
        Assert.NotNull(account);

        // Generate an ephemeral forecast even with no history
        // The forecast should return empty history but still generate projection
        var forecastRequest = new MortgageForecastRequest(
            AnnualInterestRate: 0.0599m,
            RepaymentAmount: 1560m,
            RepaymentsPerYear: 26,
            SelectedOffsetAccountIds: []);

        var forecastResponse = await client.PostAsJsonAsync(
            $"/api/mortgages/{account.Id}/forecast",
            forecastRequest);

        Assert.Equal(HttpStatusCode.OK, forecastResponse.StatusCode);

        var forecast = await forecastResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotNull(forecast);

        // Verify response structure
        Assert.True(forecast.TryGetProperty("historicalPoints", out var historicalPoints));
        Assert.True(historicalPoints.ValueKind == System.Text.Json.JsonValueKind.Array);

        Assert.True(forecast.TryGetProperty("projectedAmortisationSchedule", out var projectedSchedule));
        Assert.True(projectedSchedule.ValueKind == System.Text.Json.JsonValueKind.Array);

        Assert.True(forecast.TryGetProperty("summary", out var summary));
        Assert.True(summary.TryGetProperty("currentBalance", out var currentBalance));
        Assert.True(summary.TryGetProperty("totalRepayments", out var totalRepayments));

        Assert.True(forecast.TryGetProperty("forecast", out var forecastOutcome));
        Assert.True(forecastOutcome.TryGetProperty("status", out var status));
        Assert.True(forecastOutcome.TryGetProperty("projectedPayoffDate", out var payoffDate));
        Assert.True(forecastOutcome.TryGetProperty("totalFutureInterest", out var remainingInterest));
        Assert.True(forecastOutcome.TryGetProperty("currentOffsetBalance", out var currentOffsetBalance));

        Assert.True(forecast.TryGetProperty("assumptions", out var assumptions));
        Assert.True(assumptions.TryGetProperty("annualInterestRate", out var rateProperty));
    }

    [Fact]
    public async Task PostMortgagesForecast_WithInvalidAnnualRate_ReturnsValidationError()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var factory = new ApiTestFactory(connection);
        using var client = factory.CreateClient();

        var accountRequest = new CreateAccountRequest("Home Loan", nameof(AccountType.Mortgage));
        var accountResponse = await client.PostAsJsonAsync("/api/accounts", accountRequest);
        accountResponse.EnsureSuccessStatusCode();

        var account = await accountResponse.Content.ReadFromJsonAsync<AccountDto>();
        Assert.NotNull(account);

        var forecastRequest = new MortgageForecastRequest(
            AnnualInterestRate: -0.01m, // Invalid negative rate
            RepaymentAmount: 1560m,
            RepaymentsPerYear: 26,
            SelectedOffsetAccountIds: []);

        var response = await client.PostAsJsonAsync(
            $"/api/mortgages/{account.Id}/forecast",
            forecastRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostMortgagesForecast_WithZeroRepaymentAmount_ReturnsForecast()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var factory = new ApiTestFactory(connection);
        using var client = factory.CreateClient();

        var accountRequest = new CreateAccountRequest("Home Loan", nameof(AccountType.Mortgage));
        var accountResponse = await client.PostAsJsonAsync("/api/accounts", accountRequest);
        accountResponse.EnsureSuccessStatusCode();

        var account = await accountResponse.Content.ReadFromJsonAsync<AccountDto>();
        Assert.NotNull(account);

        using var fixtureStream = OpenFixtureStream("cba-valid.csv");
        using var formContent = new MultipartFormDataContent();
        formContent.Add(new StreamContent(fixtureStream), "file", "cba-valid.csv");

        var importResponse = await client.PostAsync($"/api/import/cba/{account.Id}", formContent);
        importResponse.EnsureSuccessStatusCode();

        var forecastRequest = new MortgageForecastRequest(
            AnnualInterestRate: 0.0599m,
            RepaymentAmount: 0m,
            RepaymentsPerYear: 12,
            SelectedOffsetAccountIds: []);

        var response = await client.PostAsJsonAsync(
            $"/api/mortgages/{account.Id}/forecast",
            forecastRequest);

        response.EnsureSuccessStatusCode();

        var forecast = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotNull(forecast);
        Assert.True(forecast.TryGetProperty("historicalPoints", out _));
        Assert.True(forecast.TryGetProperty("projectedAmortisationSchedule", out _));
    }

    [Fact]
    public async Task PostMortgagesForecast_WithImportedTransactions_DetectsRepaymentPattern()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var factory = new ApiTestFactory(connection);
        using var client = factory.CreateClient();

        // Create mortgage account
        var accountRequest = new CreateAccountRequest("Home Loan", nameof(AccountType.Mortgage));
        var accountResponse = await client.PostAsJsonAsync("/api/accounts", accountRequest);
        accountResponse.EnsureSuccessStatusCode();

        var account = await accountResponse.Content.ReadFromJsonAsync<AccountDto>();
        Assert.NotNull(account);

        // Import CBA transactions with regular monthly repayments
        using var fixtureStream = OpenFixtureStream("cba-valid.csv");
        using var formContent = new MultipartFormDataContent();
        formContent.Add(new StreamContent(fixtureStream), "file", "cba-valid.csv");

        var importResponse = await client.PostAsync($"/api/import/cba/{account.Id}", formContent);
        importResponse.EnsureSuccessStatusCode();

        // Now generate forecast which should detect repayment pattern
        var forecastRequest = new MortgageForecastRequest(
            AnnualInterestRate: 0.0599m,
            RepaymentAmount: 0m,
            RepaymentsPerYear: 0,
            SelectedOffsetAccountIds: []);

        var forecastResponse = await client.PostAsJsonAsync(
            $"/api/mortgages/{account.Id}/forecast",
            forecastRequest);

        Assert.Equal(HttpStatusCode.OK, forecastResponse.StatusCode);

        var forecast = await forecastResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotNull(forecast);

        // Verify detected repayment is included
        Assert.True(forecast.TryGetProperty("detectedRepayment", out var detectedRepayment));

        if (detectedRepayment.ValueKind != System.Text.Json.JsonValueKind.Null)
        {
            // If pattern detected, verify structure
            Assert.True(detectedRepayment.TryGetProperty("estimatedAmount", out var estimatedAmount));
            Assert.True(detectedRepayment.TryGetProperty("estimatedFrequency", out var estimatedFrequency));
            Assert.True(detectedRepayment.TryGetProperty("confidence", out var confidence));
            Assert.True(detectedRepayment.TryGetProperty("numberOfObservations", out var observations));
            Assert.True(detectedRepayment.TryGetProperty("lastObservedDate", out var lastDate));

            // Validate value ranges
            if (confidence.TryGetDecimal(out var conf))
            {
                Assert.InRange(conf, 0m, 1m);
            }
        }
    }

    [Fact]
    public async Task PostMortgagesForecast_WithNegativeCbaBalances_ReturnsPositiveOutstandingPrincipal()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var factory = new ApiTestFactory(connection);
        using var client = factory.CreateClient();

        var accountRequest = new CreateAccountRequest("Home Loan", nameof(AccountType.Mortgage));
        var accountResponse = await client.PostAsJsonAsync("/api/accounts", accountRequest);
        accountResponse.EnsureSuccessStatusCode();

        var account = await accountResponse.Content.ReadFromJsonAsync<AccountDto>();
        Assert.NotNull(account);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FinancesDashboardDbContext>();
            dbContext.Transactions.AddRange(
                new Transaction { Id = Guid.NewGuid(), AccountId = account.Id, Date = new DateOnly(2026, 1, 1), Amount = -3200m, Description = "Mortgage repayment", BankBalance = -500000m, ImportHash = "neg1", ImportedUtc = DateTime.UtcNow },
                new Transaction { Id = Guid.NewGuid(), AccountId = account.Id, Date = new DateOnly(2026, 2, 1), Amount = -3200m, Description = "Mortgage repayment", BankBalance = -498000m, ImportHash = "neg2", ImportedUtc = DateTime.UtcNow });
            await dbContext.SaveChangesAsync();
        }

        var historyResponse = await client.GetAsync($"/api/mortgages/{account.Id}/history");
        historyResponse.EnsureSuccessStatusCode();

        var history = await historyResponse.Content.ReadFromJsonAsync<List<JsonElement>>();
        Assert.NotNull(history);
        Assert.Equal(2, history.Count);
        Assert.Equal(500000m, history[0].GetProperty("balance").GetDecimal());
        Assert.Equal(498000m, history[1].GetProperty("balance").GetDecimal());

        var forecastRequest = new MortgageForecastRequest(
            AnnualInterestRate: 0.0599m,
            RepaymentAmount: 0m,
            RepaymentsPerYear: 12,
            SelectedOffsetAccountIds: []);

        var forecastResponse = await client.PostAsJsonAsync(
            $"/api/mortgages/{account.Id}/forecast",
            forecastRequest);

        forecastResponse.EnsureSuccessStatusCode();
        var forecast = await forecastResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotNull(forecast);
        var summary = forecast.GetProperty("summary");
        Assert.Equal(498000m, summary.GetProperty("currentBalance").GetDecimal());
        var forecastOutcome = forecast.GetProperty("forecast");
        Assert.Equal("PaidOff", forecastOutcome.GetProperty("status").GetString());
    }

    [Fact]
    public async Task PostMortgagesForecast_WithSelectedOffsetAccounts_ReducesEffectivePrincipal()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var factory = new ApiTestFactory(connection);
        using var client = factory.CreateClient();

        var mortgageAccountResponse = await client.PostAsJsonAsync("/api/accounts", new CreateAccountRequest("Home Loan", nameof(AccountType.Mortgage)));
        mortgageAccountResponse.EnsureSuccessStatusCode();
        var mortgageAccount = await mortgageAccountResponse.Content.ReadFromJsonAsync<AccountDto>();
        Assert.NotNull(mortgageAccount);

        var offsetAccountResponse = await client.PostAsJsonAsync("/api/accounts", new CreateAccountRequest("Daily Offset", nameof(AccountType.Transaction)));
        offsetAccountResponse.EnsureSuccessStatusCode();
        var offsetAccount = await offsetAccountResponse.Content.ReadFromJsonAsync<AccountDto>();
        Assert.NotNull(offsetAccount);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FinancesDashboardDbContext>();
            dbContext.Transactions.AddRange(
                new Transaction { Id = Guid.NewGuid(), AccountId = mortgageAccount.Id, Date = new DateOnly(2026, 1, 1), Amount = -3200m, Description = "Mortgage repayment", BankBalance = -500000m, ImportHash = "mort1", ImportedUtc = DateTime.UtcNow },
                new Transaction { Id = Guid.NewGuid(), AccountId = mortgageAccount.Id, Date = new DateOnly(2026, 2, 1), Amount = -3200m, Description = "Mortgage repayment", BankBalance = -498000m, ImportHash = "mort2", ImportedUtc = DateTime.UtcNow },
                new Transaction { Id = Guid.NewGuid(), AccountId = offsetAccount.Id, Date = new DateOnly(2026, 2, 1), Amount = 0m, Description = "Balance snapshot", BankBalance = 25000m, ImportHash = "off1", ImportedUtc = DateTime.UtcNow });
            await dbContext.SaveChangesAsync();
        }

        var forecastRequest = new MortgageForecastRequest(
            AnnualInterestRate: 0.0599m,
            RepaymentAmount: 3200m,
            RepaymentsPerYear: 12,
            SelectedOffsetAccountIds: [offsetAccount.Id]);

        var forecastResponse = await client.PostAsJsonAsync(
            $"/api/mortgages/{mortgageAccount.Id}/forecast",
            forecastRequest);

        forecastResponse.EnsureSuccessStatusCode();
        var forecast = await forecastResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotNull(forecast);

        var forecastOutcome = forecast.GetProperty("forecast");
        Assert.Equal(25000m, forecastOutcome.GetProperty("currentOffsetBalance").GetDecimal());
        Assert.Equal(473000m, forecastOutcome.GetProperty("effectivePrincipalAfterOffset").GetDecimal());
    }

    [Fact]
    public async Task PutMortgageOffsetAccounts_PersistsSelectionAndReturnsOffsetAwareSummary()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var factory = new ApiTestFactory(connection);
        using var client = factory.CreateClient();

        var mortgageAccountResponse = await client.PostAsJsonAsync("/api/accounts", new CreateAccountRequest("Home Loan", nameof(AccountType.Mortgage)));
        mortgageAccountResponse.EnsureSuccessStatusCode();
        var mortgageAccount = await mortgageAccountResponse.Content.ReadFromJsonAsync<AccountDto>();
        Assert.NotNull(mortgageAccount);

        var offsetOneResponse = await client.PostAsJsonAsync("/api/accounts", new CreateAccountRequest("Offset One", nameof(AccountType.Transaction)));
        offsetOneResponse.EnsureSuccessStatusCode();
        var offsetOne = await offsetOneResponse.Content.ReadFromJsonAsync<AccountDto>();
        Assert.NotNull(offsetOne);

        var offsetTwoResponse = await client.PostAsJsonAsync("/api/accounts", new CreateAccountRequest("Offset Two", nameof(AccountType.Savings)));
        offsetTwoResponse.EnsureSuccessStatusCode();
        var offsetTwo = await offsetTwoResponse.Content.ReadFromJsonAsync<AccountDto>();
        Assert.NotNull(offsetTwo);

        var mortgageCreateResponse = await client.PostAsJsonAsync("/api/mortgages", new CreateMortgageRequest(
            mortgageAccount.Id,
            0.0615m,
            [new InterestRatePeriodDto(new DateOnly(2024, 7, 1), null, 0.0615m)],
            []));
        mortgageCreateResponse.EnsureSuccessStatusCode();

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FinancesDashboardDbContext>();
            dbContext.Transactions.AddRange(
                new Transaction { Id = Guid.NewGuid(), AccountId = mortgageAccount.Id, Date = new DateOnly(2026, 2, 1), Amount = -3200m, Description = "Mortgage repayment", BankBalance = -500000m, ImportHash = "sum-mort", ImportedUtc = DateTime.UtcNow },
                new Transaction { Id = Guid.NewGuid(), AccountId = offsetOne.Id, Date = new DateOnly(2026, 2, 1), Amount = 0m, Description = "Balance snapshot", BankBalance = 20000m, ImportHash = "sum-off1", ImportedUtc = DateTime.UtcNow },
                new Transaction { Id = Guid.NewGuid(), AccountId = offsetTwo.Id, Date = new DateOnly(2026, 2, 2), Amount = 0m, Description = "Balance snapshot", BankBalance = 30000m, ImportHash = "sum-off2", ImportedUtc = DateTime.UtcNow });
            await dbContext.SaveChangesAsync();
        }

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/mortgages/{mortgageAccount.Id}/offset-accounts",
            new UpdateMortgageOffsetAccountsRequest([offsetOne.Id, offsetTwo.Id]));

        updateResponse.EnsureSuccessStatusCode();
        var summary = await updateResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotNull(summary);
        Assert.Equal(50000m, summary.GetProperty("totalOffsetBalance").GetDecimal());
        Assert.Equal(450000m, summary.GetProperty("interestBearingBalance").GetDecimal());

        var getSummaryResponse = await client.GetAsync($"/api/mortgages/{mortgageAccount.Id}/summary");
        getSummaryResponse.EnsureSuccessStatusCode();
        var getSummary = await getSummaryResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotNull(getSummary);
        Assert.Equal(50000m, getSummary.GetProperty("totalOffsetBalance").GetDecimal());
        Assert.Equal(450000m, getSummary.GetProperty("interestBearingBalance").GetDecimal());
        Assert.Equal(2, getSummary.GetProperty("offsetAccountIds").GetArrayLength());
    }

    [Fact]
    public async Task GetMortgageSummary_WithInterestChargeAndOffsets_ReturnsEstimatedAnnualInterestRate()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var factory = new ApiTestFactory(connection);
        using var client = factory.CreateClient();

        var mortgageAccountResponse = await client.PostAsJsonAsync("/api/accounts", new CreateAccountRequest("Home Loan", nameof(AccountType.Mortgage)));
        mortgageAccountResponse.EnsureSuccessStatusCode();
        var mortgageAccount = await mortgageAccountResponse.Content.ReadFromJsonAsync<AccountDto>();
        Assert.NotNull(mortgageAccount);

        var offsetAccountResponse = await client.PostAsJsonAsync("/api/accounts", new CreateAccountRequest("Offset", nameof(AccountType.Transaction)));
        offsetAccountResponse.EnsureSuccessStatusCode();
        var offsetAccount = await offsetAccountResponse.Content.ReadFromJsonAsync<AccountDto>();
        Assert.NotNull(offsetAccount);

        var mortgageCreateResponse = await client.PostAsJsonAsync("/api/mortgages", new CreateMortgageRequest(
            mortgageAccount.Id,
            0.0615m,
            [new InterestRatePeriodDto(new DateOnly(2024, 7, 1), null, 0.0615m)],
            []));
        mortgageCreateResponse.EnsureSuccessStatusCode();

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FinancesDashboardDbContext>();
            dbContext.Transactions.AddRange(
                new Transaction { Id = Guid.NewGuid(), AccountId = offsetAccount.Id, Date = new DateOnly(2026, 1, 30), Amount = 0m, Description = "Balance snapshot", BankBalance = 50000m, ImportHash = "offset-rate-1", ImportedUtc = DateTime.UtcNow },
                new Transaction { Id = Guid.NewGuid(), AccountId = mortgageAccount.Id, Date = new DateOnly(2026, 1, 31), Amount = 1500m, Description = "Interest charge", BankBalance = -501500m, ImportHash = "mortgage-rate-1", ImportedUtc = DateTime.UtcNow });
            await dbContext.SaveChangesAsync();
        }

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/mortgages/{mortgageAccount.Id}/offset-accounts",
            new UpdateMortgageOffsetAccountsRequest([offsetAccount.Id]));
        updateResponse.EnsureSuccessStatusCode();

        var summaryResponse = await client.GetAsync($"/api/mortgages/{mortgageAccount.Id}/summary");
        summaryResponse.EnsureSuccessStatusCode();
        var summary = await summaryResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotNull(summary);
        Assert.Equal(0.04m, summary.GetProperty("estimatedAnnualInterestRate").GetDecimal());
    }

    [Fact]
    public async Task PutMortgageOffsetAccounts_WithMortgageItself_ReturnsBadRequest()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var factory = new ApiTestFactory(connection);
        using var client = factory.CreateClient();

        var mortgageAccountResponse = await client.PostAsJsonAsync("/api/accounts", new CreateAccountRequest("Home Loan", nameof(AccountType.Mortgage)));
        mortgageAccountResponse.EnsureSuccessStatusCode();
        var mortgageAccount = await mortgageAccountResponse.Content.ReadFromJsonAsync<AccountDto>();
        Assert.NotNull(mortgageAccount);

        var mortgageCreateResponse = await client.PostAsJsonAsync("/api/mortgages", new CreateMortgageRequest(
            mortgageAccount.Id,
            0.0615m,
            [new InterestRatePeriodDto(new DateOnly(2024, 7, 1), null, 0.0615m)],
            []));
        mortgageCreateResponse.EnsureSuccessStatusCode();

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/mortgages/{mortgageAccount.Id}/offset-accounts",
            new UpdateMortgageOffsetAccountsRequest([mortgageAccount.Id]));

        Assert.Equal(HttpStatusCode.BadRequest, updateResponse.StatusCode);
    }

    [Fact]
    public async Task GetDashboardAccountSummaries_CreditCardPreviousMonthActivity_ReturnsAmountBasedValues()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var factory = new ApiTestFactory(connection);
        using var client = factory.CreateClient();

        var accountResponse = await client.PostAsJsonAsync("/api/accounts", new CreateAccountRequest("Card", nameof(AccountType.CreditCard)));
        accountResponse.EnsureSuccessStatusCode();
        var account = await accountResponse.Content.ReadFromJsonAsync<AccountDto>();
        Assert.NotNull(account);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var firstDayOfCurrentMonth = new DateOnly(today.Year, today.Month, 1);
        var lastDayOfPreviousMonth = firstDayOfCurrentMonth.AddDays(-1);
        var firstDayOfPreviousMonth = new DateOnly(lastDayOfPreviousMonth.Year, lastDayOfPreviousMonth.Month, 1);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FinancesDashboardDbContext>();
            dbContext.Transactions.AddRange(
                new Transaction { Id = Guid.NewGuid(), AccountId = account.Id, Date = firstDayOfPreviousMonth, Amount = 500m, Description = "Payment", BankBalance = 0m, ImportHash = Guid.NewGuid().ToString("N"), ImportedUtc = DateTime.UtcNow },
                new Transaction { Id = Guid.NewGuid(), AccountId = account.Id, Date = firstDayOfPreviousMonth.AddDays(4), Amount = -120m, Description = "Supermarket", BankBalance = 0m, ImportHash = Guid.NewGuid().ToString("N"), ImportedUtc = DateTime.UtcNow },
                new Transaction { Id = Guid.NewGuid(), AccountId = account.Id, Date = firstDayOfPreviousMonth.AddDays(9), Amount = -80m, Description = "Fuel", BankBalance = 0m, ImportHash = Guid.NewGuid().ToString("N"), ImportedUtc = DateTime.UtcNow },
                new Transaction { Id = Guid.NewGuid(), AccountId = account.Id, Date = firstDayOfPreviousMonth.AddDays(14), Amount = -50m, Description = "Restaurant", BankBalance = 0m, ImportHash = Guid.NewGuid().ToString("N"), ImportedUtc = DateTime.UtcNow });
            await dbContext.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/dashboard/account-summaries");
        response.EnsureSuccessStatusCode();

        var summaries = await response.Content.ReadFromJsonAsync<List<AccountDashboardSummary>>();
        Assert.NotNull(summaries);
        var summary = Assert.Single(summaries);
        Assert.Equal("CreditCard", summary.AccountType);
        Assert.Equal(500m, summary.PreviousMonthIncome);
        Assert.Equal(250m, summary.PreviousMonthExpenses);
        Assert.Equal(250m, summary.PreviousMonthNet);
        Assert.Equal(4, summary.PreviousMonthTransactionCount);
    }

    [Fact]
    public async Task PutMortgageOffsetAccounts_WithInactiveOrCreditCardOffset_ReturnsBadRequest()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var factory = new ApiTestFactory(connection);
        using var client = factory.CreateClient();

        var mortgageAccountResponse = await client.PostAsJsonAsync("/api/accounts", new CreateAccountRequest("Home Loan", nameof(AccountType.Mortgage)));
        mortgageAccountResponse.EnsureSuccessStatusCode();
        var mortgageAccount = await mortgageAccountResponse.Content.ReadFromJsonAsync<AccountDto>();
        Assert.NotNull(mortgageAccount);

        var creditCardResponse = await client.PostAsJsonAsync("/api/accounts", new CreateAccountRequest("Card", nameof(AccountType.CreditCard)));
        creditCardResponse.EnsureSuccessStatusCode();
        var creditCard = await creditCardResponse.Content.ReadFromJsonAsync<AccountDto>();
        Assert.NotNull(creditCard);

        var transactionResponse = await client.PostAsJsonAsync("/api/accounts", new CreateAccountRequest("Inactive Offset", nameof(AccountType.Transaction)));
        transactionResponse.EnsureSuccessStatusCode();
        var inactiveOffset = await transactionResponse.Content.ReadFromJsonAsync<AccountDto>();
        Assert.NotNull(inactiveOffset);

        var deactivateResponse = await client.PutAsJsonAsync($"/api/accounts/{inactiveOffset.Id}", new UpdateAccountRequest(inactiveOffset.Name, false));
        deactivateResponse.EnsureSuccessStatusCode();

        var mortgageCreateResponse = await client.PostAsJsonAsync("/api/mortgages", new CreateMortgageRequest(
            mortgageAccount.Id,
            0.0615m,
            [new InterestRatePeriodDto(new DateOnly(2024, 7, 1), null, 0.0615m)],
            []));
        mortgageCreateResponse.EnsureSuccessStatusCode();

        var creditCardUpdateResponse = await client.PutAsJsonAsync(
            $"/api/mortgages/{mortgageAccount.Id}/offset-accounts",
            new UpdateMortgageOffsetAccountsRequest([creditCard.Id]));
        Assert.Equal(HttpStatusCode.BadRequest, creditCardUpdateResponse.StatusCode);

        var inactiveUpdateResponse = await client.PutAsJsonAsync(
            $"/api/mortgages/{mortgageAccount.Id}/offset-accounts",
            new UpdateMortgageOffsetAccountsRequest([inactiveOffset.Id]));
        Assert.Equal(HttpStatusCode.BadRequest, inactiveUpdateResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteTransaction_WithValidId_RemovesTransactionAndReturnsNoContent()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var factory = new ApiTestFactory(connection);
        using var client = factory.CreateClient();

        // Create account and transaction
        var accountRequest = new CreateAccountRequest("Test Account", nameof(AccountType.Transaction));
        var accountResponse = await client.PostAsJsonAsync("/api/accounts", accountRequest);
        accountResponse.EnsureSuccessStatusCode();
        var account = await accountResponse.Content.ReadFromJsonAsync<AccountDto>();
        Assert.NotNull(account);

        Guid transactionId;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FinancesDashboardDbContext>();
            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                AccountId = account.Id,
                Date = new DateOnly(2026, 1, 15),
                Amount = 100m,
                Description = "Test Transaction",
                BankBalance = 1000m,
                ImportHash = "test-hash-delete-1",
                ImportedUtc = DateTime.UtcNow
            };
            dbContext.Transactions.Add(transaction);
            await dbContext.SaveChangesAsync();
            transactionId = transaction.Id;
        }

        // Delete the transaction
        var deleteResponse = await client.DeleteAsync($"/api/transactions/{transactionId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Verify transaction is deleted
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FinancesDashboardDbContext>();
            var deletedTransaction = await dbContext.Transactions.FirstOrDefaultAsync(t => t.Id == transactionId);
            Assert.Null(deletedTransaction);
        }
    }

    [Fact]
    public async Task DeleteTransaction_WithNonExistentId_ReturnsNotFound()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var factory = new ApiTestFactory(connection);
        using var client = factory.CreateClient();

        var nonExistentId = Guid.NewGuid();
        var response = await client.DeleteAsync($"/api/transactions/{nonExistentId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteTransaction_AllowsReimportOfSameCsvData()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var factory = new ApiTestFactory(connection);
        using var client = factory.CreateClient();

        // Create account
        var accountRequest = new CreateAccountRequest("Test Account", nameof(AccountType.Transaction));
        var accountResponse = await client.PostAsJsonAsync("/api/accounts", accountRequest);
        accountResponse.EnsureSuccessStatusCode();
        var account = await accountResponse.Content.ReadFromJsonAsync<AccountDto>();
        Assert.NotNull(account);

        Guid transactionId;
        string importHash = "reimport-test-hash-1";

        // Create a transaction
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FinancesDashboardDbContext>();
            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                AccountId = account.Id,
                Date = new DateOnly(2026, 1, 15),
                Amount = 250m,
                Description = "Reimport Test",
                BankBalance = 5000m,
                ImportHash = importHash,
                ImportedUtc = DateTime.UtcNow
            };
            dbContext.Transactions.Add(transaction);
            await dbContext.SaveChangesAsync();
            transactionId = transaction.Id;
        }

        // Delete the transaction
        var deleteResponse = await client.DeleteAsync($"/api/transactions/{transactionId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Verify it's deleted
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FinancesDashboardDbContext>();
            var deletedTransaction = await dbContext.Transactions.FirstOrDefaultAsync(t => t.Id == transactionId);
            Assert.Null(deletedTransaction);
        }

        // Now reimport via CSV import - should succeed because the original transaction's hash is gone
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FinancesDashboardDbContext>();
            var importer = new CbaCsvImporter(dbContext);

            var csvContent = "15/01/2026,250,Reimport Test,5000\n";
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            await writer.WriteAsync(csvContent);
            await writer.FlushAsync();
            stream.Seek(0, SeekOrigin.Begin);

            var result = await importer.ImportAsync(account.Id, stream);

            // Should successfully import
            Assert.Equal(1, result.Imported);
            Assert.Equal(0, result.Skipped);
            Assert.Empty(result.Errors);

            // Verify a new transaction was added with matching data
            var reimportedTransactions = await dbContext.Transactions
                .Where(t => t.AccountId == account.Id && t.Description == "Reimport Test")
                .ToListAsync();

            Assert.Single(reimportedTransactions);
            var reimportedTransaction = reimportedTransactions[0];
            Assert.Equal(new DateOnly(2026, 1, 15), reimportedTransaction.Date);
            Assert.Equal(250m, reimportedTransaction.Amount);
            Assert.Equal(5000m, reimportedTransaction.BankBalance);
        }
    }

    [Fact]
    public async Task DeleteTransaction_DoesNotAffectOtherTransactionsInAccount()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var factory = new ApiTestFactory(connection);
        using var client = factory.CreateClient();

        // Create account
        var accountRequest = new CreateAccountRequest("Test Account", nameof(AccountType.Transaction));
        var accountResponse = await client.PostAsJsonAsync("/api/accounts", accountRequest);
        accountResponse.EnsureSuccessStatusCode();
        var account = await accountResponse.Content.ReadFromJsonAsync<AccountDto>();
        Assert.NotNull(account);

        Guid transactionToDeleteId;
        List<Guid> remainingTransactionIds = [];

        // Create multiple transactions
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FinancesDashboardDbContext>();

            var transaction1 = new Transaction
            {
                Id = Guid.NewGuid(),
                AccountId = account.Id,
                Date = new DateOnly(2026, 1, 1),
                Amount = 100m,
                Description = "Transaction 1",
                BankBalance = 1000m,
                ImportHash = "hash-multi-1",
                ImportedUtc = DateTime.UtcNow
            };

            var transaction2 = new Transaction
            {
                Id = Guid.NewGuid(),
                AccountId = account.Id,
                Date = new DateOnly(2026, 1, 2),
                Amount = 200m,
                Description = "Transaction 2",
                BankBalance = 1200m,
                ImportHash = "hash-multi-2",
                ImportedUtc = DateTime.UtcNow
            };

            var transaction3 = new Transaction
            {
                Id = Guid.NewGuid(),
                AccountId = account.Id,
                Date = new DateOnly(2026, 1, 3),
                Amount = 300m,
                Description = "Transaction 3",
                BankBalance = 1500m,
                ImportHash = "hash-multi-3",
                ImportedUtc = DateTime.UtcNow
            };

            dbContext.Transactions.Add(transaction1);
            dbContext.Transactions.Add(transaction2);
            dbContext.Transactions.Add(transaction3);
            await dbContext.SaveChangesAsync();

            transactionToDeleteId = transaction2.Id;
            remainingTransactionIds.Add(transaction1.Id);
            remainingTransactionIds.Add(transaction3.Id);
        }

        // Delete one transaction
        var deleteResponse = await client.DeleteAsync($"/api/transactions/{transactionToDeleteId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Verify only the correct transaction was deleted
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FinancesDashboardDbContext>();
            var deletedTransaction = await dbContext.Transactions.FirstOrDefaultAsync(t => t.Id == transactionToDeleteId);
            Assert.Null(deletedTransaction);

            var remainingTransactions = await dbContext.Transactions
                .Where(t => t.AccountId == account.Id)
                .ToListAsync();

            Assert.Equal(2, remainingTransactions.Count);
            Assert.Contains(remainingTransactions, t => t.Id == remainingTransactionIds[0]);
            Assert.Contains(remainingTransactions, t => t.Id == remainingTransactionIds[1]);
        }
    }

    [Fact]
    public async Task GetAccountTransactions_AfterDeletion_DoesNotIncludeDeletedTransaction()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var factory = new ApiTestFactory(connection);
        using var client = factory.CreateClient();

        // Create account
        var accountRequest = new CreateAccountRequest("Test Account", nameof(AccountType.Transaction));
        var accountResponse = await client.PostAsJsonAsync("/api/accounts", accountRequest);
        accountResponse.EnsureSuccessStatusCode();
        var account = await accountResponse.Content.ReadFromJsonAsync<AccountDto>();
        Assert.NotNull(account);

        Guid transactionId;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FinancesDashboardDbContext>();
            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                AccountId = account.Id,
                Date = new DateOnly(2026, 1, 15),
                Amount = 100m,
                Description = "Test Transaction",
                BankBalance = 1000m,
                ImportHash = "test-hash-get",
                ImportedUtc = DateTime.UtcNow
            };
            dbContext.Transactions.Add(transaction);
            await dbContext.SaveChangesAsync();
            transactionId = transaction.Id;
        }

        // Delete the transaction
        var deleteResponse = await client.DeleteAsync($"/api/transactions/{transactionId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Get transactions for the account
        var getResponse = await client.GetAsync($"/api/accounts/{account.Id}/transactions");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var transactions = await getResponse.Content.ReadFromJsonAsync<List<Transaction>>();
        Assert.NotNull(transactions);
        Assert.Empty(transactions);
    }

    [Fact]
    public async Task GetAccountTransactions_WithDateFromFilter_IncludesOnlyTransactionsOnOrAfterDate()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var factory = new ApiTestFactory(connection);
        using var client = factory.CreateClient();

        var accountRequest = new CreateAccountRequest("Test Savings", nameof(AccountType.Savings));
        var accountResponse = await client.PostAsJsonAsync("/api/accounts", accountRequest);
        var account = await accountResponse.Content.ReadFromJsonAsync<AccountDto>();
        Assert.NotNull(account);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FinancesDashboardDbContext>();
            dbContext.Transactions.AddRange(
                new Transaction { Id = Guid.NewGuid(), AccountId = account.Id, Date = new DateOnly(2024, 1, 1), Amount = 100m, Description = "Early", BankBalance = 100m, ImportHash = "t1", ImportedUtc = DateTime.UtcNow },
                new Transaction { Id = Guid.NewGuid(), AccountId = account.Id, Date = new DateOnly(2024, 6, 15), Amount = 200m, Description = "Mid", BankBalance = 300m, ImportHash = "t2", ImportedUtc = DateTime.UtcNow },
                new Transaction { Id = Guid.NewGuid(), AccountId = account.Id, Date = new DateOnly(2024, 12, 31), Amount = 300m, Description = "Late", BankBalance = 600m, ImportHash = "t3", ImportedUtc = DateTime.UtcNow });
            await dbContext.SaveChangesAsync();
        }

        var response = await client.GetAsync($"/api/accounts/{account.Id}/transactions?dateFrom=2024-06-15");
        response.EnsureSuccessStatusCode();

        var transactions = await response.Content.ReadFromJsonAsync<List<Transaction>>();
        Assert.NotNull(transactions);
        Assert.Equal(2, transactions.Count);
        Assert.All(transactions, t => Assert.True(t.Date >= new DateOnly(2024, 6, 15)));
    }

    [Fact]
    public async Task GetAccountTransactions_WithDateToFilter_IncludesOnlyTransactionsOnOrBeforeDate()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var factory = new ApiTestFactory(connection);
        using var client = factory.CreateClient();

        var accountRequest = new CreateAccountRequest("Test Savings", nameof(AccountType.Savings));
        var accountResponse = await client.PostAsJsonAsync("/api/accounts", accountRequest);
        var account = await accountResponse.Content.ReadFromJsonAsync<AccountDto>();
        Assert.NotNull(account);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FinancesDashboardDbContext>();
            dbContext.Transactions.AddRange(
                new Transaction { Id = Guid.NewGuid(), AccountId = account.Id, Date = new DateOnly(2024, 1, 1), Amount = 100m, Description = "Early", BankBalance = 100m, ImportHash = "t1", ImportedUtc = DateTime.UtcNow },
                new Transaction { Id = Guid.NewGuid(), AccountId = account.Id, Date = new DateOnly(2024, 6, 15), Amount = 200m, Description = "Mid", BankBalance = 300m, ImportHash = "t2", ImportedUtc = DateTime.UtcNow },
                new Transaction { Id = Guid.NewGuid(), AccountId = account.Id, Date = new DateOnly(2024, 12, 31), Amount = 300m, Description = "Late", BankBalance = 600m, ImportHash = "t3", ImportedUtc = DateTime.UtcNow });
            await dbContext.SaveChangesAsync();
        }

        var response = await client.GetAsync($"/api/accounts/{account.Id}/transactions?dateTo=2024-06-15");
        response.EnsureSuccessStatusCode();

        var transactions = await response.Content.ReadFromJsonAsync<List<Transaction>>();
        Assert.NotNull(transactions);
        Assert.Equal(2, transactions.Count);
        Assert.All(transactions, t => Assert.True(t.Date <= new DateOnly(2024, 6, 15)));
    }

    [Fact]
    public async Task GetAccountTransactions_WithDateRangeFilter_IncludesOnlyTransactionsWithinRange()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var factory = new ApiTestFactory(connection);
        using var client = factory.CreateClient();

        var accountRequest = new CreateAccountRequest("Test Savings", nameof(AccountType.Savings));
        var accountResponse = await client.PostAsJsonAsync("/api/accounts", accountRequest);
        var account = await accountResponse.Content.ReadFromJsonAsync<AccountDto>();
        Assert.NotNull(account);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FinancesDashboardDbContext>();
            dbContext.Transactions.AddRange(
                new Transaction { Id = Guid.NewGuid(), AccountId = account.Id, Date = new DateOnly(2024, 1, 1), Amount = 100m, Description = "Early", BankBalance = 100m, ImportHash = "t1", ImportedUtc = DateTime.UtcNow },
                new Transaction { Id = Guid.NewGuid(), AccountId = account.Id, Date = new DateOnly(2024, 6, 15), Amount = 200m, Description = "Mid", BankBalance = 300m, ImportHash = "t2", ImportedUtc = DateTime.UtcNow },
                new Transaction { Id = Guid.NewGuid(), AccountId = account.Id, Date = new DateOnly(2024, 12, 31), Amount = 300m, Description = "Late", BankBalance = 600m, ImportHash = "t3", ImportedUtc = DateTime.UtcNow });
            await dbContext.SaveChangesAsync();
        }

        var response = await client.GetAsync($"/api/accounts/{account.Id}/transactions?dateFrom=2024-03-01&dateTo=2024-09-30");
        response.EnsureSuccessStatusCode();

        var transactions = await response.Content.ReadFromJsonAsync<List<Transaction>>();
        Assert.NotNull(transactions);
        Assert.Single(transactions);
        Assert.Equal("Mid", transactions[0].Description);
    }

    [Fact]
    public async Task GetAccountTransactions_WithDescriptionFilter_MatchesCaseInsensitive()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var factory = new ApiTestFactory(connection);
        using var client = factory.CreateClient();

        var accountRequest = new CreateAccountRequest("Test Savings", nameof(AccountType.Savings));
        var accountResponse = await client.PostAsJsonAsync("/api/accounts", accountRequest);
        var account = await accountResponse.Content.ReadFromJsonAsync<AccountDto>();
        Assert.NotNull(account);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FinancesDashboardDbContext>();
            dbContext.Transactions.AddRange(
                new Transaction { Id = Guid.NewGuid(), AccountId = account.Id, Date = new DateOnly(2024, 1, 1), Amount = 100m, Description = "Salary Deposit", BankBalance = 100m, ImportHash = "t1", ImportedUtc = DateTime.UtcNow },
                new Transaction { Id = Guid.NewGuid(), AccountId = account.Id, Date = new DateOnly(2024, 2, 1), Amount = -50m, Description = "Rent Payment", BankBalance = 50m, ImportHash = "t2", ImportedUtc = DateTime.UtcNow },
                new Transaction { Id = Guid.NewGuid(), AccountId = account.Id, Date = new DateOnly(2024, 3, 1), Amount = -30m, Description = "Coffee", BankBalance = 20m, ImportHash = "t3", ImportedUtc = DateTime.UtcNow });
            await dbContext.SaveChangesAsync();
        }

        var response = await client.GetAsync($"/api/accounts/{account.Id}/transactions?descriptionContains=salary");
        response.EnsureSuccessStatusCode();

        var transactions = await response.Content.ReadFromJsonAsync<List<Transaction>>();
        Assert.NotNull(transactions);
        Assert.Single(transactions);
        Assert.Equal("Salary Deposit", transactions[0].Description);
    }

    [Fact]
    public async Task GetAccountTransactions_WithMinAmountFilter_IncludesOnlyTransactionsGreaterThanOrEqual()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var factory = new ApiTestFactory(connection);
        using var client = factory.CreateClient();

        var accountRequest = new CreateAccountRequest("Test Savings", nameof(AccountType.Savings));
        var accountResponse = await client.PostAsJsonAsync("/api/accounts", accountRequest);
        var account = await accountResponse.Content.ReadFromJsonAsync<AccountDto>();
        Assert.NotNull(account);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FinancesDashboardDbContext>();
            dbContext.Transactions.AddRange(
                new Transaction { Id = Guid.NewGuid(), AccountId = account.Id, Date = new DateOnly(2024, 1, 1), Amount = 50m, Description = "Small", BankBalance = 50m, ImportHash = "t1", ImportedUtc = DateTime.UtcNow },
                new Transaction { Id = Guid.NewGuid(), AccountId = account.Id, Date = new DateOnly(2024, 2, 1), Amount = 200m, Description = "Medium", BankBalance = 250m, ImportHash = "t2", ImportedUtc = DateTime.UtcNow },
                new Transaction { Id = Guid.NewGuid(), AccountId = account.Id, Date = new DateOnly(2024, 3, 1), Amount = 1000m, Description = "Large", BankBalance = 1250m, ImportHash = "t3", ImportedUtc = DateTime.UtcNow });
            await dbContext.SaveChangesAsync();
        }

        var response = await client.GetAsync($"/api/accounts/{account.Id}/transactions?minAmount=200");
        response.EnsureSuccessStatusCode();

        var transactions = await response.Content.ReadFromJsonAsync<List<Transaction>>();
        Assert.NotNull(transactions);
        Assert.Equal(2, transactions.Count);
        Assert.All(transactions, t => Assert.True(t.Amount >= 200m));
    }

    [Fact]
    public async Task GetAccountTransactions_WithMaxAmountFilter_IncludesOnlyTransactionsLessThanOrEqual()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var factory = new ApiTestFactory(connection);
        using var client = factory.CreateClient();

        var accountRequest = new CreateAccountRequest("Test Savings", nameof(AccountType.Savings));
        var accountResponse = await client.PostAsJsonAsync("/api/accounts", accountRequest);
        var account = await accountResponse.Content.ReadFromJsonAsync<AccountDto>();
        Assert.NotNull(account);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FinancesDashboardDbContext>();
            dbContext.Transactions.AddRange(
                new Transaction { Id = Guid.NewGuid(), AccountId = account.Id, Date = new DateOnly(2024, 1, 1), Amount = 50m, Description = "Small", BankBalance = 50m, ImportHash = "t1", ImportedUtc = DateTime.UtcNow },
                new Transaction { Id = Guid.NewGuid(), AccountId = account.Id, Date = new DateOnly(2024, 2, 1), Amount = 200m, Description = "Medium", BankBalance = 250m, ImportHash = "t2", ImportedUtc = DateTime.UtcNow },
                new Transaction { Id = Guid.NewGuid(), AccountId = account.Id, Date = new DateOnly(2024, 3, 1), Amount = 1000m, Description = "Large", BankBalance = 1250m, ImportHash = "t3", ImportedUtc = DateTime.UtcNow });
            await dbContext.SaveChangesAsync();
        }

        var response = await client.GetAsync($"/api/accounts/{account.Id}/transactions?maxAmount=200");
        response.EnsureSuccessStatusCode();

        var transactions = await response.Content.ReadFromJsonAsync<List<Transaction>>();
        Assert.NotNull(transactions);
        Assert.Equal(2, transactions.Count);
        Assert.All(transactions, t => Assert.True(t.Amount <= 200m));
    }

    [Fact]
    public async Task GetAccountTransactions_WithDirectionFilterPositive_IncludesOnlyPositiveAmounts()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var factory = new ApiTestFactory(connection);
        using var client = factory.CreateClient();

        var accountRequest = new CreateAccountRequest("Test Savings", nameof(AccountType.Savings));
        var accountResponse = await client.PostAsJsonAsync("/api/accounts", accountRequest);
        var account = await accountResponse.Content.ReadFromJsonAsync<AccountDto>();
        Assert.NotNull(account);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FinancesDashboardDbContext>();
            dbContext.Transactions.AddRange(
                new Transaction { Id = Guid.NewGuid(), AccountId = account.Id, Date = new DateOnly(2024, 1, 1), Amount = 100m, Description = "Deposit", BankBalance = 100m, ImportHash = "t1", ImportedUtc = DateTime.UtcNow },
                new Transaction { Id = Guid.NewGuid(), AccountId = account.Id, Date = new DateOnly(2024, 2, 1), Amount = -50m, Description = "Withdrawal", BankBalance = 50m, ImportHash = "t2", ImportedUtc = DateTime.UtcNow },
                new Transaction { Id = Guid.NewGuid(), AccountId = account.Id, Date = new DateOnly(2024, 3, 1), Amount = 75m, Description = "Deposit 2", BankBalance = 125m, ImportHash = "t3", ImportedUtc = DateTime.UtcNow });
            await dbContext.SaveChangesAsync();
        }

        var response = await client.GetAsync($"/api/accounts/{account.Id}/transactions?transactionDirection=Positive");
        response.EnsureSuccessStatusCode();

        var transactions = await response.Content.ReadFromJsonAsync<List<Transaction>>();
        Assert.NotNull(transactions);
        Assert.Equal(2, transactions.Count);
        Assert.All(transactions, t => Assert.True(t.Amount > 0));
    }

    [Fact]
    public async Task GetAccountTransactions_WithDirectionFilterNegative_IncludesOnlyNegativeAmounts()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var factory = new ApiTestFactory(connection);
        using var client = factory.CreateClient();

        var accountRequest = new CreateAccountRequest("Test Savings", nameof(AccountType.Savings));
        var accountResponse = await client.PostAsJsonAsync("/api/accounts", accountRequest);
        var account = await accountResponse.Content.ReadFromJsonAsync<AccountDto>();
        Assert.NotNull(account);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FinancesDashboardDbContext>();
            dbContext.Transactions.AddRange(
                new Transaction { Id = Guid.NewGuid(), AccountId = account.Id, Date = new DateOnly(2024, 1, 1), Amount = 100m, Description = "Deposit", BankBalance = 100m, ImportHash = "t1", ImportedUtc = DateTime.UtcNow },
                new Transaction { Id = Guid.NewGuid(), AccountId = account.Id, Date = new DateOnly(2024, 2, 1), Amount = -50m, Description = "Withdrawal 1", BankBalance = 50m, ImportHash = "t2", ImportedUtc = DateTime.UtcNow },
                new Transaction { Id = Guid.NewGuid(), AccountId = account.Id, Date = new DateOnly(2024, 3, 1), Amount = -30m, Description = "Withdrawal 2", BankBalance = 20m, ImportHash = "t3", ImportedUtc = DateTime.UtcNow });
            await dbContext.SaveChangesAsync();
        }

        var response = await client.GetAsync($"/api/accounts/{account.Id}/transactions?transactionDirection=Negative");
        response.EnsureSuccessStatusCode();

        var transactions = await response.Content.ReadFromJsonAsync<List<Transaction>>();
        Assert.NotNull(transactions);
        Assert.Equal(2, transactions.Count);
        Assert.All(transactions, t => Assert.True(t.Amount < 0));
    }

    [Fact]
    public async Task GetAccountTransactions_WithSortByDateDescending_ReturnsMostRecentFirst()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var factory = new ApiTestFactory(connection);
        using var client = factory.CreateClient();

        var accountRequest = new CreateAccountRequest("Test Savings", nameof(AccountType.Savings));
        var accountResponse = await client.PostAsJsonAsync("/api/accounts", accountRequest);
        var account = await accountResponse.Content.ReadFromJsonAsync<AccountDto>();
        Assert.NotNull(account);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FinancesDashboardDbContext>();
            dbContext.Transactions.AddRange(
                new Transaction { Id = Guid.NewGuid(), AccountId = account.Id, Date = new DateOnly(2024, 1, 1), Amount = 100m, Description = "Old", BankBalance = 100m, ImportHash = "t1", ImportedUtc = DateTime.UtcNow },
                new Transaction { Id = Guid.NewGuid(), AccountId = account.Id, Date = new DateOnly(2024, 6, 15), Amount = 200m, Description = "Middle", BankBalance = 300m, ImportHash = "t2", ImportedUtc = DateTime.UtcNow },
                new Transaction { Id = Guid.NewGuid(), AccountId = account.Id, Date = new DateOnly(2024, 12, 31), Amount = 300m, Description = "Recent", BankBalance = 600m, ImportHash = "t3", ImportedUtc = DateTime.UtcNow });
            await dbContext.SaveChangesAsync();
        }

        var response = await client.GetAsync($"/api/accounts/{account.Id}/transactions?sortBy=Date&sortOrder=Desc");
        response.EnsureSuccessStatusCode();

        var transactions = await response.Content.ReadFromJsonAsync<List<Transaction>>();
        Assert.NotNull(transactions);
        Assert.Equal(3, transactions.Count);
        Assert.Equal("Recent", transactions[0].Description);
        Assert.Equal("Middle", transactions[1].Description);
        Assert.Equal("Old", transactions[2].Description);
    }

    [Fact]
    public async Task GetAccountTransactions_WithSortByDateAscending_ReturnsOldestFirst()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var factory = new ApiTestFactory(connection);
        using var client = factory.CreateClient();

        var accountRequest = new CreateAccountRequest("Test Savings", nameof(AccountType.Savings));
        var accountResponse = await client.PostAsJsonAsync("/api/accounts", accountRequest);
        var account = await accountResponse.Content.ReadFromJsonAsync<AccountDto>();
        Assert.NotNull(account);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FinancesDashboardDbContext>();
            dbContext.Transactions.AddRange(
                new Transaction { Id = Guid.NewGuid(), AccountId = account.Id, Date = new DateOnly(2024, 1, 1), Amount = 100m, Description = "Old", BankBalance = 100m, ImportHash = "t1", ImportedUtc = DateTime.UtcNow },
                new Transaction { Id = Guid.NewGuid(), AccountId = account.Id, Date = new DateOnly(2024, 6, 15), Amount = 200m, Description = "Middle", BankBalance = 300m, ImportHash = "t2", ImportedUtc = DateTime.UtcNow },
                new Transaction { Id = Guid.NewGuid(), AccountId = account.Id, Date = new DateOnly(2024, 12, 31), Amount = 300m, Description = "Recent", BankBalance = 600m, ImportHash = "t3", ImportedUtc = DateTime.UtcNow });
            await dbContext.SaveChangesAsync();
        }

        var response = await client.GetAsync($"/api/accounts/{account.Id}/transactions?sortBy=Date&sortOrder=Asc");
        response.EnsureSuccessStatusCode();

        var transactions = await response.Content.ReadFromJsonAsync<List<Transaction>>();
        Assert.NotNull(transactions);
        Assert.Equal(3, transactions.Count);
        Assert.Equal("Old", transactions[0].Description);
        Assert.Equal("Middle", transactions[1].Description);
        Assert.Equal("Recent", transactions[2].Description);
    }

    [Fact]
    public async Task GetAccountTransactions_WithSortByAmount_SortsByAmountCorrectly()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var factory = new ApiTestFactory(connection);
        using var client = factory.CreateClient();

        var accountRequest = new CreateAccountRequest("Test Savings", nameof(AccountType.Savings));
        var accountResponse = await client.PostAsJsonAsync("/api/accounts", accountRequest);
        var account = await accountResponse.Content.ReadFromJsonAsync<AccountDto>();
        Assert.NotNull(account);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FinancesDashboardDbContext>();
            dbContext.Transactions.AddRange(
                new Transaction { Id = Guid.NewGuid(), AccountId = account.Id, Date = new DateOnly(2024, 1, 1), Amount = 100m, Description = "A", BankBalance = 100m, ImportHash = "t1", ImportedUtc = DateTime.UtcNow },
                new Transaction { Id = Guid.NewGuid(), AccountId = account.Id, Date = new DateOnly(2024, 2, 1), Amount = 300m, Description = "C", BankBalance = 400m, ImportHash = "t2", ImportedUtc = DateTime.UtcNow },
                new Transaction { Id = Guid.NewGuid(), AccountId = account.Id, Date = new DateOnly(2024, 3, 1), Amount = 200m, Description = "B", BankBalance = 600m, ImportHash = "t3", ImportedUtc = DateTime.UtcNow });
            await dbContext.SaveChangesAsync();
        }

        var response = await client.GetAsync($"/api/accounts/{account.Id}/transactions?sortBy=Amount&sortOrder=Desc");
        response.EnsureSuccessStatusCode();

        var transactions = await response.Content.ReadFromJsonAsync<List<Transaction>>();
        Assert.NotNull(transactions);
        Assert.Equal(3, transactions.Count);
        Assert.Equal(300m, transactions[0].Amount);
        Assert.Equal(200m, transactions[1].Amount);
        Assert.Equal(100m, transactions[2].Amount);
    }

    [Fact]
    public async Task GetAccountTransactions_WithSortByDescription_SortsByDescriptionCorrectly()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var factory = new ApiTestFactory(connection);
        using var client = factory.CreateClient();

        var accountRequest = new CreateAccountRequest("Test Savings", nameof(AccountType.Savings));
        var accountResponse = await client.PostAsJsonAsync("/api/accounts", accountRequest);
        var account = await accountResponse.Content.ReadFromJsonAsync<AccountDto>();
        Assert.NotNull(account);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FinancesDashboardDbContext>();
            dbContext.Transactions.AddRange(
                new Transaction { Id = Guid.NewGuid(), AccountId = account.Id, Date = new DateOnly(2024, 1, 1), Amount = 100m, Description = "Zebra", BankBalance = 100m, ImportHash = "t1", ImportedUtc = DateTime.UtcNow },
                new Transaction { Id = Guid.NewGuid(), AccountId = account.Id, Date = new DateOnly(2024, 2, 1), Amount = 200m, Description = "Apple", BankBalance = 300m, ImportHash = "t2", ImportedUtc = DateTime.UtcNow },
                new Transaction { Id = Guid.NewGuid(), AccountId = account.Id, Date = new DateOnly(2024, 3, 1), Amount = 300m, Description = "Mango", BankBalance = 600m, ImportHash = "t3", ImportedUtc = DateTime.UtcNow });
            await dbContext.SaveChangesAsync();
        }

        var response = await client.GetAsync($"/api/accounts/{account.Id}/transactions?sortBy=Description&sortOrder=Asc");
        response.EnsureSuccessStatusCode();

        var transactions = await response.Content.ReadFromJsonAsync<List<Transaction>>();
        Assert.NotNull(transactions);
        Assert.Equal(3, transactions.Count);
        Assert.Equal("Apple", transactions[0].Description);
        Assert.Equal("Mango", transactions[1].Description);
        Assert.Equal("Zebra", transactions[2].Description);
    }

    [Fact]
    public async Task GetAccountTransactions_WithCombinedFilters_AppliesAllFiltersCorrectly()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var factory = new ApiTestFactory(connection);
        using var client = factory.CreateClient();

        var accountRequest = new CreateAccountRequest("Test Savings", nameof(AccountType.Savings));
        var accountResponse = await client.PostAsJsonAsync("/api/accounts", accountRequest);
        var account = await accountResponse.Content.ReadFromJsonAsync<AccountDto>();
        Assert.NotNull(account);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FinancesDashboardDbContext>();
            dbContext.Transactions.AddRange(
                new Transaction { Id = Guid.NewGuid(), AccountId = account.Id, Date = new DateOnly(2024, 1, 15), Amount = 50m, Description = "Salary", BankBalance = 50m, ImportHash = "t1", ImportedUtc = DateTime.UtcNow },
                new Transaction { Id = Guid.NewGuid(), AccountId = account.Id, Date = new DateOnly(2024, 2, 15), Amount = 150m, Description = "Bonus", BankBalance = 200m, ImportHash = "t2", ImportedUtc = DateTime.UtcNow },
                new Transaction { Id = Guid.NewGuid(), AccountId = account.Id, Date = new DateOnly(2024, 3, 15), Amount = 200m, Description = "Salary", BankBalance = 400m, ImportHash = "t3", ImportedUtc = DateTime.UtcNow },
                new Transaction { Id = Guid.NewGuid(), AccountId = account.Id, Date = new DateOnly(2024, 4, 15), Amount = -100m, Description = "Withdrawal", BankBalance = 300m, ImportHash = "t4", ImportedUtc = DateTime.UtcNow });
            await dbContext.SaveChangesAsync();
        }

        var response = await client.GetAsync($"/api/accounts/{account.Id}/transactions?dateFrom=2024-02-01&dateTo=2024-03-31&descriptionContains=salary&minAmount=100&transactionDirection=Positive&sortBy=Date&sortOrder=Asc");
        response.EnsureSuccessStatusCode();

        var transactions = await response.Content.ReadFromJsonAsync<List<Transaction>>();
        Assert.NotNull(transactions);
        Assert.Single(transactions);
        Assert.Equal(new DateOnly(2024, 3, 15), transactions[0].Date);
        Assert.Equal("Salary", transactions[0].Description);
        Assert.Equal(200m, transactions[0].Amount);
    }

    [Fact]
    public async Task GetAccountTransactions_WithNoMatchingFilters_ReturnsEmptyList()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var factory = new ApiTestFactory(connection);
        using var client = factory.CreateClient();

        var accountRequest = new CreateAccountRequest("Test Savings", nameof(AccountType.Savings));
        var accountResponse = await client.PostAsJsonAsync("/api/accounts", accountRequest);
        var account = await accountResponse.Content.ReadFromJsonAsync<AccountDto>();
        Assert.NotNull(account);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FinancesDashboardDbContext>();
            dbContext.Transactions.AddRange(
                new Transaction { Id = Guid.NewGuid(), AccountId = account.Id, Date = new DateOnly(2024, 1, 1), Amount = 100m, Description = "Salary", BankBalance = 100m, ImportHash = "t1", ImportedUtc = DateTime.UtcNow },
                new Transaction { Id = Guid.NewGuid(), AccountId = account.Id, Date = new DateOnly(2024, 2, 1), Amount = 200m, Description = "Bonus", BankBalance = 300m, ImportHash = "t2", ImportedUtc = DateTime.UtcNow });
            await dbContext.SaveChangesAsync();
        }

        var response = await client.GetAsync($"/api/accounts/{account.Id}/transactions?descriptionContains=nonexistent");
        response.EnsureSuccessStatusCode();

        var transactions = await response.Content.ReadFromJsonAsync<List<Transaction>>();
        Assert.NotNull(transactions);
        Assert.Empty(transactions);
    }

    [Fact]
    public async Task GetAccountTransactions_WithInvalidDateRange_ReturnsBadRequest()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var factory = new ApiTestFactory(connection);
        using var client = factory.CreateClient();

        var accountRequest = new CreateAccountRequest("Test Savings", nameof(AccountType.Savings));
        var accountResponse = await client.PostAsJsonAsync("/api/accounts", accountRequest);
        var account = await accountResponse.Content.ReadFromJsonAsync<AccountDto>();
        Assert.NotNull(account);

        var response = await client.GetAsync($"/api/accounts/{account.Id}/transactions?dateFrom=2024-12-31&dateTo=2024-01-01");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetAccountTransactions_WithInvalidSortColumn_ReturnsBadRequest()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var factory = new ApiTestFactory(connection);
        using var client = factory.CreateClient();

        var accountRequest = new CreateAccountRequest("Test Savings", nameof(AccountType.Savings));
        var accountResponse = await client.PostAsJsonAsync("/api/accounts", accountRequest);
        var account = await accountResponse.Content.ReadFromJsonAsync<AccountDto>();
        Assert.NotNull(account);

        var response = await client.GetAsync($"/api/accounts/{account.Id}/transactions?sortBy=InvalidColumn");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static SqliteConnection CreateOpenSqliteConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    private static FileStream OpenFixtureStream(string fixtureName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureName);
        return File.OpenRead(path);
    }

    private sealed class ApiTestFactory(SqliteConnection connection) : WebApplicationFactory<ApiService::Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<FinancesDashboardDbContext>>();

                services.AddSingleton(connection);
                services.AddDbContext<FinancesDashboardDbContext>((serviceProvider, options) =>
                {
                    var sqliteConnection = serviceProvider.GetRequiredService<SqliteConnection>();
                    options.UseSqlite(sqliteConnection);
                });
            });
        }
    }
}
