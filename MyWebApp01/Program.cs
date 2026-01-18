using System.IdentityModel.Tokens.Jwt;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Graph;
using Microsoft.Identity.Web;

// 0) Load .env file if present (for local dev only)
try
{
    // With optional: true does not throw exceptions if .env is not present
    Env.Load();
}
catch { /* log if you want, but ignore in prod */ }

Console.WriteLine("MAKE SURE THIS IS THE RIGHT SECRET: " + Environment.GetEnvironmentVariable("EntraID__ClientCredentials__0__ClientSecret"));

var builder = WebApplication.CreateBuilder(args);

// Read scopes from appsettings.json (single source of truth)
var graphScopes = builder.Configuration
    .GetSection("GraphV1:Scopes")
    .Get<string[]>() ?? Array.Empty<string>();


// take the authentication builder once and we can use it for both WebApp and WebAPI
// first, we set the default schemes for the WebApp...
var authenticationBuilder = builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
});

// ...then we configure both the WebApp, that uses OpenIdConnect + Cookies for interactive sign-in...
authenticationBuilder
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("EntraID"))
    
    // use scopes as "initial scopes" to request access tokens for Microsoft Graph:
    .EnableTokenAcquisitionToCallDownstreamApi(graphScopes)
    
    // configure GraphServiceClient (v5) with BaseUrl + scopes
    .AddMicrosoftGraph(builder.Configuration.GetSection("GraphV1"))
    
    // register token cache in memory (for production, consider a distributed cache)
    .AddInMemoryTokenCaches();


// ... and the WebAPI, that uses JwtBearer (access token validation) for API calls:
authenticationBuilder
    .AddMicrosoftIdentityWebApi(
        builder.Configuration.GetSection("EntraID"),
        JwtBearerDefaults.AuthenticationScheme);

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ApiScope", policy =>
    {
        policy.AuthenticationSchemes.Add(JwtBearerDefaults.AuthenticationScheme);
        policy.RequireAuthenticatedUser();
        policy.RequireScope("access_as_user");
    });
});


builder.Services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
{
    options.SaveTokens = true;
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();


#region API maps
app.MapGet("/api/ping", () => Results.Ok("pong"))
   .RequireAuthorization("ApiScope");
#endregion


#region APP maps
app.MapGet("/", () => "Hello World!");

app.MapGet("/fake_user", () =>
{
    var fakeUser = new
    {
        DisplayName = "Mauro",
        Email = "mauro@example.com"
    };
    return Results.Ok(fakeUser);
});

app.MapGet("/me", async (HttpContext context) =>
{
    var user = context.User;
    if (user.Identity?.IsAuthenticated == true)
    {
        var preferred_username = user.FindFirst("preferred_username")?.Value;
        return Results.Ok($"preferred_username: {preferred_username}");
    }
    return Results.Unauthorized();
}).RequireAuthorization();

app.MapGet("/job_title", async (GraphServiceClient graph) =>
{
    var me = await graph.Me.GetAsync(req =>
    {
        req.QueryParameters.Select = new[] { "id", "displayName", "jobTitle" };
        req.Options
             // ⬅️ key thing to do:
            .WithAuthenticationScheme(OpenIdConnectDefaults.AuthenticationScheme);
            // .WithScopes("User.Read"); // optional, if you want to force the scope
    });
    return me is null
        ? Results.NotFound()
        : Results.Ok(new { me.DisplayName, me.JobTitle });
}).RequireAuthorization();
#endregion

#region debugging helpers
app.MapGet("/id_token", async (HttpContext context) =>
{
    var idToken = await context.GetTokenAsync("id_token");
    return Results.Ok(new { IdToken = idToken });
}).RequireAuthorization();

app.MapGet("/debug", (IConfiguration config) =>
{
    return Results.Ok(new {
        FromEnv = Environment.GetEnvironmentVariable("EntraID__ClientCredentials__0__ClientSecret"),
        FromConfig = config["EntraID:ClientCredentials:0:ClientSecret"]
    });
});

app.MapGet("/access_token", async (HttpContext context) =>
{
    var accessToken = await context.GetTokenAsync("access_token");
    return Results.Ok(new { AccessToken = accessToken });
}).RequireAuthorization();

app.MapGet("/graph_token", async (ITokenAcquisition tokenAcquisition) =>
{
    // scope delegated per Graph
    var scopes = new[] { "User.Read" };

    var accessToken = await tokenAcquisition.GetAccessTokenForUserAsync(
        scopes, 
        authenticationScheme: OpenIdConnectDefaults.AuthenticationScheme);
    return Results.Ok(new { AccessToken = accessToken });
}).RequireAuthorization();


app.MapGet("/graph_token_claims", async (ITokenAcquisition tokenAcquisition) =>
{
    var token = await tokenAcquisition.GetAccessTokenForUserAsync(
        new[] { "User.Read" }, 
        authenticationScheme: OpenIdConnectDefaults.AuthenticationScheme);

    var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

    string? Claim(string type) => jwt.Claims.FirstOrDefault(c => c.Type == type)?.Value;

    return Results.Ok(new
    {
        aud = Claim("aud"),
        scp = Claim("scp"),
        appid = Claim("appid"),
        oid = Claim("oid"),
        tid = Claim("tid"),
        exp = Claim("exp"),
        iss = Claim("iss"),
        ver = Claim("ver")
    });
}).RequireAuthorization();
#endregion

app.Run();