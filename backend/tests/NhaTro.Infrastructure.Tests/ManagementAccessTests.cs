using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NhaTro.Application;
using NhaTro.Application.Abstractions;
using NhaTro.Application.UseCases;
using NhaTro.Domain.Entities;
using NhaTro.Infrastructure;
using NhaTro.Infrastructure.Persistence;

namespace NhaTro.Infrastructure.Tests;

public class ManagementAccessTests
{
    private static readonly CancellationToken Token = CancellationToken.None;

    private sealed class Fixture : IDisposable
    {
        public AppDbContext Db { get; } = new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public User Owner { get; } = User.RegisterWithPhone("09123456781", "Owner", "hash");
        public User OtherOwner { get; } = User.RegisterWithPhone("09123456782", "Other", "hash");
        public User Employee { get; }
        public Property Assigned { get; }
        public Property Unassigned { get; }
        public Property Foreign { get; }
        public Room AssignedRoom { get; }
        public Room HiddenRoom { get; }
        public ServiceProvider Provider { get; }
        private readonly IServiceScope scope;
        public IServiceProvider Services => scope.ServiceProvider;

        public Fixture()
        {
            Employee = User.CreateEmployee(Owner.Id, "09123456780", "Staff", "hash");
            Assigned = Property.Create(Owner.Id, "Assigned", "", 3500, 18000);
            Unassigned = Property.Create(Owner.Id, "Unassigned", "", 3500, 18000);
            Foreign = Property.Create(OtherOwner.Id, "Foreign", "", 3500, 18000);
            AssignedRoom = Room.Create(Assigned.Id, "101", 3000000, 0, 0);
            HiddenRoom = Room.Create(Unassigned.Id, "201", 3000000, 0, 0);
            AssignedRoom.MoveIn("Tenant A", "0901", new DateOnly(2026, 1, 1), 3000000);
            HiddenRoom.MoveIn("Tenant B", "0902", new DateOnly(2026, 1, 1), 3000000);
            Db.Users.AddRange(Owner, OtherOwner, Employee);
            Db.Properties.AddRange(Assigned, Unassigned, Foreign);
            Db.Rooms.AddRange(AssignedRoom, HiddenRoom);
            Db.StaffAssignments.Add(StaffAssignment.Create(Employee.Id, Assigned.Id));
            Db.Complaints.AddRange(
                Complaint.Create(AssignedRoom.Id, "Tenant A", "Water", "Leak", "Urgent", DateTimeOffset.UtcNow),
                Complaint.Create(HiddenRoom.Id, "Tenant B", "Power", "Outage", "Normal", DateTimeOffset.UtcNow));
            Db.SaveChanges();
            var services = new ServiceCollection();
            services.AddApplication();
            services.AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "Server=localhost;Database=Unused;Integrated Security=True;TrustServerCertificate=True"
            }).Build());
            services.AddScoped(_ => Db);
            Provider = services.BuildServiceProvider();
            scope = Provider.CreateScope();
        }

        public void Dispose() { scope.Dispose(); Provider.Dispose(); Db.Dispose(); }
    }

    [Fact]
    public async Task Employee_reads_only_assigned_rooms_tenants_and_complaints()
    {
        using var fixture = new Fixture();
        var userId = fixture.Employee.Id;
        var properties = await fixture.Services.GetRequiredService<IPropertyQueries>().ListAsync(userId, Token);
        Assert.Equal(fixture.Assigned.Id, Assert.Single(properties).Id);
        Assert.False(properties[0].CanManage);
        Assert.Single(await fixture.Services.GetRequiredService<IRoomQueries>().ListAsync(userId, null, Token));
        var store = fixture.Services.GetRequiredService<IManagementRepository>();
        Assert.Single(await store.ListTenanciesAsync(userId, null, new DateOnly(2026, 10, 20), Token));
        Assert.Single(await store.ListComplaintsAsync(userId, null, Token));
        Assert.Empty(await store.ListComplaintsAsync(userId, fixture.Unassigned.Id, Token));
        Assert.Null(await fixture.Services.GetRequiredService<IRoomRepository>().GetOwnedAsync(fixture.HiddenRoom.Id, userId, Token));
    }

    [Fact]
    public async Task Revoking_assignment_or_disabling_account_immediately_removes_access()
    {
        using var fixture = new Fixture();
        var store = fixture.Services.GetRequiredService<IManagementRepository>();
        await store.ReplaceAssignmentsAsync(fixture.Employee.Id, [], Token);
        await fixture.Db.SaveChangesAsync();
        Assert.Empty(await fixture.Db.AccessibleProperties(fixture.Employee.Id).ToListAsync());
        await store.ReplaceAssignmentsAsync(fixture.Employee.Id, [fixture.Assigned.Id], Token);
        await fixture.Db.SaveChangesAsync();
        fixture.Employee.UpdateEmployee("Staff", fixture.Employee.Phone!, true);
        await fixture.Db.SaveChangesAsync();
        Assert.Empty(await fixture.Db.AccessibleProperties(fixture.Employee.Id).ToListAsync());
        await Assert.ThrowsAsync<UnauthorizedException>(() => fixture.Services.GetRequiredService<ManagementService>().Account(fixture.Employee.Id, Token));
    }

    [Fact]
    public async Task Employee_can_issue_invoice_but_cannot_manage_employees_or_delete_room()
    {
        using var fixture = new Fixture();
        var invoice = await fixture.Services.GetRequiredService<ICommandHandler<CreateInvoiceCommand, NhaTro.Application.Dtos.InvoiceDto>>()
            .Handle(new(fixture.Employee.Id, fixture.AssignedRoom.Id, "2026-10", 10, 2, 0), Token);
        Assert.Equal(fixture.AssignedRoom.Id, invoice.RoomId);
        await Assert.ThrowsAsync<ForbiddenException>(() => fixture.Services.GetRequiredService<ManagementService>().Employees(fixture.Employee.Id, Token));
        await Assert.ThrowsAsync<NotFoundException>(() => fixture.Services.GetRequiredService<ICommandHandler<DeleteRoomCommand>>()
            .Handle(new(fixture.Employee.Id, fixture.AssignedRoom.Id), Token));
        Assert.Single(await fixture.Services.GetRequiredService<IInvoiceQueries>().ListAsync(fixture.Employee.Id, "2026-10", null, Token));
    }

    [Fact]
    public async Task Owner_cannot_assign_foreign_property_or_employee_to_complaint()
    {
        using var fixture = new Fixture();
        var service = fixture.Services.GetRequiredService<ManagementService>();
        await Assert.ThrowsAsync<NotFoundException>(() => service.SaveEmployee(fixture.Owner.Id, fixture.Employee.Id,
            new("Staff", fixture.Employee.Phone!, null, false, [fixture.Foreign.Id], [EmployeePages.Rooms]), Token));
        var complaint = await fixture.Db.Complaints.FirstAsync(item => item.RoomId == fixture.HiddenRoom.Id);
        await Assert.ThrowsAsync<NhaTro.Domain.Common.DomainException>(() => service.UpdateComplaint(fixture.Owner.Id, complaint.Id, "InProgress", fixture.Employee.Id, "", Token));
    }

    [Fact]
    public async Task Image_operations_require_room_access_and_preserve_binary_content()
    {
        using var fixture = new Fixture();
        var service = fixture.Services.GetRequiredService<ManagementService>();
        var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aShoAAAAASUVORK5CYII=");
        var id = await service.AddImage(fixture.Employee.Id, fixture.AssignedRoom.Id, "Room", "image/png", bytes, Token);
        var image = Assert.Single(await service.Images(fixture.Employee.Id, fixture.AssignedRoom.Id, Token));
        Assert.Equal(bytes, Convert.FromBase64String(image.Base64));
        await Assert.ThrowsAsync<NotFoundException>(() => service.Images(fixture.Employee.Id, fixture.HiddenRoom.Id, Token));
        await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteImage(fixture.OtherOwner.Id, fixture.AssignedRoom.Id, id, Token));
        await service.DeleteImage(fixture.Owner.Id, fixture.AssignedRoom.Id, id, Token);
        Assert.Empty(await service.Images(fixture.Owner.Id, fixture.AssignedRoom.Id, Token));
    }

    [Fact]
    public void Access_filter_translates_to_sql_server_and_new_migration_exists()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer("Server=localhost;Database=Unused;Integrated Security=True;TrustServerCertificate=True").Options);
        var sql = db.AccessibleProperties(Guid.NewGuid()).ToQueryString();
        Assert.Contains("StaffAssignments", sql);
        Assert.Contains("IsDisabled", sql);
        Assert.Contains(db.Database.GetMigrations(), migration => migration.EndsWith("_PropertyManagement", StringComparison.Ordinal));
        Assert.False(db.Database.HasPendingModelChanges());
    }
}