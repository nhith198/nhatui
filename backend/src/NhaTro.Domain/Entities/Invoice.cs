using System.Text.RegularExpressions;
using NhaTro.Domain.Common;

namespace NhaTro.Domain.Entities;

public partial class Invoice : Entity, IAggregateRoot
{
    private Invoice() { }

    public Guid RoomId { get; private set; }
    public string Month { get; private set; } = "";        // yyyy-MM
    public string TenantName { get; private set; } = "";   // snapshot at billing time
    public string Gender { get; private set; } = "";       // snapshot at billing time
    public int ElectricOld { get; private set; }
    public int ElectricNew { get; private set; }
    public int WaterOld { get; private set; }
    public int WaterNew { get; private set; }
    public decimal Rent { get; private set; }
    public decimal ElectricRate { get; private set; }
    public decimal ElectricAmount { get; private set; }
    public decimal WaterAmount { get; private set; }
    public decimal ServiceAmount { get; private set; }
    public decimal Other { get; private set; }
    public string? OtherNote { get; private set; }
    public bool WaterByOccupant { get; private set; }
    public int OccupantCount { get; private set; }
    public decimal WaterRatePerPerson { get; private set; }
    public decimal Total { get; private set; }
    public bool IsPaid { get; private set; }
    public DateTime? PaidAt { get; private set; }

    [GeneratedRegex(@"^\d{4}-(0[1-9]|1[0-2])$")]
    private static partial Regex MonthFormat();

    /// <summary>Only Room.IssueInvoice creates invoices, so the meter bookkeeping cannot be bypassed.</summary>
    internal static Invoice Issue(Guid roomId, string month, string tenantName, string gender, int electricOld, int electricNew, int waterOld, int waterNew,
        decimal rent, decimal electricRate, decimal waterRate, decimal other, decimal serviceAmount = 0,
        int occupantCount = 1, bool waterByOccupant = false, decimal waterRatePerPerson = 0, string? otherNote = null)
    {
        if (!MonthFormat().IsMatch(month ?? "")) throw new DomainException("Tháng không hợp lệ, định dạng yyyy-MM");
        if (other != 0 && string.IsNullOrWhiteSpace(otherNote)) throw new DomainException("Vui lòng nhập lý do phí khác");
        if (serviceAmount < 0 || waterRatePerPerson < 0 || occupantCount < 1)
            throw new DomainException("Thông tin tiền nước hoặc dịch vụ không hợp lệ");

        var electric = (electricNew - electricOld) * electricRate;
        var water = waterByOccupant ? occupantCount * waterRatePerPerson : (waterNew - waterOld) * waterRate;
        return new Invoice
        {
            RoomId = roomId, Month = month!, TenantName = tenantName, Gender = gender,
            ElectricOld = electricOld, ElectricNew = electricNew, WaterOld = waterOld, WaterNew = waterNew,
            Rent = rent, ElectricRate = electricRate, ElectricAmount = electric, WaterAmount = water, ServiceAmount = serviceAmount, Other = other, OtherNote = other != 0 ? otherNote!.Trim() : null,
            WaterByOccupant = waterByOccupant, OccupantCount = occupantCount, WaterRatePerPerson = waterRatePerPerson,
            Total = rent + electric + water + serviceAmount + other
        };
    }

    public void MarkPaid(DateTime paidAtUtc) { IsPaid = true; PaidAt = paidAtUtc; }
    public void MarkUnpaid() { IsPaid = false; PaidAt = null; }

}
