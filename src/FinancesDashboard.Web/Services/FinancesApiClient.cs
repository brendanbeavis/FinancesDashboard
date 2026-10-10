using FinancesDashboard.Web.Models;

namespace FinancesDashboard.Web.Services;

public sealed class FinancesApiClient(HttpClient httpClient)
{
    public async Task<IReadOnlyList<AccountModel>> GetAccountsAsync(CancellationToken cancellationToken = default)
    {
        return await httpClient.GetFromJsonAsync<List<AccountModel>>("/api/accounts", cancellationToken) ?? [];
    }

    public async Task<IReadOnlyList<AccountModel>> GetMortgageAccountsAsync(CancellationToken cancellationToken = default)
    {
        var accounts = await GetAccountsAsync(cancellationToken);
        return accounts.Where(a => a.Type == "Mortgage").ToList();
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

    public async Task<ImportResultModel> ImportColesAsync(Guid accountId, Stream fileStream, string fileName, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(fileStream), "file", fileName);

        var response = await httpClient.PostAsync($"/api/import/coles/{accountId}", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ImportResultModel>(cancellationToken);
        return result ?? throw new InvalidOperationException("Import response payload was empty.");
    }

    public async Task<DashboardSummaryModel> GetDashboardSummaryAsync(CancellationToken cancellationToken = default)
    {
        var summary = await httpClient.GetFromJsonAsync<DashboardSummaryModel>("/api/dashboard/summary", cancellationToken);
        return summary ?? throw new InvalidOperationException("Dashboard summary payload was empty.");
    }

    public async Task<IReadOnlyList<AccountDashboardSummaryModel>> GetAccountSummariesAsync(CancellationToken cancellationToken = default)
    {
        return await httpClient.GetFromJsonAsync<List<AccountDashboardSummaryModel>>("/api/dashboard/account-summaries", cancellationToken) ?? [];
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

    public async Task<AccountModel> UpdateAccountAsync(Guid id, UpdateAccountRequestModel request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync($"/api/accounts/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var account = await response.Content.ReadFromJsonAsync<AccountModel>(cancellationToken);
        return account ?? throw new InvalidOperationException("Account response payload was empty.");
    }

    public async Task DeleteAccountAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync($"/api/accounts/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<TransactionModel>> GetAccountTransactionsAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        return await httpClient.GetFromJsonAsync<List<TransactionModel>>($"/api/accounts/{accountId}/transactions", cancellationToken) ?? [];
    }

    public async Task<IReadOnlyList<TransactionModel>> GetAccountTransactionsAsync(
        Guid accountId,
        DateOnly? dateFrom,
        DateOnly? dateTo,
        string? descriptionContains,
        decimal? minAmount,
        decimal? maxAmount,
        string? transactionDirection,
        string? sortBy,
        string? sortOrder,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new List<string>();

        if (dateFrom.HasValue)
        {
            queryParams.Add($"dateFrom={dateFrom:yyyy-MM-dd}");
        }

        if (dateTo.HasValue)
        {
            queryParams.Add($"dateTo={dateTo:yyyy-MM-dd}");
        }

        if (!string.IsNullOrWhiteSpace(descriptionContains))
        {
            queryParams.Add($"descriptionContains={Uri.EscapeDataString(descriptionContains)}");
        }

        if (minAmount.HasValue)
        {
            queryParams.Add($"minAmount={minAmount}");
        }

        if (maxAmount.HasValue)
        {
            queryParams.Add($"maxAmount={maxAmount}");
        }

        if (!string.IsNullOrWhiteSpace(transactionDirection))
        {
            queryParams.Add($"transactionDirection={Uri.EscapeDataString(transactionDirection)}");
        }

        if (!string.IsNullOrWhiteSpace(sortBy))
        {
            queryParams.Add($"sortBy={Uri.EscapeDataString(sortBy)}");
        }

        if (!string.IsNullOrWhiteSpace(sortOrder))
        {
            queryParams.Add($"sortOrder={Uri.EscapeDataString(sortOrder)}");
        }

        var queryString = queryParams.Count > 0 ? $"?{string.Join("&", queryParams)}" : "";
        var url = $"/api/accounts/{accountId}/transactions{queryString}";

        return await httpClient.GetFromJsonAsync<List<TransactionModel>>(url, cancellationToken) ?? [];
    }

    public async Task DeleteTransactionAsync(Guid transactionId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync($"/api/transactions/{transactionId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<MortgageHistoryPointModel>> GetMortgageHistoryAsync(Guid accountId, DateOnly? startDate = null, DateOnly? endDate = null, CancellationToken cancellationToken = default)
    {
        var url = $"/api/mortgages/{accountId}/history";

        var queryParams = new List<string>();
        if (startDate.HasValue)
        {
            queryParams.Add($"startDate={startDate.Value:yyyy-MM-dd}");
        }
        if (endDate.HasValue)
        {
            queryParams.Add($"endDate={endDate.Value:yyyy-MM-dd}");
        }

        if (queryParams.Count > 0)
        {
            url += "?" + string.Join("&", queryParams);
        }

        return await httpClient.GetFromJsonAsync<List<MortgageHistoryPointModel>>(url, cancellationToken) ?? [];
    }

    public async Task<MortgageCurrentPositionModel?> GetMortgageSummaryAsync(Guid accountId, DateOnly? startDate = null, DateOnly? endDate = null, CancellationToken cancellationToken = default)
    {
        var url = $"/api/mortgages/{accountId}/summary";

        var queryParams = new List<string>();
        if (startDate.HasValue)
        {
            queryParams.Add($"startDate={startDate.Value:yyyy-MM-dd}");
        }
        if (endDate.HasValue)
        {
            queryParams.Add($"endDate={endDate.Value:yyyy-MM-dd}");
        }

        if (queryParams.Count > 0)
        {
            url += "?" + string.Join("&", queryParams);
        }

        var summary = await httpClient.GetFromJsonAsync<SummaryResponse?>(url, cancellationToken);
        if (summary is null)
        {
            return null;
        }

        return new MortgageCurrentPositionModel
        {
            CurrentBalance = summary.CurrentBalance,
            TotalOffsetBalance = summary.TotalOffsetBalance,
            InterestBearingBalance = summary.InterestBearingBalance,
            EstimatedAnnualInterestRate = summary.EstimatedAnnualInterestRate,
            TotalRepayments = summary.TotalRepayments,
            LatestDate = summary.LatestDate ?? DateOnly.FromDateTime(DateTime.Today),
            TransactionCount = summary.TransactionCount,
            EarliestDate = summary.EarliestDate,
            OffsetAccountIds = summary.OffsetAccountIds
        };
    }

    public async Task<MortgageCurrentPositionModel?> UpdateMortgageOffsetAccountsAsync(
        Guid accountId,
        UpdateMortgageOffsetAccountsRequestModel request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync($"/api/mortgages/{accountId}/offset-accounts", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var summary = await response.Content.ReadFromJsonAsync<MortgageCurrentPositionModel>(cancellationToken);
        return summary;
    }

    public async Task<MortgageForecastResponseModel?> GenerateMortgageForecastAsync(
        Guid accountId,
        MortgageForecastRequestModel request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(
            $"/api/mortgages/{accountId}/forecast",
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var forecast = await response.Content.ReadFromJsonAsync<MortgageForecastResponseModel>(cancellationToken);
        return forecast;
    }

    private sealed record SummaryResponse(
        int TransactionCount,
        decimal CurrentBalance,
        decimal TotalOffsetBalance,
        decimal InterestBearingBalance,
        decimal TotalRepayments,
        decimal BalanceChange,
        decimal? EstimatedAnnualInterestRate,
        DateOnly? EarliestDate,
        DateOnly? LatestDate,
        IReadOnlyList<Guid> OffsetAccountIds);
}
