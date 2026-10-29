using NhaTro.Application.Abstractions;
using NhaTro.Application.Dtos;
using NhaTro.Domain.Common;
using NhaTro.Domain.Entities;

namespace NhaTro.Application.UseCases;

public sealed record TenantProfile(string Name, string? Phone, string? IdentityNumber,
    string? Address, string? Notes, DateOnly? ExpectedEndDate, string? Gender, bool TemporaryResidenceRegistered = false);
public sealed record OccupantProfile(string Name, string? Phone, string? IdentityNumber, string? Gender, string? Address, string? Notes, bool TemporaryResidenceRegistered = false);
public sealed record EmployeeInput(string FullName, string Phone, string? Password, bool IsDisabled, List<Guid> PropertyIds, List<string> PagePermissions);
public sealed record AccountProfileInput(string FullName, string Phone, string? CurrentPassword, string? NewPassword);

public class ManagementService(IManagementRepository store, IRoomRepository rooms, IPropertyRepository properties,
    IUserRepository users, IPasswordHasher hasher, IUnitOfWork uow, TimeProvider clock)
{
    public async Task<AccountDto> Account(Guid userId, CancellationToken ct)
    {
        var user = await store.GetUserAsync(userId, ct) ?? throw new UnauthorizedException("Tài khoản không tồn tại");
        if (user.IsDisabled) throw new UnauthorizedException("Tài khoản đã bị khóa");
        var isEmployee = user.EmployerId is not null;
        var permissions = isEmployee ? await store.GetPagePermissionsAsync(user.Id, ct) : EmployeePages.All.Order().ToList();
        return new(user.Id, user.FullName, user.Phone, isEmployee, permissions);
    }

    public async Task<AccountDto> UpdateAccount(Guid userId, AccountProfileInput input, CancellationToken ct)
    {
        var user = await store.GetUserAsync(userId, ct) ?? throw new UnauthorizedException("Tài khoản không tồn tại");
        if (user.IsDisabled) throw new UnauthorizedException("Tài khoản đã bị khóa");
        var phone = User.NormalizePhone(input.Phone);
        if (!string.Equals(user.Phone, phone, StringComparison.Ordinal) && await users.PhoneExistsAsync(phone, ct))
            throw new ConflictException("Số điện thoại đã được đăng ký");
        if (!string.IsNullOrEmpty(input.NewPassword))
        {
            if (input.NewPassword.Length is < 8 or > 128) throw new DomainException("Mật khẩu mới phải từ 8 đến 128 ký tự");
            if (string.IsNullOrEmpty(input.CurrentPassword) || !hasher.Verify(user.PasswordHash, input.CurrentPassword))
                throw new DomainException("Mật khẩu hiện tại không đúng");
            user.ChangePassword(hasher.Hash(input.NewPassword));
        }
        user.UpdateProfile(input.FullName, phone);
        await uow.SaveChangesAsync(ct);
        var isEmployee = user.EmployerId is not null;
        var permissions = isEmployee ? await store.GetPagePermissionsAsync(user.Id, ct) : EmployeePages.All.Order().ToList();
        return new(user.Id, user.FullName, user.Phone, isEmployee, permissions);
    }

    private async Task RequireOwner(Guid userId, CancellationToken ct)
    {
        if ((await Account(userId, ct)).IsEmployee) throw new ForbiddenException("Chỉ chủ nhà được quản lý nhân viên");
    }

    public Task<List<TenancyDto>> Tenancies(Guid userId, Guid? propertyId, CancellationToken ct) =>
        store.ListTenanciesAsync(userId, propertyId, DateOnly.FromDateTime(clock.GetLocalNow().DateTime), ct);

    public async Task UpdateTenant(Guid userId, Guid roomId, Guid tenancyId, TenantProfile profile, CancellationToken ct)
    {
        var room = await rooms.GetOwnedAsync(roomId, userId, ct) ?? throw new NotFoundException();
        room.UpdateTenant(tenancyId, profile.Name, profile.Phone, profile.IdentityNumber, profile.Address, profile.Notes, profile.ExpectedEndDate, profile.Gender, profile.TemporaryResidenceRegistered);
        await uow.SaveChangesAsync(ct);
    }

    public Task<List<OccupantDto>> Occupants(Guid userId, Guid? propertyId, CancellationToken ct) => store.ListOccupantsAsync(userId, propertyId, ct);

    public async Task<Guid> AddOccupant(Guid userId, Guid roomId, OccupantProfile profile, CancellationToken ct)
    {
        _ = await rooms.GetOwnedAsync(roomId, userId, ct) ?? throw new NotFoundException();
        var occupant = Occupant.Create(roomId, profile.Name, profile.Phone, profile.IdentityNumber, profile.Gender, profile.Address, profile.Notes, profile.TemporaryResidenceRegistered);
        store.AddOccupant(occupant);
        await uow.SaveChangesAsync(ct);
        return occupant.Id;
    }

    public async Task UpdateOccupant(Guid userId, Guid id, Guid roomId, OccupantProfile profile, CancellationToken ct)
    {
        var occupant = await store.GetOccupantAsync(id, userId, ct) ?? throw new NotFoundException();
        _ = await rooms.GetOwnedAsync(roomId, userId, ct) ?? throw new NotFoundException();
        occupant.Update(roomId, profile.Name, profile.Phone, profile.IdentityNumber, profile.Gender, profile.Address, profile.Notes, profile.TemporaryResidenceRegistered);
        await uow.SaveChangesAsync(ct);
    }

    public async Task DeleteOccupant(Guid userId, Guid id, CancellationToken ct)
    {
        var occupant = await store.GetOccupantAsync(id, userId, ct) ?? throw new NotFoundException();
        store.RemoveOccupant(occupant);
        await uow.SaveChangesAsync(ct);
    }

    public async Task Maintenance(Guid userId, Guid roomId, bool enabled, string? damagedEquipment, CancellationToken ct)
    {
        var room = await rooms.GetOwnedAsync(roomId, userId, ct) ?? throw new NotFoundException();
        if (enabled && room.IsOccupied) room.RequestMaintenance(damagedEquipment);
        else room.SetMaintenance(enabled, damagedEquipment);
        await uow.SaveChangesAsync(ct);
    }

    public Task<List<RepairRequestDto>> Repairs(Guid userId, Guid? propertyId, CancellationToken ct) => store.ListRepairsAsync(userId, propertyId, ct);

    public async Task UpdateRepair(Guid userId, Guid roomId, string status, CancellationToken ct)
    {
        var room = await rooms.GetOwnedAsync(roomId, userId, ct) ?? throw new NotFoundException();
        room.UpdateRepairStatus(status);
        await uow.SaveChangesAsync(ct);
    }

    public async Task<List<EmployeeDto>> Employees(Guid userId, CancellationToken ct)
    {
        await RequireOwner(userId, ct);
        return await store.ListEmployeesAsync(userId, ct);
    }

    public async Task<Guid> SaveEmployee(Guid userId, Guid? id, EmployeeInput input, CancellationToken ct)
    {
        await RequireOwner(userId, ct);
        if (string.IsNullOrWhiteSpace(input.FullName) || input.FullName.Length > 200)
            throw new DomainException("Thông tin nhân viên không hợp lệ");
        var phone = User.NormalizePhone(input.Phone);
        var pagePermissions = input.PagePermissions.Distinct(StringComparer.Ordinal).ToList();
        if (pagePermissions.Any(page => !EmployeePages.All.Contains(page)))
            throw new DomainException("Danh sách quyền trang không hợp lệ");
        var propertyIds = input.PropertyIds.Distinct().ToList();
        foreach (var propertyId in propertyIds)
            _ = await properties.GetOwnedAsync(propertyId, userId, ct) ?? throw new NotFoundException("Nhà được phân công không thuộc quyền quản lý");
        User employee;
        if (id is null)
        {
            if (string.IsNullOrWhiteSpace(input.Password) || input.Password.Length is < 8 or > 128)
                throw new DomainException("Mật khẩu phải từ 8 đến 128 ký tự");
            if (await users.PhoneExistsAsync(phone, ct)) throw new ConflictException("Số điện thoại đã được đăng ký");
            employee = User.CreateEmployee(userId, phone, input.FullName, hasher.Hash(input.Password));
            users.Add(employee);
        }
        else
        {
            employee = await store.GetUserAsync(id.Value, ct) ?? throw new NotFoundException();
            if (employee.EmployerId != userId) throw new NotFoundException();
            if (!string.Equals(employee.Phone, phone, StringComparison.Ordinal) && await users.PhoneExistsAsync(phone, ct))
                throw new ConflictException("Số điện thoại đã được đăng ký");
            if (!string.IsNullOrEmpty(input.Password))
            {
                if (input.Password.Length is < 8 or > 128) throw new DomainException("Mật khẩu phải từ 8 đến 128 ký tự");
                employee.ResetEmployeePassword(hasher.Hash(input.Password));
            }
        }
        employee.UpdateEmployee(input.FullName, phone, input.IsDisabled);
        await store.ReplaceAssignmentsAsync(employee.Id, propertyIds, ct);
        await store.ReplacePagePermissionsAsync(employee.Id, pagePermissions, ct);
        await uow.SaveChangesAsync(ct);
        return employee.Id;
    }

    public async Task DeleteEmployee(Guid userId, Guid id, CancellationToken ct)
    {
        await RequireOwner(userId, ct);
        var employee = await store.GetUserAsync(id, ct) ?? throw new NotFoundException();
        if (employee.EmployerId != userId) throw new NotFoundException();
        foreach (var complaint in await store.ListComplaintsAssignedToAsync(id, ct)) complaint.Unassign();
        await store.ReplaceAssignmentsAsync(id, [], ct);
        await store.ReplacePagePermissionsAsync(id, [], ct);
        users.Remove(employee);
        await uow.SaveChangesAsync(ct);
    }

    public Task<List<ComplaintDto>> Complaints(Guid userId, Guid? propertyId, CancellationToken ct) => store.ListComplaintsAsync(userId, propertyId, ct);

    public async Task<List<AssigneeDto>> Assignees(Guid userId, Guid propertyId, CancellationToken ct)
    {
        _ = await properties.GetAccessibleAsync(propertyId, userId, ct) ?? throw new NotFoundException();
        return await store.ListAssigneesAsync(userId, propertyId, ct);
    }

    public async Task<Guid> CreateComplaint(Guid userId, Guid roomId, string tenantName, string title, string description, string priority, CancellationToken ct)
    {
        _ = await rooms.GetOwnedAsync(roomId, userId, ct) ?? throw new NotFoundException();
        var complaint = Complaint.Create(roomId, tenantName, title, description, priority, clock.GetUtcNow());
        store.AddComplaint(complaint);
        await uow.SaveChangesAsync(ct);
        return complaint.Id;
    }

    public async Task UpdateComplaint(Guid userId, Guid id, string status, Guid? assignedTo, string resolution, CancellationToken ct)
    {
        var complaint = await store.GetComplaintAsync(id, userId, ct) ?? throw new NotFoundException();
        var room = await rooms.GetOwnedAsync(complaint.RoomId, userId, ct) ?? throw new NotFoundException();
        if (assignedTo is not null && !await store.CanAssignAsync(assignedTo.Value, room.PropertyId, ct))
            throw new DomainException("Người xử lý phải có quyền truy cập nhà này");
        complaint.Update(status, assignedTo, resolution, clock.GetUtcNow());
        await uow.SaveChangesAsync(ct);
    }

    public async Task DeleteComplaint(Guid userId, Guid id, CancellationToken ct)
    {
        var complaint = await store.GetComplaintAsync(id, userId, ct) ?? throw new NotFoundException();
        store.RemoveComplaint(complaint);
        await uow.SaveChangesAsync(ct);
    }

    public async Task<List<RoomImageDto>> Images(Guid userId, Guid roomId, CancellationToken ct)
    {
        _ = await rooms.GetOwnedAsync(roomId, userId, ct) ?? throw new NotFoundException();
        return await store.ListImagesAsync(roomId, ct);
    }

    public async Task<Guid> AddImage(Guid userId, Guid roomId, string caption, string contentType, byte[] data, CancellationToken ct)
    {
        _ = await rooms.GetOwnedAsync(roomId, userId, ct) ?? throw new NotFoundException();
        if ((await store.ListImagesAsync(roomId, ct)).Count >= 8) throw new ConflictException("Mỗi phòng tối đa 8 ảnh");
        var image = RoomImage.Create(roomId, caption, contentType, data, clock.GetUtcNow());
        store.AddImage(image);
        await uow.SaveChangesAsync(ct);
        return image.Id;
    }

    public async Task DeleteImage(Guid userId, Guid roomId, Guid id, CancellationToken ct)
    {
        _ = await rooms.GetOwnedAsync(roomId, userId, ct) ?? throw new NotFoundException();
        var image = await store.GetImageAsync(id, roomId, ct) ?? throw new NotFoundException();
        store.RemoveImage(image);
        await uow.SaveChangesAsync(ct);
    }
}