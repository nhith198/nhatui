namespace NhaTro.Application.Abstractions;

public static class EmployeePages
{
    public const string Dashboard = "dashboard";
    public const string Rooms = "rooms";
    public const string Tenancies = "tenancies";
    public const string Invoices = "invoices";
    public const string Complaints = "complaints";
    public const string Financials = "financials";
    public const string Properties = "properties";
    public const string Repairs = "repairs";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Dashboard, Rooms, Tenancies, Invoices, Complaints, Financials, Properties, Repairs
    };
}