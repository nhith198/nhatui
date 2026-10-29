using System.ComponentModel.DataAnnotations;

namespace NhaTro.Api.Contracts;

// HTTP request models: input validation lives here; business rules live in Domain/Application.
public record RegisterRequest([Required, StringLength(200)] string FullName,
	[Required, StringLength(30), RegularExpression(@"^\+?[0-9][0-9\s().-]{7,14}$")] string Phone,
	[Required, MinLength(8)] string Password);
public record LoginRequest([Required, StringLength(30), RegularExpression(@"^\+?[0-9][0-9\s().-]{7,14}$")] string Phone,
	[Required] string Password);
public sealed record ForgotPasswordRequest([Required, StringLength(30), RegularExpression(@"^\+?[0-9][0-9\s().-]{7,14}$")] string Phone);
public sealed record ResetPasswordRequest([Required, StringLength(30), RegularExpression(@"^\+?[0-9][0-9\s().-]{7,14}$")] string Phone,
	[Required, RegularExpression(@"^\d{6}$")] string Code, [Required, StringLength(128, MinimumLength = 8)] string NewPassword);
public sealed record UpdateAccountRequest([Required, StringLength(200)] string FullName,
	[Required, StringLength(30), RegularExpression(@"^\+?[0-9][0-9\s().-]{7,14}$")] string Phone,
	[StringLength(128)] string? CurrentPassword, [StringLength(128)] string? NewPassword);

public record PropertyRequest([Required, StringLength(200)] string Name, [StringLength(500)] string? Address,
	[Range(0, 1_000_000)] decimal ElectricRate, [Range(0, 1_000_000)] decimal WaterRate,
	[Range(0, 1_000_000)] decimal WaterRatePerPerson = 0, [StringLength(100)] string? BankName = null,
	[StringLength(50)] string? BankAccountNumber = null, [StringLength(200)] string? BankAccountName = null,
	[Range(0, 1_000_000_000)] decimal ServiceAmountPerMonth = 0);

public record CreateRoomRequest(Guid PropertyId, [Required, StringLength(100)] string Name, [Range(0, 1_000_000_000)] decimal Price,
	[Range(0, int.MaxValue)] int LastElectric, [Range(0, int.MaxValue)] int LastWater,
	[Range(1, 100)] int OccupantCount = 1, bool WaterByOccupant = false, [Range(0, 1_000_000_000)] decimal? ServiceAmount = null, [Range(0, 1_000_000)] decimal? ElectricRate = null);
public record UpdateRoomRequest([Required, StringLength(100)] string Name, [Range(0, 1_000_000_000)] decimal Price,
	[Range(0, int.MaxValue)] int LastElectric, [Range(0, int.MaxValue)] int LastWater,
	[Range(1, 100)] int OccupantCount, bool WaterByOccupant, [Range(0, 1_000_000_000)] decimal ServiceAmount, [Range(0, 1_000_000)] decimal? ElectricRate = null);
public record RentRoomRequest([Required, StringLength(200)] string TenantName, [StringLength(30)] string? Phone, DateOnly StartDate, [Range(0, 1_000_000_000)] decimal Deposit, DateOnly? ExpectedEndDate = null);
public record ReserveRoomRequest([Required, StringLength(200)] string TenantName, [StringLength(30)] string? Phone, [Range(0, 1_000_000_000)] decimal Deposit, DateOnly HoldUntil);
public record CancelReservationRequest([Range(0, 1_000_000_000)] decimal RefundAmount);

public record CreateInvoiceRequest(Guid RoomId, [Required, RegularExpression(@"^\d{4}-(0[1-9]|1[0-2])$")] string Month,
	[Range(0, int.MaxValue)] int ElectricNew, [Range(0, int.MaxValue)] int? WaterNew, [Range(-1_000_000_000, 1_000_000_000)] decimal Other, [StringLength(200)] string? OtherNote = null);
public sealed record FinancialExpenseRequest(Guid? PropertyId,
	[Required, RegularExpression("^(Operating|Sales|Other)$")] string Category,
	[Required, StringLength(200)] string Description, [Required, StringLength(200)] string Recipient,
	[Range(typeof(decimal), "0.01", "1000000000")] decimal Amount, DateOnly Date, bool IsMonthly = false, bool IsPaid = true);

public sealed record TenantProfileRequest([Required, StringLength(200)] string Name, [StringLength(30)] string? Phone,
	[StringLength(30)] string? IdentityNumber,
	[StringLength(500)] string? Address, [StringLength(2000)] string? Notes, DateOnly? ExpectedEndDate,
	[RegularExpression("^(Nam|Nữ)$")] string? Gender = null, bool TemporaryResidenceRegistered = false);
public sealed record MaintenanceRequest(bool Enabled, string? DamagedEquipment = null);
public sealed record EmployeeRequest([Required, StringLength(200)] string FullName,
	[Required, StringLength(30), RegularExpression(@"^\+?[0-9][0-9\s().-]{7,14}$")] string Phone,
	[StringLength(128, MinimumLength = 8)] string? Password, bool IsDisabled, [Required] List<Guid> PropertyIds,
	[Required] List<string> PagePermissions);
public sealed record CreateComplaintRequest(Guid RoomId, [Required, StringLength(200)] string TenantName,
	[Required, StringLength(200)] string Title, [Required, StringLength(4000)] string Description,
	[Required, RegularExpression("^(Low|Normal|Urgent)$")] string Priority);
public sealed record UpdateRepairStatusRequest([Required, RegularExpression("^(New|InProgress|Fixed|Closed)$")] string Status);
public sealed record UpdateComplaintRequest([Required, RegularExpression("^(Open|InProgress|Resolved|Closed)$")] string Status,
	Guid? AssignedTo, [Required(AllowEmptyStrings = true), StringLength(2000)] string Resolution);
public sealed record OccupantRequest(Guid RoomId, [Required, StringLength(200)] string Name, [StringLength(30)] string? Phone,
	[StringLength(30)] string? IdentityNumber, [RegularExpression("^(Nam|Nữ)$")] string? Gender,
	[StringLength(500)] string? Address, [StringLength(2000)] string? Notes, bool TemporaryResidenceRegistered = false);
