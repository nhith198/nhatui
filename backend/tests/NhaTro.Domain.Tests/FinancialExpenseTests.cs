using NhaTro.Domain.Common;
using NhaTro.Domain.Entities;

namespace NhaTro.Domain.Tests;

public class FinancialExpenseTests
{
    [Fact]
    public void Sales_expense_keeps_monthly_accounting_details()
    {
        var ownerId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();
        var expense = FinancialExpense.Create(ownerId, propertyId, "Sales", "Hoa hồng phòng 101", "Sale An",
            1_500_000, new DateOnly(2026, 10, 12));

        Assert.Equal(ownerId, expense.OwnerId);
        Assert.Equal(propertyId, expense.PropertyId);
        Assert.Equal("Sales", expense.Category);
        Assert.Equal("Sale An", expense.Recipient);
        Assert.Equal(1_500_000m, expense.Amount);
        Assert.Equal(new DateOnly(2026, 10, 12), expense.Date);
    }

    [Fact]
    public void Expense_rejects_invalid_category_amount_and_description()
    {
        Assert.Throws<DomainException>(() => FinancialExpense.Create(Guid.NewGuid(), null, "Income", "Chi", "Sale An",
            10_000, new DateOnly(2026, 10, 12)));
        Assert.Throws<DomainException>(() => FinancialExpense.Create(Guid.NewGuid(), null, "Other", "Chi", "Sale An",
            0, new DateOnly(2026, 10, 12)));
        Assert.Throws<DomainException>(() => FinancialExpense.Create(Guid.NewGuid(), null, "Other", " ", "Sale An",
            10_000, new DateOnly(2026, 10, 12)));
    }

    [Fact]
    public void Expense_requires_recipient()
    {
        Assert.Throws<DomainException>(() => FinancialExpense.Create(Guid.NewGuid(), null, "Operating", "Điện", " ",
            10_000, new DateOnly(2026, 10, 12)));
    }
}