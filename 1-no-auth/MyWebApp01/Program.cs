// Configuration: reads from appsettings.json, environment variables, command-line args
// Logging: set the predefined logging (Console, Debug, etc.)
// Dependency Injection (DI): create the service container (builder.Services), even if you don't use it
// Even if you don't explicitly use these features, the WebApplication.CreateBuilder(args) call sets them up for you

var builder = WebApplication.CreateBuilder(args);

// Build the app.
var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapGet("/", () => Results.Ok("Hello World!"));
app.MapGet("/me", () =>
{
    var fakeUser = new
    {
        DisplayName = "Mauro",
        Email = "mauro@example.com"
    };
    return Results.Ok(fakeUser);
});

// Run the app.
app.Run();