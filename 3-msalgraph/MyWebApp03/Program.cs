// setx AzureAd__ClientCredentials__0__ClientSecret "value" -- in this case you need to restart the terminal to see it
// $env:AzureAd__ClientCredentials__0__ClientSecret="value"
// export AzureAd__ClientCredentials__0__ClientSecret="****" -- bash linux/windows

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
    .AddAuthorization()
    .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"))
    .EnableTokenAcquisitionToCallDownstreamApi() // enables token acquisition and caching
    .AddMicrosoftGraph(builder.Configuration.GetSection("GraphV1")) // <-- uses GraphServiceClient (v5)
    .AddInMemoryTokenCaches();

// Authorization: policy based on the scope of our API
builder.Services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
{
    options.SaveTokens = true; 
    options.GetClaimsFromUserInfoEndpoint = true;
    options.Scope.Add("User.Read"); 
});

var app = builder.Build();

Console.WriteLine("MAKE SURE THIS IS THE RIGHT SECRET: " + builder.Configuration["AzureAd:ClientCredentials:0:ClientSecret"]);

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "Hello World!");
app.MapGet("/debug", (IConfiguration config) =>
{
    return Results.Ok(new {
        FromEnv = Environment.GetEnvironmentVariable("AzureAd__ClientCredentials__0__ClientSecret"),
        FromConfig = config["AzureAd:ClientCredentials:0:ClientSecret"]
    });
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

// protected endpoint that reads the jobTitle from the user's Graph profile
app.MapGet("/jobtitle", async (GraphServiceClient graph) =>
{
    var me = await graph.Me.GetAsync(req =>
    {
        req.QueryParameters.Select = new[] { "id", "displayName", "jobTitle" };
    });
    return me is null
        ? Results.NotFound()
        : Results.Ok(new { me.DisplayName, me.JobTitle });
}).RequireAuthorization();


app.Run();