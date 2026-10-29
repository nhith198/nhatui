using System.Reflection;
using NetArchTest.Rules;
using NhaTro.Application.Abstractions;
using NhaTro.Domain.Entities;
using NhaTro.Infrastructure;

namespace NhaTro.ArchitectureTests;

/// <summary>Clean Architecture dependency rule: source code dependencies point inwards only.</summary>
public class DependencyRuleTests
{
    private static readonly Assembly Domain = typeof(Room).Assembly;
    private static readonly Assembly Application = typeof(IUnitOfWork).Assembly;
    private static readonly Assembly Infrastructure = typeof(NhaTro.Infrastructure.DependencyInjection).Assembly;

    private static void AssertNoDependency(Assembly layer, params string[] forbidden)
    {
        var result = Types.InAssembly(layer).ShouldNot().HaveDependencyOnAny(forbidden).GetResult();
        Assert.True(result.IsSuccessful, "Forbidden dependency in: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Domain_depends_on_nothing_outside_itself() =>
        AssertNoDependency(Domain, "NhaTro.Application", "NhaTro.Infrastructure", "NhaTro.Api", "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore");

    [Fact]
    public void Application_depends_only_on_Domain() =>
        AssertNoDependency(Application, "NhaTro.Infrastructure", "NhaTro.Api", "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore");

    [Fact]
    public void Infrastructure_does_not_depend_on_Api() =>
        AssertNoDependency(Infrastructure, "NhaTro.Api");

    [Fact]
    public void Domain_entities_do_not_expose_public_setters()
    {
        var offenders = Domain.GetTypes()
            .Where(t => t.IsClass && t.Namespace == "NhaTro.Domain.Entities")
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(p => p.SetMethod is { IsPublic: true })
            .Select(p => $"{p.DeclaringType!.Name}.{p.Name}").ToList();
        Assert.Empty(offenders);
    }
}
