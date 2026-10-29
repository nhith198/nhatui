using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NhaTro.Application.Abstractions;

namespace NhaTro.Api.Controllers;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class PagePermissionAttribute(string page, bool allowAnyGrantedPage = false) : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (!Guid.TryParse(context.HttpContext.User.FindFirstValue("sub"), out var userId))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var repository = context.HttpContext.RequestServices.GetRequiredService<IManagementRepository>();
        var user = await repository.GetUserAsync(userId, context.HttpContext.RequestAborted);
        if (user is null || user.IsDisabled)
        {
            context.Result = new UnauthorizedResult();
            return;
        }
        if (user.EmployerId is null) return;

        var pages = await repository.GetPagePermissionsAsync(userId, context.HttpContext.RequestAborted);
        if (!pages.Contains(page, StringComparer.Ordinal) && !(allowAnyGrantedPage && pages.Count > 0))
            context.Result = new ObjectResult(new { message = "Bạn không được cấp quyền truy cập trang này" }) { StatusCode = StatusCodes.Status403Forbidden };
    }
}