using NhaTro.Application.Abstractions;
using NhaTro.Application.Dtos;

namespace NhaTro.Application.UseCases;

public record CreateInvoiceCommand(Guid UserId, Guid RoomId, string Month, int ElectricNew, int? WaterNew, decimal Other, string? OtherNote = null);
public record TogglePaidCommand(Guid UserId, Guid InvoiceId);
public record ListInvoicesQuery(Guid UserId, string Month, Guid? PropertyId);
public record GetDashboardQuery(Guid UserId, string Month, Guid? PropertyId);

public class CreateInvoiceHandler(IRoomRepository rooms, IPropertyRepository properties, IInvoiceRepository invoices, IUnitOfWork uow)
    : ICommandHandler<CreateInvoiceCommand, InvoiceDto>
{
    public async Task<InvoiceDto> Handle(CreateInvoiceCommand c, CancellationToken ct)
    {
        var room = await rooms.GetOwnedAsync(c.RoomId, c.UserId, ct) ?? throw new NotFoundException();
        var property = await properties.GetAccessibleAsync(room.PropertyId, c.UserId, ct) ?? throw new NotFoundException();
        if (await invoices.ExistsAsync(room.Id, c.Month, ct)) throw new ConflictException("Phòng đã có hóa đơn tháng này");

        var invoice = room.IssueInvoice(c.Month, c.ElectricNew, c.WaterNew, c.Other,
            room.ElectricRate ?? property.ElectricRate, property.WaterRate, property.WaterRatePerPerson, c.OtherNote);
        invoices.Add(invoice);
        await uow.SaveChangesAsync(ct);

        return new InvoiceDto(invoice.Id, room.Id, room.Name, property.Name, invoice.TenantName, invoice.Month, invoice.Gender,
            invoice.ElectricOld, invoice.ElectricNew, invoice.WaterOld, invoice.WaterNew,
            invoice.Rent, invoice.ElectricAmount, invoice.WaterAmount, invoice.Other, invoice.Total, invoice.IsPaid,
            invoice.ServiceAmount, invoice.WaterByOccupant, invoice.OccupantCount, invoice.WaterRatePerPerson, invoice.ElectricRate, invoice.OtherNote,
            property.Id, property.QrCode != null);
    }
}

public class TogglePaidHandler(IInvoiceRepository invoices, IUnitOfWork uow, TimeProvider clock) : ICommandHandler<TogglePaidCommand>
{
    public async Task Handle(TogglePaidCommand c, CancellationToken ct)
    {
        var invoice = await invoices.GetOwnedAsync(c.InvoiceId, c.UserId, ct) ?? throw new NotFoundException();
        if (invoice.IsPaid) invoice.MarkUnpaid(); else invoice.MarkPaid(clock.GetUtcNow().UtcDateTime);
        await uow.SaveChangesAsync(ct);
    }
}

public record DeleteInvoiceCommand(Guid UserId, Guid InvoiceId);

public class DeleteInvoiceHandler(IInvoiceRepository invoices, IRoomRepository rooms, IUnitOfWork uow)
    : ICommandHandler<DeleteInvoiceCommand>
{
    public async Task Handle(DeleteInvoiceCommand c, CancellationToken ct)
    {
        var invoice = await invoices.GetOwnedAsync(c.InvoiceId, c.UserId, ct) ?? throw new NotFoundException();
        if (invoice.IsPaid) throw new ConflictException("Hóa đơn đã thu, không thể xóa");
        if (await invoices.HasLaterInvoiceAsync(invoice.RoomId, invoice.Month, ct))
            throw new ConflictException("Chỉ có thể xóa hóa đơn mới nhất của phòng");
        var room = await rooms.GetOwnedAsync(invoice.RoomId, c.UserId, ct) ?? throw new NotFoundException();

        room.SetLatestReadings(invoice.ElectricOld, invoice.WaterOld);
        invoices.Remove(invoice);
        await uow.SaveChangesAsync(ct);
    }
}

public class ListInvoicesHandler(IInvoiceQueries queries) : IQueryHandler<ListInvoicesQuery, List<InvoiceDto>>
{
    public Task<List<InvoiceDto>> Handle(ListInvoicesQuery q, CancellationToken ct) => queries.ListAsync(q.UserId, q.Month, q.PropertyId, ct);
}

public class GetDashboardHandler(IDashboardQueries queries) : IQueryHandler<GetDashboardQuery, DashboardDto>
{
    public Task<DashboardDto> Handle(GetDashboardQuery q, CancellationToken ct) => queries.GetAsync(q.UserId, q.Month, q.PropertyId, ct);
}
