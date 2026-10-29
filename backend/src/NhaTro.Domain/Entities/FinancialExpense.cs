using NhaTro.Domain.Common;

namespace NhaTro.Domain.Entities;

public class FinancialExpense : Entity, IAggregateRoot
{
    private FinancialExpense() { }

    public Guid OwnerId { get; private set; }
    public Guid? PropertyId { get; private set; }
    public string Category { get; private set; } = "";
    public string Description { get; private set; } = "";
    public string? Recipient { get; private set; }
    public decimal Amount { get; private set; }
    public DateOnly Date { get; private set; }
    public bool IsMonthly { get; private set; }
    public bool IsPaid { get; private set; } = true;
    public DateOnly? LastGeneratedMonth { get; private set; }
    public Guid? RecurringSourceId { get; private set; }

    public static FinancialExpense Create(Guid ownerId, Guid? propertyId, string category, string description,
        string recipient, decimal amount, DateOnly date, bool isMonthly = false, bool isPaid = true)
    {
        var expense = new FinancialExpense { OwnerId = ownerId };
        expense.Update(propertyId, category, description, recipient, amount, date, isMonthly, isPaid);
        return expense;
    }

    public void Update(Guid? propertyId, string category, string description, string recipient, decimal amount, DateOnly date, bool isMonthly = false, bool isPaid = true)
    {
        if (category is not ("Operating" or "Sales" or "Other")) throw new DomainException("Loại chi phí không hợp lệ");
        if (string.IsNullOrWhiteSpace(description) || description.Trim().Length > 200)
            throw new DomainException("Nội dung chi phí phải từ 1 đến 200 ký tự");
        if (string.IsNullOrWhiteSpace(recipient) || recipient.Trim().Length > 200) throw new DomainException("Người nhận là bắt buộc, tối đa 200 ký tự");
        if (amount <= 0 || amount > 1_000_000_000) throw new DomainException("Số tiền chi phí không hợp lệ");
        if (date == default) throw new DomainException("Ngày chi phí không hợp lệ");

        PropertyId = propertyId;
        Category = category;
        Description = description.Trim();
        Recipient = recipient.Trim();
        Amount = amount;
        Date = date;
        IsMonthly = isMonthly;
        IsPaid = isPaid;
        LastGeneratedMonth = isMonthly ? LastGeneratedMonth ?? new DateOnly(date.Year, date.Month, 1) : null;
    }

    public FinancialExpense GenerateFor(DateOnly month)
    {
        var day = Math.Min(Date.Day, DateTime.DaysInMonth(month.Year, month.Month));
        var copy = Create(OwnerId, PropertyId, Category, Description, Recipient ?? string.Empty, Amount, new DateOnly(month.Year, month.Month, day), isPaid: IsPaid);
        copy.RecurringSourceId = Id;
        LastGeneratedMonth = month;
        return copy;
    }
}