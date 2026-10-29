using NhaTro.Domain.Common;

namespace NhaTro.Domain.Entities;

public class EmployeePagePermission : Entity
{
    private EmployeePagePermission() { }

    public Guid UserId { get; private set; }
    public string Page { get; private set; } = "";

    public static EmployeePagePermission Create(Guid userId, string page) => new() { UserId = userId, Page = page };
}