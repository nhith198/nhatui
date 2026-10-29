using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NhaTro.Application.Abstractions;
using NhaTro.Application.Dtos;
using NhaTro.Domain.Entities;
using NhaTro.Infrastructure.Persistence;

namespace NhaTro.Infrastructure.Tests;

public class ManagementApiTests
{
    private sealed class Factory : WebApplicationFactory<Program>
    {
        private readonly string database = Guid.NewGuid().ToString();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "only-for-isolated-tests-not-a-real-secret-123456789",
                ["Jwt:Issuer"] = "tests", ["Jwt:Audience"] = "tests", ["Jwt:ExpiresHours"] = "1"
            }));
            builder.ConfigureServices(services => services.AddScoped(_ => new AppDbContext(
                new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(database).Options)));
        }

        public (HttpClient Client, User Owner, Room Room) OwnerClient()
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var owner = User.RegisterWithPhone("09123456783", "Owner", "unused");
            typeof(User).GetProperty(nameof(User.CreatedAt))!.SetValue(owner, DateTimeOffset.UtcNow.AddYears(-1));
            var property = Property.Create(owner.Id, "Test property", "", 3500, 18000);
            var room = Room.Create(property.Id, "101", 3000000, 0, 0);
            room.MoveIn("Test tenant", "0901234567", new DateOnly(2026, 1, 1), 3000000);
            db.Users.Add(owner); db.Properties.Add(property); db.Rooms.Add(room); db.SaveChanges();
            var client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", scope.ServiceProvider.GetRequiredService<ITokenService>().Create(owner));
            return (client, owner, room);
        }
    }

    [Fact]
    public async Task Image_multipart_upload_has_valid_location_and_round_trips_binary_data()
    {
        await using var factory = new Factory();
        var (client, _, room) = factory.OwnerClient();
        using (client)
        {
            var data = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aShoAAAAASUVORK5CYII=");
            using var multipart = new MultipartFormDataContent();
            var file = new ByteArrayContent(data);
            file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            multipart.Add(file, "file", "room.png"); multipart.Add(new StringContent("Room photo"), "caption");
            var response = await client.PostAsync($"/api/rooms/{room.Id}/images", multipart);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.NotNull(response.Headers.Location);
            var image = await client.GetFromJsonAsync<RoomImageDto>(response.Headers.Location);
            Assert.Equal(data, Convert.FromBase64String(image!.Base64));
            Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(response.Headers.Location)).StatusCode);
        }
    }

    [Fact]
    public async Task Complaint_endpoints_validate_resolution_and_maintenance_request_keeps_occupied_room_status()
    {
        await using var factory = new Factory();
        var (client, owner, room) = factory.OwnerClient();
        using (client)
        {
            var response = await client.PostAsJsonAsync("/api/complaints", new { RoomId = room.Id, TenantName = "Tenant", Title = "Leak", Description = "Bathroom pipe leak", Priority = "Urgent" });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var complaint = await client.GetFromJsonAsync<ComplaintDto>(response.Headers.Location);
            Assert.Equal("Open", complaint!.Status);
            var invalid = await client.PutAsJsonAsync(response.Headers.Location, new { Status = "Resolved", AssignedTo = owner.Id, Resolution = "" });
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            var resolved = await client.PutAsJsonAsync(response.Headers.Location, new { Status = "Resolved", AssignedTo = owner.Id, Resolution = "Repaired pipe" });
            Assert.Equal(HttpStatusCode.NoContent, resolved.StatusCode);
            var request = await client.PutAsJsonAsync($"/api/rooms/{room.Id}/maintenance", new { Enabled = true, DamagedEquipment = "Air conditioner broken" });
            Assert.Equal(HttpStatusCode.NoContent, request.StatusCode);
            var rooms = await client.GetFromJsonAsync<List<RoomDto>>("/api/rooms");
            var listed = rooms!.Single(r => r.Id == room.Id);
            Assert.False(listed.IsUnderMaintenance);
            Assert.Equal("Air conditioner broken", listed.MaintenanceNote);
        }
    }

    [Fact]
    public async Task Financial_report_excludes_planned_expenses_from_total()
    {
        await using var factory = new Factory();
        var (client, _, room) = factory.OwnerClient();
        using (client)
        {
            var month = DateTime.UtcNow.ToString("yyyy-MM");
            var date = DateOnly.FromDateTime(DateTime.UtcNow);
            var paidResponse = await client.PostAsJsonAsync("/api/financials/expenses", new
            {
                PropertyId = room.PropertyId, Category = "Operating", Description = "Sửa điện", Recipient = "Thợ điện",
                Amount = 200_000m, Date = date, IsPaid = true
            });
            Assert.Equal(HttpStatusCode.Created, paidResponse.StatusCode);
            var plannedResponse = await client.PostAsJsonAsync("/api/financials/expenses", new
            {
                PropertyId = room.PropertyId, Category = "Operating", Description = "Sửa nước", Recipient = "Thợ nước",
                Amount = 300_000m, Date = date, IsPaid = false
            });
            Assert.Equal(HttpStatusCode.Created, plannedResponse.StatusCode);

            var report = await client.GetFromJsonAsync<FinancialReportDto>(
                $"/api/financials?month={month}&propertyId={room.PropertyId}");
            Assert.NotNull(report);
            Assert.Equal(200_000m, report.Expenses);
            Assert.Equal(200_000m, report.History[^1].Expenses);
            Assert.Equal(2, report.ExpenseEntries.Count);
            Assert.Contains(report.ExpenseEntries, entry => entry.Description == "Sửa nước" && !entry.IsPaid);
        }
    }

    [Fact]
    public async Task Marking_planned_expense_as_paid_adds_it_to_total()
    {
        await using var factory = new Factory();
        var (client, _, room) = factory.OwnerClient();
        using (client)
        {
            var month = DateTime.UtcNow.ToString("yyyy-MM");
            var date = DateOnly.FromDateTime(DateTime.UtcNow);
            var created = await client.PostAsJsonAsync("/api/financials/expenses", new
            {
                PropertyId = room.PropertyId, Category = "Operating", Description = "Tiền Chủ Nhà", Recipient = "Chủ nhà",
                Amount = 38_000_000m, Date = date, IsMonthly = false, IsPaid = false
            });
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

            var updated = await client.PutAsJsonAsync($"/api/financials/expenses/{id}", new
            {
                PropertyId = room.PropertyId, Category = "Operating", Description = "Tiền Chủ Nhà", Recipient = "Chủ nhà",
                Amount = 38_000_000m, Date = date, IsMonthly = false, IsPaid = true
            });
            Assert.Equal(HttpStatusCode.NoContent, updated.StatusCode);

            var report = await client.GetFromJsonAsync<FinancialReportDto>(
                $"/api/financials?month={month}&propertyId={room.PropertyId}");
            Assert.NotNull(report);
            Assert.Equal(38_000_000m, report.Expenses);
            Assert.Contains(report.ExpenseEntries, entry => entry.Id == id && entry.IsPaid);
        }
    }

    [Fact]
    public async Task Paid_expense_cannot_be_updated_or_deleted()
    {
        await using var factory = new Factory();
        var (client, _, room) = factory.OwnerClient();
        using (client)
        {
            var month = DateTime.UtcNow.ToString("yyyy-MM");
            var date = DateOnly.FromDateTime(DateTime.UtcNow);
            var created = await client.PostAsJsonAsync("/api/financials/expenses", new
            {
                PropertyId = room.PropertyId, Category = "Operating", Description = "Tiền điện", Recipient = "Thợ điện",
                Amount = 500_000m, Date = date, IsMonthly = false, IsPaid = true
            });
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

            var updated = await client.PutAsJsonAsync($"/api/financials/expenses/{id}", new
            {
                PropertyId = room.PropertyId, Category = "Operating", Description = "Tiền điện", Recipient = "Thợ điện",
                Amount = 600_000m, Date = date, IsMonthly = false, IsPaid = false
            });
            Assert.Equal(HttpStatusCode.Conflict, updated.StatusCode);

            var deleted = await client.DeleteAsync($"/api/financials/expenses/{id}");
            Assert.Equal(HttpStatusCode.Conflict, deleted.StatusCode);

            var report = await client.GetFromJsonAsync<FinancialReportDto>(
                $"/api/financials?month={month}&propertyId={room.PropertyId}");
            Assert.NotNull(report);
            Assert.Equal(500_000m, report.Expenses);
            Assert.Contains(report.ExpenseEntries, entry => entry.Id == id && entry.IsPaid);
        }
    }

    [Fact]
    public async Task Financial_report_uses_paid_invoice_month_and_tracks_sales_costs_for_owner_only()
    {
        await using var factory = new Factory();
        var (client, owner, room) = factory.OwnerClient();
        using (client)
        {
            var month = DateTime.UtcNow.ToString("yyyy-MM");
            var invoiceResponse = await client.PostAsJsonAsync("/api/invoices", new
            {
                RoomId = room.Id, Month = month, ElectricNew = 0, WaterNew = 0, Other = 0
            });
            Assert.Equal(HttpStatusCode.OK, invoiceResponse.StatusCode);
            var invoice = await invoiceResponse.Content.ReadFromJsonAsync<InvoiceDto>();
            Assert.NotNull(invoice);
            Assert.Equal(HttpStatusCode.NoContent,
                (await client.PostAsync($"/api/invoices/{invoice.Id}/toggle-paid", null)).StatusCode);

            var expenseResponse = await client.PostAsJsonAsync("/api/financials/expenses", new
            {
                PropertyId = room.PropertyId, Category = "Sales", Description = "Hoa hồng phòng 101",
                Recipient = "Sale An", Amount = 500_000m, Date = DateOnly.FromDateTime(DateTime.UtcNow)
            });
            Assert.Equal(HttpStatusCode.Created, expenseResponse.StatusCode);

            var report = await client.GetFromJsonAsync<FinancialReportDto>(
                $"/api/financials?month={month}&propertyId={room.PropertyId}");
            Assert.NotNull(report);
            Assert.Equal(invoice.Total, report.Collected);
            Assert.Equal(500_000m, report.Expenses);
            Assert.Equal(invoice.Total - 500_000m, report.Profit);
            Assert.Equal("Sales", Assert.Single(report.ExpenseEntries).Category);
            Assert.Equal(12, report.History.Count);

            User employee;
            string token;
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                employee = User.CreateEmployee(owner.Id, "09123456781", "Employee", "unused");
                db.Users.Add(employee);
                db.SaveChanges();
                token = scope.ServiceProvider.GetRequiredService<ITokenService>().Create(employee);
            }

            using var staffClient = factory.CreateClient();
            staffClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            Assert.Equal(HttpStatusCode.Forbidden, (await staffClient.GetAsync("/api/properties")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await staffClient.GetAsync($"/api/financials?month={month}")).StatusCode);
        }
    }

    [Fact]
    public void Billing_and_page_permission_migration_is_discoverable_and_generates_additive_sql()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=localhost;Database=NhaTro;User Id=sa;Password=Test-only-Password123;TrustServerCertificate=True")
            .Options;
        using var db = new AppDbContext(options);

        Assert.Contains("20261022141957_BillingAndEmployeePagePermissions", db.Database.GetMigrations());
        var sql = db.GetService<IMigrator>().GenerateScript(
            "20261022080000_FinancialExpenses", "20261022141957_BillingAndEmployeePagePermissions");
        Assert.Contains("EmployeePagePermissions", sql);
        Assert.Contains("WaterRatePerPerson", sql);
        Assert.Contains("BankAccountNumber", sql);
        Assert.Contains("OccupantCount", sql);
        Assert.Contains("ServiceAmount", sql);
    }

    [Fact]
    public async Task Per_occupant_water_invoice_uses_property_rate_and_room_service_fee()
    {
        await using var factory = new Factory();
        var (client, _, room) = factory.OwnerClient();
        using (client)
        {
            var propertyResponse = await client.PutAsJsonAsync($"/api/properties/{room.PropertyId}", new
            {
                Name = "Test property", Address = "", ElectricRate = 3500m, WaterRate = 18000m,
                WaterRatePerPerson = 20000m, BankName = "Bank A", BankAccountNumber = "123456", BankAccountName = "Owner"
            });
            Assert.Equal(HttpStatusCode.NoContent, propertyResponse.StatusCode);

            var roomResponse = await client.PutAsJsonAsync($"/api/rooms/{room.Id}", new
            {
                Name = "101", Price = 3_000_000m, LastElectric = 0, LastWater = 0,
                OccupantCount = 3, WaterByOccupant = true, ServiceAmount = 50_000m
            });
            Assert.Equal(HttpStatusCode.NoContent, roomResponse.StatusCode);

            var properties = await client.GetFromJsonAsync<List<PropertyDto>>("/api/properties");
            Assert.NotNull(properties);
            var property = Assert.Single(properties);
            Assert.Equal("123456", property.BankAccountNumber);
            Assert.Equal(20_000m, property.WaterRatePerPerson);
            var rooms = await client.GetFromJsonAsync<List<RoomDto>>("/api/rooms");
            Assert.NotNull(rooms);
            var updatedRoom = Assert.Single(rooms);
            Assert.Equal(3, updatedRoom.OccupantCount);
            Assert.True(updatedRoom.WaterByOccupant);
            Assert.Equal(50_000m, updatedRoom.ServiceAmount);

            var invoiceResponse = await client.PostAsJsonAsync("/api/invoices", new
            {
                RoomId = room.Id, Month = "2026-11", ElectricNew = 0, WaterNew = (int?)null, Other = 0m
            });
            Assert.Equal(HttpStatusCode.OK, invoiceResponse.StatusCode);
            var invoice = await invoiceResponse.Content.ReadFromJsonAsync<InvoiceDto>();
            Assert.Equal(60_000m, invoice!.WaterAmount);
            Assert.Equal(50_000m, invoice.ServiceAmount);
            Assert.True(invoice.WaterByOccupant);
            Assert.Equal(3_110_000m, invoice.Total);
        }
    }

    [Fact]
    public async Task Create_room_defaults_service_fee_from_property_but_keeps_explicit_zero()
    {
        await using var factory = new Factory();
        var (client, _, room) = factory.OwnerClient();
        using (client)
        {
            var propertyUpdate = await client.PutAsJsonAsync($"/api/properties/{room.PropertyId}", new
            {
                Name = "Test property", Address = "", ElectricRate = 3500m, WaterRate = 18000m,
                ServiceAmountPerMonth = 75_000m
            });
            Assert.Equal(HttpStatusCode.NoContent, propertyUpdate.StatusCode);

            var inherited = await client.PostAsJsonAsync("/api/rooms", new
            {
                PropertyId = room.PropertyId, Name = "102", Price = 3_000_000m,
                LastElectric = 0, LastWater = 0, OccupantCount = 1, WaterByOccupant = false
            });
            Assert.Equal(HttpStatusCode.Created, inherited.StatusCode);

            var free = await client.PostAsJsonAsync("/api/rooms", new
            {
                PropertyId = room.PropertyId, Name = "103", Price = 3_000_000m,
                LastElectric = 0, LastWater = 0, OccupantCount = 1, WaterByOccupant = false, ServiceAmount = 0m
            });
            Assert.Equal(HttpStatusCode.Created, free.StatusCode);

            var rooms = await client.GetFromJsonAsync<List<RoomDto>>("/api/rooms");
            Assert.NotNull(rooms);
            Assert.Equal(75_000m, rooms.Single(item => item.Name == "102").ServiceAmount);
            Assert.Equal(0m, rooms.Single(item => item.Name == "103").ServiceAmount);
        }
    }

    [Fact]
    public async Task Account_profile_updates_name_phone_and_requires_current_password_for_password_changes()
    {
        await using var factory = new Factory();
        var (client, owner, _) = factory.OwnerClient();
        using (client)
        {
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
                db.Users.Single(user => user.Id == owner.Id).ChangePassword(hasher.Hash("CurrentPassword123"));
                db.SaveChanges();
            }

            var before = await client.GetFromJsonAsync<AccountDto>("/api/account");
            Assert.Equal("09123456783", before!.Phone);

            var updated = await client.PutAsJsonAsync("/api/account", new
            {
                FullName = "Updated Owner", Phone = "09123456784", CurrentPassword = (string?)null, NewPassword = (string?)null
            });
            Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
            var account = await updated.Content.ReadFromJsonAsync<AccountDto>();
            Assert.Equal("Updated Owner", account!.FullName);
            Assert.Equal("09123456784", account.Phone);

            var rejectedPasswordChange = await client.PutAsJsonAsync("/api/account", new
            {
                FullName = account.FullName, Phone = account.Phone, CurrentPassword = "WrongPassword123", NewPassword = "NewPassword123"
            });
            Assert.Equal(HttpStatusCode.BadRequest, rejectedPasswordChange.StatusCode);

            var passwordChange = await client.PutAsJsonAsync("/api/account", new
            {
                FullName = account.FullName, Phone = account.Phone, CurrentPassword = "CurrentPassword123", NewPassword = "NewPassword123"
            });
            Assert.Equal(HttpStatusCode.OK, passwordChange.StatusCode);

            using var loginClient = factory.CreateClient();
            var login = await loginClient.PostAsJsonAsync("/api/auth/login", new
            {
                Phone = "09123456784", Password = "NewPassword123"
            });
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        }
    }

    [Fact]
    public async Task Staff_creation_login_forbidden_actions_and_disabling_are_enforced_over_http()
    {
        await using var factory = new Factory();
        var (client, _, room) = factory.OwnerClient();
        using (client)
        {
            var response = await client.PostAsJsonAsync("/api/employees", new { FullName = "Employee", Phone = "09123456780", Password = "Test-only-Password123", IsDisabled = false, PropertyIds = new[] { room.PropertyId }, PagePermissions = new[] { "rooms" } });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var employee = await client.GetFromJsonAsync<EmployeeDto>(response.Headers.Location);
            using var staffClient = factory.CreateClient();
            var login = await staffClient.PostAsJsonAsync("/api/auth/login", new { Phone = "09123456780", Password = "Test-only-Password123" });
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            var auth = await login.Content.ReadFromJsonAsync<AuthResult>();
            Assert.True(auth!.IsEmployee);
            staffClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
            Assert.Single((await staffClient.GetFromJsonAsync<List<RoomDto>>("/api/rooms"))!);
            Assert.Equal(HttpStatusCode.Forbidden, (await staffClient.GetAsync("/api/invoices?month=2026-10")).StatusCode);
            var assignedProperties = await staffClient.GetFromJsonAsync<List<PropertyDto>>("/api/properties");
            var assignedProperty = Assert.Single(assignedProperties!);
            Assert.Empty(assignedProperty.BankAccountNumber);
            Assert.Equal(HttpStatusCode.Forbidden, (await staffClient.PutAsJsonAsync($"/api/properties/{assignedProperty.Id}", new
            {
                assignedProperty.Name, assignedProperty.Address, assignedProperty.ElectricRate, assignedProperty.WaterRate,
                assignedProperty.WaterRatePerPerson, assignedProperty.BankName, assignedProperty.BankAccountNumber, assignedProperty.BankAccountName
            })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await staffClient.GetAsync("/api/employees")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await staffClient.GetAsync("/api/account")).StatusCode);
            var forbiddenPermissionEdit = await staffClient.PutAsJsonAsync($"/api/employees/{employee!.Id}", new
            {
                employee.FullName, employee.Phone, Password = (string?)null, IsDisabled = false, employee.PropertyIds,
                PagePermissions = new[] { "invoices" }
            });
            Assert.Equal(HttpStatusCode.Forbidden, forbiddenPermissionEdit.StatusCode);

            var updatedEmployee = await client.PutAsJsonAsync($"/api/employees/{employee.Id}", new
            {
                employee.FullName, employee.Phone, Password = (string?)null, IsDisabled = false, employee.PropertyIds,
                PagePermissions = new[] { "invoices" }
            });
            Assert.Equal(HttpStatusCode.NoContent, updatedEmployee.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await staffClient.GetAsync("/api/rooms")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await staffClient.GetAsync("/api/invoices?month=2026-10")).StatusCode);

            var disable = await client.PutAsJsonAsync($"/api/employees/{employee.Id}", new { employee.FullName, employee.Phone, Password = (string?)null, IsDisabled = true, employee.PropertyIds, PagePermissions = new[] { "invoices" } });
            Assert.Equal(HttpStatusCode.NoContent, disable.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await staffClient.GetAsync("/api/rooms")).StatusCode);
        }
    }
}