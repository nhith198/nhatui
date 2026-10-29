using NhaTro.Application.Abstractions;
using NhaTro.Application.Dtos;
using NhaTro.Domain.Common;
using NhaTro.Domain.Entities;

namespace NhaTro.Application.UseCases;

public record CreateRoomCommand(Guid UserId, Guid PropertyId, string Name, decimal Price, int LastElectric, int LastWater,
    int OccupantCount, bool WaterByOccupant, decimal? ServiceAmount, decimal? ElectricRate = null);
public record UpdateRoomCommand(Guid UserId, Guid RoomId, string Name, decimal Price, int LastElectric, int LastWater,
    int OccupantCount, bool WaterByOccupant, decimal ServiceAmount, decimal? ElectricRate = null);
public record DeleteRoomCommand(Guid UserId, Guid RoomId);
public record RentRoomCommand(Guid UserId, Guid RoomId, string TenantName, string? Phone, DateOnly StartDate, decimal Deposit, DateOnly? ExpectedEndDate = null);
public record CheckoutRoomCommand(Guid UserId, Guid RoomId);
public record ReserveRoomCommand(Guid UserId, Guid RoomId, string TenantName, string? Phone, decimal Deposit, DateOnly HoldUntil);
public record CancelReservationCommand(Guid UserId, Guid RoomId, decimal RefundAmount);
public record ListRoomsQuery(Guid UserId, Guid? PropertyId);

public class CreateRoomHandler(IPropertyRepository properties, IRoomRepository rooms, IManagementRepository users, IUnitOfWork uow) : ICommandHandler<CreateRoomCommand>
{
    public async Task Handle(CreateRoomCommand c, CancellationToken ct)
    {
        var user = await users.GetUserAsync(c.UserId, ct) ?? throw new UnauthorizedException("Tài khoản không tồn tại");
        if (user.EmployerId is not null) throw new ForbiddenException("Chỉ chủ nhà được thêm phòng");
        var property = await properties.GetOwnedAsync(c.PropertyId, c.UserId, ct) ?? throw new NotFoundException();
        var room = Room.Create(c.PropertyId, c.Name, c.Price, c.LastElectric, c.LastWater,
            c.OccupantCount, c.WaterByOccupant, c.ServiceAmount ?? property.ServiceAmountPerMonth, c.ElectricRate);
        if (await rooms.NameExistsAsync(c.PropertyId, room.Name, ct)) throw new ConflictException("Tên phòng đã tồn tại trong nhà này");
        rooms.Add(room);
        await uow.SaveChangesAsync(ct);
    }
}

public class UpdateRoomHandler(IRoomRepository rooms, IManagementRepository users, IUnitOfWork uow)
    : ICommandHandler<UpdateRoomCommand>
{
    public async Task Handle(UpdateRoomCommand c, CancellationToken ct)
    {
        var user = await users.GetUserAsync(c.UserId, ct) ?? throw new UnauthorizedException("Tài khoản không tồn tại");
        if (user.EmployerId is not null) throw new ForbiddenException("Chỉ chủ nhà được sửa phòng");
        var room = await rooms.GetOwnedAsync(c.RoomId, c.UserId, ct) ?? throw new NotFoundException();
        room.UpdateDetails(c.Name, c.Price, c.LastElectric, c.LastWater, c.OccupantCount,
            c.WaterByOccupant, c.ServiceAmount, c.ElectricRate);
        await uow.SaveChangesAsync(ct);
    }
}

public class DeleteRoomHandler(IRoomRepository rooms, IPropertyRepository properties, IUnitOfWork uow) : ICommandHandler<DeleteRoomCommand>
{
    public async Task Handle(DeleteRoomCommand c, CancellationToken ct)
    {
        var room = await rooms.GetOwnedAsync(c.RoomId, c.UserId, ct) ?? throw new NotFoundException();
        _ = await properties.GetOwnedAsync(room.PropertyId, c.UserId, ct) ?? throw new NotFoundException();
        rooms.Remove(room);   // tenancies and invoices are removed by cascade
        await uow.SaveChangesAsync(ct);
    }
}

public class RentRoomHandler(IRoomRepository rooms, IUnitOfWork uow) : ICommandHandler<RentRoomCommand>
{
    public async Task Handle(RentRoomCommand c, CancellationToken ct)
    {
        var room = await rooms.GetOwnedAsync(c.RoomId, c.UserId, ct) ?? throw new NotFoundException();
        var tenancy = room.MoveIn(c.TenantName, c.Phone, c.StartDate, c.Deposit);
        room.UpdateTenant(tenancy.Id, c.TenantName, c.Phone, null, null, null, c.ExpectedEndDate);
        await uow.SaveChangesAsync(ct);
    }
}

public class CheckoutRoomHandler(IRoomRepository rooms, IUnitOfWork uow, TimeProvider clock) : ICommandHandler<CheckoutRoomCommand>
{
    public async Task Handle(CheckoutRoomCommand c, CancellationToken ct)
    {
        var room = await rooms.GetOwnedAsync(c.RoomId, c.UserId, ct) ?? throw new NotFoundException();
        room.MoveOut(DateOnly.FromDateTime(clock.GetLocalNow().DateTime));
        await uow.SaveChangesAsync(ct);
    }
}

public class ReserveRoomHandler(IRoomRepository rooms, IDepositReceiptRepository receipts, IUnitOfWork uow, TimeProvider clock) : ICommandHandler<ReserveRoomCommand>
{
    public async Task Handle(ReserveRoomCommand c, CancellationToken ct)
    {
        var room = await rooms.GetOwnedAsync(c.RoomId, c.UserId, ct) ?? throw new NotFoundException();
        var receipt = room.Reserve(c.TenantName, c.Phone, c.Deposit, DateOnly.FromDateTime(clock.GetLocalNow().DateTime), c.HoldUntil, clock.GetUtcNow().UtcDateTime);
        if (receipt is not null) receipts.Add(receipt);
        await uow.SaveChangesAsync(ct);
    }
}

public class CancelReservationHandler(IRoomRepository rooms, IDepositReceiptRepository receipts, IUnitOfWork uow, TimeProvider clock)
    : ICommandHandler<CancelReservationCommand>
{
    public async Task Handle(CancelReservationCommand c, CancellationToken ct)
    {
        var room = await rooms.GetOwnedAsync(c.RoomId, c.UserId, ct) ?? throw new NotFoundException();
        room.CancelReservation();
        var receipt = await receipts.GetOpenForRoomAsync(room.Id, ct);
        if (receipt is null)
        {
            if (c.RefundAmount != 0) throw new DomainException("Phòng không có tiền cọc để hoàn");
        }
        else receipt.Refund(c.RefundAmount, clock.GetUtcNow().UtcDateTime);
        await uow.SaveChangesAsync(ct);
    }
}

public record ReleaseExpiredReservationsCommand;

public class ReleaseExpiredReservationsHandler(IRoomRepository rooms, IUnitOfWork uow, TimeProvider clock)
    : ICommandHandler<ReleaseExpiredReservationsCommand>
{
    public async Task Handle(ReleaseExpiredReservationsCommand _, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        foreach (var room in await rooms.GetExpiredReservationsAsync(today, ct)) room.ReleaseIfExpired(today);
        await uow.SaveChangesAsync(ct);
    }
}

public class ListRoomsHandler(IRoomQueries queries) : IQueryHandler<ListRoomsQuery, List<RoomDto>>
{
    public Task<List<RoomDto>> Handle(ListRoomsQuery q, CancellationToken ct) => queries.ListAsync(q.UserId, q.PropertyId, ct);
}
