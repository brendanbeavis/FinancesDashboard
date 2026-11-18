namespace FinancesDashboard.Application.Dto;

/// <summary>
/// Query filter and sort parameters for transaction retrieval.
/// </summary>
public sealed record TransactionQueryFilterRequest
{
    public Guid AccountId { get; init; }

    /// <summary>
    /// Include transactions on or after this date (yyyy-MM-dd).
    /// </summary>
    public DateOnly? DateFrom { get; init; }

    /// <summary>
    /// Include transactions on or before this date (yyyy-MM-dd).
    /// </summary>
    public DateOnly? DateTo { get; init; }

    /// <summary>
    /// Case-insensitive substring match on Description.
    /// </summary>
    public string? DescriptionContains { get; init; }

    /// <summary>
    /// Include transactions with Amount greater than or equal to this value.
    /// </summary>
    public decimal? MinAmount { get; init; }

    /// <summary>
    /// Include transactions with Amount less than or equal to this value.
    /// </summary>
    public decimal? MaxAmount { get; init; }

    /// <summary>
    /// Filter by transaction direction: "All", "Positive", "Negative", or null/empty for "All".
    /// </summary>
    public string? TransactionDirection { get; init; }

    /// <summary>
    /// Sort column: "Date", "Description", "Amount", "BankBalance". Default: "Date".
    /// </summary>
    public string? SortBy { get; init; } = "Date";

    /// <summary>
    /// Sort order: "Asc" or "Desc". Default: "Desc" (newest first).
    /// </summary>
    public string? SortOrder { get; init; } = "Desc";

    /// <summary>
    /// Validates the filter parameters.
    /// </summary>
    public (bool IsValid, List<string> Errors) Validate()
    {
        var errors = new List<string>();

        // Validate date range
        if (DateFrom.HasValue && DateTo.HasValue && DateFrom > DateTo)
        {
            errors.Add("DateFrom must be on or before DateTo.");
        }

        // Validate amount range
        if (MinAmount.HasValue && MaxAmount.HasValue && MinAmount > MaxAmount)
        {
            errors.Add("MinAmount must be less than or equal to MaxAmount.");
        }

        // Validate sort parameters
        var validSortColumns = new[] { "Date", "Description", "Amount", "BankBalance" };
        if (!string.IsNullOrEmpty(SortBy) && !validSortColumns.Contains(SortBy))
        {
            errors.Add($"SortBy must be one of: {string.Join(", ", validSortColumns)}.");
        }

        var validSortOrders = new[] { "Asc", "Desc" };
        if (!string.IsNullOrEmpty(SortOrder) && !validSortOrders.Contains(SortOrder))
        {
            errors.Add("SortOrder must be 'Asc' or 'Desc'.");
        }

        // Validate transaction direction
        var validDirections = new[] { "All", "Positive", "Negative" };
        if (!string.IsNullOrEmpty(TransactionDirection) && !validDirections.Contains(TransactionDirection))
        {
            errors.Add("TransactionDirection must be 'All', 'Positive', or 'Negative'.");
        }

        return (errors.Count == 0, errors);
    }
}
