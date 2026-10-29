using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using NhaTro.Application.Abstractions;
using NhaTro.Domain.Entities;

namespace NhaTro.Infrastructure.Security;

internal class PasswordHasherAdapter : IPasswordHasher
{
    private static readonly PasswordHasher<object> Hasher = new();
    private static readonly object Subject = new();

    public string Hash(string password) => Hasher.HashPassword(Subject, password);
    public bool Verify(string hash, string password) => Hasher.VerifyHashedPassword(Subject, hash, password) != PasswordVerificationResult.Failed;
}

internal class TokenService(IConfiguration cfg) : ITokenService
{
    public string Create(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg["Jwt:Key"]!));
        var claims = new List<Claim> { new("sub", user.Id.ToString()) };
        if (!string.IsNullOrWhiteSpace(user.Phone)) claims.Add(new Claim("phone_number", user.Phone));
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = cfg["Jwt:Issuer"],
            Audience = cfg["Jwt:Audience"],
            Expires = DateTime.UtcNow.AddHours(cfg.GetValue("Jwt:ExpiresHours", 12)),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
