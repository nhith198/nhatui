using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using NhaTro.Application.Abstractions;

namespace NhaTro.Infrastructure.Security;

// In-memory: reset codes are short-lived and are lost on API restart.
internal class PasswordResetStore : IPasswordResetStore
{
    private readonly ConcurrentDictionary<string, PasswordResetEntry> entries = new();

    public void Save(string phone, PasswordResetEntry entry) => entries[phone] = entry;
    public PasswordResetEntry? Get(string phone) => entries.GetValueOrDefault(phone);
    public void Remove(string phone) => entries.TryRemove(phone, out _);
}

// No SMS provider is configured yet; the code is written to the log. Replace with a real sender before production.
internal class LogResetCodeSender(ILogger<LogResetCodeSender> logger) : IResetCodeSender
{
    public Task SendAsync(string phone, string code, CancellationToken ct)
    {
        logger.LogWarning("Mã đặt lại mật khẩu cho {Phone}: {Code}", phone, code);
        return Task.CompletedTask;
    }
}
