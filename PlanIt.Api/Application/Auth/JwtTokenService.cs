using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PlanIt.Api.Domain.Entities;
using PlanIt.Api.Startup.Options;

namespace PlanIt.Api.Application.Auth;

// Mints access tokens. The *validation* side (issuer/audience/lifetime/signing key) is already
// configured in Program.cs's AddJwtBearer — this is purely the minting counterpart, so the claims
// and signing here must line up with what that validation expects.
public class JwtTokenService(IOptions<JwtOptions> jwtOptions) : IJwtTokenService
{
    public (string AccessToken, int ExpiresInSeconds) CreateAccessToken(User user)
    {
        var options = jwtOptions.Value;

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim("unique_name", user.Username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(options.ExpirationMinutes),
            signingCredentials: signingCredentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        return (accessToken, options.ExpirationMinutes * 60);
    }
}
