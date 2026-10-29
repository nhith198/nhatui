using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NhaTro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPropertyQrCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "QrCode",
                table: "Properties",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QrContentType",
                table: "Properties",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QrCode",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "QrContentType",
                table: "Properties");
        }
    }
}
