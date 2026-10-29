using NhaTro.Domain.Common;

namespace NhaTro.Domain.Entities;

public class User : Entity, IAggregateRoot
{
    private User() { }

    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public string FullName { get; private set; } = "";
    public string PasswordHash { get; private set; } = "";
    public Guid? EmployerId { get; private set; }
    public bool IsDisabled { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public static User CreateEmployee(Guid ownerId, string phone, string fullName, string passwordHash)
    {
        var user = RegisterWithPhone(phone, fullName, passwordHash);
        user.EmployerId = ownerId;
        return user;
    }

    public void UpdateEmployee(string fullName, string phone, bool disabled)
    {
        if (EmployerId is null) throw new DomainException("Tài khoản không phải nhân viên");
        if (string.IsNullOrWhiteSpace(fullName) || fullName.Trim().Length > 200)
            throw new DomainException("Tên nhân viên không hợp lệ");
        FullName = fullName.Trim();
        Phone = NormalizePhone(phone);
        IsDisabled = disabled;
    }

    public void UpdateProfile(string fullName, string phone)
    {
        if (string.IsNullOrWhiteSpace(fullName) || fullName.Trim().Length > 200)
            throw new DomainException("Họ tên phải từ 1 đến 200 ký tự");
        var normalizedPhone = NormalizePhone(phone);
        FullName = fullName.Trim();
        Phone = normalizedPhone;
    }

    public void ChangePassword(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash)) throw new DomainException("Mật khẩu không hợp lệ");
        PasswordHash = passwordHash;
    }

    public void ResetEmployeePassword(string passwordHash)
    {
        if (EmployerId is null) throw new DomainException("Tài khoản không phải nhân viên");
        PasswordHash = passwordHash;
    }

    public static User RegisterWithPhone(string phone, string fullName, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(fullName)) throw new DomainException("Họ tên không được để trống");
        return new User { Phone = NormalizePhone(phone), FullName = fullName.Trim(), PasswordHash = passwordHash };
    }

    public static string NormalizePhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) throw new DomainException("Số điện thoại không được để trống");
        var value = phone.Trim();
        var hasPlus = value.StartsWith('+');
        if (value.Skip(hasPlus ? 1 : 0).Any(c => c == '+' || !(char.IsAsciiDigit(c) || c is ' ' or '(' or ')' or '.' or '-')))
            throw new DomainException("Số điện thoại không hợp lệ");
        var digits = new string(value.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length is < 8 or > 15) throw new DomainException("Số điện thoại không hợp lệ");
        return hasPlus ? $"+{digits}" : digits;
    }
}
