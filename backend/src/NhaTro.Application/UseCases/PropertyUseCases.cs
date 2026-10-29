using NhaTro.Application.Abstractions;
using NhaTro.Application.Dtos;
using NhaTro.Domain.Entities;

namespace NhaTro.Application.UseCases;

public record CreatePropertyCommand(Guid UserId, string Name, string? Address, decimal ElectricRate, decimal WaterRate,
    decimal WaterRatePerPerson, string? BankName, string? BankAccountNumber, string? BankAccountName, decimal ServiceAmountPerMonth);
public record UpdatePropertyCommand(Guid UserId, Guid PropertyId, string Name, string? Address, decimal ElectricRate, decimal WaterRate,
    decimal WaterRatePerPerson, string? BankName, string? BankAccountNumber, string? BankAccountName, decimal ServiceAmountPerMonth);
public record DeletePropertyCommand(Guid UserId, Guid PropertyId);
public record ListPropertiesQuery(Guid UserId);
public record UploadPropertyQrCommand(Guid UserId, Guid PropertyId, byte[] Data, string ContentType);
public record DeletePropertyQrCommand(Guid UserId, Guid PropertyId);
public record GetPropertyQrQuery(Guid UserId, Guid PropertyId);

public class CreatePropertyHandler(IPropertyRepository properties, IManagementRepository management, IUnitOfWork uow) : ICommandHandler<CreatePropertyCommand, PropertyDto>
{
    public async Task<PropertyDto> Handle(CreatePropertyCommand c, CancellationToken ct)
    {
        var user = await management.GetUserAsync(c.UserId, ct) ?? throw new UnauthorizedException("Tài khoản không tồn tại");
        if (user.EmployerId is not null) throw new ForbiddenException("Chỉ chủ nhà được thêm nhà");
        var p = Property.Create(c.UserId, c.Name, c.Address, c.ElectricRate, c.WaterRate,
            c.WaterRatePerPerson, c.BankName, c.BankAccountNumber, c.BankAccountName, c.ServiceAmountPerMonth);
        properties.Add(p);
        await uow.SaveChangesAsync(ct);
        return new PropertyDto(p.Id, p.Name, p.Address, p.ElectricRate, p.WaterRate, 0, 0,
            WaterRatePerPerson: p.WaterRatePerPerson, BankName: p.BankName,
            BankAccountNumber: p.BankAccountNumber, BankAccountName: p.BankAccountName,
            ServiceAmountPerMonth: p.ServiceAmountPerMonth);
    }
}

public class UpdatePropertyHandler(IPropertyRepository properties, IUnitOfWork uow) : ICommandHandler<UpdatePropertyCommand>
{
    public async Task Handle(UpdatePropertyCommand c, CancellationToken ct)
    {
        var p = await properties.GetOwnedAsync(c.PropertyId, c.UserId, ct) ?? throw new NotFoundException();
        p.Update(c.Name, c.Address, c.ElectricRate, c.WaterRate,
            c.WaterRatePerPerson, c.BankName, c.BankAccountNumber, c.BankAccountName, c.ServiceAmountPerMonth);
        await uow.SaveChangesAsync(ct);
    }
}

public class DeletePropertyHandler(IPropertyRepository properties, IRoomRepository rooms, IUnitOfWork uow) : ICommandHandler<DeletePropertyCommand>
{
    public async Task Handle(DeletePropertyCommand c, CancellationToken ct)
    {
        var p = await properties.GetOwnedAsync(c.PropertyId, c.UserId, ct) ?? throw new NotFoundException();
        if (await rooms.AnyInPropertyAsync(p.Id, ct)) throw new ConflictException("Hãy xóa các phòng của nhà này trước");
        properties.Remove(p);
        await uow.SaveChangesAsync(ct);
    }
}

public class UploadPropertyQrHandler(IPropertyRepository properties, IUnitOfWork uow) : ICommandHandler<UploadPropertyQrCommand>
{
    public async Task Handle(UploadPropertyQrCommand c, CancellationToken ct)
    {
        var p = await properties.GetOwnedAsync(c.PropertyId, c.UserId, ct) ?? throw new NotFoundException();
        p.SetQrCode(c.Data, c.ContentType);
        await uow.SaveChangesAsync(ct);
    }
}

public class DeletePropertyQrHandler(IPropertyRepository properties, IUnitOfWork uow) : ICommandHandler<DeletePropertyQrCommand>
{
    public async Task Handle(DeletePropertyQrCommand c, CancellationToken ct)
    {
        var p = await properties.GetOwnedAsync(c.PropertyId, c.UserId, ct) ?? throw new NotFoundException();
        p.RemoveQrCode();
        await uow.SaveChangesAsync(ct);
    }
}

public class GetPropertyQrHandler(IPropertyRepository properties) : IQueryHandler<GetPropertyQrQuery, PropertyQrDto?>
{
    public async Task<PropertyQrDto?> Handle(GetPropertyQrQuery q, CancellationToken ct)
    {
        var p = await properties.GetOwnedAsync(q.PropertyId, q.UserId, ct) ?? throw new NotFoundException();
        return p.QrCode is null ? null : new PropertyQrDto(p.QrCode, p.QrContentType);
    }
}

public class ListPropertiesHandler(IPropertyQueries queries) : IQueryHandler<ListPropertiesQuery, List<PropertyDto>>
{
    public Task<List<PropertyDto>> Handle(ListPropertiesQuery q, CancellationToken ct) => queries.ListAsync(q.UserId, ct);
}
