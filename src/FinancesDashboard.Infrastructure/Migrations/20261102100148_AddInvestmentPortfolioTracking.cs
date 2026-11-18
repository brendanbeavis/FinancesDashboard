using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinancesDashboard.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInvestmentPortfolioTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccountNumber",
                table: "Accounts",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Accounts",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "Accounts",
                type: "TEXT",
                maxLength: 8,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IncludeInNetWorth",
                table: "Accounts",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "Accounts",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Accounts",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateTable(
                name: "InvestmentInstruments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    InstrumentType = table.Column<int>(type: "INTEGER", nullable: false),
                    Exchange = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    CurrencyCode = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvestmentInstruments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PortfolioImportBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    InvestmentAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    FileHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ImportDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SnapshotDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TotalRowsRead = table.Column<int>(type: "INTEGER", nullable: false),
                    SuccessfulRows = table.Column<int>(type: "INTEGER", nullable: false),
                    FailedRows = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalMarketValue = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TotalCostBase = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    ImportStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorSummary = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortfolioImportBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PortfolioImportBatches_Accounts_InvestmentAccountId",
                        column: x => x.InvestmentAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PortfolioSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    InvestmentAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SnapshotDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CurrencyCode = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    TotalMarketValue = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TotalCostBase = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TotalUnrealisedProfitLoss = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TotalUnrealisedProfitLossPercentage = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    TotalDailyValueChange = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortfolioSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PortfolioSnapshots_Accounts_InvestmentAccountId",
                        column: x => x.InvestmentAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PortfolioHoldings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    InvestmentAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    InvestmentInstrumentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Units = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    AverageCostPerUnit = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    TotalCostBase = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    CurrentPrice = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    CurrentMarketValue = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    UnrealisedProfitLoss = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    UnrealisedProfitLossPercentage = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    CurrencyCode = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsClosed = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortfolioHoldings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PortfolioHoldings_Accounts_InvestmentAccountId",
                        column: x => x.InvestmentAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PortfolioHoldings_InvestmentInstruments_InvestmentInstrumentId",
                        column: x => x.InvestmentInstrumentId,
                        principalTable: "InvestmentInstruments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PortfolioImportIssues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PortfolioImportBatchId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RowNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    InstrumentCode = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    RawRow = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortfolioImportIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PortfolioImportIssues_PortfolioImportBatches_PortfolioImportBatchId",
                        column: x => x.PortfolioImportBatchId,
                        principalTable: "PortfolioImportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HoldingSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PortfolioSnapshotId = table.Column<Guid>(type: "TEXT", nullable: false),
                    InvestmentInstrumentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Units = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    AverageCostPerUnit = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    TotalCostBase = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    MarketPrice = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    MarketValue = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    DailyPriceChange = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    DailyPriceChangePercentage = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    UnrealisedProfitLoss = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    UnrealisedProfitLossPercentage = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    PortfolioWeightPercentage = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HoldingSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HoldingSnapshots_InvestmentInstruments_InvestmentInstrumentId",
                        column: x => x.InvestmentInstrumentId,
                        principalTable: "InvestmentInstruments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HoldingSnapshots_PortfolioSnapshots_PortfolioSnapshotId",
                        column: x => x.PortfolioSnapshotId,
                        principalTable: "PortfolioSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_Type",
                table: "Accounts",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_HoldingSnapshots_InvestmentInstrumentId",
                table: "HoldingSnapshots",
                column: "InvestmentInstrumentId");

            migrationBuilder.CreateIndex(
                name: "IX_HoldingSnapshots_PortfolioSnapshotId_InvestmentInstrumentId",
                table: "HoldingSnapshots",
                columns: new[] { "PortfolioSnapshotId", "InvestmentInstrumentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentInstruments_Exchange_Code",
                table: "InvestmentInstruments",
                columns: new[] { "Exchange", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioHoldings_InvestmentAccountId",
                table: "PortfolioHoldings",
                column: "InvestmentAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioHoldings_InvestmentAccountId_InvestmentInstrumentId",
                table: "PortfolioHoldings",
                columns: new[] { "InvestmentAccountId", "InvestmentInstrumentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioHoldings_InvestmentInstrumentId",
                table: "PortfolioHoldings",
                column: "InvestmentInstrumentId");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioImportBatches_InvestmentAccountId_FileHash",
                table: "PortfolioImportBatches",
                columns: new[] { "InvestmentAccountId", "FileHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioImportBatches_InvestmentAccountId_SnapshotDateTime",
                table: "PortfolioImportBatches",
                columns: new[] { "InvestmentAccountId", "SnapshotDateTime" });

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioImportIssues_PortfolioImportBatchId",
                table: "PortfolioImportIssues",
                column: "PortfolioImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioSnapshots_InvestmentAccountId_SnapshotDateTime",
                table: "PortfolioSnapshots",
                columns: new[] { "InvestmentAccountId", "SnapshotDateTime" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HoldingSnapshots");

            migrationBuilder.DropTable(
                name: "PortfolioHoldings");

            migrationBuilder.DropTable(
                name: "PortfolioImportIssues");

            migrationBuilder.DropTable(
                name: "PortfolioSnapshots");

            migrationBuilder.DropTable(
                name: "InvestmentInstruments");

            migrationBuilder.DropTable(
                name: "PortfolioImportBatches");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_Type",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "AccountNumber",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "IncludeInNetWorth",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Accounts");
        }
    }
}
