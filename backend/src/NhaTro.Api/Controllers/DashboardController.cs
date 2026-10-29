using Microsoft.AspNetCore.Mvc;
using NhaTro.Application.Abstractions;
using NhaTro.Application.Dtos;
using NhaTro.Application.UseCases;

namespace NhaTro.Api.Controllers;

[PagePermission(EmployeePages.Dashboard), Route("api/dashboard")]
public class DashboardController(IQueryHandler<GetDashboardQuery, DashboardDto> get) : ApiBase
{
    [HttpGet] public Task<DashboardDto> Get(string month, Guid? propertyId, CancellationToken ct) => get.Handle(new GetDashboardQuery(UserId, month, propertyId), ct);
}
