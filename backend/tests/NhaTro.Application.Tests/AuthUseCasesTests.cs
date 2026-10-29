using NhaTro.Application.Abstractions;
using NhaTro.Application.UseCases;
using NhaTro.Domain.Entities;

namespace NhaTro.Application.Tests;

public class AuthUseCasesTests
{
    private sealed class FakeUsers : IUserRepository
    {
        public List<User> Items { get; } = [];
        public Task<bool> PhoneExistsAsync(string phone, CancellationToken ct) => Task.FromResult(Items.Any(user => user.Phone == phone));
        public Task<User?> GetByPhoneAsync(string phone, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(user => user.Phone == phone));
        public void Add(User user) => Items.Add(user);
        public void Remove(User user) => Items.Remove(user);
    }

    private sealed class FakeHasher : IPasswordHasher
    {
        public string Hash(string password) => $"hash:{password}";
        public bool Verify(string hash, string password) => hash == Hash(password);
    }

    private sealed class FakeTokens : ITokenService
    {
        public string Create(User user) => user.Id.ToString();
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);
    }

    [Fact]
    public async Task Registers_with_normalized_phone_number()
    {
        var users = new FakeUsers();
        var handler = new RegisterHandler(users, new FakeHasher(), new FakeTokens(), new FakeUnitOfWork());

        await handler.Handle(new RegisterCommand("Nguyen Van A", "+84 912-345-678", "password123"), CancellationToken.None);

        Assert.Equal("+84912345678", Assert.Single(users.Items).Phone);
    }

    [Fact]
    public async Task Rejects_duplicate_phone_even_when_formatting_differs()
    {
        var users = new FakeUsers();
        users.Add(User.RegisterWithPhone("+84912345678", "Nguyen Van A", "hash"));
        var handler = new RegisterHandler(users, new FakeHasher(), new FakeTokens(), new FakeUnitOfWork());

        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(
            new RegisterCommand("Nguyen Van B", "+84 912-345-678", "password123"), CancellationToken.None));
    }

    [Fact]
    public async Task Logs_in_with_phone_for_owner_and_employee_accounts()
    {
        var users = new FakeUsers();
        users.Add(User.RegisterWithPhone("+84912345678", "Phone User", "hash:password123"));
        users.Add(User.CreateEmployee(Guid.NewGuid(), "09123456780", "Employee", "hash:password123"));
        var handler = new LoginHandler(users, new FakeHasher(), new FakeTokens());

        var phoneResult = await handler.Handle(new LoginCommand("+84 912-345-678", "password123"), CancellationToken.None);
        var employeeResult = await handler.Handle(new LoginCommand("09123456780", "password123"), CancellationToken.None);

        Assert.Equal("Phone User", phoneResult.FullName);
        Assert.Equal("Employee", employeeResult.FullName);
        Assert.True(employeeResult.IsEmployee);
    }
}