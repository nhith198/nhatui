using Microsoft.EntityFrameworkCore;
using NhaTro.Application.Abstractions;
using NhaTro.Application.Dtos;

namespace NhaTro.Infrastructure.Persistence;

// Read side: no aggregates are materialised, EF projects straight into DTOs.

internal class PropertyQueries(AppDbContext db) : IPropertyQueries
{
    public Task<List<PropertyDto>> ListAsync(Guid userId, CancellationToken ct) =>
        db.AccessibleProperties(userId).OrderBy(p => p.Name)
            .Select(p => new PropertyDto(p.Id, p.Name, p.Address, p.ElectricRate, p.WaterRate,
                db.Rooms.Count(r => r.PropertyId == p.Id),
                db.Rooms.Count(r => r.PropertyId == p.Id && r.Tenancies.Any(t => t.EndDate == null)), p.OwnerId == userId,
                p.WaterRatePerPerson, p.OwnerId == userId ? p.BankName : "",
                p.OwnerId == userId ? p.BankAccountNumber : "", p.OwnerId == userId ? p.BankAccountName : "",
                p.ServiceAmountPerMonth, p.OwnerId == userId && p.QrCode != null))
            .ToListAsync(ct);
}

internal class RoomQueries(AppDbContext db) : IRoomQueries
{
    public Task<List<RoomDto>> ListAsync(Guid userId, Guid? propertyId, CancellationToken ct) =>
        (from r in db.Rooms
         join p in db.AccessibleProperties(userId) on r.PropertyId equals p.Id
         where propertyId == null || p.Id == propertyId
         orderby p.Name, r.Name
         select new RoomDto(r.Id, p.Id, p.Name, r.Name, r.Price, r.LastElectric, r.LastWater,
             r.Tenancies.Where(t => t.EndDate == null)
                 .Select(t => new TenantDto(t.Id, t.TenantName, t.Phone, t.StartDate, t.Deposit)).FirstOrDefault(),
             r.IsUnderMaintenance, db.RoomImages.Count(image => image.RoomId == r.Id),
             r.OccupantCount, r.WaterByOccupant, r.ServiceAmount, p.WaterRatePerPerson, r.ElectricRate, r.MaintenanceNote,
             r.ReservedTenantName, r.ReservedPhone, r.ReservedDeposit, r.ReservedDate, r.ReservedUntil))
        .ToListAsync(ct);
}

internal class InvoiceQueries(AppDbContext db) : IInvoiceQueries
{
    public Task<List<InvoiceDto>> ListAsync(Guid userId, string month, Guid? propertyId, CancellationToken ct) =>
        (from i in db.Invoices
         join r in db.Rooms on i.RoomId equals r.Id
         join p in db.AccessibleProperties(userId) on r.PropertyId equals p.Id
         where i.Month == month && (propertyId == null || p.Id == propertyId)
         orderby p.Name, r.Name
         select new InvoiceDto(i.Id, r.Id, r.Name, p.Name, i.TenantName, i.Month, i.Gender,
             i.ElectricOld, i.ElectricNew, i.WaterOld, i.WaterNew,
             i.Rent, i.ElectricAmount, i.WaterAmount, i.Other, i.Total, i.IsPaid,
             i.ServiceAmount, i.WaterByOccupant, i.OccupantCount, i.WaterRatePerPerson, i.ElectricRate, i.OtherNote,
             p.Id, p.QrCode != null))
        .ToListAsync(ct);
}

internal class DashboardQueries(AppDbContext db) : IDashboardQueries
{
    public async Task<DashboardDto> GetAsync(Guid userId, string month, Guid? propertyId, CancellationToken ct)
    {
        var rooms = db.Rooms.Where(r => db.AccessibleProperties(userId).Any(p => p.Id == r.PropertyId && (propertyId == null || p.Id == propertyId)));
        var invoices = db.Invoices.Where(i => i.Month == month && rooms.Any(r => r.Id == i.RoomId));

        var total = await rooms.CountAsync(ct);
        var occupied = await rooms.CountAsync(r => r.Tenancies.Any(t => t.EndDate == null), ct);
        var invoiced = await invoices.SumAsync(i => (decimal?)i.Total, ct) ?? 0;
        var collected = await invoices.Where(i => i.IsPaid).SumAsync(i => (decimal?)i.Total, ct) ?? 0;

        var missing = await (from r in rooms
                             join p in db.Properties on r.PropertyId equals p.Id
                             where r.Tenancies.Any(t => t.EndDate == null) && !db.Invoices.Any(i => i.RoomId == r.Id && i.Month == month)
                             orderby p.Name, r.Name
                             select new MissingInvoiceDto(r.Id, r.Name, p.Name,
                                 r.Tenancies.Where(t => t.EndDate == null).Select(t => t.TenantName).First()))
                            .ToListAsync(ct);

        return new DashboardDto(total, occupied, invoiced, collected, invoiced - collected, missing);
    }
}

internal class FinancialQueries(AppDbContext db) : IFinancialQueries
{
    public async Task<FinancialReportDto> GetAsync(Guid userId, string month, Guid? propertyId, CancellationToken ct)
    {
        var user = await db.Users.Where(account => account.Id == userId)
            .Select(account => new { OwnerId = account.EmployerId ?? account.Id, IsEmployee = account.EmployerId != null })
            .SingleAsync(ct);
        var ownerCreatedAt = await db.Users.Where(account => account.Id == user.OwnerId).Select(account => account.CreatedAt).SingleAsync(ct);
        var accessibleProperties = db.AccessibleProperties(userId);
        var year = int.Parse(month.AsSpan(0, 4));
        var monthNumber = int.Parse(month.AsSpan(5, 2));
        var selectedMonth = new DateOnly(year, monthNumber, 1);
        var rangeStart = selectedMonth.AddMonths(-11);
        var rangeEnd = selectedMonth.AddMonths(1);
        var paidStart = new DateTime(rangeStart.Year, rangeStart.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var paidEnd = new DateTime(rangeEnd.Year, rangeEnd.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var paidByMonth = await (from invoice in db.Invoices
                                 join room in db.Rooms on invoice.RoomId equals room.Id
                                 join property in accessibleProperties on room.PropertyId equals property.Id
                                 where (propertyId == null || property.Id == propertyId)
                                     && invoice.IsPaid && invoice.PaidAt >= paidStart && invoice.PaidAt < paidEnd
                                 group invoice by new { invoice.PaidAt!.Value.Year, invoice.PaidAt.Value.Month } into items
                                 select new { items.Key.Year, items.Key.Month, Amount = items.Sum(invoice => invoice.Total) })
            .ToListAsync(ct);

        var depositByMonth = await (from receipt in db.DepositReceipts
                                    join room in db.Rooms on receipt.RoomId equals room.Id
                                    join property in accessibleProperties on room.PropertyId equals property.Id
                                    where (propertyId == null || property.Id == propertyId)
                                        && receipt.ReceivedAt >= paidStart && receipt.ReceivedAt < paidEnd
                                    group receipt by new { receipt.ReceivedAt.Year, receipt.ReceivedAt.Month } into items
                                    select new { items.Key.Year, items.Key.Month, Amount = items.Sum(receipt => receipt.Amount) })
            .ToListAsync(ct);

        var expenseByMonth = await db.FinancialExpenses
            .Where(expense => expense.OwnerId == user.OwnerId && expense.IsPaid && expense.Date >= rangeStart && expense.Date < rangeEnd
                && (propertyId == null || expense.PropertyId == propertyId)
                && (!user.IsEmployee || (expense.PropertyId != null && accessibleProperties.Any(property => property.Id == expense.PropertyId))))
            .GroupBy(expense => new { expense.Date.Year, expense.Date.Month })
            .Select(items => new { items.Key.Year, items.Key.Month, Amount = items.Sum(expense => expense.Amount) })
            .ToListAsync(ct);

        var expenseEntries = await (from expense in db.FinancialExpenses
                                    join property in db.Properties on expense.PropertyId equals property.Id into propertyGroup
                                    from property in propertyGroup.DefaultIfEmpty()
                                    where expense.OwnerId == user.OwnerId && expense.Date >= selectedMonth && expense.Date < rangeEnd
                                        && (propertyId == null || expense.PropertyId == propertyId)
                                        && (!user.IsEmployee || (expense.PropertyId != null && accessibleProperties.Any(accessible => accessible.Id == expense.PropertyId)))
                                    orderby expense.Date descending, expense.Id
                                    select new FinancialExpenseDto(expense.Id, expense.PropertyId,
                                        property == null ? null : property.Name, expense.Category, expense.Description,
                                        expense.Recipient, expense.Amount, expense.Date, expense.IsMonthly, expense.IsPaid, expense.RecurringSourceId != null))
            .ToListAsync(ct);

        var monthlyExpenses = await (from expense in db.FinancialExpenses
                                     join property in db.Properties on expense.PropertyId equals property.Id into propertyGroup
                                     from property in propertyGroup.DefaultIfEmpty()
                                     where expense.OwnerId == user.OwnerId && expense.IsMonthly
                                         && (propertyId == null || expense.PropertyId == propertyId)
                                         && (!user.IsEmployee || (expense.PropertyId != null && accessibleProperties.Any(accessible => accessible.Id == expense.PropertyId)))
                                     orderby expense.Description, expense.Id
                                     select new FinancialExpenseDto(expense.Id, expense.PropertyId,
                                         property == null ? null : property.Name, expense.Category, expense.Description,
                                         expense.Recipient, expense.Amount, expense.Date, expense.IsMonthly, expense.IsPaid, expense.RecurringSourceId != null))
            .ToListAsync(ct);

        var refundByMonth = await (from receipt in db.DepositReceipts
                                   join room in db.Rooms on receipt.RoomId equals room.Id
                                   join property in accessibleProperties on room.PropertyId equals property.Id
                                   where (propertyId == null || property.Id == propertyId)
                                       && receipt.RefundedAt != null && receipt.RefundedAt >= paidStart && receipt.RefundedAt < paidEnd
                                   group receipt by new { receipt.RefundedAt!.Value.Year, receipt.RefundedAt.Value.Month } into items
                                   select new { items.Key.Year, items.Key.Month, Amount = items.Sum(receipt => -(receipt.RefundedAmount ?? 0)) })
            .ToListAsync(ct);

        var monthStart = new DateTime(year, monthNumber, 1, 0, 0, 0, DateTimeKind.Utc);
        var depositReceipts = await (from receipt in db.DepositReceipts
                                     join room in db.Rooms on receipt.RoomId equals room.Id
                                     join property in accessibleProperties on room.PropertyId equals property.Id
                                     where (propertyId == null || property.Id == propertyId)
                                         && receipt.ReceivedAt >= monthStart && receipt.ReceivedAt < monthStart.AddMonths(1)
                                     orderby receipt.ReceivedAt descending
                                     select new DepositReceiptDto(receipt.Id, room.Id, room.Name, property.Name, receipt.TenantName, receipt.Phone,
                                         receipt.Amount, receipt.HoldUntil, receipt.ReceivedAt, receipt.RefundedAt, receipt.RefundedAmount))
            .ToListAsync(ct);

        var paidLookup = paidByMonth.Concat(depositByMonth).Concat(refundByMonth)
            .GroupBy(x => (x.Year, x.Month))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
        var expenseLookup = expenseByMonth.ToDictionary(x => (x.Year, x.Month), x => x.Amount);
        var accountMonth = new DateOnly(ownerCreatedAt.UtcDateTime.Year, ownerCreatedAt.UtcDateTime.Month, 1);
        var historyStart = accountMonth > rangeStart ? accountMonth : rangeStart;
        var historyCount = Math.Max(0, (selectedMonth.Year - historyStart.Year) * 12 + selectedMonth.Month - historyStart.Month + 1);
        var history = Enumerable.Range(0, historyCount).Select(offset =>
        {
            var current = historyStart.AddMonths(offset);
            var collected = paidLookup.GetValueOrDefault((current.Year, current.Month));
            var expenses = expenseLookup.GetValueOrDefault((current.Year, current.Month));
            return new MonthlyProfitDto($"{current.Year:0000}-{current.Month:00}", collected, expenses, collected - expenses);
        }).ToList();
        var selected = history.Count > 0 ? history[^1] : new MonthlyProfitDto(month, 0, 0, 0);
        return new FinancialReportDto(month, selected.Collected, selected.Expenses, selected.Profit, expenseEntries, history, depositReceipts, monthlyExpenses);
    }
}
