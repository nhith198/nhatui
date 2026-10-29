using NhaTro.Application.Dtos;
using NhaTro.Domain.Entities;

namespace NhaTro.Application.Abstractions;

// ---- use-case contracts -------------------------------------------------------------------------
public interface ICommandHandler<in TCommand> { Task Handle(TCommand command, CancellationToken ct); }
public interface ICommandHandler<in TCommand, TResult> { Task<TResult> Handle(TCommand command, CancellationToken ct); }
public interface IQueryHandler<in TQuery, TResult> { Task<TResult> Handle(TQuery query, CancellationToken ct); }

// ---- persistence ports (write side: aggregates) -----------------------------------------------
public interface IUnitOfWork { Task<int> SaveChangesAsync(CancellationToken ct = default); }

public interface IUserRepository
{
    Task<bool> PhoneExistsAsync(string phone, CancellationToken ct);
    Task<User?> GetByPhoneAsync(string phone, CancellationToken ct);
    void Add(User user);
    void Remove(User user);
}

public interface IPropertyRepository
{
    Task<Property?> GetOwnedAsync(Guid id, Guid ownerId, CancellationToken ct);
    Task<Property?> GetAccessibleAsync(Guid id, Guid userId, CancellationToken ct) => GetOwnedAsync(id, userId, ct);
    void Add(Property property);
    void Remove(Property property);
}

public interface IRoomRepository
{
    Task<Room?> GetOwnedAsync(Guid id, Guid ownerId, CancellationToken ct);
    Task<bool> NameExistsAsync(Guid propertyId, string name, CancellationToken ct);
    Task<bool> AnyInPropertyAsync(Guid propertyId, CancellationToken ct);
    Task<List<Room>> GetExpiredReservationsAsync(DateOnly today, CancellationToken ct);
    void Add(Room room);
    void Remove(Room room);
}

public interface IInvoiceRepository
{
    Task<Invoice?> GetOwnedAsync(Guid id, Guid ownerId, CancellationToken ct);
    Task<bool> ExistsAsync(Guid roomId, string month, CancellationToken ct);
    Task<bool> HasLaterInvoiceAsync(Guid roomId, string month, CancellationToken ct);
    void Add(Invoice invoice);
    void Remove(Invoice invoice);
}

public interface IDepositReceiptRepository
{
    Task<DepositReceipt?> GetOpenForRoomAsync(Guid roomId, CancellationToken ct);
    Task<DepositReceipt?> GetAccessibleAsync(Guid id, Guid userId, CancellationToken ct);
    void Add(DepositReceipt receipt);
    void Remove(DepositReceipt receipt);
}

public interface IFinancialExpenseRepository
{
    Task<FinancialExpense?> GetOwnedAsync(Guid id, Guid ownerId, CancellationToken ct);
    Task<List<FinancialExpense>> GetRecurringTemplatesAsync(Guid ownerId, CancellationToken ct);
    void Add(FinancialExpense expense);
    void Remove(FinancialExpense expense);
}

// ---- persistence ports (read side: projections straight to DTOs) ----------------------------
public interface IPropertyQueries { Task<List<PropertyDto>> ListAsync(Guid userId, CancellationToken ct); }
public interface IRoomQueries { Task<List<RoomDto>> ListAsync(Guid userId, Guid? propertyId, CancellationToken ct); }
public interface IInvoiceQueries { Task<List<InvoiceDto>> ListAsync(Guid userId, string month, Guid? propertyId, CancellationToken ct); }
public interface IDashboardQueries { Task<DashboardDto> GetAsync(Guid userId, string month, Guid? propertyId, CancellationToken ct); }
public interface IFinancialQueries { Task<FinancialReportDto> GetAsync(Guid ownerId, string month, Guid? propertyId, CancellationToken ct); }

public interface IManagementRepository
{
    Task<User?> GetUserAsync(Guid id, CancellationToken ct);
    Task<List<string>> GetPagePermissionsAsync(Guid userId, CancellationToken ct);
    Task<List<TenancyDto>> ListTenanciesAsync(Guid userId, Guid? propertyId, DateOnly today, CancellationToken ct);
    Task<List<EmployeeDto>> ListEmployeesAsync(Guid ownerId, CancellationToken ct);
    Task<List<Complaint>> ListComplaintsAssignedToAsync(Guid employeeId, CancellationToken ct);
    Task ReplaceAssignmentsAsync(Guid employeeId, IReadOnlyCollection<Guid> propertyIds, CancellationToken ct);
    Task ReplacePagePermissionsAsync(Guid employeeId, IReadOnlyCollection<string> pages, CancellationToken ct);
    Task<bool> CanAssignAsync(Guid userId, Guid propertyId, CancellationToken ct);
    Task<List<AssigneeDto>> ListAssigneesAsync(Guid userId, Guid propertyId, CancellationToken ct);
    Task<List<ComplaintDto>> ListComplaintsAsync(Guid userId, Guid? propertyId, CancellationToken ct);
    Task<Complaint?> GetComplaintAsync(Guid id, Guid userId, CancellationToken ct);
    void AddComplaint(Complaint complaint);
    void RemoveComplaint(Complaint complaint);
    Task<List<RepairRequestDto>> ListRepairsAsync(Guid userId, Guid? propertyId, CancellationToken ct);
    Task<List<RoomImageDto>> ListImagesAsync(Guid roomId, CancellationToken ct);
    Task<RoomImage?> GetImageAsync(Guid id, Guid roomId, CancellationToken ct);
    void AddImage(RoomImage image);
    void RemoveImage(RoomImage image);
    Task<List<OccupantDto>> ListOccupantsAsync(Guid userId, Guid? propertyId, CancellationToken ct);
    Task<Occupant?> GetOccupantAsync(Guid id, Guid userId, CancellationToken ct);
    void AddOccupant(Occupant occupant);
    void RemoveOccupant(Occupant occupant);
}

// ---- other ports ------------------------------------------------------------------------------
public interface IPasswordHasher { string Hash(string password); bool Verify(string hash, string password); }
public interface ITokenService { string Create(User user); }

// ---- application errors (mapped to HTTP by the API layer) ------------------------------------
public abstract class AppException(string message) : Exception(message) { }
public class NotFoundException(string message = "Không tìm thấy dữ liệu") : AppException(message) { }
public class ConflictException(string message) : AppException(message) { }
public class UnauthorizedException(string message) : AppException(message) { }
public class ForbiddenException(string message) : AppException(message) { }
