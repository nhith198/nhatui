using System.Globalization;
using NhaTro.Application.Abstractions;
using NhaTro.Application.Dtos;
using NhaTro.Domain.Common;
using NhaTro.Domain.Entities;

namespace NhaTro.Application.UseCases;

public sealed record FinancialExpenseInput(Guid? PropertyId, string Category, string Description, string Recipient,
    decimal Amount, DateOnly Date, bool IsMonthly = false, bool IsPaid = true);

public sealed class FinancialService(IManagementRepository users, IPropertyRepository properties,
    IFinancialExpenseRepository expenses, IFinancialQueries queries, IUnitOfWork uow, TimeProvider clock, IDepositReceiptRepository deposits)
{
    public async Task<FinancialReportDto> Report(Guid userId, string month, Guid? propertyId, CancellationToken ct)
    {
        var (ownerId, user) = await RequireFinancialAccess(userId, ct);
        var selectedMonth = ParseMonth(month);
        if (propertyId is not null) await ValidateProperty(userId, propertyId, ct);
        await GenerateRecurringExpenses(ownerId, selectedMonth, ct);
        return await queries.GetAsync(userId, month, propertyId, ct);
    }

    private async Task GenerateRecurringExpenses(Guid ownerId, DateOnly selectedMonth, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        var currentMonth = new DateOnly(today.Year, today.Month, 1);
        var lastMonth = selectedMonth < currentMonth ? selectedMonth : currentMonth;
        var generated = false;
        foreach (var template in await expenses.GetRecurringTemplatesAsync(ownerId, ct))
        {
            var month = (template.LastGeneratedMonth ?? new DateOnly(template.Date.Year, template.Date.Month, 1)).AddMonths(1);
            for (; month <= lastMonth; month = month.AddMonths(1))
            {
                expenses.Add(template.GenerateFor(month));
                generated = true;
            }
        }
        if (generated) await uow.SaveChangesAsync(ct);
    }

    public async Task<Guid> CreateExpense(Guid userId, FinancialExpenseInput input, CancellationToken ct)
    {
        var (ownerId, user) = await RequireFinancialAccess(userId, ct);
        await ValidateProperty(userId, input.PropertyId, ct);
        if (user.EmployerId is not null && input.PropertyId is null)
            throw new ForbiddenException("Nhân viên chỉ được ghi chi phí cho nhà được phân công");
        var expense = FinancialExpense.Create(ownerId, input.PropertyId, input.Category, input.Description,
            input.Recipient, input.Amount, input.Date, input.IsMonthly, input.IsPaid);
        expenses.Add(expense);
        await uow.SaveChangesAsync(ct);
        return expense.Id;
    }

    public async Task UpdateExpense(Guid userId, Guid id, FinancialExpenseInput input, CancellationToken ct)
    {
        var (ownerId, user) = await RequireFinancialAccess(userId, ct);
        await ValidateProperty(userId, input.PropertyId, ct);
        if (user.EmployerId is not null && input.PropertyId is null)
            throw new ForbiddenException("Nhân viên chỉ được ghi chi phí cho nhà được phân công");
        var expense = await expenses.GetOwnedAsync(id, ownerId, ct) ?? throw new NotFoundException();
        if (user.EmployerId is not null && expense.PropertyId is null) throw new NotFoundException();
        if (expense.PropertyId is not null) await ValidateProperty(userId, expense.PropertyId, ct);
        if (expense.IsPaid) throw new ConflictException("Khoản chi đã chi, không thể sửa");
        expense.Update(input.PropertyId, input.Category, input.Description, input.Recipient, input.Amount, input.Date, input.IsMonthly, input.IsPaid);
        await uow.SaveChangesAsync(ct);
    }

    public async Task DeleteExpense(Guid userId, Guid id, CancellationToken ct)
    {
        var (ownerId, user) = await RequireFinancialAccess(userId, ct);
        var expense = await expenses.GetOwnedAsync(id, ownerId, ct) ?? throw new NotFoundException();
        if (user.EmployerId is not null && expense.PropertyId is null) throw new NotFoundException();
        if (expense.PropertyId is not null) await ValidateProperty(userId, expense.PropertyId, ct);
        if (expense.IsPaid) throw new ConflictException("Khoản chi đã chi, không thể xóa");
        expenses.Remove(expense);
        await uow.SaveChangesAsync(ct);
    }

    public async Task DeleteDepositReceipt(Guid userId, Guid id, CancellationToken ct)
    {
        await RequireFinancialAccess(userId, ct);
        var receipt = await deposits.GetAccessibleAsync(id, userId, ct) ?? throw new NotFoundException("Không tìm thấy phiếu cọc");
        deposits.Remove(receipt);
        await uow.SaveChangesAsync(ct);
    }

    private async Task<(Guid OwnerId, User User)> RequireFinancialAccess(Guid userId, CancellationToken ct)
    {
        var user = await users.GetUserAsync(userId, ct) ?? throw new UnauthorizedException("Tài khoản không tồn tại");
        if (user.IsDisabled) throw new UnauthorizedException("Tài khoản đã bị khóa");
        if (user.EmployerId is not null && !(await users.GetPagePermissionsAsync(userId, ct)).Contains(EmployeePages.Financials))
            throw new ForbiddenException("Bạn không được cấp quyền truy cập trang tài chính");
        return (user.EmployerId ?? user.Id, user);
    }

    private async Task ValidateProperty(Guid userId, Guid? propertyId, CancellationToken ct)
    {
        if (propertyId is not null) _ = await properties.GetAccessibleAsync(propertyId.Value, userId, ct) ?? throw new NotFoundException("Nhà không thuộc quyền quản lý");
    }

    private static DateOnly ParseMonth(string month)
    {
        if (!DateOnly.TryParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date))
            throw new DomainException("Tháng không hợp lệ, định dạng yyyy-MM");
        return date;
    }
}