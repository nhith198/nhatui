using NhaTro.Domain.Common;

namespace NhaTro.Domain.Entities;

/// <summary>A rental period inside a Room aggregate. EndDate == null means the tenant currently lives there.</summary>
public class Tenancy : Entity
{
    private Tenancy() { }

    public Guid RoomId { get; private set; }
    public string TenantName { get; private set; } = "";
    public string Phone { get; private set; } = "";
    public DateOnly StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public decimal Deposit { get; private set; }
    public DateOnly? ExpectedEndDate { get; private set; }
    public string IdentityNumber { get; private set; } = "";
    public string Gender { get; private set; } = "";
    public string Address { get; private set; } = "";
    public string Notes { get; private set; } = "";
    public bool TemporaryResidenceRegistered { get; private set; }
    public bool IsActive => EndDate is null;

    public string ContractStatus(DateOnly today) => EndDate is not null ? "Ended"
        : StartDate > today ? "Upcoming"
        : ExpectedEndDate < today ? "Expired"
        : ExpectedEndDate <= today.AddDays(30) ? "Expiring" : "Active";

    internal void UpdateProfile(string name, string? phone, string? identityNumber,
        string? address, string? notes, DateOnly? expectedEndDate, string? gender, bool temporaryResidenceRegistered = false)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Tên khách thuê không được để trống");
        if (expectedEndDate < StartDate) throw new DomainException("Ngày hết hạn phải từ ngày bắt đầu thuê");
        if (name.Trim().Length > 200 || (phone?.Length ?? 0) > 30 || (identityNumber?.Length ?? 0) > 30
            || (address?.Length ?? 0) > 500 || (notes?.Length ?? 0) > 2000)
            throw new DomainException("Thông tin khách thuê vượt quá độ dài cho phép");
        if (gender is not (null or "" or "Nam" or "Nữ")) throw new DomainException("Giới tính phải là Nam hoặc Nữ");
        TenantName = name.Trim();
        Phone = phone?.Trim() ?? "";
        IdentityNumber = identityNumber?.Trim() ?? "";
        Gender = gender?.Trim() ?? "";
        Address = address?.Trim() ?? "";
        Notes = notes?.Trim() ?? "";
        TemporaryResidenceRegistered = temporaryResidenceRegistered;
        ExpectedEndDate = expectedEndDate;
    }

    internal static Tenancy Start(Guid roomId, string tenantName, string? phone, DateOnly startDate, decimal deposit)
    {
        if (string.IsNullOrWhiteSpace(tenantName)) throw new DomainException("Tên khách thuê không được để trống");
        if (deposit < 0) throw new DomainException("Tiền cọc không được âm");
        var tenancy = new Tenancy { RoomId = roomId, StartDate = startDate, Deposit = deposit };
        tenancy.UpdateProfile(tenantName, phone, null, null, null, null, null);
        return tenancy;
    }

    internal void End(DateOnly date)
    {
        if (date < StartDate) throw new DomainException("Ngày trả phòng không được trước ngày vào ở");
        EndDate = date;
    }
}
