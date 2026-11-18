using FinancesDashboard.Application.Dto;
using FinancesDashboard.Application.Services;
using FinancesDashboard.Domain.Models;

namespace FinancesDashboard.Tests;

/// <summary>
/// Unit tests for deterministic repayment pattern detection.
/// Tests cover amount consistency, frequency detection, confidence scoring,
/// and edge cases including irregular patterns and insufficient observations.
/// </summary>
public sealed class RepaymentDetectionTests
{
    private readonly RepaymentAnalysisService _service = new();

    #region Insufficient Data Tests

    [Fact]
    public void AnalyzeRepaymentPatternDetailed_WithNoTransactions_ReturnsNull()
    {
        // Arrange
        var transactions = new List<Transaction>();

        // Act
        var result = _service.AnalyzeRepaymentPatternDetailed(transactions);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void AnalyzeRepaymentPatternDetailed_WithSingleTransaction_ReturnsNull()
    {
        // Arrange
        var transactions = new List<Transaction>
        {
            new() { Date = new DateOnly(2024, 1, 1), Amount = -1500m, Description = "Loan Repayment", ImportHash = "hash1" }
        };

        // Act
        var result = _service.AnalyzeRepaymentPatternDetailed(transactions);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void AnalyzeRepaymentPatternDetailed_WithNoNegativeTransactions_ReturnsNull()
    {
        // Arrange
        var transactions = new List<Transaction>
        {
            new() { Date = new DateOnly(2024, 1, 1), Amount = 1500m, Description = "Interest", ImportHash = "hash1" },
            new() { Date = new DateOnly(2024, 2, 1), Amount = 1500m, Description = "Interest", ImportHash = "hash2" }
        };

        // Act
        var result = _service.AnalyzeRepaymentPatternDetailed(transactions);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void AnalyzeRepaymentPatternDetailed_WithCaseInsensitiveDescriptions_DetectsRepaymentPattern()
    {
        var transactions = new List<Transaction>
        {
            CreateTransaction(new DateOnly(2023, 1, 1), -1560m, "loan repayment", "hash1"),
            CreateTransaction(new DateOnly(2023, 1, 15), -1560m, "MORTGAGE PAYMENT", "hash2"),
            CreateTransaction(new DateOnly(2023, 1, 29), -1560m, "Repayment", "hash3"),
            CreateTransaction(new DateOnly(2023, 2, 12), -1560m, "Mortgage Repayment", "hash4")
        };

        var result = _service.AnalyzeRepaymentPatternDetailed(transactions);

        Assert.NotNull(result);
        Assert.Equal(1560m, result.EstimatedAmount);
        Assert.Equal(26, result.EstimatedFrequency);
        Assert.Equal(4, result.NumberOfObservations);
    }

    [Fact]
    public void AnalyzeRepaymentPatternDetailed_WithMissingDescriptions_ReturnsNull()
    {
        var transactions = GenerateRegularPayments(
            startDate: new DateOnly(2023, 1, 1),
            amount: -1560m,
            gapDays: 14,
            paymentCount: 4);

        foreach (var transaction in transactions)
        {
            transaction.Description = null;
        }

        var result = _service.AnalyzeRepaymentPatternDetailed(transactions);

        Assert.Null(result);
    }

    [Fact]
    public void AnalyzeRepaymentPatternDetailed_WithOnlyOneNegativeTransaction_ReturnsNull()
    {
        // Arrange
        var transactions = new List<Transaction>
        {
            new() { Date = new DateOnly(2024, 1, 1), Amount = 1500m, Description = "Interest", ImportHash = "hash1" },
            new() { Date = new DateOnly(2024, 2, 1), Amount = -1500m, Description = "Loan Repayment", ImportHash = "hash2" }
        };

        // Act
        var result = _service.AnalyzeRepaymentPatternDetailed(transactions);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region Weekly Payment Tests

    [Fact]
    public void AnalyzeRepaymentPatternDetailed_WithWeeklyPayments_DetectsWeekly()
    {
        // Arrange: 52 payments over 1 year, every 7 days, consistent -500 amount
        var transactions = GenerateRegularPayments(
            startDate: new DateOnly(2023, 1, 1),
            amount: -500m,
            gapDays: 7,
            paymentCount: 52);

        // Act
        var result = _service.AnalyzeRepaymentPatternDetailed(transactions);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(500m, result.EstimatedAmount);
        Assert.Equal(52, result.EstimatedFrequency);
        Assert.Equal("Weekly", result.FrequencyLabel);
        Assert.Equal(52, result.NumberOfObservations);
        Assert.True(result.Confidence >= 0.8m);
        Assert.Equal(7, result.AverageGapDays);
    }

    #endregion

    #region Fortnightly Payment Tests

    [Fact]
    public void AnalyzeRepaymentPatternDetailed_WithFornightlyPayments_DetectsFortnightly()
    {
        // Arrange: 26 payments over 1 year, every 14 days, consistent -1560 amount
        var transactions = GenerateRegularPayments(
            startDate: new DateOnly(2023, 1, 1),
            amount: -1560m,
            gapDays: 14,
            paymentCount: 26);

        // Act
        var result = _service.AnalyzeRepaymentPatternDetailed(transactions);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1560m, result.EstimatedAmount);
        Assert.Equal(26, result.EstimatedFrequency);
        Assert.Equal("Fortnightly", result.FrequencyLabel);
        Assert.Equal(26, result.NumberOfObservations);
        Assert.True(result.Confidence >= 0.8m);
        Assert.Equal(14, result.AverageGapDays);
    }

    [Fact]
    public void AnalyzeRepaymentPatternDetailed_WithFornightlyPaymentsAndNoiseTransactions_IgnoresNoise()
    {
        // Arrange: Mix of fortnightly payments with other transactions
        var transactions = new List<Transaction>
        {
            CreateTransaction(new DateOnly(2023, 1, 1), -1560m, "Loan Repayment", "hash"),
            CreateTransaction(new DateOnly(2023, 1, 5), 50m, "Fee Reversal", "hash"),
            CreateTransaction(new DateOnly(2023, 1, 15), -1560m, "Loan Repayment", "hash"),
            CreateTransaction(new DateOnly(2023, 1, 20), -100m, "Other Charge", "hash"),
            CreateTransaction(new DateOnly(2023, 1, 29), -1560m, "Loan Repayment", "hash"),
            CreateTransaction(new DateOnly(2023, 2, 12), -1560m, "Loan Repayment", "hash")
        };

        // Act
        var result = _service.AnalyzeRepaymentPatternDetailed(transactions);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1560m, result.EstimatedAmount);
        Assert.Equal(4, result.NumberOfObservations);
        Assert.True(result.AmountConsistency >= 0.5m);
    }

    #endregion

    #region Monthly Payment Tests

    [Fact]
    public void AnalyzeRepaymentPatternDetailed_WithMonthlyPayments30Days_DetectsMonthly()
    {
        // Arrange: 12 payments, monthly with ~30 day intervals
        var transactions = GenerateRegularPayments(
            startDate: new DateOnly(2023, 1, 1),
            amount: -2000m,
            gapDays: 30,
            paymentCount: 12);

        // Act
        var result = _service.AnalyzeRepaymentPatternDetailed(transactions);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2000m, result.EstimatedAmount);
        Assert.Equal(12, result.EstimatedFrequency);
        Assert.Contains("Monthly", result.FrequencyLabel);
        Assert.Equal(12, result.NumberOfObservations);
        Assert.True(result.Confidence >= 0.7m);
    }

    [Fact]
    public void AnalyzeRepaymentPatternDetailed_WithMonthlyPayments31Days_DetectsMonthly()
    {
        // Arrange: 12 payments, monthly with ~31 day intervals
        var transactions = GenerateRegularPayments(
            startDate: new DateOnly(2023, 1, 1),
            amount: -2000m,
            gapDays: 31,
            paymentCount: 12);

        // Act
        var result = _service.AnalyzeRepaymentPatternDetailed(transactions);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(12, result.EstimatedFrequency);
        Assert.Contains("Monthly", result.FrequencyLabel);
        Assert.True(result.Confidence >= 0.7m);
    }

    #endregion

    #region Amount Variation Tests

    [Fact]
    public void AnalyzeRepaymentPatternDetailed_WithSmallAmountVariations_StillDetectsPattern()
    {
        // Arrange: Payments with ±2% variation in amount (within 5% tolerance)
        var transactions = new List<Transaction>
        {
            CreateTransaction(new DateOnly(2023, 1, 1), -1560m, "Loan Repayment", "hash"),
            CreateTransaction(new DateOnly(2023, 1, 15), -1575m, "Loan Repayment", "hash"),
            CreateTransaction(new DateOnly(2023, 1, 29), -1545m, "Loan Repayment", "hash"),
            CreateTransaction(new DateOnly(2023, 2, 12), -1560m, "Loan Repayment", "hash"),
        };

        // Act
        var result = _service.AnalyzeRepaymentPatternDetailed(transactions);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(4, result.NumberOfObservations);
        Assert.True(result.AmountConsistency >= 0.8m);
    }

    [Fact]
    public void AnalyzeRepaymentPatternDetailed_WithChangingRepaymentAmount_DetectsLargestCluster()
    {
        // Arrange: Mix of different payment amounts
        var transactions = new List<Transaction>
        {
            CreateTransaction(new DateOnly(2023, 1, 1), -1500m, "Loan Repayment", "hash"),
            CreateTransaction(new DateOnly(2023, 1, 15), -1500m, "Loan Repayment", "hash"),
            CreateTransaction(new DateOnly(2023, 1, 29), -1500m, "Loan Repayment", "hash"),
            CreateTransaction(new DateOnly(2023, 2, 12), -1500m, "Loan Repayment", "hash"),
            CreateTransaction(new DateOnly(2023, 2, 26), -1600m, "Loan Repayment", "hash"),
            CreateTransaction(new DateOnly(2023, 3, 12), -1600m, "Loan Repayment", "hash"),
        };

        // Act
        var result = _service.AnalyzeRepaymentPatternDetailed(transactions);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1500m, result.EstimatedAmount);
        Assert.Equal(4, result.NumberOfObservations);
        Assert.True(result.AmountConsistency >= 0.5m);
    }

    #endregion

    #region Irregular Pattern Tests

    [Fact]
    public void AnalyzeRepaymentPatternDetailed_WithIrregularGaps_WithHighConsistency_StillDetectsPattern()
    {
        // Arrange: Irregular gaps but still relatively consistent pattern
        var transactions = new List<Transaction>
        {
            CreateTransaction(new DateOnly(2023, 1, 1), -1500m, "Loan Repayment", "hash"),
            CreateTransaction(new DateOnly(2023, 1, 18), -1500m, "Loan Repayment", "hash"),
            CreateTransaction(new DateOnly(2023, 2, 1), -1500m, "Loan Repayment", "hash"),
            CreateTransaction(new DateOnly(2023, 2, 17), -1500m, "Loan Repayment", "hash"),
            CreateTransaction(new DateOnly(2023, 3, 3), -1500m, "Loan Repayment", "hash"),
        };

        // Act
        var result = _service.AnalyzeRepaymentPatternDetailed(transactions);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1500m, result.EstimatedAmount);
        Assert.Equal(5, result.NumberOfObservations);
        Assert.True(result.GapStandardDeviation < 5m);
    }

    [Fact]
    public void AnalyzeRepaymentPatternDetailed_WithHighlyIrregularGaps_LowConfidence()
    {
        // Arrange: Very inconsistent gaps
        var transactions = new List<Transaction>
        {
            CreateTransaction(new DateOnly(2023, 1, 1), -1500m, "Loan Repayment", "hash"),
            CreateTransaction(new DateOnly(2023, 1, 5), -1500m, "Loan Repayment", "hash"),
            CreateTransaction(new DateOnly(2023, 2, 10), -1500m, "Loan Repayment", "hash"),
            CreateTransaction(new DateOnly(2023, 2, 15), -1500m, "Loan Repayment", "hash"),
            CreateTransaction(new DateOnly(2023, 4, 1), -1500m, "Loan Repayment", "hash"),
        };

        // Act
        var result = _service.AnalyzeRepaymentPatternDetailed(transactions);

        // Assert
        if (result is not null)
        {
            Assert.True(result.Confidence < 0.5m);
        }
    }

    [Fact]
    public void AnalyzeRepaymentPatternDetailed_WithOneOffPayment_ReturnsRecurringPatternFromDominantCluster()
    {
        var transactions = new List<Transaction>
        {
            CreateTransaction(new DateOnly(2023, 1, 1), -1560m, "Mortgage Payment", "hash1"),
            CreateTransaction(new DateOnly(2023, 1, 15), -1560m, "Mortgage Payment", "hash2"),
            CreateTransaction(new DateOnly(2023, 1, 29), -1560m, "Mortgage Payment", "hash3"),
            CreateTransaction(new DateOnly(2023, 8, 1), -10000m, "Extra Repayment", "hash4")
        };

        var result = _service.AnalyzeRepaymentPatternDetailed(transactions);

        Assert.NotNull(result);
        Assert.Equal(1560m, result.EstimatedAmount);
        Assert.True(result.NumberOfObservations >= 3);
    }

    [Fact]
    public void AnalyzeRepaymentPatternDetailed_WithNoDetectableRepayments_ReturnsNull()
    {
        var transactions = new List<Transaction>
        {
            CreateTransaction(new DateOnly(2023, 1, 1), -1560m, "Transfer to offset", "hash1"),
            CreateTransaction(new DateOnly(2023, 1, 15), -1560m, "Interest charge", "hash2"),
            CreateTransaction(new DateOnly(2023, 1, 29), -1560m, "Adjustment", "hash3"),
            CreateTransaction(new DateOnly(2023, 2, 12), -1560m, "Payment to savings", "hash4")
        };

        var result = _service.AnalyzeRepaymentPatternDetailed(transactions);

        Assert.Null(result);
    }

    [Fact]
    public void AnalyzeRepaymentPatternDetailed_WithExtraRepayments_IgnoresOutlierAndDetectsRecurringPattern()
    {
        var transactions = new List<Transaction>
        {
            CreateTransaction(new DateOnly(2023, 1, 1), -1560m, "Mortgage Repayment", "hash1"),
            CreateTransaction(new DateOnly(2023, 1, 15), -1560m, "Mortgage Repayment", "hash2"),
            CreateTransaction(new DateOnly(2023, 1, 29), -1560m, "Mortgage Repayment", "hash3"),
            CreateTransaction(new DateOnly(2023, 2, 12), -1560m, "Mortgage Repayment", "hash4"),
            CreateTransaction(new DateOnly(2023, 2, 13), -2500m, "Extra Repayment", "hash5")
        };

        var result = _service.AnalyzeRepaymentPatternDetailed(transactions);

        Assert.NotNull(result);
        Assert.Equal(1560m, result.EstimatedAmount);
        Assert.Equal(26, result.EstimatedFrequency);
    }

    [Fact]
    public void AnalyzeRepaymentPatternDetailed_WithUnrelatedTransactions_IgnoresNoiseAndDetectsRecurringPattern()
    {
        var transactions = new List<Transaction>
        {
            CreateTransaction(new DateOnly(2023, 1, 1), -1560m, "Loan Payment", "hash1"),
            CreateTransaction(new DateOnly(2023, 1, 8), 50m, "Interest Refund", "hash2"),
            CreateTransaction(new DateOnly(2023, 1, 15), -1560m, "Loan Payment", "hash3"),
            CreateTransaction(new DateOnly(2023, 1, 20), -25m, "Fee", "hash4"),
            CreateTransaction(new DateOnly(2023, 1, 29), -1560m, "Loan Payment", "hash5"),
            CreateTransaction(new DateOnly(2023, 2, 12), -1560m, "Loan Payment", "hash6")
        };

        var result = _service.AnalyzeRepaymentPatternDetailed(transactions);

        Assert.NotNull(result);
        Assert.Equal(1560m, result.EstimatedAmount);
        Assert.Equal(4, result.NumberOfObservations);
    }

    #endregion

    #region Multiple Candidates Tests

    [Fact]
    public void AnalyzeRepaymentPatternDetailed_WithMultipleCandidates_SelectsLargestCluster()
    {
        // Arrange: Two competing patterns
        var transactions = new List<Transaction>
        {
            // Fortnightly 1500 pattern (6 observations)
            CreateTransaction(new DateOnly(2023, 1, 1), -1500m, "Loan Repayment", "hash"),
            CreateTransaction(new DateOnly(2023, 1, 15), -1500m, "Loan Repayment", "hash"),
            CreateTransaction(new DateOnly(2023, 1, 29), -1500m, "Loan Repayment", "hash"),
            CreateTransaction(new DateOnly(2023, 2, 12), -1500m, "Loan Repayment", "hash"),
            CreateTransaction(new DateOnly(2023, 2, 26), -1500m, "Loan Repayment", "hash"),
            CreateTransaction(new DateOnly(2023, 3, 12), -1500m, "Loan Repayment", "hash"),
            // Monthly 2000 pattern (4 observations)
            CreateTransaction(new DateOnly(2023, 1, 5), -2000m, "Special Payment", "hash"),
            CreateTransaction(new DateOnly(2023, 2, 5), -2000m, "Special Payment", "hash"),
            CreateTransaction(new DateOnly(2023, 3, 5), -2000m, "Special Payment", "hash"),
            CreateTransaction(new DateOnly(2023, 4, 5), -2000m, "Special Payment", "hash"),
        };

        // Act
        var result = _service.AnalyzeRepaymentPatternDetailed(transactions);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1500m, result.EstimatedAmount);
        Assert.Equal(6, result.NumberOfObservations);
    }

    #endregion

    #region Confidence Scoring Tests

    [Fact]
    public void AnalyzeRepaymentPatternDetailed_WithPerfectPattern_HasHighConfidence()
    {
        // Arrange: 12 perfect monthly payments
        var transactions = GenerateRegularPayments(
            startDate: new DateOnly(2023, 1, 1),
            amount: -2500m,
            gapDays: 30,
            paymentCount: 12);

        // Act
        var result = _service.AnalyzeRepaymentPatternDetailed(transactions);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Confidence >= 0.85m);
        Assert.Equal(1.0m, result.AmountConsistency);
    }

    [Fact]
    public void AnalyzeRepaymentPatternDetailed_ConfidenceIsComposite()
    {
        // Arrange: 4 fortnightly -1500 payments
        var transactions = GenerateRegularPayments(
            startDate: new DateOnly(2023, 1, 1),
            amount: -1500m,
            gapDays: 14,
            paymentCount: 4);

        // Act
        var result = _service.AnalyzeRepaymentPatternDetailed(transactions);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Confidence > 0m);
        Assert.True(result.Confidence <= 1m);
        Assert.True(result.Confidence >= 0.8m);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void AnalyzeRepaymentPatternDetailed_WithTwoPayments_DetectsPattern()
    {
        // Arrange: Minimum viable pattern
        var transactions = new List<Transaction>
        {
            CreateTransaction(new DateOnly(2023, 1, 1), -1500m, "Loan Repayment", "hash"),
            CreateTransaction(new DateOnly(2023, 1, 15), -1500m, "Loan Repayment", "hash"),
        };

        // Act
        var result = _service.AnalyzeRepaymentPatternDetailed(transactions);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1500m, result.EstimatedAmount);
        Assert.Equal(2, result.NumberOfObservations);
    }

    [Fact]
    public void BackwardCompatibility_AnalyzeRepaymentPattern_ReturnsDetectedRepayment()
    {
        // Arrange: 4 fortnightly payments
        var transactions = GenerateRegularPayments(
            startDate: new DateOnly(2023, 1, 1),
            amount: -1560m,
            gapDays: 14,
            paymentCount: 4);

        // Act
        var result = _service.AnalyzeRepaymentPattern(transactions);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1560m, result.RepaymentAmount);
        Assert.Equal(26, result.RepaymentsPerYear);
        Assert.True(result.Confidence > 0m);
    }

    [Fact]
    public void BackwardCompatibility_AnalyzeRepaymentPattern_ReturnsNullWhenNoPattern()
    {
        // Arrange
        var transactions = new List<Transaction>
        {
            CreateTransaction(new DateOnly(2023, 1, 1), 1500m, "Deposit", "hash")
        };

        // Act
        var result = _service.AnalyzeRepaymentPattern(transactions);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Generates a list of regular payments for testing.
    /// </summary>
    private static List<Transaction> GenerateRegularPayments(
        DateOnly startDate,
        decimal amount,
        int gapDays,
        int paymentCount)
    {
        var transactions = new List<Transaction>();
        var currentDate = startDate;

        for (var i = 0; i < paymentCount; i++)
        {
            transactions.Add(CreateTransaction(
                date: currentDate,
                amount: amount,
                description: "Loan Repayment",
                hash: $"hash{i}"
            ));
            currentDate = currentDate.AddDays(gapDays);
        }

        return transactions;
    }

    /// <summary>
    /// Helper to create a Transaction with required fields for testing.
    /// </summary>
    private static Transaction CreateTransaction(
        DateOnly date,
        decimal amount,
        string description,
        string hash)
    {
        return new Transaction
        {
            Date = date,
            Amount = amount,
            Description = description,
            ImportHash = hash,
            Id = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            BankBalance = 0m,
            ImportedUtc = DateTime.UtcNow
        };
    }

    #endregion
}

