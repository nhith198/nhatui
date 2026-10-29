namespace NhaTro.Application.Dtos;

public record AuthResult(string Token, string FullName, bool IsEmployee = false);

public record PropertyDto(Guid Id, string Name, string Address, decimal ElectricRate, decimal WaterRate, int RoomCount, int OccupiedCount, bool CanManage = true,
    decimal WaterRatePerPerson = 0, string BankName = "", string BankAccountNumber = "", string BankAccountName = "",
    decimal ServiceAmountPerMonth = 0, bool HasQrCode = false);
public sealed record PropertyQrDto(byte[] Data, string ContentType);

public record TenantDto(Guid TenancyId, string Name, string Phone, DateOnly StartDate, decimal Deposit);
public record RoomDto(Guid Id, Guid PropertyId, string PropertyName, string Name, decimal Price, int LastElectric, int LastWater, TenantDto? Tenant, bool IsUnderMaintenance = false, int ImageCount = 0,
    int OccupantCount = 1, bool WaterByOccupant = false, decimal ServiceAmount = 0, decimal WaterRatePerPerson = 0, decimal? ElectricRate = null, string? MaintenanceNote = null,
    string? ReservedTenantName = null, string? ReservedPhone = null, decimal? ReservedDeposit = null, DateOnly? ReservedDate = null, DateOnly? ReservedUntil = null);

public sealed record TenancyDto(Guid Id, Guid RoomId, Guid PropertyId, string PropertyName, string RoomName,
    string Name, string Phone, string IdentityNumber, string Address, string Notes,
    DateOnly StartDate, DateOnly? ExpectedEndDate, DateOnly? EndDate, decimal Deposit, string Status, string Gender, bool TemporaryResidenceRegistered = false);
public sealed record RoomImageDto(Guid Id, string Caption, string ContentType, string Base64);
public sealed record OccupantDto(Guid Id, Guid RoomId, Guid PropertyId, string PropertyName, string RoomName,
    string Name, string Phone, string IdentityNumber, string Gender, string Address, string Notes, bool TemporaryResidenceRegistered = false);
public sealed record EmployeeDto(Guid Id, string FullName, string Phone, bool IsDisabled, List<Guid> PropertyIds, List<string> PagePermissions);
public sealed record AccountDto(Guid Id, string FullName, string? Phone, bool IsEmployee, List<string> PagePermissions);
public sealed record AssigneeDto(Guid Id, string FullName);
public sealed record ComplaintDto(Guid Id, Guid RoomId, Guid PropertyId, string PropertyName, string RoomName,
    string TenantName, string Title, string Description, string Priority, string Status, Guid? AssignedTo,
    string? AssignedName, string Resolution, DateTimeOffset CreatedAt, DateTimeOffset? ResolvedAt);

public sealed record RepairRequestDto(Guid RoomId, Guid PropertyId, string PropertyName, string RoomName, string? TenantName, string Description, string Status);

public record InvoiceDto(Guid Id, Guid RoomId, string RoomName, string PropertyName, string TenantName, string Month,
    string Gender,
    int ElectricOld, int ElectricNew, int WaterOld, int WaterNew,
    decimal Rent, decimal ElectricAmount, decimal WaterAmount, decimal Other, decimal Total, bool IsPaid,
    decimal ServiceAmount = 0, bool WaterByOccupant = false, int OccupantCount = 1, decimal WaterRatePerPerson = 0, decimal ElectricRate = 0, string? OtherNote = null,
    Guid PropertyId = default, bool PropertyHasQr = false);

public record MissingInvoiceDto(Guid RoomId, string RoomName, string PropertyName, string TenantName);
public record DashboardDto(int TotalRooms, int OccupiedRooms, decimal Invoiced, decimal Collected, decimal Outstanding, List<MissingInvoiceDto> Missing);

public sealed record FinancialExpenseDto(Guid Id, Guid? PropertyId, string? PropertyName, string Category,
    string Description, string? Recipient, decimal Amount, DateOnly Date, bool IsMonthly, bool IsPaid, bool IsGenerated);
public sealed record MonthlyProfitDto(string Month, decimal Collected, decimal Expenses, decimal Profit);
public sealed record DepositReceiptDto(Guid Id, Guid RoomId, string RoomName, string PropertyName, string TenantName, string? Phone,
    decimal Amount, DateOnly HoldUntil, DateTime ReceivedAt, DateTime? RefundedAt, decimal? RefundedAmount);
public sealed record FinancialReportDto(string Month, decimal Collected, decimal Expenses, decimal Profit,
    List<FinancialExpenseDto> ExpenseEntries, List<MonthlyProfitDto> History, List<DepositReceiptDto> DepositReceipts,
    List<FinancialExpenseDto> MonthlyExpenses);
