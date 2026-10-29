using Microsoft.EntityFrameworkCore;
using NhaTro.Application.Abstractions;
using NhaTro.Domain.Entities;

namespace NhaTro.Infrastructure.Persistence;

internal class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<bool> PhoneExistsAsync(string phone, CancellationToken ct) => db.Users.AnyAsync(u => u.Phone == phone, ct);
    public Task<User?> GetByPhoneAsync(string phone, CancellationToken ct) => db.Users.FirstOrDefaultAsync(u => u.Phone == phone, ct);
    public void Add(User user) => db.Users.Add(user);
    public void Remove(User user) => db.Users.Remove(user);
}

internal class PropertyRepository(AppDbContext db) : IPropertyRepository
{
    public Task<Property?> GetOwnedAsync(Guid id, Guid ownerId, CancellationToken ct) =>
        db.Properties.FirstOrDefaultAsync(p => p.Id == id && p.OwnerId == ownerId, ct);
    public Task<Property?> GetAccessibleAsync(Guid id, Guid userId, CancellationToken ct) =>
        db.AccessibleProperties(userId).FirstOrDefaultAsync(p => p.Id == id, ct);
    public void Add(Property property) => db.Properties.Add(property);
    public void Remove(Property property) => db.Properties.Remove(property);
}

internal class RoomRepository(AppDbContext db) : IRoomRepository
{
    public Task<Room?> GetOwnedAsync(Guid id, Guid ownerId, CancellationToken ct) =>
        db.Rooms.Include(r => r.Tenancies)
            .FirstOrDefaultAsync(r => r.Id == id && db.AccessibleProperties(ownerId).Any(p => p.Id == r.PropertyId), ct);
    public Task<bool> NameExistsAsync(Guid propertyId, string name, CancellationToken ct) =>
        db.Rooms.AnyAsync(r => r.PropertyId == propertyId && r.Name == name, ct);
    public Task<bool> AnyInPropertyAsync(Guid propertyId, CancellationToken ct) => db.Rooms.AnyAsync(r => r.PropertyId == propertyId, ct);
    public Task<List<Room>> GetExpiredReservationsAsync(DateOnly today, CancellationToken ct) =>
        db.Rooms.Where(r => r.ReservedUntil < today).ToListAsync(ct);
    public void Add(Room room) => db.Rooms.Add(room);
    public void Remove(Room room) => db.Rooms.Remove(room);
}

internal class InvoiceRepository(AppDbContext db) : IInvoiceRepository
{
    public Task<Invoice?> GetOwnedAsync(Guid id, Guid ownerId, CancellationToken ct) =>
        db.Invoices.FirstOrDefaultAsync(i => i.Id == id &&
            db.Rooms.Any(r => r.Id == i.RoomId && db.AccessibleProperties(ownerId).Any(p => p.Id == r.PropertyId)), ct);
    public Task<bool> ExistsAsync(Guid roomId, string month, CancellationToken ct) => db.Invoices.AnyAsync(i => i.RoomId == roomId && i.Month == month, ct);
    public async Task<bool> HasLaterInvoiceAsync(Guid roomId, string month, CancellationToken ct) =>
        (await db.Invoices.Where(i => i.RoomId == roomId).Select(i => i.Month).ToListAsync(ct)).Any(m => string.CompareOrdinal(m, month) > 0);
    public void Add(Invoice invoice) => db.Invoices.Add(invoice);
    public void Remove(Invoice invoice) => db.Invoices.Remove(invoice);
}

internal class DepositReceiptRepository(AppDbContext db) : IDepositReceiptRepository
{
    public Task<DepositReceipt?> GetOpenForRoomAsync(Guid roomId, CancellationToken ct) =>
        db.DepositReceipts.Where(r => r.RoomId == roomId && r.RefundedAt == null).OrderByDescending(r => r.ReceivedAt).FirstOrDefaultAsync(ct);
    public Task<DepositReceipt?> GetAccessibleAsync(Guid id, Guid userId, CancellationToken ct) =>
        db.DepositReceipts.FirstOrDefaultAsync(r => r.Id == id &&
            db.Rooms.Any(room => room.Id == r.RoomId && db.AccessibleProperties(userId).Any(p => p.Id == room.PropertyId)), ct);
    public void Add(DepositReceipt receipt) => db.DepositReceipts.Add(receipt);
    public void Remove(DepositReceipt receipt) => db.DepositReceipts.Remove(receipt);
}

internal class FinancialExpenseRepository(AppDbContext db) : IFinancialExpenseRepository
{
    public Task<FinancialExpense?> GetOwnedAsync(Guid id, Guid ownerId, CancellationToken ct) =>
        db.FinancialExpenses.FirstOrDefaultAsync(e => e.Id == id && e.OwnerId == ownerId, ct);
    public Task<List<FinancialExpense>> GetRecurringTemplatesAsync(Guid ownerId, CancellationToken ct) =>
        db.FinancialExpenses.Where(e => e.OwnerId == ownerId && e.IsMonthly).ToListAsync(ct);
    public void Add(FinancialExpense expense) => db.FinancialExpenses.Add(expense);
    public void Remove(FinancialExpense expense) => db.FinancialExpenses.Remove(expense);
}
