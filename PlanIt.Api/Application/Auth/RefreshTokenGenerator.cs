using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace PlanIt.Api.Application.Auth;

// Shared by AuthService now (initial issuance on register/login) and by the refresh-rotation
// logic landing in step 4, which looks up a presented token by the same hash. Raw tokens are
// 256-bit random values; a fast hash (SHA-256) is appropriate for the stored value since a
// refresh token is already high-entropy random, unlike a password (planit-api-contracts-backend.md §4).
public static class RefreshTokenGenerator
{
    public static string CreateRawToken() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string rawToken) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
