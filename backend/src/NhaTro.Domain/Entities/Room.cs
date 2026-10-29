using NhaTro.Domain.Common;

namespace NhaTro.Domain.Entities;

/// <summary>
/// A rentable room. Aggregate root for its tenancies; it alone decides who may move in or out
/// and when an invoice may be issued. Other aggregates (Property, Invoice) are referenced by id only.
/// </summary>
public class Room : Entity, IAggregateRoot
{
    private readonly List<Tenancy> _tenancies = [];

    private Room() { }

    public Guid PropertyId { get; private set; }
    public string Name { get; private set; } = "";
    public decimal Price { get; private set; }
    public int LastElectric { get; private set; }   // latest meter readings
    public int LastWater { get; private set; }
    public int OccupantCount { get; private set; } = 1;
    public bool WaterByOccupant { get; private set; }
    public decimal ServiceAmount { get; private set; }
    public decimal? ElectricRate { get; private set; }   // null = use the property's electric rate
    public bool IsUnderMaintenance { get; private set; }
    public string? MaintenanceNote { get; private set; }   // damaged equipment reported when maintenance starts
    public string? MaintenanceStatus { get; private set; }   // repair request status; null when no request is open
    public string? ReservedTenantName { get; private set; }   // deposit held for a future move-in
    public string? ReservedPhone { get; private set; }
    public decimal? ReservedDeposit { get; private set; }
    public DateOnly? ReservedDate { get; private set; }
    public DateOnly? ReservedUntil { get; private set; }
    public bool IsReserved => ReservedTenantName is not null;
    public IReadOnlyCollection<Tenancy> Tenancies => _tenancies;

    public Tenancy? CurrentTenancy => _tenancies.FirstOrDefault(t => t.IsActive);
    public bool IsOccupied => CurrentTenancy is not null;

    public static Room Create(Guid propertyId, string name, decimal price, int lastElectric, int lastWater,
        int occupantCount = 1, bool waterByOccupant = false, decimal serviceAmount = 0, decimal? electricRate = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Tên phòng không được để trống");
        if (price < 0) throw new DomainException("Giá thuê không được âm");
        if (lastElectric < 0 || lastWater < 0) throw new DomainException("Chỉ số công tơ không được âm");
        ValidateBilling(occupantCount, serviceAmount);
        ValidateElectricRate(electricRate);
        return new Room
        {
            PropertyId = propertyId, Name = name.Trim(), Price = price, LastElectric = lastElectric, LastWater = lastWater,
            OccupantCount = occupantCount, WaterByOccupant = waterByOccupant, ServiceAmount = serviceAmount, ElectricRate = electricRate
        };
    }

    public void UpdateDetails(string name, decimal price, int lastElectric, int lastWater, int occupantCount,
        bool waterByOccupant, decimal serviceAmount, decimal? electricRate)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Tên phòng không được để trống");
        if (price < 0) throw new DomainException("Giá thuê không được âm");
        if (lastElectric < 0 || lastWater < 0) throw new DomainException("Chỉ số công tơ không được âm");
        ValidateBilling(occupantCount, serviceAmount);
        ValidateElectricRate(electricRate);
        Name = name.Trim();
        Price = price;
        LastElectric = lastElectric;
        LastWater = lastWater;
        OccupantCount = occupantCount;
        WaterByOccupant = waterByOccupant;
        ServiceAmount = serviceAmount;
        ElectricRate = electricRate;
    }

    private static void ValidateElectricRate(decimal? electricRate)
    {
        if (electricRate < 0) throw new DomainException("Giá điện riêng không được âm");
    }

    private static void ValidateBilling(int occupantCount, decimal serviceAmount)
    {
        if (occupantCount < 1) throw new DomainException("Số người ở phải ít nhất là 1");
        if (serviceAmount < 0) throw new DomainException("Tiền dịch vụ không được âm");
    }

    public Tenancy MoveIn(string tenantName, string? phone, DateOnly startDate, decimal deposit)
    {
        if (IsUnderMaintenance) throw new DomainException("Phòng đang bảo trì", DomainErrorKind.Conflict);
        if (IsOccupied) throw new DomainException("Phòng đang có người thuê", DomainErrorKind.Conflict);
        var tenancy = Tenancy.Start(Id, tenantName, phone, startDate, deposit);
        _tenancies.Add(tenancy);
        ClearReservation();
        return tenancy;
    }

    public DepositReceipt? Reserve(string tenantName, string? phone, decimal deposit, DateOnly date, DateOnly holdUntil, DateTime receivedAtUtc)
    {
        if (IsUnderMaintenance) throw new DomainException("Phòng đang bảo trì", DomainErrorKind.Conflict);
        if (IsOccupied) throw new DomainException("Phòng đang có người thuê", DomainErrorKind.Conflict);
        if (IsReserved) throw new DomainException("Phòng đã được giữ cọc", DomainErrorKind.Conflict);
        if (string.IsNullOrWhiteSpace(tenantName)) throw new DomainException("Tên khách thuê không được để trống");
        if (tenantName.Trim().Length > 200 || (phone?.Length ?? 0) > 30) throw new DomainException("Thông tin khách thuê vượt quá độ dài cho phép");
        if (deposit < 0) throw new DomainException("Tiền cọc không được âm");
        if (holdUntil < date) throw new DomainException("Hạn giữ phòng phải từ ngày cọc trở đi");
        ReservedTenantName = tenantName.Trim();
        ReservedPhone = phone?.Trim();
        ReservedDeposit = deposit;
        ReservedDate = date;
        ReservedUntil = holdUntil;
        return deposit > 0 ? DepositReceipt.Issue(Id, ReservedTenantName, ReservedPhone, deposit, holdUntil, receivedAtUtc) : null;
    }

    public void CancelReservation()
    {
        if (!IsReserved) throw new DomainException("Phòng chưa được giữ cọc", DomainErrorKind.Conflict);
        ClearReservation();
    }

    public bool ReleaseIfExpired(DateOnly today)
    {
        if (!IsReserved || ReservedUntil is not { } until || until >= today) return false;
        ClearReservation();
        return true;
    }

    private void ClearReservation()
    {
        ReservedTenantName = null;
        ReservedPhone = null;
        ReservedDeposit = null;
        ReservedDate = null;
        ReservedUntil = null;
    }

    public static readonly IReadOnlyList<string> RepairStatuses = ["New", "InProgress", "Fixed", "Closed"];

    public void SetMaintenance(bool enabled, string? damagedEquipment = null)
    {
        MaintenanceNote = enabled ? ValidateDamagedEquipment(damagedEquipment) : null;
        MaintenanceStatus = null;
        IsUnderMaintenance = enabled;
    }

    // Records a repair request for a rented room without changing its maintenance status.
    public void RequestMaintenance(string? damagedEquipment)
    {
        MaintenanceNote = ValidateDamagedEquipment(damagedEquipment);
        MaintenanceStatus = RepairStatuses[0];
    }

    public void UpdateRepairStatus(string status)
    {
        if (MaintenanceNote is null) throw new DomainException("Phòng chưa có yêu cầu sửa chữa", DomainErrorKind.Conflict);
        if (!RepairStatuses.Contains(status)) throw new DomainException("Trạng thái yêu cầu sửa chữa không hợp lệ");
        MaintenanceStatus = status;
    }

    private static string ValidateDamagedEquipment(string? damagedEquipment)
    {
        var note = damagedEquipment?.Trim();
        if (string.IsNullOrEmpty(note)) throw new DomainException("Vui lòng nhập thiết bị hư hỏng");
        if (note.Length > 500) throw new DomainException("Thông tin thiết bị hư hỏng tối đa 500 ký tự");
        return note;
    }

    public void UpdateTenant(Guid tenancyId, string name, string? phone, string? identityNumber,
        string? address, string? notes, DateOnly? expectedEndDate, string? gender = null, bool temporaryResidenceRegistered = false)
    {
        var tenancy = _tenancies.SingleOrDefault(t => t.Id == tenancyId)
            ?? throw new DomainException("Không tìm thấy khách thuê trong phòng");
        tenancy.UpdateProfile(name, phone, identityNumber, address, notes, expectedEndDate, gender, temporaryResidenceRegistered);
    }

    public void MoveOut(DateOnly date)
    {
        var tenancy = CurrentTenancy ?? throw new DomainException("Phòng đang trống", DomainErrorKind.Conflict);
        tenancy.End(date);
    }

    /// <summary>Bills the current tenant for the given month and advances the stored meter readings.</summary>
    public Invoice IssueInvoice(string month, int electricNew, int? waterNew, decimal other, decimal electricRate,
        decimal waterRate, decimal waterRatePerPerson = 0, string? otherNote = null)
    {
        var tenancy = CurrentTenancy ?? throw new DomainException("Phòng đang trống, không thể lập hóa đơn", DomainErrorKind.Conflict);
        if (electricNew < LastElectric) throw new DomainException("Chỉ số điện mới phải lớn hơn hoặc bằng chỉ số cũ");
        if (!WaterByOccupant && (waterNew is null || waterNew < LastWater))
            throw new DomainException("Chỉ số nước mới phải lớn hơn hoặc bằng chỉ số cũ");

        var waterReading = WaterByOccupant ? LastWater : waterNew!.Value;
        var invoice = Invoice.Issue(Id, month, tenancy.TenantName, tenancy.Gender, LastElectric, electricNew, LastWater, waterReading,
            Price, electricRate, waterRate, other, ServiceAmount, OccupantCount, WaterByOccupant, waterRatePerPerson, otherNote);
        LastElectric = electricNew;
        if (!WaterByOccupant) LastWater = waterReading;
        return invoice;
    }

    /// <summary>Keeps the stored meter readings in step with the room's latest invoice.</summary>
    public void SetLatestReadings(int electric, int water) { LastElectric = electric; LastWater = water; }
}
