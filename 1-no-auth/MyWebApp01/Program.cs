var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.MapGet("/me", () =>
{
    var fakeUser = new
    {
        DisplayName = "Mauro",
        Email = "mauro@example.com"
    };
    return Results.Ok(fakeUser);
});

app.Run();