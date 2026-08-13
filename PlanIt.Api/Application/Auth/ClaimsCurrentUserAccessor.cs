using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Http;

namespace PlanIt.Api.Application.Auth;

// Reads the authenticated user's id from the "sub" claim minted by JwtTokenService.
public class ClaimsCurrentUserAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentUserAccessor
{
    public Guid UserId
    {
        get
        {
            var subClaim = httpContextAccessor.HttpContext?.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? throw new InvalidOperationException("No authenticated user in the current request.");
            return Guid.Parse(subClaim);
        }
    }
}
