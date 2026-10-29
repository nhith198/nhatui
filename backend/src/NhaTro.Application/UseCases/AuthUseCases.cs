using System.Security.Cryptography;
using NhaTro.Application.Abstractions;
using NhaTro.Application.Dtos;
using NhaTro.Domain.Common;
using NhaTro.Domain.Entities;

namespace NhaTro.Application.UseCases;

public record RegisterCommand(string FullName, string Phone, string Password);
public record LoginCommand(string Phone, string Password);
public record ForgotPasswordCommand(string Phone);
public record ResetPasswordCommand(string Phone, string Code, string NewPassword);

public class RegisterHandler(IUserRepository users, IPasswordHasher hasher, ITokenService tokens, IUnitOfWork uow) : ICommandHandler<RegisterCommand, AuthResult>
{
    public async Task<AuthResult> Handle(RegisterCommand c, CancellationToken ct)
    {
        var phone = User.NormalizePhone(c.Phone);
        if (await users.PhoneExistsAsync(phone, ct)) throw new ConflictException("Số điện thoại đã được đăng ký");
        var user = User.RegisterWithPhone(phone, c.FullName, hasher.Hash(c.Password));
        users.Add(user);
        await uow.SaveChangesAsync(ct);
        return new AuthResult(tokens.Create(user), user.FullName);
    }
}

public class LoginHandler(IUserRepository users, IPasswordHasher hasher, ITokenService tokens) : ICommandHandler<LoginCommand, AuthResult>
{
    public async Task<AuthResult> Handle(LoginCommand c, CancellationToken ct)
    {
        var user = await users.GetByPhoneAsync(User.NormalizePhone(c.Phone), ct);
        if (user is null || user.IsDisabled || !hasher.Verify(user.PasswordHash, c.Password)) throw new UnauthorizedException("Số điện thoại hoặc mật khẩu không đúng");
        return new AuthResult(tokens.Create(user), user.FullName, user.EmployerId is not null);
    }
}

public class ForgotPasswordHandler(IUserRepository users, IPasswordHasher hasher, IPasswordResetStore store, IResetCodeSender sender, TimeProvider clock)
    : ICommandHandler<ForgotPasswordCommand>
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);

    public async Task Handle(ForgotPasswordCommand c, CancellationToken ct)
    {
        var phone = User.NormalizePhone(c.Phone);
        var user = await users.GetByPhoneAsync(phone, ct);
        // Same response whether or not the phone is registered, to prevent account enumeration.
        if (user is null || user.IsDisabled) return;
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        store.Save(phone, new PasswordResetEntry { CodeHash = hasher.Hash(code), ExpiresAt = clock.GetUtcNow().Add(CodeLifetime) });
        await sender.SendAsync(phone, code, ct);
    }
}

public class ResetPasswordHandler(IUserRepository users, IPasswordHasher hasher, IPasswordResetStore store, IUnitOfWork uow, TimeProvider clock)
    : ICommandHandler<ResetPasswordCommand>
{
    private const int MaxAttempts = 5;
    private const string InvalidCode = "Mã xác nhận không hợp lệ hoặc đã hết hạn";

    public async Task Handle(ResetPasswordCommand c, CancellationToken ct)
    {
        if (c.NewPassword.Length is < 8 or > 128) throw new DomainException("Mật khẩu mới phải từ 8 đến 128 ký tự");
        var phone = User.NormalizePhone(c.Phone);
        var entry = store.Get(phone);
        if (entry is null || entry.ExpiresAt < clock.GetUtcNow() || entry.FailedAttempts >= MaxAttempts) throw new DomainException(InvalidCode);
        if (!hasher.Verify(entry.CodeHash, c.Code))
        {
            entry.FailedAttempts++;
            throw new DomainException("Mã xác nhận không đúng");
        }
        var user = await users.GetByPhoneAsync(phone, ct);
        if (user is null || user.IsDisabled) throw new DomainException(InvalidCode);
        user.ChangePassword(hasher.Hash(c.NewPassword));
        store.Remove(phone);
        await uow.SaveChangesAsync(ct);
    }
}
