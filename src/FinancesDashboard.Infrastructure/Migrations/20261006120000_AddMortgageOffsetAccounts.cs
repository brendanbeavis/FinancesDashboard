using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinancesDashboard.Infrastructure.Migrations
{
    public partial class AddMortgageOffsetAccounts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MortgageOffsetAccounts",
                columns: table => new
                {
                    MortgageAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OffsetAccountId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MortgageOffsetAccounts", x => new { x.MortgageAccountId, x.OffsetAccountId });
                    table.ForeignKey(
                        name: "FK_MortgageOffsetAccounts_Accounts_MortgageAccountId",
                        column: x => x.MortgageAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MortgageOffsetAccounts_Accounts_OffsetAccountId",
                        column: x => x.OffsetAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MortgageOffsetAccounts_OffsetAccountId",
                table: "MortgageOffsetAccounts",
                column: "OffsetAccountId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MortgageOffsetAccounts");
        }
    }
}
