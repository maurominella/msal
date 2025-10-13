using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));

builder.Services.AddAuthorization();

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

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


app.Run();