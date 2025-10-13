// dotnet add package Microsoft.Identity.Client
// dotnet add package System.Net.Http.Json
using Microsoft.Identity.Client;
using System.Net.Http.Headers;

var tenantId = "3ad0b905-34ab-4116-93d9-c1dcc2d35af6";
var clientId = "e9ed8892-6401-4404-8dd0-ba96619b9243"; // PingTester01RegisteredApp
var scopes = new[] { $"api://2353bdd3-8b4c-4d6c-9d55-8560cdec268f/superheroes.wonderwoman" };

var pca = PublicClientApplicationBuilder
    .Create(clientId)
    .WithTenantId(tenantId)
    .Build();

var result = await pca.AcquireTokenWithDeviceCode(scopes, dc =>
{
    Console.WriteLine(dc.Message);
    return Task.CompletedTask;
}).ExecuteAsync();

Console.WriteLine($"Token:\n{result.AccessToken}\n");

using var http = new HttpClient();

http.DefaultRequestHeaders.Authorization =
    new AuthenticationHeaderValue("Bearer", result.AccessToken);

var resp = await http.GetAsync("http://localhost:5011/api/ping");

Console.WriteLine($"{(int)resp.StatusCode} {resp.ReasonPhrase}");

Console.WriteLine(await resp.Content.ReadAsStringAsync());