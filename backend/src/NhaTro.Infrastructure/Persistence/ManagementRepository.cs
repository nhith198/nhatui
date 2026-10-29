using Microsoft.EntityFrameworkCore;
using NhaTro.Application.Abstractions;
using NhaTro.Application.Dtos;
using NhaTro.Domain.Entities;

namespace NhaTro.Infrastructure.Persistence;

internal class ManagementRepository(AppDbContext db) : IManagementRepository
{
    public Task<User?> GetUserAsync(Guid id, CancellationToken ct) => db.Users.FirstOrDefaultAsync(user => user.Id == id, ct);

    public Task<List<string>> GetPagePermissionsAsync(Guid userId, CancellationToken ct) => db.EmployeePagePermissions
        .Where(permission => permission.UserId == userId).Select(permission => permission.Page).ToListAsync(ct);

    public async Task<List<TenancyDto>> ListTenanciesAsync(Guid userId, Guid? propertyId, DateOnly today, CancellationToken ct)
    {
        var rows = await (from tenancy in db.Tenancies.AsNoTracking()
                          join room in db.Rooms on tenancy.RoomId equals room.Id
                          join property in db.AccessibleProperties(userId) on room.PropertyId equals property.Id
                          where propertyId == null || property.Id == propertyId
                          orderby tenancy.EndDate != null, property.Name, room.Name, tenancy.StartDate descending
                          select new { Tenancy = tenancy, Room = room.Name, PropertyId = property.Id, Property = property.Name }).ToListAsync(ct);
        return rows.Select(row => new TenancyDto(row.Tenancy.Id, row.Tenancy.RoomId, row.PropertyId, row.Property, row.Room,
            row.Tenancy.TenantName, row.Tenancy.Phone, row.Tenancy.IdentityNumber, row.Tenancy.Address,
            row.Tenancy.Notes, row.Tenancy.StartDate, row.Tenancy.ExpectedEndDate, row.Tenancy.EndDate,
            row.Tenancy.Deposit, row.Tenancy.ContractStatus(today), row.Tenancy.Gender, row.Tenancy.TemporaryResidenceRegistered)).ToList();
    }

    public Task<List<EmployeeDto>> ListEmployeesAsync(Guid ownerId, CancellationToken ct) => db.Users
        .Where(user => user.EmployerId == ownerId).OrderBy(user => user.FullName)
        .Select(user => new EmployeeDto(user.Id, user.FullName, user.Phone ?? "", user.IsDisabled,
            db.StaffAssignments.Where(assignment => assignment.UserId == user.Id).Select(assignment => assignment.PropertyId).ToList(),
            db.EmployeePagePermissions.Where(permission => permission.UserId == user.Id).Select(permission => permission.Page).ToList()))
        .ToListAsync(ct);

    public Task<List<Complaint>> ListComplaintsAssignedToAsync(Guid employeeId, CancellationToken ct) =>
        db.Complaints.Where(complaint => complaint.AssignedTo == employeeId).ToListAsync(ct);

    public async Task ReplaceAssignmentsAsync(Guid employeeId, IReadOnlyCollection<Guid> propertyIds, CancellationToken ct)
    {
        var current = await db.StaffAssignments.Where(assignment => assignment.UserId == employeeId).ToListAsync(ct);
        db.StaffAssignments.RemoveRange(current.Where(assignment => !propertyIds.Contains(assignment.PropertyId)));
        foreach (var propertyId in propertyIds.Where(propertyId => !current.Any(assignment => assignment.PropertyId == propertyId)))
            db.StaffAssignments.Add(StaffAssignment.Create(employeeId, propertyId));
    }

    public async Task ReplacePagePermissionsAsync(Guid employeeId, IReadOnlyCollection<string> pages, CancellationToken ct)
    {
        var current = await db.EmployeePagePermissions.Where(permission => permission.UserId == employeeId).ToListAsync(ct);
        db.EmployeePagePermissions.RemoveRange(current.Where(permission => !pages.Contains(permission.Page)));
        foreach (var page in pages.Where(page => !current.Any(permission => permission.Page == page)))
            db.EmployeePagePermissions.Add(EmployeePagePermission.Create(employeeId, page));
    }

    public Task<bool> CanAssignAsync(Guid userId, Guid propertyId, CancellationToken ct) => db.AccessibleProperties(userId).AnyAsync(property => property.Id == propertyId, ct);

    public Task<List<AssigneeDto>> ListAssigneesAsync(Guid userId, Guid propertyId, CancellationToken ct) => db.Users
        .Where(user => !user.IsDisabled && db.Properties.Any(property => property.Id == propertyId &&
            (property.OwnerId == user.Id || (user.EmployerId == property.OwnerId && db.StaffAssignments.Any(assignment => assignment.UserId == user.Id && assignment.PropertyId == property.Id)))))
        .OrderBy(user => user.FullName).Select(user => new AssigneeDto(user.Id, user.FullName)).ToListAsync(ct);

    public Task<List<ComplaintDto>> ListComplaintsAsync(Guid userId, Guid? propertyId, CancellationToken ct) =>
        (from complaint in db.Complaints
         join room in db.Rooms on complaint.RoomId equals room.Id
         join property in db.AccessibleProperties(userId) on room.PropertyId equals property.Id
         where propertyId == null || property.Id == propertyId
         orderby complaint.CreatedAt descending
         select new ComplaintDto(complaint.Id, room.Id, property.Id, property.Name, room.Name, complaint.TenantName,
             complaint.Title, complaint.Description, complaint.Priority, complaint.Status, complaint.AssignedTo,
             db.Users.Where(user => user.Id == complaint.AssignedTo).Select(user => user.FullName).FirstOrDefault(),
             complaint.Resolution, complaint.CreatedAt, complaint.ResolvedAt)).ToListAsync(ct);

    public Task<List<RepairRequestDto>> ListRepairsAsync(Guid userId, Guid? propertyId, CancellationToken ct) =>
        (from room in db.Rooms
         join property in db.AccessibleProperties(userId) on room.PropertyId equals property.Id
         where room.MaintenanceNote != null && room.MaintenanceStatus != null && (propertyId == null || property.Id == propertyId)
         orderby property.Name, room.Name
         select new RepairRequestDto(room.Id, property.Id, property.Name, room.Name,
             db.Tenancies.Where(tenancy => tenancy.RoomId == room.Id && tenancy.EndDate == null).Select(tenancy => tenancy.TenantName).FirstOrDefault(),
             room.MaintenanceNote!, room.MaintenanceStatus!)).ToListAsync(ct);

    public Task<Complaint?> GetComplaintAsync(Guid id, Guid userId, CancellationToken ct) => db.Complaints
        .FirstOrDefaultAsync(complaint => complaint.Id == id && db.Rooms.Any(room => room.Id == complaint.RoomId
            && db.AccessibleProperties(userId).Any(property => property.Id == room.PropertyId)), ct);
    public void AddComplaint(Complaint complaint) => db.Complaints.Add(complaint);
    public void RemoveComplaint(Complaint complaint) => db.Complaints.Remove(complaint);

    public async Task<List<RoomImageDto>> ListImagesAsync(Guid roomId, CancellationToken ct)
    {
        var images = await db.RoomImages.AsNoTracking().Where(image => image.RoomId == roomId).OrderBy(image => image.CreatedAt).ToListAsync(ct);
        return images.Select(image => new RoomImageDto(image.Id, image.Caption, image.ContentType, Convert.ToBase64String(image.Data))).ToList();
    }
    public Task<RoomImage?> GetImageAsync(Guid id, Guid roomId, CancellationToken ct) => db.RoomImages.FirstOrDefaultAsync(image => image.Id == id && image.RoomId == roomId, ct);
    public void AddImage(RoomImage image) => db.RoomImages.Add(image);
    public void RemoveImage(RoomImage image) => db.RoomImages.Remove(image);

    public Task<List<OccupantDto>> ListOccupantsAsync(Guid userId, Guid? propertyId, CancellationToken ct) =>
        (from occupant in db.Occupants.AsNoTracking()
         join room in db.Rooms on occupant.RoomId equals room.Id
         join property in db.AccessibleProperties(userId) on room.PropertyId equals property.Id
         where propertyId == null || property.Id == propertyId
         orderby property.Name, room.Name, occupant.Name
         select new OccupantDto(occupant.Id, room.Id, property.Id, property.Name, room.Name, occupant.Name, occupant.Phone,
             occupant.IdentityNumber, occupant.Gender, occupant.Address, occupant.Notes, occupant.TemporaryResidenceRegistered)).ToListAsync(ct);
    public Task<Occupant?> GetOccupantAsync(Guid id, Guid userId, CancellationToken ct) => db.Occupants
        .FirstOrDefaultAsync(occupant => occupant.Id == id && db.Rooms.Any(room => room.Id == occupant.RoomId
            && db.AccessibleProperties(userId).Any(property => property.Id == room.PropertyId)), ct);
    public void AddOccupant(Occupant occupant) => db.Occupants.Add(occupant);
    public void RemoveOccupant(Occupant occupant) => db.Occupants.Remove(occupant);
}