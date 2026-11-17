namespace FinancesDashboard.Application.Dto;

public sealed record InvestmentImportPreviewHoldingDto(
    int RowNumber,
    string InstrumentCode,
    string Section,
    decimal Units,
    decimal AverageCostPerUnit,
    decimal CurrentPrice,
    decimal CalculatedCostBase,
    decimal CalculatedMarketValue,
    decimal CalculatedUnrealisedProfitLoss,
    decimal CalculatedUnrealisedProfitLossPercentage,
    decimal? SourceMarketValue,
    decimal? SourceUnrealisedProfitLoss,
    decimal? SourceUnrealisedProfitLossPercentage,
    decimal? SourceWeightPercentage,
    decimal? DailyPriceChange,
    decimal? DailyPriceChangePercentage,
    decimal? DailyValueChange,
    bool IsValid,
    IReadOnlyList<string> ValidationErrors,
    IReadOnlyList<string> ReconciliationWarnings,
    string RawRow);
