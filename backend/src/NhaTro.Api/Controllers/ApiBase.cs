using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NhaTro.Api.Controllers;

[ApiController, Authorize]
public abstract class ApiBase : ControllerBase
{
    protected Guid UserId => Guid.Parse(User.FindFirstValue("sub")!);
}
