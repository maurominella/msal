using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Graph;
using Microsoft.Identity.Web;

var builder = WebApplication.CreateBuilder(args);

// 1) WebApp with Cookies schema
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"))
    .EnableTokenAcquisitionToCallDownstreamApi() // enables token acquisition and caching
    .AddMicrosoftGraph(builder.Configuration.GetSection("GraphV1")) // <-- uses GraphServiceClient (v5)
    .AddInMemoryTokenCaches();

// Authorization: policy based on the scope of our API
builder.Services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
{
    options.Scope.Add("User.Read");
});

// 2) WebAPI: Bearer Schema (no default schema here!)
builder.Services
    .AddAuthentication() // <-- NOTE: no default schema here!
    .AddMicrosoftIdentityWebApi( // <-- instead of AddMicrosoftIdentityWebApp
        builder.Configuration.GetSection("AzureAd"),
        JwtBearerDefaults.AuthenticationScheme);

// Authorization: policy based on the scope of our API
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ApiScope",
        policy => policy.RequireScope("superheroes.wonderwoman")); // our API scope
});

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

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

// protected endpoint that reads the jobTitle from the user's Graph profile
app.MapGet("/me/jobtitle", async (GraphServiceClient graph) =>
{
    var me = await graph.Me.GetAsync(req =>
    {
        req.QueryParameters.Select = new[] { "id", "displayName", "jobTitle" };
        req.Options
            .WithAuthenticationScheme(OpenIdConnectDefaults.AuthenticationScheme) // ⬅️ key thing to do
            .WithScopes("User.Read"); // optional, se vuoi forzare gli scope
    });
    return me is null
        ? Results.NotFound()
        : Results.Ok(new { me.DisplayName, me.JobTitle });
})
.RequireAuthorization();

app.MapGet("/api/ping", () => Results.Ok("pong"))
   .RequireAuthorization(policy =>
       policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
             .RequireAuthenticatedUser()
             .RequireScope("superheroes.wonderwoman"));


app.Run();