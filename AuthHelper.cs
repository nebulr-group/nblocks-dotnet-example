using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json;

public static class AuthHelper
{
    private const string JWKS_URI = "https://auth.nblocks.cloud/.well-known/jwks.json";
    private const string ISSUER = "https://auth.nblocks.cloud";

    public static async Task<ClaimsPrincipal?> RequireAuth(HttpContext context, string appId)
    {
        var accessToken = context.Request.Cookies["access_token"];
        var refreshToken = context.Request.Cookies["refresh_token"];

        if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
        {
            context.Response.Redirect("/login");
            return null;
        }

        var tokenHandler = new JwtSecurityTokenHandler();

        bool IsTokenExpiringSoon(string token, int bufferInSeconds = 300)
        {
            var jwtToken = tokenHandler.ReadJwtToken(token);
            var expUnix = long.Parse(jwtToken.Payload.Exp.ToString());
            var expTime = DateTimeOffset.FromUnixTimeSeconds(expUnix);
            return (expTime - DateTimeOffset.UtcNow).TotalSeconds < bufferInSeconds;
        }

        try
        {
            if (IsTokenExpiringSoon(accessToken) || context.Request.Query.ContainsKey("forceRefresh"))
            {
                Console.WriteLine("🔁 Refreshing token...");
                var refreshPayload = JsonSerializer.Serialize(new { refreshToken });
                var refreshContent = new StringContent(refreshPayload, Encoding.UTF8, "application/json");

                var refreshResponse = await new HttpClient().PostAsync(
                    $"https://auth.nblocks.cloud/token/refresh/{appId}", refreshContent);

                if (!refreshResponse.IsSuccessStatusCode)
                {
                    context.Response.Redirect("/login");
                    return null;
                }

                var refreshedJson = await refreshResponse.Content.ReadAsStringAsync();
                var refreshed = JsonDocument.Parse(refreshedJson).RootElement;

                accessToken = refreshed.GetProperty("access_token").GetString();
                refreshToken = refreshed.GetProperty("refresh_token").GetString();

                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict,
                    Expires = DateTimeOffset.UtcNow.AddHours(1)
                };

                context.Response.Cookies.Append("access_token", accessToken, cookieOptions);
                context.Response.Cookies.Append("refresh_token", refreshToken, cookieOptions);
            }

            // Validate token
            var keys = new JsonWebKeySet(await new HttpClient().GetStringAsync(JWKS_URI));

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = ISSUER,
                ValidateAudience = false,
                ValidateLifetime = true,
                IssuerSigningKeys = keys.Keys
            };

            var principal = tokenHandler.ValidateToken(accessToken, validationParameters, out _);
            return principal;
        }
        catch
        {
            context.Response.Redirect("/login");
            return null;
        }
    }
}
