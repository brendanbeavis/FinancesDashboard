namespace FinancesDashboard.Web.Models;

/// <summary>
/// Model for client-side errors logged from Blazor components.
/// </summary>
public sealed class ClientErrorLogModel
{
    public required string Message { get; set; }
    public string? StackTrace { get; set; }
    public required string ComponentName { get; set; }
    public required DateTime Timestamp { get; set; }
    public string? UserAgent { get; set; }
    public string? ExceptionType { get; set; }
}
