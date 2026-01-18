using Microsoft.Identity.Client;
using System.Net.Http.Headers;

var tenantId = "3ad0b905-34ab-4116-93d9-c1dcc2d35af6";
var clientId = "043335c5-8625-43d7-bb0a-ba0a9e65dff1"; // console app registration ID
var apiScope = "api://211b26e8-ca58-4150-8989-b7c608931ed9/access_as_user"; // Web API app registration scope

var app = PublicClientApplicationBuilder
    .Create(clientId)
    .WithAuthority(AzureCloudInstance.AzurePublic, tenantId)
    .WithRedirectUri("http://localhost") // per completezza, non sempre usata da device code
    .Build();

var result = await app.AcquireTokenWithDeviceCode(
        new[] { apiScope },
        deviceCodeCallback =>
        {
            Console.WriteLine(deviceCodeCallback.Message);
            return Task.CompletedTask;
        })
    .ExecuteAsync();

var http = new HttpClient();

http.DefaultRequestHeaders.Authorization =
    new AuthenticationHeaderValue("Bearer", result.AccessToken);

var response = await http.GetAsync("http://localhost:5001/api/ping"); // porta della tua web app
var content = await response.Content.ReadAsStringAsync();

Console.WriteLine($"Status: {response.StatusCode}");
Console.WriteLine(content);


Console.WriteLine("=== ACCESS TOKEN (per MyWebApp01 API) ===");
Console.WriteLine(result.AccessToken);

Console.WriteLine("Press ENTER to exit...");
Console.ReadLine();