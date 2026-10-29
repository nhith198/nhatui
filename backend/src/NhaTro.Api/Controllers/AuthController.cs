using Microsoft.AspNetCore.Mvc;
using NhaTro.Api.Contracts;
using NhaTro.Application.Abstractions;
using NhaTro.Application.Dtos;
using NhaTro.Application.UseCases;

namespace NhaTro.Api.Controllers;

[ApiController, Route("api/auth")]
public class AuthController(
    ICommandHandler<RegisterCommand, AuthResult> register,
    ICommandHandler<LoginCommand, AuthResult> login,
    ICommandHandler<ForgotPasswordCommand> forgotPassword,
    ICommandHandler<ResetPasswordCommand> resetPassword) : ControllerBase
{
    [HttpPost("register")]
    public Task<AuthResult> Register(RegisterRequest r, CancellationToken ct) => register.Handle(new RegisterCommand(r.FullName, r.Phone, r.Password), ct);

    [HttpPost("login")]
    public Task<AuthResult> Login(LoginRequest r, CancellationToken ct) => login.Handle(new LoginCommand(r.Phone, r.Password), ct);

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest r, CancellationToken ct)
    {
        await forgotPassword.Handle(new ForgotPasswordCommand(r.Phone), ct);
        return NoContent();
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest r, CancellationToken ct)
    {
        await resetPassword.Handle(new ResetPasswordCommand(r.Phone, r.Code, r.NewPassword), ct);
        return NoContent();
    }
}
