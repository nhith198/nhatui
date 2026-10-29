using Microsoft.AspNetCore.Mvc;
using NhaTro.Api.Contracts;
using NhaTro.Application.Dtos;
using NhaTro.Application.UseCases;
using NhaTro.Application.Abstractions;

namespace NhaTro.Api.Controllers;

[Route("api/account")]
public class AccountController(ManagementService service) : ApiBase
{
    [HttpGet]
    public Task<AccountDto> Get(CancellationToken ct) => service.Account(UserId, ct);

    [HttpPut]
    public Task<AccountDto> Update(UpdateAccountRequest request, CancellationToken ct) =>
        service.UpdateAccount(UserId, new AccountProfileInput(request.FullName, request.Phone,
            request.CurrentPassword, request.NewPassword), ct);
}

[PagePermission(EmployeePages.Tenancies), Route("api/tenancies")]
public class TenanciesController(ManagementService service) : ApiBase
{
    [HttpGet]
    public Task<List<TenancyDto>> List(Guid? propertyId, CancellationToken ct) => service.Tenancies(UserId, propertyId, ct);

    [HttpPut("{id:guid}/rooms/{roomId:guid}")]
    public async Task<IActionResult> Update(Guid id, Guid roomId, TenantProfileRequest request, CancellationToken ct)
    {
        await service.UpdateTenant(UserId, roomId, id, new TenantProfile(request.Name, request.Phone, request.IdentityNumber,
            request.Address, request.Notes, request.ExpectedEndDate, request.Gender, request.TemporaryResidenceRegistered), ct);
        return NoContent();
    }
}

[Route("api/employees")]
public class EmployeesController(ManagementService service) : ApiBase
{
    [HttpGet]
    public Task<List<EmployeeDto>> List(CancellationToken ct) => service.Employees(UserId, ct);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EmployeeDto>> Get(Guid id, CancellationToken ct)
    {
        var employee = (await service.Employees(UserId, ct)).FirstOrDefault(employee => employee.Id == id);
        return employee is null ? NotFound() : Ok(employee);
    }

    [HttpPost]
    public async Task<IActionResult> Create(EmployeeRequest request, CancellationToken ct)
    {
        var id = await service.SaveEmployee(UserId, null, Input(request), ct);
        return CreatedAtAction(nameof(Get), new { id }, new { id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, EmployeeRequest request, CancellationToken ct)
    {
        await service.SaveEmployee(UserId, id, Input(request), ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteEmployee(UserId, id, ct);
        return NoContent();
    }

    private static EmployeeInput Input(EmployeeRequest request) => new(request.FullName, request.Phone, request.Password,
        request.IsDisabled, request.PropertyIds, request.PagePermissions);
}

[PagePermission(EmployeePages.Complaints), Route("api/complaints")]
public class ComplaintsController(ManagementService service) : ApiBase
{
    [HttpGet]
    public Task<List<ComplaintDto>> List(Guid? propertyId, CancellationToken ct) => service.Complaints(UserId, propertyId, ct);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ComplaintDto>> Get(Guid id, CancellationToken ct)
    {
        var complaint = (await service.Complaints(UserId, null, ct)).FirstOrDefault(complaint => complaint.Id == id);
        return complaint is null ? NotFound() : Ok(complaint);
    }

    [HttpGet("assignees")]
    public Task<List<AssigneeDto>> Assignees(Guid propertyId, CancellationToken ct) => service.Assignees(UserId, propertyId, ct);

    [HttpPost]
    public async Task<IActionResult> Create(CreateComplaintRequest request, CancellationToken ct)
    {
        var id = await service.CreateComplaint(UserId, request.RoomId, request.TenantName, request.Title, request.Description, request.Priority, ct);
        return CreatedAtAction(nameof(Get), new { id }, new { id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateComplaintRequest request, CancellationToken ct)
    {
        await service.UpdateComplaint(UserId, id, request.Status, request.AssignedTo, request.Resolution, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteComplaint(UserId, id, ct);
        return NoContent();
    }
}

[PagePermission(EmployeePages.Repairs), Route("api/repairs")]
public class RepairsController(ManagementService service) : ApiBase
{
    [HttpGet]
    public Task<List<RepairRequestDto>> List(Guid? propertyId, CancellationToken ct) => service.Repairs(UserId, propertyId, ct);

    [HttpPut("{roomId:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid roomId, UpdateRepairStatusRequest request, CancellationToken ct)
    {
        await service.UpdateRepair(UserId, roomId, request.Status, ct);
        return NoContent();
    }
}

[PagePermission(EmployeePages.Rooms), Route("api/rooms/{roomId:guid}/images")]
public class RoomImagesController(ManagementService service) : ApiBase
{
    [HttpGet]
    public Task<List<RoomImageDto>> List(Guid roomId, CancellationToken ct) => service.Images(UserId, roomId, ct);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RoomImageDto>> Get(Guid roomId, Guid id, CancellationToken ct)
    {
        var image = (await service.Images(UserId, roomId, ct)).FirstOrDefault(image => image.Id == id);
        return image is null ? NotFound() : Ok(image);
    }

    [HttpPost, RequestSizeLimit(2_200_000), RequestFormLimits(MultipartBodyLengthLimit = 2_200_000)]
    public async Task<IActionResult> Create(Guid roomId, IFormFile file, [FromForm] string? caption, CancellationToken ct)
    {
        if (file.Length is 0 or > 2_097_152) return BadRequest(new { message = "Ảnh tối đa 2 MB" });
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);
        var id = await service.AddImage(UserId, roomId, caption ?? "", file.ContentType, buffer.ToArray(), ct);
        return CreatedAtAction(nameof(Get), new { roomId, id }, new { id });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid roomId, Guid id, CancellationToken ct)
    {
        await service.DeleteImage(UserId, roomId, id, ct);
        return NoContent();
    }
}

[PagePermission(EmployeePages.Tenancies), Route("api/occupants")]
public class OccupantsController(ManagementService service) : ApiBase
{
    [HttpGet]
    public Task<List<OccupantDto>> List(Guid? propertyId, CancellationToken ct) => service.Occupants(UserId, propertyId, ct);

    [HttpPost]
    public async Task<IActionResult> Create(OccupantRequest request, CancellationToken ct)
    {
        var id = await service.AddOccupant(UserId, request.RoomId, Profile(request), ct);
        return Ok(new { id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, OccupantRequest request, CancellationToken ct)
    {
        await service.UpdateOccupant(UserId, id, request.RoomId, Profile(request), ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteOccupant(UserId, id, ct);
        return NoContent();
    }

    private static OccupantProfile Profile(OccupantRequest request) => new(request.Name, request.Phone, request.IdentityNumber,
        request.Gender, request.Address, request.Notes, request.TemporaryResidenceRegistered);
}