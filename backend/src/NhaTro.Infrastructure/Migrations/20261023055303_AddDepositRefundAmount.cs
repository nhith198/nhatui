using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NhaTro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDepositRefundAmount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "RefundedAmount",
                table: "DepositReceipts",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            // Legacy refunds were always full refunds.
            migrationBuilder.Sql("UPDATE DepositReceipts SET RefundedAmount = Amount WHERE RefundedAt IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RefundedAmount",
                table: "DepositReceipts");
        }
    }
}
