using FinancesDashboard.Domain.Enums;
using FinancesDashboard.Web.Models;

namespace FinancesDashboard.Tests;

public sealed class DashboardSortingAndFilteringTests
{
    private static List<AccountDashboardSummaryModel> CreateTestSummaries()
    {
        return new List<AccountDashboardSummaryModel>
        {
            new(Guid.NewGuid(), "Savings Account", "Savings", 5000m, false, 2000m, 500m, 1500m, new DateOnly(2024, 12, 15), 15, 3, true),
            new(Guid.NewGuid(), "Checking Account", "Transaction", 10000m, false, 3000m, 1000m, 2000m, new DateOnly(2024, 12, 20), 25, 5, true),
            new(Guid.NewGuid(), "Credit Card", "CreditCard", 2500m, true, 0m, 2500m, -2500m, new DateOnly(2024, 12, 18), 12, 4, true),
            new(Guid.NewGuid(), "Home Loan", "Mortgage", 350000m, true, 0m, 0m, 0m, new DateOnly(2024, 12, 1), 1, 0, true),
            new(Guid.NewGuid(), "Investment", "Investment", 25000m, false, 500m, 0m, 500m, new DateOnly(2024, 12, 10), 8, 1, true)
        };
    }

    [Fact]
    public void SortByAccountName_Ascending_ReturnsAlphabetical()
    {
        var summaries = CreateTestSummaries();

        var sorted = summaries
            .OrderBy(s => s.AccountName)
            .ToList();

        Assert.Equal("Checking Account", sorted[0].AccountName);
        Assert.Equal("Credit Card", sorted[1].AccountName);
        Assert.Equal("Home Loan", sorted[2].AccountName);
        Assert.Equal("Investment", sorted[3].AccountName);
        Assert.Equal("Savings Account", sorted[4].AccountName);
    }

    [Fact]
    public void SortByAccountName_Descending_ReturnsReverseAlphabetical()
    {
        var summaries = CreateTestSummaries();

        var sorted = summaries
            .OrderByDescending(s => s.AccountName)
            .ToList();

        Assert.Equal("Savings Account", sorted[0].AccountName);
        Assert.Equal("Investment", sorted[1].AccountName);
        Assert.Equal("Home Loan", sorted[2].AccountName);
        Assert.Equal("Credit Card", sorted[3].AccountName);
        Assert.Equal("Checking Account", sorted[4].AccountName);
    }

    [Fact]
    public void SortByAccountType_Ascending_ReturnsTypeOrder()
    {
        var summaries = CreateTestSummaries();

        var sorted = summaries
            .OrderBy(s => s.AccountType)
            .ThenBy(s => s.AccountName)
            .ToList();

        Assert.Equal("CreditCard", sorted[0].AccountType);
        Assert.Equal("Investment", sorted[1].AccountType);
        Assert.Equal("Mortgage", sorted[2].AccountType);
        Assert.Equal("Savings", sorted[3].AccountType);
        Assert.Equal("Transaction", sorted[4].AccountType);
    }

    [Fact]
    public void SortByCurrentPosition_Ascending_WithLiabilityNormalization()
    {
        var summaries = CreateTestSummaries();

        // Normalize liability positions (negate) for correct sorting
        // This way: smaller assets (5000 < 10000 < 25000) and liabilities 
        // are ordered by their negated value where larger debt = smaller negated value
        var sorted = summaries
            .OrderBy(s => s.IsLiability ? -s.CurrentPosition : s.CurrentPosition)
            .ThenBy(s => s.AccountName)
            .ToList();

        // Liabilities sorted ascending by negated value: -350000 = -350000 vs -2500 = -2500
        // Assets sorted by value: 5000, 10000, 25000
        // So order is: Home Loan (-350000), Credit Card (-2500), Savings (5000), Checking (10000), Investment (25000)
        Assert.Equal("Home Loan", sorted[0].AccountName);
        Assert.Equal("Credit Card", sorted[1].AccountName);
        Assert.Equal("Savings Account", sorted[2].AccountName);
        Assert.Equal("Checking Account", sorted[3].AccountName);
        Assert.Equal("Investment", sorted[4].AccountName);
    }

    [Fact]
    public void SortByCurrentPosition_Descending_WithLiabilityNormalization()
    {
        var summaries = CreateTestSummaries();

        var sorted = summaries
            .OrderByDescending(s => s.IsLiability ? -s.CurrentPosition : s.CurrentPosition)
            .ThenBy(s => s.AccountName)
            .ToList();

        // Reverse order: Investment (25000), Checking (10000), Savings (5000), Credit Card (-2500), Home Loan (-350000)
        Assert.Equal("Investment", sorted[0].AccountName);
        Assert.Equal("Checking Account", sorted[1].AccountName);
        Assert.Equal("Savings Account", sorted[2].AccountName);
        Assert.Equal("Credit Card", sorted[3].AccountName);
        Assert.Equal("Home Loan", sorted[4].AccountName);
    }

    [Fact]
    public void SortByPreviousMonthIncome_Descending_ReturnsHighestIncomeFirst()
    {
        var summaries = CreateTestSummaries();

        var sorted = summaries
            .OrderByDescending(s => s.PreviousMonthIncome)
            .ThenBy(s => s.AccountName)
            .ToList();

        Assert.Equal(3000m, sorted[0].PreviousMonthIncome);
        Assert.Equal(2000m, sorted[1].PreviousMonthIncome);
        Assert.Equal(500m, sorted[2].PreviousMonthIncome);
        Assert.Equal(0m, sorted[3].PreviousMonthIncome);
        Assert.Equal(0m, sorted[4].PreviousMonthIncome);
    }

    [Fact]
    public void SortByPreviousMonthExpenses_Descending_ReturnsHighestExpensesFirst()
    {
        var summaries = CreateTestSummaries();

        var sorted = summaries
            .OrderByDescending(s => s.PreviousMonthExpenses)
            .ThenBy(s => s.AccountName)
            .ToList();

        Assert.Equal(2500m, sorted[0].PreviousMonthExpenses);
        Assert.Equal(1000m, sorted[1].PreviousMonthExpenses);
        Assert.Equal(500m, sorted[2].PreviousMonthExpenses);
        Assert.Equal(0m, sorted[3].PreviousMonthExpenses);
        Assert.Equal(0m, sorted[4].PreviousMonthExpenses);
    }

    [Fact]
    public void SortByPreviousMonthNet_Descending_ReturnsHighestNetFirst()
    {
        var summaries = CreateTestSummaries();

        var sorted = summaries
            .OrderByDescending(s => s.PreviousMonthNet)
            .ThenBy(s => s.AccountName)
            .ToList();

        Assert.Equal(2000m, sorted[0].PreviousMonthNet);
        Assert.Equal(1500m, sorted[1].PreviousMonthNet);
        Assert.Equal(500m, sorted[2].PreviousMonthNet);
        Assert.Equal(0m, sorted[3].PreviousMonthNet);
        Assert.Equal(-2500m, sorted[4].PreviousMonthNet);
    }

    [Fact]
    public void SortByLastTransactionDate_Descending_ReturnsMostRecentFirst()
    {
        var summaries = CreateTestSummaries();

        var sorted = summaries
            .OrderByDescending(s => s.LastTransactionDate)
            .ThenBy(s => s.AccountName)
            .ToList();

        Assert.Equal(new DateOnly(2024, 12, 20), sorted[0].LastTransactionDate);
        Assert.Equal(new DateOnly(2024, 12, 18), sorted[1].LastTransactionDate);
        Assert.Equal(new DateOnly(2024, 12, 15), sorted[2].LastTransactionDate);
        Assert.Equal(new DateOnly(2024, 12, 10), sorted[3].LastTransactionDate);
        Assert.Equal(new DateOnly(2024, 12, 1), sorted[4].LastTransactionDate);
    }

    [Fact]
    public void FilterByAccountName_CaseInsensitive_FindsMatches()
    {
        var summaries = CreateTestSummaries();

        var filtered = summaries
            .Where(s => s.AccountName.Contains("checking", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Single(filtered);
        Assert.Equal("Checking Account", filtered[0].AccountName);
    }

    [Fact]
    public void FilterByAccountName_PartialMatch_FindsMatches()
    {
        var summaries = CreateTestSummaries();

        var filtered = summaries
            .Where(s => s.AccountName.Contains("account", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Equal(2, filtered.Count); // "Checking Account" and "Savings Account" - not "Investment"
        Assert.Contains(filtered, s => s.AccountName == "Checking Account");
        Assert.Contains(filtered, s => s.AccountName == "Savings Account");
        Assert.DoesNotContain(filtered, s => s.AccountName == "Credit Card");
    }

    [Fact]
    public void FilterByAccountType_ReturnsOnlyMatchingType()
    {
        var summaries = CreateTestSummaries();

        var filtered = summaries
            .Where(s => s.AccountType.Equals("Savings", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Single(filtered);
        Assert.Equal("Savings Account", filtered[0].AccountName);
        Assert.Equal("Savings", filtered[0].AccountType);
    }

    [Fact]
    public void FilterByAccountType_CreditCard_ReturnsLiabilities()
    {
        var summaries = CreateTestSummaries();

        var filtered = summaries
            .Where(s => s.AccountType.Equals("CreditCard", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Single(filtered);
        Assert.Equal("Credit Card", filtered[0].AccountName);
        Assert.True(filtered[0].IsLiability);
    }

    [Fact]
    public void CombinedFilters_ByNameAndType_ReturnsBothMustMatch()
    {
        var summaries = CreateTestSummaries();

        var filtered = summaries
            .Where(s => s.AccountName.Contains("account", StringComparison.OrdinalIgnoreCase) && 
                       s.AccountType.Equals("Savings", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Single(filtered);
        Assert.Equal("Savings Account", filtered[0].AccountName);
    }

    [Fact]
    public void CombinedFilters_EmptyResult_WhenNoMatch()
    {
        var summaries = CreateTestSummaries();

        var filtered = summaries
            .Where(s => s.AccountName.Contains("NonExistent", StringComparison.OrdinalIgnoreCase) &&
                       s.AccountType.Equals("Transaction", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Empty(filtered);
    }

    [Fact]
    public void FilterAndSort_CombinationWorks()
    {
        var summaries = CreateTestSummaries();

        // Filter by accounts with activity last month
        var filtered = summaries
            .Where(s => s.PreviousMonthTransactionCount > 0)
            .OrderByDescending(s => s.PreviousMonthNet)
            .ThenBy(s => s.AccountName)
            .ToList();

        Assert.Equal(4, filtered.Count); // Excluding Mortgage with 0 previous month activity
        Assert.Equal(2000m, filtered[0].PreviousMonthNet); // Checking
        Assert.Equal(1500m, filtered[1].PreviousMonthNet); // Savings
        Assert.Equal(500m, filtered[2].PreviousMonthNet);  // Investment
        Assert.Equal(-2500m, filtered[3].PreviousMonthNet); // Credit Card
    }

    [Fact]
    public void DeterministicOrdering_TiebreakerWorks()
    {
        var summaries = new List<AccountDashboardSummaryModel>
        {
            new(Guid.NewGuid(), "Beta", "Savings", 1000m, false, 100m, 50m, 50m, new DateOnly(2024, 12, 15), 5, 1, true),
            new(Guid.NewGuid(), "Alpha", "Savings", 1000m, false, 100m, 50m, 50m, new DateOnly(2024, 12, 15), 5, 1, true),
            new(Guid.NewGuid(), "Gamma", "Savings", 1000m, false, 100m, 50m, 50m, new DateOnly(2024, 12, 15), 5, 1, true)
        };

        // Sort by CurrentPosition (all same) with AccountName tiebreaker
        var sorted = summaries
            .OrderBy(s => s.CurrentPosition)
            .ThenBy(s => s.AccountName)
            .ToList();

        Assert.Equal("Alpha", sorted[0].AccountName);
        Assert.Equal("Beta", sorted[1].AccountName);
        Assert.Equal("Gamma", sorted[2].AccountName);
    }

    [Fact]
    public void EmptyFilterResults_ReturnsEmptyList()
    {
        var summaries = CreateTestSummaries();

        var filtered = summaries
            .Where(s => s.AccountName.Contains("XYZ", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Empty(filtered);
    }

    [Fact]
    public void FilterByAccountType_AllTypes_ReturnsSeparateLists()
    {
        var summaries = CreateTestSummaries();
        var types = new[] { "Transaction", "Savings", "CreditCard", "Mortgage", "Investment", "Other" };

        foreach (var type in types)
        {
            var filtered = summaries
                .Where(s => s.AccountType.Equals(type, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (type != "Other")
            {
                Assert.Single(filtered);
                Assert.Equal(type, filtered[0].AccountType);
            }
            else
            {
                Assert.Empty(filtered); // No "Other" type in test data
            }
        }
    }

    [Fact]
    public void LiabilitySorting_PositionNormalization_PreservesLiabilityLabel()
    {
        var summaries = CreateTestSummaries();

        var sorted = summaries
            .OrderBy(s => s.IsLiability ? -s.CurrentPosition : s.CurrentPosition)
            .ThenBy(s => s.AccountName)
            .ToList();

        // Verify all liabilities are still marked as liabilities
        foreach (var summary in sorted)
        {
            if (summary.AccountType is "CreditCard" or "Mortgage")
            {
                Assert.True(summary.IsLiability);
            }
            else
            {
                Assert.False(summary.IsLiability);
            }
        }
    }
}
