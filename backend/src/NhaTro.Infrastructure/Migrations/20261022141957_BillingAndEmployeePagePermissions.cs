using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NhaTro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BillingAndEmployeePagePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OccupantCount",
                table: "Rooms",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<decimal>(
                name: "ServiceAmount",
                table: "Rooms",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "WaterByOccupant",
                table: "Rooms",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "BankAccountName",
                table: "Properties",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BankAccountNumber",
                table: "Properties",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BankName",
                table: "Properties",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "WaterRatePerPerson",
                table: "Properties",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "OccupantCount",
                table: "Invoices",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<decimal>(
                name: "ServiceAmount",
                table: "Invoices",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "WaterByOccupant",
                table: "Invoices",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "WaterRatePerPerson",
                table: "Invoices",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "EmployeePagePermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Page = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeePagePermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeePagePermissions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePagePermissions_UserId_Page",
                table: "EmployeePagePermissions",
                columns: new[] { "UserId", "Page" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeePagePermissions");

            migrationBuilder.DropColumn(
                name: "OccupantCount",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "ServiceAmount",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "WaterByOccupant",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "BankAccountName",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "BankAccountNumber",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "BankName",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "WaterRatePerPerson",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "OccupantCount",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ServiceAmount",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "WaterByOccupant",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "WaterRatePerPerson",
                table: "Invoices");
        }
    }
}
