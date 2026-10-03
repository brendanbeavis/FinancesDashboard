using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using FinancesDashboard.Application.CbaImport;
using FinancesDashboard.Domain.Models;
using FinancesDashboard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinancesDashboard.Application.ColesCreditCardImport;

public sealed class ColesCreditCardCsvImporter(FinancesDashboardDbContext dbContext)
{
    // Coles date format uses "Sept" (4 chars) instead of "Sep" (3 chars)
    private static readonly string[] DateFormats = ["dd MMM yy", "dd MMMM yy"];
    private static readonly CultureInfo AuCulture = new CultureInfo("en-AU");

    public async Task<ImportResult> ImportAsync(Guid accountId, Stream csvStream, CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        var imported = 0;
        var skipped = 0;

        var accountExists = await dbContext.Accounts.AnyAsync(account => account.Id == accountId, cancellationToken);
        if (!accountExists)
        {
            errors.Add($"Account '{accountId}' was not found.");
            return new ImportResult(imported, skipped, errors);
        }

        var seenHashes = new HashSet<string>(StringComparer.Ordinal);

        using var reader = new StreamReader(csvStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var csvConfiguration = new CsvConfiguration(AuCulture)
        {
            HasHeaderRecord = true,
            TrimOptions = TrimOptions.Trim
        };

        using var csv = new CsvReader(reader, csvConfiguration);

        while (await csv.ReadAsync())
        {
            var row = csv.Parser.Row;

            if (!TryParseRow(csv, accountId, row, out var transaction, out var parseError))
            {
                errors.Add(parseError!);
                continue;
            }

            if (!seenHashes.Add(transaction.ImportHash))
            {
                skipped++;
                continue;
            }

            var exists = await dbContext.Transactions.AnyAsync(existing => existing.ImportHash == transaction.ImportHash, cancellationToken);
            if (exists)
            {
                skipped++;
                continue;
            }

            dbContext.Transactions.Add(transaction);
            imported++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ImportResult(imported, skipped, errors);
    }

    public static string CreateImportHash(Guid accountId, DateOnly date, decimal amount, string description, decimal bankBalance)
    {
        var hashInput = string.Create(
            CultureInfo.InvariantCulture,
            $"{accountId:N}|{date:yyyy-MM-dd}|{amount}|{description}|{bankBalance}");

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(hashInput));
        return Convert.ToHexString(hashBytes);
    }

    private static bool TryParseRow(CsvReader csv, Guid accountId, int row, out Transaction transaction, out string? error)
    {
        transaction = null!;
        error = null;

        string dateText;
        string amountText;
        string description;

        try
        {
            // CSV columns: Date,Amount,Account Number,,Transaction Type,Transaction Details,Category,Merchant Name,Processed On
            dateText = csv.GetField(0) ?? string.Empty;
            amountText = csv.GetField(1) ?? string.Empty;
            var transactionDetails = csv.GetField(5) ?? string.Empty;
            var merchantName = csv.GetField(7) ?? string.Empty;

            // Use merchant name if available, otherwise use transaction details
            description = !string.IsNullOrWhiteSpace(merchantName) 
                ? merchantName 
                : transactionDetails;
        }
        catch (Exception)
        {
            error = $"Row {row}: Error reading CSV fields.";
            return false;
        }

        // Try multiple date formats to handle "Sept" (4 chars) and "Sep" (3 chars)
        if (!DateOnly.TryParseExact(dateText, DateFormats, AuCulture, DateTimeStyles.None, out var date))
        {
            error = $"Row {row}: Invalid date '{dateText}'. Expected format dd MMM yy (e.g., '30 Sept 26').";
            return false;
        }

        if (!decimal.TryParse(amountText, NumberStyles.Number, AuCulture, out var amount))
        {
            error = $"Row {row}: Invalid amount '{amountText}'.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            error = $"Row {row}: Description is required.";
            return false;
        }

        // For credit card imports, bank balance is 0 as it's not provided in the CSV
        var bankBalance = 0m;

        transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Date = date,
            Amount = amount,
            Description = description,
            BankBalance = bankBalance,
            ImportHash = CreateImportHash(accountId, date, amount, description, bankBalance),
            ImportedUtc = DateTime.UtcNow
        };

        return true;
    }
}
