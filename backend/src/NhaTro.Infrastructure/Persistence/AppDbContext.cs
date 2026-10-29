using Microsoft.EntityFrameworkCore;
using NhaTro.Application.Abstractions;
using NhaTro.Domain.Entities;

namespace NhaTro.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Property> Properties => Set<Property>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Tenancy> Tenancies => Set<Tenancy>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<DepositReceipt> DepositReceipts => Set<DepositReceipt>();
    public DbSet<FinancialExpense> FinancialExpenses => Set<FinancialExpense>();
    public DbSet<StaffAssignment> StaffAssignments => Set<StaffAssignment>();
    public DbSet<EmployeePagePermission> EmployeePagePermissions => Set<EmployeePagePermission>();
    public DbSet<RoomImage> RoomImages => Set<RoomImage>();
    public DbSet<Complaint> Complaints => Set<Complaint>();
    public DbSet<Occupant> Occupants => Set<Occupant>();

    public IQueryable<Property> AccessibleProperties(Guid userId) => Properties.Where(p => Users.Any(u =>
        u.Id == userId && !u.IsDisabled &&
        (p.OwnerId == userId || (u.EmployerId == p.OwnerId && StaffAssignments.Any(a => a.UserId == userId && a.PropertyId == p.Id)))));

    protected override void ConfigureConventions(ModelConfigurationBuilder cb)
        => cb.Properties<decimal>().HavePrecision(18, 2);

    protected override void OnModelCreating(ModelBuilder b)
        => b.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
