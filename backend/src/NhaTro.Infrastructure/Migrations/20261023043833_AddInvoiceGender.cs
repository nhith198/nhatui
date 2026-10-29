using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NhaTro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceGender : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Gender",
                table: "Invoices",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(@"
UPDATE i SET i.Gender = t.Gender
FROM Invoices i
CROSS APPLY (
    SELECT TOP 1 tn.Gender
    FROM Tenancies tn
    WHERE tn.RoomId = i.RoomId AND tn.TenantName = i.TenantName
    ORDER BY tn.StartDate DESC
) t
WHERE i.Gender = '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Gender",
                table: "Invoices");
        }
    }
}
