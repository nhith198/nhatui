using NhaTro.Domain.Common;

namespace NhaTro.Domain.Entities;

/// <summary>Confirmation issued when a reservation deposit is received. Its amount counts as revenue in the month it was received.</summary>
public class DepositReceipt : Entity, IAggregateRoot
{
    private DepositReceipt() { }

    public Guid RoomId { get; private set; }
    public string TenantName { get; private set; } = "";   // snapshot at receipt time
    public string? Phone { get; private set; }
    public decimal Amount { get; private set; }
    public DateOnly HoldUntil { get; private set; }
    public DateTime ReceivedAt { get; private set; }   // UTC
    public DateTime? RefundedAt { get; private set; }   // UTC, set when the reservation is cancelled
    public decimal? RefundedAmount { get; private set; }
    public bool IsRefunded => RefundedAt is not null;

    public void Refund(decimal amount, DateTime refundedAtUtc)
    {
        if (IsRefunded) throw new DomainException("Phiếu cọc đã được hoàn");
        if (amount < 0 || amount > Amount) throw new DomainException("Số tiền hoàn phải từ 0 đến tiền cọc");
        RefundedAmount = amount;
        RefundedAt = refundedAtUtc;
    }

    /// <summary>Only Room.Reserve issues deposit receipts, so a receipt always matches a reservation.</summary>
    internal static DepositReceipt Issue(Guid roomId, string tenantName, string? phone, decimal amount, DateOnly holdUntil, DateTime receivedAtUtc) =>
        new()
        {
            RoomId = roomId, TenantName = tenantName, Phone = phone, Amount = amount, HoldUntil = holdUntil, ReceivedAt = receivedAtUtc
        };
}
