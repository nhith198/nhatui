using Microsoft.AspNetCore.Mvc;
using NhaTro.Api.Contracts;
using NhaTro.Application.Abstractions;
using NhaTro.Application.Dtos;
using NhaTro.Application.UseCases;

namespace NhaTro.Api.Controllers;

[Route("api/properties")]
public class PropertiesController(
    IQueryHandler<ListPropertiesQuery, List<PropertyDto>> list,
    ICommandHandler<CreatePropertyCommand, PropertyDto> create,
    ICommandHandler<UpdatePropertyCommand> update,
    ICommandHandler<DeletePropertyCommand> delete,
    ICommandHandler<UploadPropertyQrCommand> uploadQr,
    ICommandHandler<DeletePropertyQrCommand> deleteQr,
    IQueryHandler<GetPropertyQrQuery, PropertyQrDto?> getQr) : ApiBase
{
    [HttpGet, PagePermission(EmployeePages.Properties, allowAnyGrantedPage: true)]
    public Task<List<PropertyDto>> List(CancellationToken ct) => list.Handle(new ListPropertiesQuery(UserId), ct);

    [HttpPost, PagePermission(EmployeePages.Properties)] public Task<PropertyDto> Create(PropertyRequest r, CancellationToken ct) =>
        create.Handle(new CreatePropertyCommand(UserId, r.Name, r.Address, r.ElectricRate, r.WaterRate,
            r.WaterRatePerPerson, r.BankName, r.BankAccountNumber, r.BankAccountName, r.ServiceAmountPerMonth), ct);

    [HttpPut("{id:guid}"), PagePermission(EmployeePages.Properties)]
    public async Task<IActionResult> Update(Guid id, PropertyRequest r, CancellationToken ct)
    {
        await update.Handle(new UpdatePropertyCommand(UserId, id, r.Name, r.Address, r.ElectricRate, r.WaterRate,
            r.WaterRatePerPerson, r.BankName, r.BankAccountNumber, r.BankAccountName, r.ServiceAmountPerMonth), ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}"), PagePermission(EmployeePages.Properties)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await delete.Handle(new DeletePropertyCommand(UserId, id), ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/qr"), PagePermission(EmployeePages.Properties)]
    public async Task<IActionResult> GetQr(Guid id, CancellationToken ct)
    {
        var qr = await getQr.Handle(new GetPropertyQrQuery(UserId, id), ct);
        return qr is null ? NotFound() : File(qr.Data, qr.ContentType);
    }

    [HttpPut("{id:guid}/qr"), PagePermission(EmployeePages.Properties), RequestSizeLimit(2_200_000), RequestFormLimits(MultipartBodyLengthLimit = 2_200_000)]
    public async Task<IActionResult> UploadQr(Guid id, IFormFile file, CancellationToken ct)
    {
        if (file.Length is 0 or > 2_097_152) return BadRequest(new { message = "Ảnh QR tối đa 2 MB" });
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);
        await uploadQr.Handle(new UploadPropertyQrCommand(UserId, id, buffer.ToArray(), file.ContentType), ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}/qr"), PagePermission(EmployeePages.Properties)]
    public async Task<IActionResult> DeleteQr(Guid id, CancellationToken ct)
    {
        await deleteQr.Handle(new DeletePropertyQrCommand(UserId, id), ct);
        return NoContent();
    }
}
