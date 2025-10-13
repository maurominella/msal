// $env:AzureAd__ClientCredentials__0__ClientSecret=""
// export AzureAd__ClientCredentials__0__ClientSecret="****"

using DotNetEnv;
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

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"))
    .EnableTokenAcquisitionToCallDownstreamApi() // enables token acquisition and caching
    .AddMicrosoftGraph(builder.Configuration.GetSection("GraphV1")) // <-- uses GraphServiceClient (v5)
    .AddInMemoryTokenCaches();

// Authorization: policy based on the scope of our API
builder.Services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
{
    options.Scope.Add("User.Read"); 
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
    });
    return me is null
        ? Results.NotFound()
        : Results.Ok(new { me.DisplayName, me.JobTitle });
})
.RequireAuthorization();


app.Run();