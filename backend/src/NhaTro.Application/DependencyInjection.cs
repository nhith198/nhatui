using Microsoft.Extensions.DependencyInjection;
using NhaTro.Application.Abstractions;

namespace NhaTro.Application;

public static class DependencyInjection
{
    private static readonly Type[] HandlerContracts = [typeof(ICommandHandler<>), typeof(ICommandHandler<,>), typeof(IQueryHandler<,>)];

    /// <summary>Registers every command/query handler found in this assembly.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<UseCases.ManagementService>();
        services.AddScoped<UseCases.FinancialService>();
        foreach (var impl in typeof(DependencyInjection).Assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false }))
            foreach (var contract in impl.GetInterfaces().Where(i => i.IsGenericType && HandlerContracts.Contains(i.GetGenericTypeDefinition())))
                services.AddScoped(contract, impl);
        return services;
    }
}
