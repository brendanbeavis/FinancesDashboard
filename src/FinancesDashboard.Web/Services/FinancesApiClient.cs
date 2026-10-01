using FinancesDashboard.Web.Models;

namespace FinancesDashboard.Web.Services;

public sealed class FinancesApiClient(HttpClient httpClient)
{
    public async Task<IReadOnlyList<AccountModel>> GetAccountsAsync(CancellationToken cancellationToken = default)
    {
        return await httpClient.GetFromJsonAsync<List<AccountModel>>("/api/accounts", cancellationToken) ?? [];
    }

    public async Task<AccountModel> CreateAccountAsync(CreateAccountRequestModel request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("/api/accounts", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var account = await response.Content.ReadFromJsonAsync<AccountModel>(cancellationToken);
        return account ?? throw new InvalidOperationException("Account response payload was empty.");
    }

    public async Task<ImportResultModel> ImportCbaAsync(Guid accountId, Stream fileStream, string fileName, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(fileStream), "file", fileName);

        var response = await httpClient.PostAsync($"/api/import/cba/{accountId}", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ImportResultModel>(cancellationToken);
        return result ?? throw new InvalidOperationException("Import response payload was empty.");
    }

    public async Task<DashboardSummaryModel> GetDashboardSummaryAsync(CancellationToken cancellationToken = default)
    {
        var summary = await httpClient.GetFromJsonAsync<DashboardSummaryModel>("/api/dashboard/summary", cancellationToken);
        return summary ?? throw new InvalidOperationException("Dashboard summary payload was empty.");
    }

    public async Task<IReadOnlyList<BalancePointModel>> GetBalanceHistoryAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        return await httpClient.GetFromJsonAsync<List<BalancePointModel>>($"/api/dashboard/balance?accountId={accountId}", cancellationToken) ?? [];
    }

    public async Task<IReadOnlyList<MortgageModel>> GetMortgagesAsync(CancellationToken cancellationToken = default)
    {
        return await httpClient.GetFromJsonAsync<List<MortgageModel>>("/api/mortgages", cancellationToken) ?? [];
    }

    public async Task<MortgageModel> CreateMortgageAsync(CreateMortgageRequestModel request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("/api/mortgages", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var mortgage = await response.Content.ReadFromJsonAsync<MortgageModel>(cancellationToken);
        return mortgage ?? throw new InvalidOperationException("Mortgage response payload was empty.");
    }
}
