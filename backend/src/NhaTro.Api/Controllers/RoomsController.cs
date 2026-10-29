using Microsoft.AspNetCore.Mvc;
using NhaTro.Api.Contracts;
using NhaTro.Application.Abstractions;
using NhaTro.Application.Dtos;
using NhaTro.Application.UseCases;

namespace NhaTro.Api.Controllers;

[PagePermission(EmployeePages.Rooms), Route("api/rooms")]
public class RoomsController(
    IQueryHandler<ListRoomsQuery, List<RoomDto>> list,
    ICommandHandler<CreateRoomCommand> create,
    ICommandHandler<UpdateRoomCommand> update,
    ICommandHandler<DeleteRoomCommand> delete,
    ICommandHandler<RentRoomCommand> rent,
    ICommandHandler<CheckoutRoomCommand> checkout,
    ICommandHandler<ReserveRoomCommand> reserve,
    ICommandHandler<CancelReservationCommand> cancelReservation,
    ManagementService management) : ApiBase
{
    [HttpGet] public Task<List<RoomDto>> List(Guid? propertyId, CancellationToken ct) => list.Handle(new ListRoomsQuery(UserId, propertyId), ct);

    [HttpPost]
    public async Task<IActionResult> Create(CreateRoomRequest r, CancellationToken ct)
    {
        await create.Handle(new CreateRoomCommand(UserId, r.PropertyId, r.Name, r.Price, r.LastElectric, r.LastWater,
            r.OccupantCount, r.WaterByOccupant, r.ServiceAmount, r.ElectricRate), ct);
        return StatusCode(StatusCodes.Status201Created);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateRoomRequest r, CancellationToken ct)
    {
        await update.Handle(new UpdateRoomCommand(UserId, id, r.Name, r.Price, r.LastElectric, r.LastWater,
            r.OccupantCount, r.WaterByOccupant, r.ServiceAmount, r.ElectricRate), ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await delete.Handle(new DeleteRoomCommand(UserId, id), ct); return NoContent(); }

    [HttpPost("{id:guid}/rent")]
    public async Task<IActionResult> Rent(Guid id, RentRoomRequest r, CancellationToken ct)
    {
        await rent.Handle(new RentRoomCommand(UserId, id, r.TenantName, r.Phone, r.StartDate, r.Deposit, r.ExpectedEndDate), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/checkout")]
    public async Task<IActionResult> Checkout(Guid id, CancellationToken ct) { await checkout.Handle(new CheckoutRoomCommand(UserId, id), ct); return NoContent(); }

    [HttpPost("{id:guid}/reserve")]
    public async Task<IActionResult> Reserve(Guid id, ReserveRoomRequest r, CancellationToken ct)
    {
        await reserve.Handle(new ReserveRoomCommand(UserId, id, r.TenantName, r.Phone, r.Deposit, r.HoldUntil), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/cancel-reservation")]
    public async Task<IActionResult> CancelReservation(Guid id, CancelReservationRequest r, CancellationToken ct)
    {
        await cancelReservation.Handle(new CancelReservationCommand(UserId, id, r.RefundAmount), ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/maintenance")]
    public async Task<IActionResult> Maintenance(Guid id, MaintenanceRequest request, CancellationToken ct)
    {
        await management.Maintenance(UserId, id, request.Enabled, request.DamagedEquipment, ct);
        return NoContent();
    }
}
