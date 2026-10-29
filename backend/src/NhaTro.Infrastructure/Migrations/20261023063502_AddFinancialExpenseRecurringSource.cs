using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NhaTro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialExpenseRecurringSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "LastGeneratedMonth",
                table: "FinancialExpenses",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RecurringSourceId",
                table: "FinancialExpenses",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastGeneratedMonth",
                table: "FinancialExpenses");

            migrationBuilder.DropColumn(
                name: "RecurringSourceId",
                table: "FinancialExpenses");
        }
    }
}
