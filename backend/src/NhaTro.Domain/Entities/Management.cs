using NhaTro.Domain.Common;

namespace NhaTro.Domain.Entities;

public class StaffAssignment : Entity
{
    private StaffAssignment() { }
    public Guid UserId { get; private set; }
    public Guid PropertyId { get; private set; }
    public static StaffAssignment Create(Guid userId, Guid propertyId) => new() { UserId = userId, PropertyId = propertyId };
}

public class RoomImage : Entity, IAggregateRoot
{
    private RoomImage() { }
    public Guid RoomId { get; private set; }
    public string Caption { get; private set; } = "";
    public string ContentType { get; private set; } = "";
    public byte[] Data { get; private set; } = [];
    public DateTimeOffset CreatedAt { get; private set; }

    public static RoomImage Create(Guid roomId, string caption, string contentType, byte[] data, DateTimeOffset now)
    {
        if (caption.Length > 200) throw new DomainException("Chú thích tối đa 200 ký tự");
        if (data.Length is 0 or > 2_097_152) throw new DomainException("Ảnh tối đa 2 MB");
        var valid = contentType switch
        {
            "image/jpeg" => data.Length >= 3 && data[0] == 0xff && data[1] == 0xd8 && data[2] == 0xff,
            "image/png" => data.Length >= 8 && data.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            "image/webp" => data.Length >= 12 && data.AsSpan(0, 4).SequenceEqual("RIFF"u8) && data.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            _ => false
        };
        if (!valid) throw new DomainException("Chỉ chấp nhận ảnh JPEG, PNG hoặc WebP hợp lệ");
        return new() { RoomId = roomId, Caption = caption.Trim(), ContentType = contentType, Data = data, CreatedAt = now };
    }
}

public class Complaint : Entity, IAggregateRoot
{
    private Complaint() { }
    public Guid RoomId { get; private set; }
    public string TenantName { get; private set; } = "";
    public string Title { get; private set; } = "";
    public string Description { get; private set; } = "";
    public string Priority { get; private set; } = "Normal";
    public string Status { get; private set; } = "Open";
    public Guid? AssignedTo { get; private set; }
    public string Resolution { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }

    public static Complaint Create(Guid roomId, string tenantName, string title, string description, string priority, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(tenantName) || tenantName.Length > 200 || string.IsNullOrWhiteSpace(title) || title.Length > 200
            || string.IsNullOrWhiteSpace(description) || description.Length > 4000)
            throw new DomainException("Thông tin khiếu nại không hợp lệ");
        if (priority is not ("Low" or "Normal" or "Urgent")) throw new DomainException("Mức ưu tiên không hợp lệ");
        return new() { RoomId = roomId, TenantName = tenantName.Trim(), Title = title.Trim(), Description = description.Trim(), Priority = priority, CreatedAt = now };
    }

    public void Update(string status, Guid? assignedTo, string resolution, DateTimeOffset now)
    {
        if (status is not ("Open" or "InProgress" or "Resolved" or "Closed")) throw new DomainException("Trạng thái khiếu nại không hợp lệ");
        if (resolution.Length > 2000) throw new DomainException("Kết quả xử lý tối đa 2000 ký tự");
        if (status is "Resolved" or "Closed" && string.IsNullOrWhiteSpace(resolution))
            throw new DomainException("Vui lòng nhập kết quả xử lý trước khi hoàn tất");
        Status = status;
        AssignedTo = assignedTo;
        Resolution = resolution.Trim();
        ResolvedAt = status is "Resolved" or "Closed" ? ResolvedAt ?? now : null;
    }

    public void Unassign() => AssignedTo = null;
}