using NhaTro.Application.Abstractions;
using NhaTro.Domain.Common;

namespace NhaTro.Api;

/// <summary>Translates domain and application errors into HTTP responses ({ "message": ... }).</summary>
public class ErrorHandlingMiddleware(RequestDelegate next)
{
    public async Task Invoke(HttpContext ctx)
    {
        try { await next(ctx); }
        catch (Exception e) when (e is AppException or DomainException)
        {
            ctx.Response.StatusCode = e switch
            {
                NotFoundException => StatusCodes.Status404NotFound,
                UnauthorizedException => StatusCodes.Status401Unauthorized,
                ForbiddenException => StatusCodes.Status403Forbidden,
                ConflictException => StatusCodes.Status409Conflict,
                DomainException { Kind: DomainErrorKind.Conflict } => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status400BadRequest
            };
            await ctx.Response.WriteAsJsonAsync(new { message = e.Message });
        }
    }
}
