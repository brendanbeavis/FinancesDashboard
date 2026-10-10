using FinancesDashboard.Application.Dto;
using FinancesDashboard.Domain.Models;

namespace FinancesDashboard.Application.Services;

/// <summary>
/// Analyzes historical mortgage transactions to detect and derive repayment patterns.
/// Uses deterministic algorithms to identify recurring payments without AI/LLM.
/// </summary>
public sealed class RepaymentAnalysisService
{
    /// <summary>
    /// Detected repayment pattern from historical data.
    /// </summary>
    public sealed record DetectedRepayment(
        decimal RepaymentAmount,
        int RepaymentsPerYear,
        decimal Confidence);

    /// <summary>
    /// Analyzes transaction history to detect typical repayment patterns.
    /// Returns detailed pattern with metrics or null if no pattern detected.
    /// </summary>
    /// <param name="transactions">Mortgage account transactions, ideally in chronological order.</param>
    /// <returns>Detailed repayment pattern with detection metrics, or null if no pattern detected.</returns>
    public MortgageRepaymentPattern? AnalyzeRepaymentPatternDetailed(IReadOnlyList<Transaction> transactions)
    {
        if (transactions.Count < 2)
        {
            return null;
        }

        var candidatePayments = transactions
            .Where(IsRepaymentCandidate)
            .OrderBy(t => t.Date)
            .ToList();

        if (candidatePayments.Count < 2)
        {
            return null;
        }

        // Create parallel lists of amounts and their source transactions
        var paymentAmounts = candidatePayments.Select(p => Math.Abs(p.Amount)).ToList();

        // Group amounts by similarity (within 5% tolerance)
        var amountGroups = GroupAmountsBySimilarity(paymentAmounts, candidatePayments, 0.05m);

        if (amountGroups.Count == 0)
        {
            return null;
        }

        var mostCommonGroup = amountGroups.First(); // Already sorted by count

        if (mostCommonGroup.Transactions.Count < 2)
        {
            return null;
        }

        // Use median of most common group
        var repaymentAmount = mostCommonGroup.Amount;
        var matchingPayments = mostCommonGroup.Transactions.OrderBy(t => t.Date).ToList();

        // Analyze payment dates for frequency
        var dateGaps = CalculateDateGaps(matchingPayments);

        if (dateGaps.Count == 0)
        {
            return null;
        }

        // Determine frequency from typical gaps
        var (frequency, expectedGap, frequencyLabel) = DetermineFrequency(dateGaps);

        if (frequency == 0)
        {
            return null;
        }

        // Calculate confidence metrics
        var amountConsistency = (decimal)mostCommonGroup.Transactions.Count / candidatePayments.Count;
        var frequencyConsistency = CalculateFrequencyConsistency(dateGaps, expectedGap);
        var overallConfidence = (amountConsistency + frequencyConsistency) / 2m;

        // Calculate gap statistics
        var averageGap = (int)Math.Round(dateGaps.Average());
        var gapStandardDeviation = CalculateStandardDeviation(dateGaps);

        var pattern = new MortgageRepaymentPattern(
            EstimatedAmount: repaymentAmount,
            EstimatedFrequency: frequency,
            Confidence: Math.Min(1m, Math.Max(0m, overallConfidence)),
            NumberOfObservations: mostCommonGroup.Transactions.Count,
            LastObservedDate: matchingPayments.Last().Date,
            FrequencyLabel: frequencyLabel,
            AverageGapDays: averageGap,
            AmountConsistency: amountConsistency,
            GapStandardDeviation: gapStandardDeviation,
            FirstObservedDate: matchingPayments.First().Date);

        return pattern;
    }

    /// <summary>
    /// Analyzes transaction history to detect typical repayment patterns.
    /// Legacy method for backward compatibility.
    /// </summary>
    /// <param name="transactions">Mortgage account transactions in chronological order.</param>
    /// <returns>Detected repayment pattern with amount, frequency, and confidence (0.0-1.0).</returns>
    public DetectedRepayment? AnalyzeRepaymentPattern(IReadOnlyList<Transaction> transactions)
    {
        var pattern = AnalyzeRepaymentPatternDetailed(transactions);
        if (pattern is null)
        {
            return null;
        }

        return new DetectedRepayment(
            pattern.EstimatedAmount,
            pattern.EstimatedFrequency,
            pattern.Confidence);
    }

    /// <summary>
    /// Determines whether a transaction description clearly indicates a mortgage repayment.
    /// </summary>
    private static bool IsRepaymentCandidate(Transaction transaction)
    {
        if (transaction.Amount == 0m)
        {
            return false;
        }

        var description = transaction.Description?.Trim();
        if (string.IsNullOrWhiteSpace(description))
        {
            return false;
        }

        return MatchesRepaymentDescription(description);
    }

    /// <summary>
    /// Determines whether a description clearly indicates a repayment/payment.
    /// </summary>
    private static bool MatchesRepaymentDescription(string description)
    {
        return description.Contains("loan repayment", StringComparison.OrdinalIgnoreCase)
            || description.Contains("loan payment", StringComparison.OrdinalIgnoreCase)
            || description.Contains("repayment", StringComparison.OrdinalIgnoreCase)
            || description.Contains("mortgage repayment", StringComparison.OrdinalIgnoreCase)
            || description.Contains("mortgage payment", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Groups similar payment amounts from transactions within tolerance.
    /// Returns list of amount groups sorted by transaction count (descending).
    /// </summary>
    private static List<AmountGroup> GroupAmountsBySimilarity(
        List<decimal> amounts,
        List<Transaction> sourceTransactions,
        decimal tolerance)
    {
        var groups = new List<AmountGroup>();

        for (var i = 0; i < amounts.Count; i++)
        {
            var amount = amounts[i];
            var sourceTransaction = sourceTransactions[i];

            var existingGroup = groups.FirstOrDefault(g =>
            {
                var baseAmount = g.Amount;
                var percentDifference = Math.Abs((amount - baseAmount) / baseAmount);
                return percentDifference <= tolerance;
            });

            if (existingGroup is not null)
            {
                existingGroup.Amounts.Add(amount);
                existingGroup.Transactions.Add(sourceTransaction);
            }
            else
            {
                groups.Add(new AmountGroup(amount) { Transactions = new List<Transaction> { sourceTransaction } });
            }
        }

        // Sort by transaction count descending
        return groups.Count == 0 ? new() : new List<AmountGroup>(groups.OrderByDescending(g => g.Transactions.Count));
    }

    /// <summary>
    /// Calculates gaps in days between consecutive transactions.
    /// </summary>
    private static List<int> CalculateDateGaps(List<Transaction> sortedTransactions)
    {
        var gaps = new List<int>();
        for (var i = 1; i < sortedTransactions.Count; i++)
        {
            var gap = (sortedTransactions[i].Date.ToDateTime(TimeOnly.MinValue) -
                       sortedTransactions[i - 1].Date.ToDateTime(TimeOnly.MinValue)).Days;
            if (gap > 0)
            {
                gaps.Add(gap);
            }
        }
        return gaps;
    }

    /// <summary>
    /// Calculates standard deviation of gap values.
    /// </summary>
    private static decimal CalculateStandardDeviation(List<int> gaps)
    {
        if (gaps.Count < 2)
        {
            return 0m;
        }

        var average = gaps.Average();
        var variance = gaps.Select(g => Math.Pow(g - average, 2)).Average();
        return (decimal)Math.Sqrt(variance);
    }

    /// <summary>
    /// Calculates frequency consistency based on gap adherence.
    /// Returns 0.0-1.0 score.
    /// </summary>
    private static decimal CalculateFrequencyConsistency(List<int> gaps, int expectedGap)
    {
        if (gaps.Count == 0)
        {
            return 0m;
        }

        // Calculate average deviation from expected gap
        var totalDeviation = gaps.Select(g => Math.Abs(g - expectedGap)).Sum();
        var averageDeviation = (decimal)totalDeviation / gaps.Count;

        // Tolerance: allow up to 25% variance on average
        var tolerance = expectedGap * 0.25m;
        var consistency = Math.Max(0m, 1m - (averageDeviation / tolerance));
        return Math.Min(1m, consistency);
    }

    /// <summary>
    /// Determines repayment frequency from payment gaps.
    /// Uses adaptive matching based on gap distribution.
    /// Returns (RepaymentsPerYear, ExpectedGapInDays, Label).
    /// </summary>
    private static (int Frequency, int ExpectedGap, string Label) DetermineFrequency(List<int> gaps)
    {
        if (gaps.Count == 0)
        {
            return (0, 0, "Unknown");
        }

        var averageGap = gaps.Average();

        // Define frequency candidates with expected gap ranges
        var frequencies = new[]
        {
            (Frequency: 52, GapDays: 7, Label: "Weekly"),
            (Frequency: 26, GapDays: 14, Label: "Fortnightly"),
            (Frequency: 24, GapDays: 15, Label: "Bi-weekly (15)"),
            (Frequency: 12, GapDays: 30, Label: "Monthly"),
            (Frequency: 12, GapDays: 31, Label: "Monthly (31)"),
            (Frequency: 4, GapDays: 91, Label: "Quarterly"),
            (Frequency: 2, GapDays: 182, Label: "Semi-annual"),
            (Frequency: 1, GapDays: 365, Label: "Annual"),
        };

        // Adaptive tolerance: tighter with more observations
        var adaptiveTolerance = Math.Max(0.05, 0.15 - (gaps.Count * 0.01));

        foreach (var freq in frequencies)
        {
            var toleranceRange = freq.GapDays * adaptiveTolerance;
            if (Math.Abs(averageGap - freq.GapDays) <= toleranceRange)
            {
                return (freq.Frequency, freq.GapDays, freq.Label);
            }
        }

        // No exact match - check if irregular but consistent pattern
        var gapStdDev = CalculateStandardDeviation(gaps);
        if (gapStdDev < (decimal)averageGap * 0.15m) // Relatively consistent despite not matching standard frequencies
        {
            return DetermineIrregularFrequency(averageGap);
        }

        return (0, 0, "Irregular");
    }

    /// <summary>
    /// Determines frequency for payments that don't match standard frequencies
    /// but show consistency in their own pattern.
    /// </summary>
    private static (int Frequency, int ExpectedGap, string Label) DetermineIrregularFrequency(double averageGapDays)
    {
        // Map to closest standard frequency
        if (averageGapDays < 10)
            return (52, 7, "Weekly (approx)");
        if (averageGapDays < 21)
            return (26, 14, "Fortnightly (approx)");
        if (averageGapDays < 45)
            return (12, 30, "Monthly (approx)");
        if (averageGapDays < 120)
            return (4, 91, "Quarterly (approx)");
        if (averageGapDays < 270)
            return (2, 182, "Semi-annual (approx)");

        return (1, 365, "Annual (approx)");
    }

    /// <summary>
    /// Helper class for grouping similar payment amounts.
    /// </summary>
    private sealed class AmountGroup
    {
        public AmountGroup(decimal baseAmount)
        {
            Amount = baseAmount;
            Amounts = new List<decimal> { baseAmount };
            Transactions = new List<Transaction>();
        }

        public decimal Amount { get; }
        public List<decimal> Amounts { get; }
        public List<Transaction> Transactions { get; set; }
    }
}
