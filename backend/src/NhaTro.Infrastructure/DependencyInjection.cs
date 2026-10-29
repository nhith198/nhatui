using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NhaTro.Application.Abstractions;
using NhaTro.Infrastructure.Persistence;
using NhaTro.Infrastructure.Security;

namespace NhaTro.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration cfg)
    {
        services.AddDbContext<AppDbContext>(o => o.UseSqlServer(cfg.GetConnectionString("Default")));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPropertyRepository, PropertyRepository>();
        services.AddScoped<IRoomRepository, RoomRepository>();
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<IDepositReceiptRepository, DepositReceiptRepository>();
        services.AddScoped<IFinancialExpenseRepository, FinancialExpenseRepository>();

        services.AddScoped<IPropertyQueries, PropertyQueries>();
        services.AddScoped<IRoomQueries, RoomQueries>();
        services.AddScoped<IManagementRepository, ManagementRepository>();
        services.AddScoped<IInvoiceQueries, InvoiceQueries>();
        services.AddScoped<IDashboardQueries, DashboardQueries>();
        services.AddScoped<IFinancialQueries, FinancialQueries>();

        services.AddSingleton<IPasswordHasher, PasswordHasherAdapter>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<IPasswordResetStore, PasswordResetStore>();
        services.AddSingleton<IResetCodeSender, LogResetCodeSender>();
        return services;
    }

    /// <summary>Development helper: apply migrations if the project has any, otherwise create the schema directly.</summary>
    public static void InitializeDatabase(this IServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (db.Database.GetMigrations().Any()) db.Database.Migrate(); else db.Database.EnsureCreated();
    }
}
