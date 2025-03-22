This is a minimal .NET web app that demonstrates how to integrate authentication and feature flags using [Nblocks](https://nblocks.dev).

It mirrors functionality from a React implementation and shows how to:

- ✅ Redirect to Nblocks login
- ✅ Handle the OAuth callback
- ✅ Secure pages and check for tokens
- ✅ Refresh access tokens automatically

---

## 🚀 Features

- Authentication with Nblocks via OAuth2
- Cookie-based storage of access and refresh tokens
- Auto-refresh of expiring access tokens
- Reusable helper for securing routes
- Lightweight, no frontend required

---

## Before you start
```
Replace const string APP_ID = "XXX"; in Program.cs with the your AppID that you can find in the nblocks control center when you navigate to `keys`
```

```
Configure the callback to point to the right URL
You do this under Authentication - Security.

i.e if your app runs on localhost:5224 make sure that the callback URL is http://localhost:5242/auth/oauth-callback
```

## Usage
You have the following url:s in the application

**/**                       (A page that is not secured)
**/login**                  (redirects to nblocks login)  
**/secure**                 (A page that is secured. if you are not logged in you will be redirected to /login)  
**/auth/oauth/callback**   (The callback that nblocks calls upon successfull login)


# Implementation steps

## 🔐 Nblocks Authentication Flow (React + .NET Example)

This document outlines the four main steps to implement authentication using **Nblocks**, with **React examples** alongside their **.NET counterparts**.

---

## ✅ 1. Redirect to Login

**🧠 Goal:** Redirect the user to the Nblocks login page.

### React Example

```js
const APP_ID = "YOUR_APP_ID";
window.location.replace(`https://auth.nblocks.cloud/url/login/${APP_ID}`);
```

### .NET Example

```csharp
app.MapGet("/login", (HttpContext context) =>
{
    var redirectUrl = $"https://auth.nblocks.cloud/url/login/{APP_ID}";
    context.Response.Redirect(redirectUrl);
    return Task.CompletedTask;
});
```

---

## ✅ 2. Handle OAuth Callback

**🧠 Goal:** Receive the `code` from Nblocks and exchange it for tokens.

### React Example

```js
const tokens = await fetch(`https://auth.nblocks.cloud/token/code/${APP_ID}`, {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({ code }),
});
```

### .NET Example

```csharp
var tokenResponse = await httpClient.PostAsync(
    $"https://auth.nblocks.cloud/token/code/{APP_ID}",
    new StringContent(JsonSerializer.Serialize(new { code }), Encoding.UTF8, "application/json")
);

var tokenData = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync()).RootElement;

context.Response.Cookies.Append("access_token", tokenData.GetProperty("access_token").GetString(), cookieOptions);
context.Response.Cookies.Append("refresh_token", tokenData.GetProperty("refresh_token").GetString(), cookieOptions);

context.Response.Redirect("/");
```

---

## ✅ 3. Secure a Page

**🧠 Goal:** Block access to secure pages unless the user is authenticated.

### React Example

```js
if (!localStorage.getItem("access_token")) {
  return <Navigate to="/login" />
}
```

### .NET Example (with helper)

```csharp
app.MapGet("/secure", async (HttpContext context) =>
{
    var user = await AuthHelper.RequireAuth(context, APP_ID);
    if (user == null) return;

    await context.Response.WriteAsync("Welcome to the secure page!");
});
```

---

## ✅ 4. Refresh Tokens Automatically

**🧠 Goal:** Automatically refresh the access token before it expires using the `refresh_token`.

### React Example

```js
const refreshedTokens = await fetch(`https://auth.nblocks.cloud/token/refresh/${APP_ID}`, {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({ refreshToken }),
});
```

### .NET Example (inside helper)

```csharp
if (IsTokenExpiringSoon(accessToken))
{
    var refreshResponse = await httpClient.PostAsync(
        $"https://auth.nblocks.cloud/token/refresh/{APP_ID}",
        new StringContent(JsonSerializer.Serialize(new { refreshToken }), Encoding.UTF8, "application/json")
    );

    // Save new tokens in cookies
}
```

---


