using NhaTro.Domain.Common;
using NhaTro.Domain.Entities;

namespace NhaTro.Domain.Tests;

public class UserTests
{
    [Fact]
    public void Update_profile_trims_name_and_normalizes_phone()
    {
        var user = User.RegisterWithPhone("0901234567", "Old name", "hash");

        user.UpdateProfile("  New name  ", "+84 901 234 567");

        Assert.Equal("New name", user.FullName);
        Assert.Equal("+84901234567", user.Phone);
    }

    [Fact]
    public void Update_profile_rejects_invalid_name_and_phone()
    {
        var user = User.RegisterWithPhone("0901234567", "Old name", "hash");

        Assert.Throws<DomainException>(() => user.UpdateProfile(" ", "0901234567"));
        Assert.Throws<DomainException>(() => user.UpdateProfile("Name", "invalid"));
        Assert.Equal("Old name", user.FullName);
        Assert.Equal("0901234567", user.Phone);
    }
}