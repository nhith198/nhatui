using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using NhaTro.Api;
using NhaTro.Application;
using NhaTro.Application.Abstractions;
using NhaTro.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Composition root: the only place that knows every layer.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddHostedService<ReservationExpiryWorker>();
builder.Services.AddOpenApi();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.MapInboundClaims = false;
    o.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var id = context.Principal?.FindFirst("sub")?.Value;
            if (!Guid.TryParse(id, out var userId)) { context.Fail("Invalid account"); return; }
            var store = context.HttpContext.RequestServices.GetRequiredService<IManagementRepository>();
            var user = await store.GetUserAsync(userId, context.HttpContext.RequestAborted);
            if (user is null || user.IsDisabled) context.Fail("Account disabled");
        }
    };
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
        ClockSkew = TimeSpan.FromMinutes(1)
    };
});
builder.Services.AddAuthorization();

var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.Services.InitializeDatabase();   // production schema is applied by the pipeline (EF migrations bundle)
}

app.UseCors();
app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program { }
