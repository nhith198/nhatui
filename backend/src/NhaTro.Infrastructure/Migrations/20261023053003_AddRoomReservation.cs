using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NhaTro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomReservation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "ReservedDate",
                table: "Rooms",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReservedDeposit",
                table: "Rooms",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReservedPhone",
                table: "Rooms",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReservedTenantName",
                table: "Rooms",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReservedDate",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "ReservedDeposit",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "ReservedPhone",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "ReservedTenantName",
                table: "Rooms");
        }
    }
}
