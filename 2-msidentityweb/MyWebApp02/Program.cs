using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;

// Configuration: reads from appsettings.json, environment variables, command-line args
// Logging: set the predefined logging (Console, Debug, etc.)
// Dependency Injection (DI): create the service container (builder.Services), even if you don't use it
// Even if you don't explicitly use these features, the WebApplication.CreateBuilder(args) call sets them up for you
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services
    // register authorization services to protect endpoints with .RequireAuthorization
    .AddAuthorization()
    // register Authentication middleware like OpenID Connect, that will be configured to use Entra ID thorugh Microsoft.Identity.Web
    .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme) 
    // register Microsoft.Identity.Web to configure OpenID Connect as defined in appsettings.json, which gives an identity to our app
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("EntraID"));

builder.Services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
{
    options.SaveTokens = true; // ✅ save ID token and access token in AuthenticationProperties
});

// Build the app.
var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapGet("/", () => Results.Ok("Hello World!"));

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

app.MapGet("/tokenid", async (HttpContext context) =>
{
    var idToken = await context.GetTokenAsync("id_token");
    return Results.Ok(new { IdToken = idToken });
}).RequireAuthorization();


// Run the app.
app.Run();