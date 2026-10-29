using NhaTro.Domain.Common;
using NhaTro.Domain.Entities;

namespace NhaTro.Domain.Tests;

public class PropertyTests
{
    [Fact]
    public void Property_stores_per_occupant_water_rate_and_bank_details()
    {
        var property = Property.Create(Guid.NewGuid(), "  Nhà A  ", "  Hà Nội  ", 3500, 18000,
            25000, "  Ngân hàng A  ", "  123456789  ", "  NGUYEN VAN A  ", 75000);

        Assert.Equal(25000m, property.WaterRatePerPerson);
        Assert.Equal("Ngân hàng A", property.BankName);
        Assert.Equal("123456789", property.BankAccountNumber);
        Assert.Equal("NGUYEN VAN A", property.BankAccountName);
        Assert.Equal(75_000m, property.ServiceAmountPerMonth);
    }

    [Fact]
    public void Property_rejects_negative_per_occupant_water_rate()
    {
        Assert.Throws<DomainException>(() => Property.Create(Guid.NewGuid(), "Nhà A", "", 3500, 18000, -1));
    }
}