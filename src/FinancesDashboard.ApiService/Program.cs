using FinancesDashboard.Application.CbaImport;
using FinancesDashboard.Application.ColesCreditCardImport;
using FinancesDashboard.Application.Dto;
using FinancesDashboard.Application.Forecasting;
using FinancesDashboard.Application.Services;
using FinancesDashboard.Domain.Enums;
using FinancesDashboard.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

builder.Services.AddDbContext<FinancesDashboardDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("financesdb") ?? "Data Source=financesdashboard.db";
    options.UseSqlite(connectionString);
});

var forecastOptions = builder.Configuration
    .GetSection(ForecastOptions.SectionName)
    .Get<ForecastOptions>() ?? new ForecastOptions();

var mortgageProjectionOptions = builder.Configuration
    .GetSection(MortgageProjectionOptions.SectionName)
    .Get<MortgageProjectionOptions>() ?? new MortgageProjectionOptions();

builder.Services.AddSingleton(forecastOptions);
builder.Services.AddSingleton(mortgageProjectionOptions);
builder.Services.AddScoped<IForecastStrategy, AverageMonthlyNetCashflowForecastStrategy>();
builder.Services.AddScoped<MortgageAmortizationService>();
builder.Services.AddScoped<MortgageHistoryAnalysisService>();
builder.Services.AddScoped<RepaymentAnalysisService>();
builder.Services.AddScoped<MortgageAmortizationForecastService>();

builder.Services.AddScoped<CbaCsvImporter>();
builder.Services.AddScoped<ColesCreditCardCsvImporter>();
builder.Services.AddScoped<AccountApplicationService>();
builder.Services.AddScoped<CreditCardPositionService>();
builder.Services.AddScoped<DashboardApplicationService>();
builder.Services.AddScoped<MortgageApplicationService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.MapOpenApi();
}
else
{
    app.UseExceptionHandler();
}

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<FinancesDashboardDbContext>();
    dbContext.Database.Migrate();
}

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/accounts", async (AccountApplicationService service, CancellationToken cancellationToken) =>
{
    var accounts = await service.GetAccountsAsync(cancellationToken);
    return Results.Ok(accounts);
});

app.MapPost("/api/accounts", async (
    [FromBody] CreateAccountRequest? request,
    AccountApplicationService service,
    CancellationToken cancellationToken) =>
{
    if (request is null)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["request"] = ["Request body is required."]
        });
    }

    var errors = new Dictionary<string, string[]>();

    if (string.IsNullOrWhiteSpace(request.Name))
    {
        errors["name"] = ["Name is required."];
    }

    if (!Enum.TryParse<AccountType>(request.Type, ignoreCase: true, out _))
    {
        errors["type"] = ["Type must be one of: Transaction, Savings, CreditCard, Mortgage, Investment, Other."];
    }

    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    var account = await service.CreateAccountAsync(request, cancellationToken);
    return Results.Created($"/api/accounts/{account.Id}", account);
});

app.MapPut("/api/accounts/{id:guid}", async (
    Guid id,
    [FromBody] UpdateAccountRequest? request,
    AccountApplicationService service,
    CancellationToken cancellationToken) =>
{
    if (request is null)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["request"] = ["Request body is required."]
        });
    }

    var errors = new Dictionary<string, string[]>();

    if (string.IsNullOrWhiteSpace(request.Name))
    {
        errors["name"] = ["Name is required."];
    }

    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    try
    {
        var account = await service.UpdateAccountAsync(id, request, cancellationToken);
        return Results.Ok(account);
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound();
    }
});

app.MapDelete("/api/accounts/{id:guid}", async (
    Guid id,
    AccountApplicationService service,
    CancellationToken cancellationToken) =>
{
    try
    {
        await service.DeleteAccountAsync(id, cancellationToken);
        return Results.NoContent();
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound();
    }
});

app.MapGet("/api/accounts/{id:guid}/transactions", async (
    Guid id,
    [FromQuery] DateOnly? dateFrom,
    [FromQuery] DateOnly? dateTo,
    [FromQuery] string? descriptionContains,
    [FromQuery] decimal? minAmount,
    [FromQuery] decimal? maxAmount,
    [FromQuery] string? transactionDirection,
    [FromQuery] string? sortBy,
    [FromQuery] string? sortOrder,
    AccountApplicationService service,
    CancellationToken cancellationToken) =>
{
    var filter = new TransactionQueryFilterRequest
    {
        AccountId = id,
        DateFrom = dateFrom,
        DateTo = dateTo,
        DescriptionContains = descriptionContains,
        MinAmount = minAmount,
        MaxAmount = maxAmount,
        TransactionDirection = transactionDirection,
        SortBy = sortBy,
        SortOrder = sortOrder
    };

    var (isValid, errors) = filter.Validate();
    if (!isValid)
    {
        var problemDict = new Dictionary<string, string[]>();
        for (int i = 0; i < errors.Count; i++)
        {
            problemDict[$"validation_{i}"] = new[] { errors[i] };
        }
        return Results.ValidationProblem(problemDict);
    }

    var transactions = await service.GetAccountTransactionsAsync(filter, cancellationToken);
    return Results.Ok(transactions);
});

app.MapDelete("/api/transactions/{transactionId:guid}", async (
    Guid transactionId,
    AccountApplicationService service,
    CancellationToken cancellationToken) =>
{
    try
    {
        await service.DeleteTransactionAsync(transactionId, cancellationToken);
        return Results.NoContent();
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound();
    }
});

app.MapPost("/api/import/cba/{accountId:guid}", async (
    Guid accountId,
    IFormFile? file,
    CbaCsvImporter importer,
    CancellationToken cancellationToken) =>
{
    if (file is null || file.Length == 0)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["file"] = ["A non-empty CSV file is required."]
        });
    }

    await using var stream = file.OpenReadStream();
    var result = await importer.ImportAsync(accountId, stream, cancellationToken);
    return Results.Ok(result);
})
.DisableAntiforgery();

app.MapPost("/api/import/coles/{accountId:guid}", async (
    Guid accountId,
    IFormFile? file,
    ColesCreditCardCsvImporter importer,
    CancellationToken cancellationToken) =>
{
    if (file is null || file.Length == 0)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["file"] = ["A non-empty CSV file is required."]
        });
    }

    await using var stream = file.OpenReadStream();
    var result = await importer.ImportAsync(accountId, stream, cancellationToken);
    return Results.Ok(result);
})
.DisableAntiforgery();

app.MapGet("/api/dashboard/summary", async (DashboardApplicationService service, CancellationToken cancellationToken) =>
{
    var summary = await service.GetSummaryAsync(cancellationToken);
    return Results.Ok(summary);
});

app.MapGet("/api/dashboard/balance", async (
    [FromQuery] Guid? accountId,
    DashboardApplicationService service,
    CancellationToken cancellationToken) =>
{
    if (accountId is null || accountId == Guid.Empty)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["accountId"] = ["accountId is required."]
        });
    }

    var points = await service.GetBalanceAsync(accountId.Value, cancellationToken);
    return Results.Ok(points);
});

app.MapGet("/api/dashboard/cashflow", async (
    [FromQuery] Guid? accountId,
    DashboardApplicationService service,
    CancellationToken cancellationToken) =>
{
    if (accountId is null || accountId == Guid.Empty)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["accountId"] = ["accountId is required."]
        });
    }

    var points = await service.GetCashflowAsync(accountId.Value, cancellationToken);
    return Results.Ok(points);
});

app.MapGet("/api/dashboard/credit-card-spend", async (DashboardApplicationService service, CancellationToken cancellationToken) =>
{
    var points = await service.GetCreditCardSpendAsync(cancellationToken);
    return Results.Ok(points);
});

app.MapGet("/api/dashboard/account-summaries", async (DashboardApplicationService service, CancellationToken cancellationToken) =>
{
    var summaries = await service.GetAccountSummariesAsync(cancellationToken);
    return Results.Ok(summaries);
});

app.MapGet("/api/dashboard/forecast", async (
    [FromQuery] Guid? accountId,
    [FromQuery] int? months,
    DashboardApplicationService service,
    CancellationToken cancellationToken) =>
{
    var errors = new Dictionary<string, string[]>();

    if (accountId is null || accountId == Guid.Empty)
    {
        errors["accountId"] = ["accountId is required."];
    }

    if (months is null || months <= 0)
    {
        errors["months"] = ["months must be greater than zero."];
    }

    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    var points = await service.GetForecastAsync(accountId!.Value, months!.Value, cancellationToken);
    return Results.Ok(points);
});

app.MapGet("/api/mortgages", async (MortgageApplicationService service, CancellationToken cancellationToken) =>
{
    var mortgages = await service.GetMortgagesAsync(cancellationToken);
    return Results.Ok(mortgages);
});

app.MapGet("/api/mortgages/accounts", async (AccountApplicationService service, CancellationToken cancellationToken) =>
{
    var accounts = await service.GetAccountsByTypeAsync(AccountType.Mortgage, cancellationToken);
    return Results.Ok(accounts);
});

app.MapPost("/api/mortgages", async (
    [FromBody] CreateMortgageRequest? request,
    MortgageApplicationService service,
    CancellationToken cancellationToken) =>
{
    if (request is null)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["request"] = ["Request body is required."]
        });
    }

    var errors = new Dictionary<string, string[]>();

    if (request.AccountId == Guid.Empty)
    {
        errors["accountId"] = ["AccountId is required and must be a valid GUID."];
    }

    if (request.AnnualInterestRate < 0m)
    {
        errors["annualInterestRate"] = ["AnnualInterestRate must be zero or greater."];
    }

    if (request.InterestRateHistory is null)
    {
        errors["interestRateHistory"] = ["InterestRateHistory is required (empty list is allowed)."]; 
    }
    else
    {
        for (var index = 0; index < request.InterestRateHistory.Count; index++)
        {
            var period = request.InterestRateHistory[index];

            if (period.AnnualInterestRate < 0m)
            {
                errors[$"interestRateHistory[{index}].annualInterestRate"] = ["AnnualInterestRate must be zero or greater."];
            }

            if (period.EndDate is not null && period.EndDate < period.StartDate)
            {
                errors[$"interestRateHistory[{index}].endDate"] = ["EndDate must be on or after StartDate."];
            }
        }
    }

    if (request.OffsetAccountIds is null)
    {
        errors["offsetAccountIds"] = ["OffsetAccountIds is required (empty list is allowed)."]; 
    }

    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    try
    {
        var mortgage = await service.CreateMortgageAsync(request, cancellationToken);
        return Results.Created($"/api/mortgages/{mortgage.Id}", mortgage);
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
});

app.MapPut("/api/mortgages/{accountId}/offset-accounts", async (
    Guid accountId,
    [FromBody] UpdateMortgageOffsetAccountsRequest? request,
    MortgageApplicationService service,
    CancellationToken cancellationToken) =>
{
    if (request is null)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["request"] = new[] { "Request body is required." }
        });
    }

    if (request.OffsetAccountIds is null)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["offsetAccountIds"] = new[] { "OffsetAccountIds is required (empty list is allowed)." }
        });
    }

    try
    {
        var summary = await service.UpdateMortgageOffsetAccountsAsync(accountId, request, cancellationToken);
        return Results.Ok(summary);
    }
    catch (ArgumentException ex)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["offsetAccountIds"] = new[] { ex.Message }
        });
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
});

app.MapPost("/api/mortgages/{accountId}/forecast", async (
    Guid accountId,
    [FromBody] MortgageForecastRequest? request,
    MortgageApplicationService service,
    CancellationToken cancellationToken) =>
{
    if (request is null)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["request"] = ["Request body is required."]
        });
    }

    var errors = new Dictionary<string, string[]>();

    if (request.AnnualInterestRate < 0m)
    {
        errors["annualInterestRate"] = ["AnnualInterestRate must be zero or greater."];
    }

    if (request.RepaymentAmount < 0m)
    {
        errors["repaymentAmount"] = ["RepaymentAmount must be zero or greater."];
    }

    if (request.RepaymentsPerYear < 0)
    {
        errors["repaymentsPerYear"] = ["RepaymentsPerYear must be zero or greater."];
    }

    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    try
    {
        var forecast = await service.GenerateEphemeralForecastAsync(accountId, request, cancellationToken);
        return Results.Ok(forecast);
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
});

app.MapGet("/api/mortgages/{accountId}/history", async (
    Guid accountId,
    DateOnly? startDate,
    DateOnly? endDate,
    MortgageHistoryAnalysisService service,
    CancellationToken cancellationToken) =>
{
    var history = await service.GetHistoryAsync(accountId, startDate, endDate, cancellationToken);
    return Results.Ok(history);
});

app.MapGet("/api/mortgages/{accountId}/summary", async (
    Guid accountId,
    DateOnly? startDate,
    DateOnly? endDate,
    MortgageApplicationService service,
    CancellationToken cancellationToken) =>
{
    try
    {
        var summary = await service.GetMortgageCurrentPositionAsync(accountId, startDate, endDate, cancellationToken);
        return Results.Ok(summary);
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
});

app.MapGet("/api/mortgages/{accountId}/current-balance", async (
    Guid accountId,
    MortgageHistoryAnalysisService service,
    CancellationToken cancellationToken) =>
{
    var balance = await service.GetCurrentBalanceAsync(accountId, cancellationToken);
    return Results.Ok(new { currentBalance = balance });
});

app.MapDefaultEndpoints();
app.Run();

public partial class Program;
