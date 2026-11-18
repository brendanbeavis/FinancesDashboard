using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinancesDashboard.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Mortgages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    OriginalPrincipal = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    CurrentPrincipal = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    AnnualInterestRate = table.Column<decimal>(type: "TEXT", precision: 9, scale: 6, nullable: false),
                    ScheduledRepayment = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    RepaymentsPerYear = table.Column<int>(type: "INTEGER", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    ExpectedEndDate = table.Column<DateOnly>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mortgages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AccountGroupMembers",
                columns: table => new
                {
                    AccountGroupId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AccountId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountGroupMembers", x => new { x.AccountGroupId, x.AccountId });
                    table.ForeignKey(
                        name: "FK_AccountGroupMembers_AccountGroups_AccountGroupId",
                        column: x => x.AccountGroupId,
                        principalTable: "AccountGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AccountGroupMembers_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    BankBalance = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    ImportHash = table.Column<string>(type: "TEXT", nullable: false),
                    ImportedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Transactions_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InterestRatePeriods",
                columns: table => new
                {
                    MortgageId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    AnnualInterestRate = table.Column<decimal>(type: "TEXT", precision: 9, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterestRatePeriods", x => new { x.MortgageId, x.StartDate });
                    table.ForeignKey(
                        name: "FK_InterestRatePeriods_Mortgages_MortgageId",
                        column: x => x.MortgageId,
                        principalTable: "Mortgages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountGroupMembers_AccountId",
                table: "AccountGroupMembers",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_AccountId",
                table: "Transactions",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_AccountId_Date",
                table: "Transactions",
                columns: new[] { "AccountId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_Date",
                table: "Transactions",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_ImportHash",
                table: "Transactions",
                column: "ImportHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountGroupMembers");

            migrationBuilder.DropTable(
                name: "InterestRatePeriods");

            migrationBuilder.DropTable(
                name: "Transactions");

            migrationBuilder.DropTable(
                name: "AccountGroups");

            migrationBuilder.DropTable(
                name: "Mortgages");

            migrationBuilder.DropTable(
                name: "Accounts");
        }
    }
}
