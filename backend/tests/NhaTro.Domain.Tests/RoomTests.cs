using NhaTro.Domain.Common;
using NhaTro.Domain.Entities;

namespace NhaTro.Domain.Tests;

public class RoomTests
{
    private static Room NewRoom() => Room.Create(Guid.NewGuid(), "P101", 3_000_000, lastElectric: 100, lastWater: 10);
    private static void MoveInTenant(Room r) => r.MoveIn("Nguyễn Văn An", "0901234567", new DateOnly(2026, 1, 1), 3_000_000);

    [Fact]
    public void Maintenance_blocks_renting_but_does_not_change_occupancy()
    {
        var room = NewRoom();
        room.SetMaintenance(true, "Điều hòa hỏng");
        Assert.Throws<DomainException>(() => MoveInTenant(room));
        room.SetMaintenance(false);
        MoveInTenant(room);
        room.SetMaintenance(true, "Điều hòa hỏng");
        Assert.True(room.IsOccupied);
        room.SetMaintenance(false);
        Assert.True(room.IsOccupied);
    }

    [Fact]
    public void Maintenance_requires_damaged_equipment_and_clears_it_when_ended()
    {
        var room = NewRoom();
        Assert.Throws<DomainException>(() => room.SetMaintenance(true, "  "));
        room.SetMaintenance(true, "Bóng đèn hỏng");
        Assert.Equal("Bóng đèn hỏng", room.MaintenanceNote);
        room.SetMaintenance(false);
        Assert.Null(room.MaintenanceNote);
    }

    [Fact]
    public void Contract_status_uses_expected_end_but_only_checkout_frees_room()
    {
        var room = NewRoom();
        MoveInTenant(room);
        var tenant = room.CurrentTenancy!;
        room.UpdateTenant(tenant.Id, "An", "0901", "123", "HCM", "", new DateOnly(2026, 7, 1));
        Assert.Equal("Active", tenant.ContractStatus(new DateOnly(2026, 5, 1)));
        Assert.Equal("Expiring", tenant.ContractStatus(new DateOnly(2026, 6, 15)));
        Assert.Equal("Expired", tenant.ContractStatus(new DateOnly(2026, 7, 2)));
        Assert.True(room.IsOccupied);
        room.MoveOut(new DateOnly(2026, 7, 3));
        Assert.Equal("Ended", tenant.ContractStatus(new DateOnly(2026, 7, 3)));
        Assert.False(room.IsOccupied);
    }

    [Fact]
    public void Invalid_contract_date_does_not_change_profile()
    {
        var room = NewRoom();
        MoveInTenant(room);
        var tenant = room.CurrentTenancy!;
        Assert.Throws<DomainException>(() => room.UpdateTenant(tenant.Id, "Other", "", "", "", "", new DateOnly(2025, 1, 1)));
        Assert.Equal("Nguyễn Văn An", tenant.TenantName);
    }

    [Fact]
    public void Complaint_requires_resolution_before_closing_and_supports_reopening()
    {
        var now = DateTimeOffset.UtcNow;
        var complaint = Complaint.Create(Guid.NewGuid(), "An", "Rò nước", "Ống nước bị rò", "Urgent", now);
        Assert.Throws<DomainException>(() => complaint.Update("Closed", null, "", now));
        complaint.Update("Resolved", null, "Đã sửa", now);
        Assert.NotNull(complaint.ResolvedAt);
        complaint.Update("Open", null, "Cần kiểm tra lại", now);
        Assert.Null(complaint.ResolvedAt);
    }

    [Fact]
    public void Image_rejects_fake_content_and_oversized_uploads()
    {
        Assert.Throws<DomainException>(() => RoomImage.Create(Guid.NewGuid(), "", "image/png", "not-an-image"u8.ToArray(), DateTimeOffset.UtcNow));
        Assert.Throws<DomainException>(() => RoomImage.Create(Guid.NewGuid(), "", "image/jpeg", new byte[2_097_153], DateTimeOffset.UtcNow));
    }

    [Fact]
    public void MoveIn_makes_room_occupied_and_rejects_a_second_tenant()
    {
        var room = NewRoom();
        MoveInTenant(room);

        Assert.True(room.IsOccupied);
        var ex = Assert.Throws<DomainException>(() => MoveInTenant(room));
        Assert.Equal(DomainErrorKind.Conflict, ex.Kind);
    }

    [Fact]
    public void MoveOut_frees_the_room_and_keeps_history()
    {
        var room = NewRoom();
        MoveInTenant(room);
        room.MoveOut(new DateOnly(2026, 6, 30));

        Assert.False(room.IsOccupied);
        Assert.Single(room.Tenancies);
        MoveInTenant(room);   // can be rented again
        Assert.Equal(2, room.Tenancies.Count);
    }

    [Fact]
    public void MoveOut_before_start_date_is_rejected()
    {
        var room = NewRoom();
        MoveInTenant(room);
        Assert.Throws<DomainException>(() => room.MoveOut(new DateOnly(2025, 12, 31)));
    }

    [Fact]
    public void IssueInvoice_computes_amounts_and_advances_meters()
    {
        var room = NewRoom();
        MoveInTenant(room);

        var invoice = room.IssueInvoice("2026-10", electricNew: 150, waterNew: 15, other: 50_000, electricRate: 3500, waterRate: 18000, otherNote: "Phí giặt");

        Assert.Equal(175_000m, invoice.ElectricAmount);   // 50 kWh x 3.500
        Assert.Equal(90_000m, invoice.WaterAmount);       // 5 m3 x 18.000
        Assert.Equal(3_315_000m, invoice.Total);
        Assert.Equal("Nguyễn Văn An", invoice.TenantName);
        Assert.Equal((150, 15), (room.LastElectric, room.LastWater));
    }

    [Fact]
    public void Per_occupant_water_and_service_fee_are_snapshotted_without_advancing_water_meter()
    {
        var propertyId = Guid.NewGuid();
        var room = Room.Create(propertyId, "P101", 3_000_000, lastElectric: 100, lastWater: 10,
            occupantCount: 3, waterByOccupant: true, serviceAmount: 50_000);
        MoveInTenant(room);

        var invoice = room.IssueInvoice("2026-10", electricNew: 120, waterNew: null, other: 10_000,
            electricRate: 3500, waterRate: 18_000, waterRatePerPerson: 20_000, otherNote: "Phí giặt");

        Assert.Equal(60_000m, invoice.WaterAmount);
        Assert.Equal(50_000m, invoice.ServiceAmount);
        Assert.Equal(3, invoice.OccupantCount);
        Assert.True(invoice.WaterByOccupant);
        Assert.Equal(20_000m, invoice.WaterRatePerPerson);
        Assert.Equal(3_190_000m, invoice.Total);
        Assert.Equal((120, 10), (room.LastElectric, room.LastWater));
    }

    [Fact]
    public void IssueInvoice_rejects_readings_lower_than_previous_and_leaves_meters_untouched()
    {
        var room = NewRoom();
        MoveInTenant(room);

        Assert.Throws<DomainException>(() => room.IssueInvoice("2026-10", 99, 15, 0, 3500, 18000));
        Assert.Equal((100, 10), (room.LastElectric, room.LastWater));
    }

    [Fact]
    public void IssueInvoice_requires_a_tenant_and_a_valid_month()
    {
        var room = NewRoom();
        Assert.Equal(DomainErrorKind.Conflict, Assert.Throws<DomainException>(() => room.IssueInvoice("2026-10", 120, 12, 0, 3500, 18000)).Kind);

        MoveInTenant(room);
        Assert.Throws<DomainException>(() => room.IssueInvoice("2026-13", 120, 12, 0, 3500, 18000));
    }

    [Fact]
    public void Invoice_payment_can_be_toggled()
    {
        var room = NewRoom();
        MoveInTenant(room);
        var invoice = room.IssueInvoice("2026-10", 120, 12, 0, 3500, 18000);

        invoice.MarkPaid(new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc));
        Assert.True(invoice.IsPaid);
        Assert.NotNull(invoice.PaidAt);

        invoice.MarkUnpaid();
        Assert.False(invoice.IsPaid);
        Assert.Null(invoice.PaidAt);
    }
}
