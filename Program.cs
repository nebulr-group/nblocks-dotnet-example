using System.Text.Json;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.WebUtilities;
using System.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Replace this with your actual APP ID from Nblocks
// const string APP_ID = "XXX";
const string APP_ID = "671279b938f34e0008b0f80b";

// Home page

app.MapGet("/", () => "Hello World!");

//this is a secure page that requires a valid access token
app.MapGet("/secure", async (HttpContext context) =>
{
    var user = await AuthHelper.RequireAuth(context, APP_ID);
    if (user == null) return;

    await context.Response.WriteAsync("Welcome back to the secure page!");
});


// Login redirect
app.MapGet("/login", (HttpContext context) =>
{
    var redirectUrl = $"https://auth.nblocks.cloud/url/login/{APP_ID}";
    context.Response.Redirect(redirectUrl);
    return Task.CompletedTask;
});

app.MapGet("/auth/oauth-callback", async (HttpContext context) =>
{
    var code = context.Request.Query["code"].ToString();
    if (string.IsNullOrEmpty(code))
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("Missing code");
        return;
    }

    var httpClient = new HttpClient();
    var payload = JsonSerializer.Serialize(new { code });
    var content = new StringContent(payload, Encoding.UTF8, "application/json");

    var tokenResponse = await httpClient.PostAsync(
        $"https://auth.nblocks.cloud/token/code/{APP_ID}", content
    );

    if (!tokenResponse.IsSuccessStatusCode)
    {
        context.Response.StatusCode = 401;
        await context.Response.WriteAsync("Token exchange failed");
        return;
    }

    var tokenJson = await tokenResponse.Content.ReadAsStringAsync();
    var tokenData = JsonDocument.Parse(tokenJson).RootElement;

    var accessToken = tokenData.GetProperty("access_token").GetString();
    var refreshToken = tokenData.GetProperty("refresh_token").GetString();
    var idToken = tokenData.GetProperty("id_token").GetString();

    // Verify the access token
    var jwksUri = "https://auth.nblocks.cloud/.well-known/jwks.json";
    var tokenHandler = new JwtSecurityTokenHandler();
    var keys = new JsonWebKeySet(await httpClient.GetStringAsync(jwksUri));

    var validationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = "https://auth.nblocks.cloud",
        ValidateAudience = false,
        ValidateLifetime = true,
        IssuerSigningKeys = keys.Keys
    };

    try
    {
        var principal = tokenHandler.ValidateToken(accessToken, validationParameters, out _);
        var user = principal.Identity?.Name ?? "Unknown user";

        // ✅ Save access token in a secure HTTP-only cookie
        context.Response.Cookies.Append("access_token", accessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = false, //NOTE only in development mode
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddHours(1)
        });

        context.Response.Cookies.Append("refresh_token", refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = false,  //NOTE only in development mode
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(7) // usually longer than access token
        });

        // 🔁 Redirect back to the homepage
        context.Response.Redirect("/");
    }
    catch (Exception ex)
    {
        context.Response.StatusCode = 403;
        await context.Response.WriteAsync($"Token validation failed: {ex.Message}");
    }
});



static bool IsTokenExpiringSoon(string token, int bufferInSeconds = 300) // 5 minutes
{
    var handler = new JwtSecurityTokenHandler();
    var jwtToken = handler.ReadJwtToken(token);
    var expUnix = long.Parse(jwtToken.Payload.Exp.ToString());
    var expTime = DateTimeOffset.FromUnixTimeSeconds(expUnix);
    var currentTime = DateTimeOffset.UtcNow;

    return (expTime - currentTime).TotalSeconds < bufferInSeconds;
}

app.Run();
