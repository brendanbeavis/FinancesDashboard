using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using FinancesDashboard.Domain.Models;
using FinancesDashboard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinancesDashboard.Application.CbaImport;

public sealed class CbaCsvImporter(FinancesDashboardDbContext dbContext)
{
    private const string DateFormat = "dd/MM/yyyy";

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
        var csvConfiguration = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = false,
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
        string bankBalanceText;

        try
        {
            dateText = csv.GetField(0) ?? string.Empty;
            amountText = csv.GetField(1) ?? string.Empty;
            description = csv.GetField(2) ?? string.Empty;
            bankBalanceText = csv.GetField(3) ?? string.Empty;
        }
        catch (Exception)
        {
            error = $"Row {row}: Expected 4 columns (TIMESTAMP, AMOUNT, DESCRIPTION, NET_ACCOUNT_TOTAL).";
            return false;
        }

        if (!DateOnly.TryParseExact(dateText, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            error = $"Row {row}: Invalid date '{dateText}'. Expected format dd/MM/yyyy.";
            return false;
        }

        if (!decimal.TryParse(amountText, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            error = $"Row {row}: Invalid amount '{amountText}'.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            error = $"Row {row}: Description is required.";
            return false;
        }

        if (!decimal.TryParse(bankBalanceText, NumberStyles.Number, CultureInfo.InvariantCulture, out var bankBalance))
        {
            error = $"Row {row}: Invalid NET_ACCOUNT_TOTAL '{bankBalanceText}'.";
            return false;
        }

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
