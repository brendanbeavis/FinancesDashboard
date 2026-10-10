using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinancesDashboard.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MortgageRefactorToAccountBased : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentPrincipal",
                table: "Mortgages");

            migrationBuilder.DropColumn(
                name: "ExpectedEndDate",
                table: "Mortgages");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "Mortgages");

            migrationBuilder.DropColumn(
                name: "OriginalPrincipal",
                table: "Mortgages");

            migrationBuilder.RenameColumn(
                name: "StartDate",
                table: "Mortgages",
                newName: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Mortgages_AccountId",
                table: "Mortgages",
                column: "AccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_Mortgages_Accounts_AccountId",
                table: "Mortgages",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Mortgages_Accounts_AccountId",
                table: "Mortgages");

            migrationBuilder.DropIndex(
                name: "IX_Mortgages_AccountId",
                table: "Mortgages");

            migrationBuilder.RenameColumn(
                name: "AccountId",
                table: "Mortgages",
                newName: "StartDate");

            migrationBuilder.AddColumn<decimal>(
                name: "CurrentPrincipal",
                table: "Mortgages",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ExpectedEndDate",
                table: "Mortgages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Mortgages",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "OriginalPrincipal",
                table: "Mortgages",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }
    }
}
