using NhaTro.Application.Abstractions;
using NhaTro.Application.UseCases;
using NhaTro.Domain.Common;
using NhaTro.Domain.Entities;

namespace NhaTro.Application.Tests;

public class CreateInvoiceHandlerTests
{
    // ---- hand-written fakes for the ports; no database, no mocking framework ----
    private sealed class FakePropertyRepo : IPropertyRepository
    {
        public List<Property> Items { get; } = [];
        public Task<Property?> GetOwnedAsync(Guid id, Guid ownerId, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(p => p.Id == id && p.OwnerId == ownerId));
        public void Add(Property property) => Items.Add(property);
        public void Remove(Property property) => Items.Remove(property);
    }

    private sealed class FakeRoomRepo(FakePropertyRepo properties) : IRoomRepository
    {
        public List<Room> Items { get; } = [];
        public Task<Room?> GetOwnedAsync(Guid id, Guid ownerId, CancellationToken ct) =>
            Task.FromResult(Items.FirstOrDefault(r => r.Id == id && properties.Items.Any(p => p.Id == r.PropertyId && p.OwnerId == ownerId)));
        public Task<bool> NameExistsAsync(Guid propertyId, string name, CancellationToken ct) => Task.FromResult(Items.Any(r => r.PropertyId == propertyId && r.Name == name));
        public Task<bool> AnyInPropertyAsync(Guid propertyId, CancellationToken ct) => Task.FromResult(Items.Any(r => r.PropertyId == propertyId));
        public Task<List<Room>> GetExpiredReservationsAsync(DateOnly today, CancellationToken ct) =>
            Task.FromResult(Items.Where(r => r.ReservedUntil is not null && r.ReservedUntil < today).ToList());
        public void Add(Room room) => Items.Add(room);
        public void Remove(Room room) => Items.Remove(room);
    }

    private sealed class FakeInvoiceRepo : IInvoiceRepository
    {
        public List<Invoice> Items { get; } = [];
        public Task<Invoice?> GetOwnedAsync(Guid id, Guid ownerId, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(i => i.Id == id));
        public Task<bool> ExistsAsync(Guid roomId, string month, CancellationToken ct) => Task.FromResult(Items.Any(i => i.RoomId == roomId && i.Month == month));
        public Task<bool> HasLaterInvoiceAsync(Guid roomId, string month, CancellationToken ct) => Task.FromResult(Items.Any(i => i.RoomId == roomId && string.CompareOrdinal(i.Month, month) > 0));
        public void Add(Invoice invoice) => Items.Add(invoice);
        public void Remove(Invoice invoice) => Items.Remove(invoice);
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int Saves { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken ct = default) { Saves++; return Task.FromResult(1); }
    }

    private sealed class Sut
    {
        public Guid OwnerId { get; } = Guid.NewGuid();
        public Room Room { get; }
        public FakeInvoiceRepo Invoices { get; } = new();
        public FakeUnitOfWork Uow { get; } = new();
        public CreateInvoiceHandler Handler { get; }

        public Sut(bool occupied = true)
        {
            var properties = new FakePropertyRepo();
            var rooms = new FakeRoomRepo(properties);
            var property = Property.Create(OwnerId, "Nhà A", "Gò Vấp", 3500, 18000);
            properties.Add(property);
            Room = Room.Create(property.Id, "P101", 3_000_000, 100, 10);
            if (occupied) Room.MoveIn("Nguyễn Văn An", null, new DateOnly(2026, 1, 1), 0);
            rooms.Add(Room);
            Handler = new CreateInvoiceHandler(rooms, properties, Invoices, Uow);
        }

        public CreateInvoiceCommand Command(Guid? userId = null, string month = "2026-10", int electric = 150, int water = 15) =>
            new(userId ?? OwnerId, Room.Id, month, electric, water, 50_000, "Phí giặt");
    }

    [Fact]
    public async Task Issues_invoice_using_property_rates_and_saves_once()
    {
        var sut = new Sut();

        var dto = await sut.Handler.Handle(sut.Command(), CancellationToken.None);

        Assert.Equal(3_315_000m, dto.Total);
        Assert.Equal("Nhà A", dto.PropertyName);
        Assert.Single(sut.Invoices.Items);
        Assert.Equal(1, sut.Uow.Saves);
    }

    [Fact]
    public async Task Rejects_second_invoice_for_the_same_month()
    {
        var sut = new Sut();
        await sut.Handler.Handle(sut.Command(), CancellationToken.None);
        await Assert.ThrowsAsync<ConflictException>(() => sut.Handler.Handle(sut.Command(electric: 200, water: 20), CancellationToken.None));
        Assert.Equal(1, sut.Uow.Saves);
    }

    [Fact]
    public async Task Hides_rooms_that_belong_to_another_landlord()
    {
        var sut = new Sut();
        await Assert.ThrowsAsync<NotFoundException>(() => sut.Handler.Handle(sut.Command(userId: Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Surfaces_domain_rule_violations_without_saving()
    {
        var vacant = new Sut(occupied: false);
        await Assert.ThrowsAsync<DomainException>(() => vacant.Handler.Handle(vacant.Command(), CancellationToken.None));

        var lowReading = new Sut();
        await Assert.ThrowsAsync<DomainException>(() => lowReading.Handler.Handle(lowReading.Command(electric: 90), CancellationToken.None));

        Assert.Equal(0, vacant.Uow.Saves + lowReading.Uow.Saves);
    }
}
