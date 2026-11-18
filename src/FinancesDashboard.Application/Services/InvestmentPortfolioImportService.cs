using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;
using FinancesDashboard.Application.Dto;
using FinancesDashboard.Domain.Enums;
using FinancesDashboard.Domain.Models;
using FinancesDashboard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinancesDashboard.Application.Services;

public sealed class InvestmentPortfolioImportService(
    FinancesDashboardDbContext dbContext,
    ILogger<InvestmentPortfolioImportService> logger)
{
    private static readonly string[] RequiredColumns = ["Code", "Avail Units", "Purchase $", "Last $", "Mkt Value $"];

    public async Task<InvestmentImportPreviewResult> PreviewImportAsync(
        Guid investmentAccountId,
        Stream csvStream,
        string fileName,
        decimal tolerance = 0.05m,
        CancellationToken cancellationToken = default)
    {
        var account = await dbContext.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == investmentAccountId, cancellationToken);

        if (account is null)
        {
            return BuildBlockingResult(investmentAccountId, fileName, "", [new InvestmentImportIssueDto(0, null, "Investment account was not found.", string.Empty)]);
        }

        if (account.Type != AccountType.Investment)
        {
            return BuildBlockingResult(investmentAccountId, fileName, "", [new InvestmentImportIssueDto(0, null, "Selected account is not an Investment account.", string.Empty)]);
        }

        using var memoryStream = new MemoryStream();
        await csvStream.CopyToAsync(memoryStream, cancellationToken);
        var fileBytes = memoryStream.ToArray();
        var fileHash = ComputeFileHash(fileBytes);

        memoryStream.Position = 0;
        using var reader = new StreamReader(memoryStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var configuration = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = false,
            TrimOptions = TrimOptions.Trim
        };

        using var csv = new CsvReader(reader, configuration);

        var rows = new List<CsvRow>();
        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();

            rows.Add(new CsvRow(csv.Parser.Row, csv.Parser.Record?.ToArray() ?? []));
        }

        if (rows.Count == 0)
        {
            return BuildBlockingResult(investmentAccountId, fileName, fileHash, [new InvestmentImportIssueDto(0, null, "CSV file is empty.", string.Empty)]);
        }

        var sourceAccountNumber = ExtractAccountNumber(rows);
        var snapshotDateTime = ExtractSnapshotDateTime(rows);

        var header = FindHeader(rows);
        if (header is null)
        {
            return BuildBlockingResult(investmentAccountId, fileName, fileHash, [new InvestmentImportIssueDto(0, null, "Could not detect holdings header row.", string.Empty)]);
        }

        var columnLookup = BuildColumnLookup(header.Fields);
        var missingColumns = RequiredColumns
            .Where(column => !columnLookup.ContainsKey(column))
            .ToList();

        if (missingColumns.Count > 0)
        {
            return new InvestmentImportPreviewResult(
                investmentAccountId,
                fileName,
                fileHash,
                sourceAccountNumber,
                snapshotDateTime,
                account.CurrencyCode,
                tolerance,
                rows.Count,
                0,
                0,
                true,
                missingColumns,
                await IsDuplicateFileAsync(investmentAccountId, fileHash, cancellationToken),
                await IsDuplicateSnapshotAsync(investmentAccountId, snapshotDateTime, cancellationToken),
                true,
                0m,
                0m,
                0m,
                [],
                [new InvestmentImportIssueDto(header.RowNumber, null, $"Missing required columns: {string.Join(", ", missingColumns)}", string.Join(",", header.Fields))]);
        }

        var holdings = new List<InvestmentImportPreviewHoldingDto>();
        var issues = new List<InvestmentImportIssueDto>();
        var seenInstrumentCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var section = "Unknown";

        foreach (var row in rows.Where(r => r.RowNumber > header.RowNumber))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var firstField = GetField(row.Fields, 0);
            if (string.IsNullOrWhiteSpace(firstField))
            {
                continue;
            }

            if (IsSectionHeader(firstField))
            {
                section = firstField.Trim();
                continue;
            }

            if (IsIgnorableStructuralRow(firstField))
            {
                continue;
            }

            var validationErrors = new List<string>();
            var reconciliationWarnings = new List<string>();

            var code = GetField(row.Fields, GetColumnIndex(columnLookup, "Code"));
            if (string.IsNullOrWhiteSpace(code))
            {
                validationErrors.Add("Instrument code is required.");
            }
            else if (!seenInstrumentCodes.Add(code))
            {
                validationErrors.Add($"Duplicate instrument code '{code}' found in file.");
            }

            var units = ParseRequiredDecimal(row, columnLookup, "Avail Units", validationErrors);
            var purchasePrice = ParseRequiredDecimal(row, columnLookup, "Purchase $", validationErrors);
            var currentPrice = ParseRequiredDecimal(row, columnLookup, "Last $", validationErrors);
            var sourceMarketValue = ParseOptionalDecimal(row, columnLookup, "Mkt Value $", validationErrors);

            var sourceProfitLoss = ParseOptionalDecimal(row, columnLookup, "Profit/Loss $", validationErrors);
            var sourceProfitLossPercentage = ParseOptionalDecimal(row, columnLookup, "P/L %", validationErrors);
            var sourceWeightPercentage = ParseOptionalDecimal(row, columnLookup, "Wgt %", validationErrors);
            var dailyPriceChange = ParseOptionalDecimal(row, columnLookup, "Change $", validationErrors);
            var dailyPriceChangePercentage = ParseOptionalDecimal(row, columnLookup, "Chg %", validationErrors);
            var dailyValueChange = ParseOptionalDecimal(row, columnLookup, "Value Chg $", validationErrors);

            if (units < 0m)
            {
                validationErrors.Add("Units must be non-negative.");
            }

            if (purchasePrice < 0m)
            {
                validationErrors.Add("Purchase price must be non-negative.");
            }

            if (currentPrice < 0m)
            {
                validationErrors.Add("Current price must be non-negative.");
            }

            var calculatedCostBase = Round2(units * purchasePrice);
            var calculatedMarketValue = Round2(units * currentPrice);
            var calculatedProfitLoss = Round2(calculatedMarketValue - calculatedCostBase);
            var calculatedProfitLossPercentage = calculatedCostBase == 0m
                ? 0m
                : Round6((calculatedProfitLoss / calculatedCostBase) * 100m);

            if (sourceMarketValue.HasValue && Math.Abs(calculatedMarketValue - sourceMarketValue.Value) > tolerance)
            {
                reconciliationWarnings.Add($"Calculated market value ({calculatedMarketValue}) differs from source market value ({sourceMarketValue.Value}).");
            }

            if (sourceProfitLoss.HasValue && Math.Abs(calculatedProfitLoss - sourceProfitLoss.Value) > tolerance)
            {
                reconciliationWarnings.Add($"Calculated unrealised P/L ({calculatedProfitLoss}) differs from source P/L ({sourceProfitLoss.Value}).");
            }

            if (sourceProfitLossPercentage.HasValue && Math.Abs(calculatedProfitLossPercentage - sourceProfitLossPercentage.Value) > tolerance)
            {
                reconciliationWarnings.Add($"Calculated unrealised return ({calculatedProfitLossPercentage}) differs from source return ({sourceProfitLossPercentage.Value}).");
            }

            var isValid = validationErrors.Count == 0;
            var holding = new InvestmentImportPreviewHoldingDto(
                row.RowNumber,
                code,
                section,
                units,
                purchasePrice,
                currentPrice,
                calculatedCostBase,
                calculatedMarketValue,
                calculatedProfitLoss,
                calculatedProfitLossPercentage,
                sourceMarketValue,
                sourceProfitLoss,
                sourceProfitLossPercentage,
                sourceWeightPercentage,
                dailyPriceChange,
                dailyPriceChangePercentage,
                dailyValueChange,
                isValid,
                validationErrors,
                reconciliationWarnings,
                string.Join(",", row.Fields));

            holdings.Add(holding);

            if (!isValid)
            {
                foreach (var error in validationErrors)
                {
                    issues.Add(new InvestmentImportIssueDto(row.RowNumber, code, error, holding.RawRow));
                }
            }
        }

        if (holdings.Count == 0)
        {
            issues.Add(new InvestmentImportIssueDto(0, null, "No holdings rows were found in the CSV sections.", string.Empty));
        }

        var validHoldings = holdings.Where(h => h.IsValid).ToList();
        var totalCostBase = Round2(validHoldings.Sum(h => h.CalculatedCostBase));
        var totalMarketValue = Round2(validHoldings.Sum(h => h.CalculatedMarketValue));
        var totalProfitLoss = Round2(totalMarketValue - totalCostBase);

        var duplicateFileDetected = await IsDuplicateFileAsync(investmentAccountId, fileHash, cancellationToken);
        var duplicateSnapshotDetected = await IsDuplicateSnapshotAsync(investmentAccountId, snapshotDateTime, cancellationToken);

        var hasBlockingErrors = missingColumns.Count > 0 || snapshotDateTime is null || validHoldings.Count == 0;
        if (snapshotDateTime is null)
        {
            issues.Add(new InvestmentImportIssueDto(0, null, "Snapshot timestamp could not be extracted from the CSV.", string.Empty));
            hasBlockingErrors = true;
        }

        return new InvestmentImportPreviewResult(
            investmentAccountId,
            fileName,
            fileHash,
            sourceAccountNumber,
            snapshotDateTime,
            account.CurrencyCode,
            tolerance,
            rows.Count,
            validHoldings.Count,
            issues.Count,
            false,
            missingColumns,
            duplicateFileDetected,
            duplicateSnapshotDetected,
            hasBlockingErrors,
            totalMarketValue,
            totalCostBase,
            totalProfitLoss,
            holdings,
            issues);
    }

    public async Task<InvestmentImportCommitResult> ConfirmImportAsync(
        ConfirmInvestmentImportRequest request,
        CancellationToken cancellationToken = default)
    {
        var account = await dbContext.Accounts.FirstOrDefaultAsync(a => a.Id == request.InvestmentAccountId, cancellationToken);
        if (account is null)
        {
            throw new KeyNotFoundException("Investment account was not found.");
        }

        if (account.Type != AccountType.Investment)
        {
            throw new ArgumentException("Selected account is not an Investment account.");
        }

        var warnings = new List<string>();

        var existingSnapshot = await dbContext.PortfolioSnapshots
            .FirstOrDefaultAsync(
                s => s.InvestmentAccountId == request.InvestmentAccountId && s.SnapshotDateTime == request.SnapshotDateTime,
                cancellationToken);

        var duplicateFileBatch = await dbContext.PortfolioImportBatches
            .FirstOrDefaultAsync(batch => batch.InvestmentAccountId == request.InvestmentAccountId && batch.FileHash == request.FileHash, cancellationToken);

        if (duplicateFileBatch is not null)
        {
            warnings.Add("An identical file hash has already been imported for this account.");

            if (!request.ReplaceExistingSnapshot)
            {
                throw new InvalidOperationException("An identical file has already been imported. Set replaceExistingSnapshot to true to re-import.");
            }

            dbContext.PortfolioImportIssues.RemoveRange(
                dbContext.PortfolioImportIssues.Where(issue => issue.PortfolioImportBatchId == duplicateFileBatch.Id));
            dbContext.PortfolioImportBatches.Remove(duplicateFileBatch);
            warnings.Add("Existing import batch with the same file hash was replaced.");
        }

        var replacedExistingSnapshot = false;
        if (existingSnapshot is not null)
        {
            if (!request.ReplaceExistingSnapshot)
            {
                throw new InvalidOperationException("A snapshot already exists for this account and snapshot timestamp. Set replaceExistingSnapshot to true to replace it.");
            }

            dbContext.HoldingSnapshots.RemoveRange(dbContext.HoldingSnapshots.Where(s => s.PortfolioSnapshotId == existingSnapshot.Id));
            dbContext.PortfolioSnapshots.Remove(existingSnapshot);
            replacedExistingSnapshot = true;
            warnings.Add("Existing snapshot for the same timestamp was replaced.");
        }

        var validHoldings = request.Holdings
            .Where(holding => holding.IsValid)
            .ToList();

        if (validHoldings.Count == 0)
        {
            throw new InvalidOperationException("No valid holdings were supplied for import confirmation.");
        }

        var importBatch = new PortfolioImportBatch
        {
            Id = Guid.NewGuid(),
            InvestmentAccountId = request.InvestmentAccountId,
            FileName = request.FileName,
            FileHash = request.FileHash,
            ImportDateTime = DateTime.UtcNow,
            SnapshotDateTime = request.SnapshotDateTime,
            TotalRowsRead = request.Holdings.Count,
            SuccessfulRows = validHoldings.Count,
            FailedRows = request.Issues.Count,
            TotalMarketValue = Round2(validHoldings.Sum(holding => holding.CalculatedMarketValue)),
            TotalCostBase = Round2(validHoldings.Sum(holding => holding.CalculatedCostBase)),
            ImportStatus = PortfolioImportStatus.Confirmed,
            ErrorSummary = request.Issues.Count == 0 ? null : string.Join(" | ", request.Issues.Select(issue => issue.ErrorMessage).Distinct())
        };

        dbContext.PortfolioImportBatches.Add(importBatch);

        foreach (var issue in request.Issues)
        {
            dbContext.PortfolioImportIssues.Add(new PortfolioImportIssue
            {
                Id = Guid.NewGuid(),
                PortfolioImportBatchId = importBatch.Id,
                RowNumber = issue.RowNumber,
                InstrumentCode = issue.InstrumentCode,
                ErrorMessage = issue.ErrorMessage,
                RawRow = issue.RawRow
            });
        }

        var snapshot = new PortfolioSnapshot
        {
            Id = Guid.NewGuid(),
            InvestmentAccountId = request.InvestmentAccountId,
            SnapshotDateTime = request.SnapshotDateTime,
            CurrencyCode = request.CurrencyCode,
            TotalMarketValue = importBatch.TotalMarketValue,
            TotalCostBase = importBatch.TotalCostBase,
            TotalUnrealisedProfitLoss = Round2(importBatch.TotalMarketValue - importBatch.TotalCostBase),
            TotalUnrealisedProfitLossPercentage = importBatch.TotalCostBase == 0m
                ? 0m
                : Round6(((importBatch.TotalMarketValue - importBatch.TotalCostBase) / importBatch.TotalCostBase) * 100m),
            TotalDailyValueChange = Round2(validHoldings.Sum(holding => holding.DailyValueChange ?? 0m)),
            Source = "CsvImport",
            CreatedAt = DateTime.UtcNow
        };

        dbContext.PortfolioSnapshots.Add(snapshot);

        var normalizedCodes = validHoldings
            .Select(holding => holding.InstrumentCode.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existingInstruments = await dbContext.InvestmentInstruments
            .Where(instrument => instrument.Exchange == "ASX" && normalizedCodes.Contains(instrument.Code))
            .ToDictionaryAsync(instrument => instrument.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var code in normalizedCodes.Where(code => !existingInstruments.ContainsKey(code)))
        {
            var instrument = new InvestmentInstrument
            {
                Id = Guid.NewGuid(),
                Code = code,
                Name = null,
                InstrumentType = InvestmentInstrumentType.Other,
                Exchange = "ASX",
                CurrencyCode = request.CurrencyCode,
                IsActive = true
            };

            dbContext.InvestmentInstruments.Add(instrument);
            existingInstruments[code] = instrument;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var existingCurrentHoldings = await dbContext.PortfolioHoldings
            .Where(h => h.InvestmentAccountId == request.InvestmentAccountId)
            .ToListAsync(cancellationToken);

        var existingByInstrumentId = existingCurrentHoldings
            .ToDictionary(holding => holding.InvestmentInstrumentId);

        foreach (var holding in validHoldings)
        {
            var code = holding.InstrumentCode.Trim().ToUpperInvariant();
            var instrument = existingInstruments[code];

            dbContext.HoldingSnapshots.Add(new HoldingSnapshot
            {
                Id = Guid.NewGuid(),
                PortfolioSnapshotId = snapshot.Id,
                InvestmentInstrumentId = instrument.Id,
                Units = holding.Units,
                AverageCostPerUnit = holding.AverageCostPerUnit,
                TotalCostBase = holding.CalculatedCostBase,
                MarketPrice = holding.CurrentPrice,
                MarketValue = holding.CalculatedMarketValue,
                DailyPriceChange = holding.DailyPriceChange ?? 0m,
                DailyPriceChangePercentage = holding.DailyPriceChangePercentage ?? 0m,
                UnrealisedProfitLoss = holding.CalculatedUnrealisedProfitLoss,
                UnrealisedProfitLossPercentage = holding.CalculatedUnrealisedProfitLossPercentage,
                PortfolioWeightPercentage = snapshot.TotalMarketValue == 0m
                    ? 0m
                    : Round6((holding.CalculatedMarketValue / snapshot.TotalMarketValue) * 100m)
            });

            if (existingByInstrumentId.TryGetValue(instrument.Id, out var currentHolding))
            {
                currentHolding.Units = holding.Units;
                currentHolding.AverageCostPerUnit = holding.AverageCostPerUnit;
                currentHolding.TotalCostBase = holding.CalculatedCostBase;
                currentHolding.CurrentPrice = holding.CurrentPrice;
                currentHolding.CurrentMarketValue = holding.CalculatedMarketValue;
                currentHolding.UnrealisedProfitLoss = holding.CalculatedUnrealisedProfitLoss;
                currentHolding.UnrealisedProfitLossPercentage = holding.CalculatedUnrealisedProfitLossPercentage;
                currentHolding.CurrencyCode = request.CurrencyCode;
                currentHolding.LastUpdatedAt = DateTime.UtcNow;
                currentHolding.IsClosed = false;
            }
            else
            {
                dbContext.PortfolioHoldings.Add(new PortfolioHolding
                {
                    Id = Guid.NewGuid(),
                    InvestmentAccountId = request.InvestmentAccountId,
                    InvestmentInstrumentId = instrument.Id,
                    Units = holding.Units,
                    AverageCostPerUnit = holding.AverageCostPerUnit,
                    TotalCostBase = holding.CalculatedCostBase,
                    CurrentPrice = holding.CurrentPrice,
                    CurrentMarketValue = holding.CalculatedMarketValue,
                    UnrealisedProfitLoss = holding.CalculatedUnrealisedProfitLoss,
                    UnrealisedProfitLossPercentage = holding.CalculatedUnrealisedProfitLossPercentage,
                    CurrencyCode = request.CurrencyCode,
                    LastUpdatedAt = DateTime.UtcNow,
                    IsClosed = false
                });
            }
        }

        var importedInstrumentIds = validHoldings
            .Select(holding => existingInstruments[holding.InstrumentCode.Trim().ToUpperInvariant()].Id)
            .ToHashSet();

        var closedHoldings = 0;
        foreach (var stale in existingCurrentHoldings.Where(existing => !importedInstrumentIds.Contains(existing.InvestmentInstrumentId)))
        {
            stale.Units = 0m;
            stale.CurrentPrice = 0m;
            stale.CurrentMarketValue = 0m;
            stale.UnrealisedProfitLoss = Round2(0m - stale.TotalCostBase);
            stale.UnrealisedProfitLossPercentage = stale.TotalCostBase == 0m ? 0m : -100m;
            stale.LastUpdatedAt = DateTime.UtcNow;
            stale.IsClosed = true;
            closedHoldings++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new InvestmentImportCommitResult(
            importBatch.Id,
            snapshot.Id,
            validHoldings.Count,
            request.Issues.Count,
            closedHoldings,
            snapshot.TotalMarketValue,
            snapshot.TotalCostBase,
            snapshot.TotalUnrealisedProfitLoss,
            replacedExistingSnapshot,
            importBatch.ImportStatus.ToString(),
            warnings);
    }

    private static InvestmentImportPreviewResult BuildBlockingResult(
        Guid accountId,
        string fileName,
        string fileHash,
        IReadOnlyList<InvestmentImportIssueDto> issues)
    {
        return new InvestmentImportPreviewResult(
            accountId,
            fileName,
            fileHash,
            null,
            null,
            "AUD",
            0.05m,
            0,
            0,
            issues.Count,
            true,
            RequiredColumns,
            false,
            false,
            true,
            0m,
            0m,
            0m,
            [],
            issues);
    }

    private static string ExtractAccountNumber(IReadOnlyList<CsvRow> rows)
    {
        var row = rows.FirstOrDefault(r => GetField(r.Fields, 0).StartsWith("Account Number", StringComparison.OrdinalIgnoreCase));
        if (row is null)
        {
            return string.Empty;
        }

        var first = GetField(row.Fields, 0);
        var split = first.Split(':', 2);
        if (split.Length == 2)
        {
            return split[1].Trim();
        }

        return string.Empty;
    }

    private static DateTime? ExtractSnapshotDateTime(IReadOnlyList<CsvRow> rows)
    {
        var target = rows
            .Select(row => GetField(row.Fields, 0))
            .FirstOrDefault(value => value.Contains("As of", StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(target))
        {
            return null;
        }

        var match = Regex.Match(target, @"As of\s+(?<timestamp>.+)$", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return null;
        }

        var timestampText = match.Groups["timestamp"].Value
            .Replace("Sydney Time,", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(".", string.Empty)
            .Trim();

        var formats = new[]
        {
            "h:mm:ss tt dd MMM yyyy",
            "h:mm:ss tt, dd MMM yyyy",
            "H:mm:ss dd MMM yyyy"
        };

        foreach (var format in formats)
        {
            if (DateTime.TryParseExact(timestampText, format, CultureInfo.GetCultureInfo("en-AU"), DateTimeStyles.AssumeLocal, out var parsed))
            {
                return parsed;
            }
        }

        if (DateTime.TryParse(timestampText, CultureInfo.GetCultureInfo("en-AU"), DateTimeStyles.AssumeLocal, out var fallback))
        {
            return fallback;
        }

        return null;
    }

    private static CsvRow? FindHeader(IReadOnlyList<CsvRow> rows)
    {
        return rows.FirstOrDefault(row =>
        {
            var fields = row.Fields.Select(field => field.Trim()).ToList();
            return RequiredColumns.All(required => fields.Contains(required, StringComparer.OrdinalIgnoreCase));
        });
    }

    private static Dictionary<string, int> BuildColumnLookup(IReadOnlyList<string> header)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < header.Count; index++)
        {
            var column = header[index].Trim();
            if (!string.IsNullOrWhiteSpace(column) && !map.ContainsKey(column))
            {
                map[column] = index;
            }
        }

        return map;
    }

    private static int GetColumnIndex(Dictionary<string, int> columnLookup, string column)
    {
        return columnLookup.TryGetValue(column, out var index) ? index : -1;
    }

    private static bool IsSectionHeader(string firstField)
    {
        var normalized = firstField.Trim();
        return normalized.Equals("CHESS", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("Issuer Sponsored Holdings", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsIgnorableStructuralRow(string firstField)
    {
        var normalized = firstField.Trim();

        return normalized.StartsWith("Subtotal", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("Total", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("There are no", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("Account Number", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("Share Holdings", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("Code", StringComparison.OrdinalIgnoreCase);
    }

    private static decimal ParseRequiredDecimal(
        CsvRow row,
        Dictionary<string, int> columnLookup,
        string column,
        List<string> errors)
    {
        var index = GetColumnIndex(columnLookup, column);
        if (index < 0)
        {
            errors.Add($"Missing required column '{column}'.");
            return 0m;
        }

        var raw = GetField(row.Fields, index);
        if (string.IsNullOrWhiteSpace(raw))
        {
            errors.Add($"Column '{column}' is required.");
            return 0m;
        }

        if (!TryParseDecimal(raw, out var value))
        {
            errors.Add($"Column '{column}' contains invalid number '{raw}'.");
            return 0m;
        }

        return value;
    }

    private static decimal? ParseOptionalDecimal(
        CsvRow row,
        Dictionary<string, int> columnLookup,
        string column,
        List<string> errors)
    {
        var index = GetColumnIndex(columnLookup, column);
        if (index < 0)
        {
            return null;
        }

        var raw = GetField(row.Fields, index);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (!TryParseDecimal(raw, out var value))
        {
            errors.Add($"Column '{column}' contains invalid number '{raw}'.");
            return null;
        }

        return value;
    }

    private static bool TryParseDecimal(string raw, out decimal value)
    {
        var sanitized = raw.Trim().Replace(",", string.Empty, StringComparison.Ordinal);
        return decimal.TryParse(sanitized, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value)
            || decimal.TryParse(sanitized, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.GetCultureInfo("en-AU"), out value);
    }

    private static string ComputeFileHash(byte[] bytes)
    {
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static string GetField(IReadOnlyList<string> fields, int index)
    {
        if (index < 0 || index >= fields.Count)
        {
            return string.Empty;
        }

        return fields[index]?.Trim() ?? string.Empty;
    }

    private async Task<bool> IsDuplicateFileAsync(Guid accountId, string fileHash, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fileHash))
        {
            return false;
        }

        return await dbContext.PortfolioImportBatches
            .AsNoTracking()
            .AnyAsync(batch => batch.InvestmentAccountId == accountId && batch.FileHash == fileHash, cancellationToken);
    }

    private async Task<bool> IsDuplicateSnapshotAsync(Guid accountId, DateTime? snapshotDateTime, CancellationToken cancellationToken)
    {
        if (!snapshotDateTime.HasValue)
        {
            return false;
        }

        return await dbContext.PortfolioSnapshots
            .AsNoTracking()
            .AnyAsync(snapshot => snapshot.InvestmentAccountId == accountId && snapshot.SnapshotDateTime == snapshotDateTime.Value, cancellationToken);
    }

    private static decimal Round2(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static decimal Round6(decimal value) => decimal.Round(value, 6, MidpointRounding.AwayFromZero);

    private sealed record CsvRow(int RowNumber, string[] Fields);
}
