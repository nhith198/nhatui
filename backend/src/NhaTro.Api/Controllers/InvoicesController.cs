using Microsoft.AspNetCore.Mvc;
using NhaTro.Api.Contracts;
using NhaTro.Application.Abstractions;
using NhaTro.Application.Dtos;
using NhaTro.Application.UseCases;

namespace NhaTro.Api.Controllers;

[PagePermission(EmployeePages.Invoices), Route("api/invoices")]
public class InvoicesController(
    IQueryHandler<ListInvoicesQuery, List<InvoiceDto>> list,
    ICommandHandler<CreateInvoiceCommand, InvoiceDto> create,
    ICommandHandler<TogglePaidCommand> togglePaid,
    ICommandHandler<DeleteInvoiceCommand> delete) : ApiBase
{
    [HttpGet] public Task<List<InvoiceDto>> List(string month, Guid? propertyId, CancellationToken ct) => list.Handle(new ListInvoicesQuery(UserId, month, propertyId), ct);

    [HttpPost] public Task<InvoiceDto> Create(CreateInvoiceRequest r, CancellationToken ct) =>
        create.Handle(new CreateInvoiceCommand(UserId, r.RoomId, r.Month, r.ElectricNew, r.WaterNew, r.Other, r.OtherNote), ct);

    [HttpPost("{id:guid}/toggle-paid")]
    public async Task<IActionResult> TogglePaid(Guid id, CancellationToken ct) { await togglePaid.Handle(new TogglePaidCommand(UserId, id), ct); return NoContent(); }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await delete.Handle(new DeleteInvoiceCommand(UserId, id), ct);
        return NoContent();
    }
}
