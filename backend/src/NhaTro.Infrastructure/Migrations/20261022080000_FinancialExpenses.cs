using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using NhaTro.Infrastructure.Persistence;

#nullable disable

namespace NhaTro.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261022080000_FinancialExpenses")]
public partial class FinancialExpenses : Migration
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasAnnotation("ProductVersion", "10.0.13")
            .HasAnnotation("Relational:MaxIdentifierLength", 128);
        SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

        modelBuilder.Entity("NhaTro.Domain.Entities.FinancialExpense", b =>
        {
            b.Property<Guid>("Id").HasColumnType("uniqueidentifier");
            b.Property<decimal>("Amount").HasPrecision(18, 2).HasColumnType("decimal(18,2)");
            b.Property<string>("Category").IsRequired().HasMaxLength(20).HasColumnType("nvarchar(20)");
            b.Property<DateOnly>("Date").HasColumnType("date");
            b.Property<string>("Description").IsRequired().HasMaxLength(200).HasColumnType("nvarchar(200)");
            b.Property<Guid>("OwnerId").HasColumnType("uniqueidentifier");
            b.Property<Guid?>("PropertyId").HasColumnType("uniqueidentifier");
            b.Property<string>("Recipient").HasMaxLength(200).HasColumnType("nvarchar(200)");
            b.HasKey("Id");
            b.HasIndex("OwnerId", "Date");
            b.HasIndex("PropertyId");
            b.ToTable("FinancialExpenses");
        });

        modelBuilder.Entity("NhaTro.Domain.Entities.FinancialExpense", b =>
        {
            b.HasOne("NhaTro.Domain.Entities.User", null).WithMany().HasForeignKey("OwnerId")
                .OnDelete(DeleteBehavior.NoAction).IsRequired();
            b.HasOne("NhaTro.Domain.Entities.Property", null).WithMany().HasForeignKey("PropertyId")
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "FinancialExpenses",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Category = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Recipient = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                Date = table.Column<DateOnly>(type: "date", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FinancialExpenses", x => x.Id);
                table.ForeignKey("FK_FinancialExpenses_Properties_PropertyId", x => x.PropertyId,
                    "Properties", "Id", onDelete: ReferentialAction.SetNull);
                table.ForeignKey("FK_FinancialExpenses_Users_OwnerId", x => x.OwnerId,
                    "Users", "Id", onDelete: ReferentialAction.NoAction);
            });

        migrationBuilder.CreateIndex("IX_FinancialExpenses_OwnerId_Date", "FinancialExpenses", new[] { "OwnerId", "Date" });
        migrationBuilder.CreateIndex("IX_FinancialExpenses_PropertyId", "FinancialExpenses", "PropertyId");
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "FinancialExpenses");
}