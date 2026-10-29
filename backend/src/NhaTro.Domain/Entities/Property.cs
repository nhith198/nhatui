using NhaTro.Domain.Common;

namespace NhaTro.Domain.Entities;

/// <summary>A rental house/building owned by a landlord. Holds the utility rates used when billing its rooms.</summary>
public class Property : Entity, IAggregateRoot
{
    private Property() { }

    public Guid OwnerId { get; private set; }
    public string Name { get; private set; } = "";
    public string Address { get; private set; } = "";
    public decimal ElectricRate { get; private set; }   // ₫/kWh
    public decimal WaterRate { get; private set; }      // ₫/m³
    public decimal WaterRatePerPerson { get; private set; }
    public decimal ServiceAmountPerMonth { get; private set; }
    public string BankName { get; private set; } = "";
    public string BankAccountNumber { get; private set; } = "";
    public string BankAccountName { get; private set; } = "";
    public byte[]? QrCode { get; private set; }
    public string QrContentType { get; private set; } = "";

    public static Property Create(Guid ownerId, string name, string? address, decimal electricRate, decimal waterRate,
        decimal waterRatePerPerson = 0, string? bankName = null, string? bankAccountNumber = null, string? bankAccountName = null,
        decimal serviceAmountPerMonth = 0)
    {
        var p = new Property { OwnerId = ownerId };
        p.Update(name, address, electricRate, waterRate, waterRatePerPerson, bankName, bankAccountNumber, bankAccountName,
            serviceAmountPerMonth);
        return p;
    }

    public void Update(string name, string? address, decimal electricRate, decimal waterRate,
        decimal waterRatePerPerson = 0, string? bankName = null, string? bankAccountNumber = null, string? bankAccountName = null,
        decimal serviceAmountPerMonth = 0)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Tên nhà không được để trống");
        if (electricRate < 0 || waterRate < 0 || waterRatePerPerson < 0 || serviceAmountPerMonth < 0)
            throw new DomainException("Đơn giá điện nước và dịch vụ không được âm");
        if (bankName?.Length > 100 || bankAccountNumber?.Length > 50 || bankAccountName?.Length > 200)
            throw new DomainException("Thông tin tài khoản ngân hàng không hợp lệ");
        Name = name.Trim();
        Address = address?.Trim() ?? "";
        ElectricRate = electricRate;
        WaterRate = waterRate;
        WaterRatePerPerson = waterRatePerPerson;
        ServiceAmountPerMonth = serviceAmountPerMonth;
        BankName = bankName?.Trim() ?? "";
        BankAccountNumber = bankAccountNumber?.Trim() ?? "";
        BankAccountName = bankAccountName?.Trim() ?? "";
    }

    public void SetQrCode(byte[] data, string contentType)
    {
        if (contentType is not ("image/png" or "image/jpeg")) throw new DomainException("Mã QR phải là ảnh PNG hoặc JPG");
        if (data.Length is 0 or > 2_097_152) throw new DomainException("Ảnh QR tối đa 2 MB");
        QrCode = data;
        QrContentType = contentType;
    }

    public void RemoveQrCode()
    {
        QrCode = null;
        QrContentType = "";
    }
}
