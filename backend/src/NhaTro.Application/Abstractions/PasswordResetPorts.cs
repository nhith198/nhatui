namespace NhaTro.Application.Abstractions;

public sealed class PasswordResetEntry
{
    public required string CodeHash { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public int FailedAttempts { get; set; }
}

public interface IPasswordResetStore
{
    void Save(string phone, PasswordResetEntry entry);
    PasswordResetEntry? Get(string phone);
    void Remove(string phone);
}

public interface IResetCodeSender
{
    Task SendAsync(string phone, string code, CancellationToken ct);
}
