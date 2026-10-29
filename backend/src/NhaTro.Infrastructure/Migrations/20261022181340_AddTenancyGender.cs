using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NhaTro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenancyGender : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Gender",
                table: "Tenancies",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Gender",
                table: "Tenancies");
        }
    }
}
