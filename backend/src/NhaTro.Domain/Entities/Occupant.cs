using NhaTro.Domain.Common;

namespace NhaTro.Domain.Entities;

/// <summary>A person living in a room who is not named on the contract.</summary>
public class Occupant : Entity, IAggregateRoot
{
    private Occupant() { }

    public Guid RoomId { get; private set; }
    public string Name { get; private set; } = "";
    public string Phone { get; private set; } = "";
    public string IdentityNumber { get; private set; } = "";
    public string Gender { get; private set; } = "";
    public string Address { get; private set; } = "";
    public string Notes { get; private set; } = "";
    public bool TemporaryResidenceRegistered { get; private set; }

    public static Occupant Create(Guid roomId, string name, string? phone, string? identityNumber, string? gender, string? address, string? notes, bool temporaryResidenceRegistered = false)
    {
        var occupant = new Occupant();
        occupant.Update(roomId, name, phone, identityNumber, gender, address, notes, temporaryResidenceRegistered);
        return occupant;
    }

    public void Update(Guid roomId, string name, string? phone, string? identityNumber, string? gender, string? address, string? notes, bool temporaryResidenceRegistered = false)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200) throw new DomainException("Tên người ở không hợp lệ");
        if ((phone?.Length ?? 0) > 30 || (identityNumber?.Length ?? 0) > 30 || (address?.Length ?? 0) > 500 || (notes?.Length ?? 0) > 2000)
            throw new DomainException("Thông tin người ở vượt quá độ dài cho phép");
        if (gender is not (null or "" or "Nam" or "Nữ")) throw new DomainException("Giới tính phải là Nam hoặc Nữ");
        RoomId = roomId;
        Name = name.Trim();
        Phone = phone?.Trim() ?? "";
        IdentityNumber = identityNumber?.Trim() ?? "";
        Gender = gender?.Trim() ?? "";
        Address = address?.Trim() ?? "";
        Notes = notes?.Trim() ?? "";
        TemporaryResidenceRegistered = temporaryResidenceRegistered;
    }
}
