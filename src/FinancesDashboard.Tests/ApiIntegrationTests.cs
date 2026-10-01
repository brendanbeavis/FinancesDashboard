extern alias ApiService;

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FinancesDashboard.Application.CbaImport;
using FinancesDashboard.Application.Dto;
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
    public async Task PostMortgages_WithRateHistory_ReturnsProjectedAmortisation()
    {
        await using var connection = CreateOpenSqliteConnection();
        await using var factory = new ApiTestFactory(connection);
        using var client = factory.CreateClient();

        var request = new CreateMortgageRequest(
            "Home Loan",
            600000m,
            550000m,
            0.0615m,
            3200m,
            12,
            new DateOnly(2024, 7, 1),
            null,
            [new InterestRatePeriodDto(new DateOnly(2024, 7, 1), null, 0.0615m)]);

        var response = await client.PostAsJsonAsync("/api/mortgages", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var mortgage = await response.Content.ReadFromJsonAsync<MortgageDto>();
        Assert.NotNull(mortgage);
        Assert.Single(mortgage.InterestRateHistory);
        Assert.True(mortgage.Projection.IsProjected);
        Assert.NotEmpty(mortgage.Projection.Assumption);
        Assert.NotEmpty(mortgage.Projection.AmortisationSchedule);

        var listedResponse = await client.GetAsync("/api/mortgages");
        listedResponse.EnsureSuccessStatusCode();

        var mortgages = await listedResponse.Content.ReadFromJsonAsync<List<MortgageDto>>();
        Assert.NotNull(mortgages);
        var stored = Assert.Single(mortgages.Where(item => item.Id == mortgage.Id));
        Assert.Single(stored.InterestRateHistory);
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
