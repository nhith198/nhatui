using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NhaTro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RoomElectricRate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ElectricRate",
                table: "Rooms",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ElectricRate",
                table: "Rooms");
        }
    }
}
