using FinancesDashboard.Web.Models;
using System.Text.Json;

namespace FinancesDashboard.Web.Services;

/// <summary>
/// Service for logging client-side exceptions to the server.
/// This allows for centralized error tracking and debugging of issues that occur in Blazor components.
/// </summary>
public sealed class ClientErrorLogger(HttpClient httpClient, ILogger<ClientErrorLogger> logger)
{
    /// <summary>
    /// Logs an exception that occurred in a Blazor component or event handler.
    /// </summary>
    public async Task LogExceptionAsync(Exception ex, string componentName = "Unknown")
    {
        try
        {
            var errorModel = new ClientErrorLogModel
            {
                Message = ex.Message,
                StackTrace = ex.StackTrace,
                ComponentName = componentName,
                Timestamp = DateTime.UtcNow,
                UserAgent = await GetUserAgent(),
                ExceptionType = ex.GetType().FullName ?? "Unknown"
            };

            var json = JsonSerializer.Serialize(errorModel);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            try
            {
                var response = await httpClient.PostAsync("/api/errors/client", content);

                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning("Failed to log client exception to server: {StatusCode}", response.StatusCode);
                }
            }
            catch (HttpRequestException ex2)
            {
                // If we can't reach the server, just log locally
                logger.LogError(ex2, "Failed to send client error to server");
            }
        }
        catch (Exception logEx)
        {
            // Never throw from the error logging itself
            logger.LogError(logEx, "Error while logging client exception");
        }
    }

    private async Task<string> GetUserAgent()
    {
        try
        {
            // In a real implementation, you'd use JS interop to get the actual user agent
            // For now, return a placeholder
            return "Blazor Client";
        }
        catch
        {
            return "Unknown";
        }
    }
}
