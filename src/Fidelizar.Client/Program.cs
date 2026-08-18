using Fidelizar.Client;
using Fidelizar.Client.Api;
using Fidelizar.Client.Auth;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Same origin as Fidelizar.Api — one deployable unit (ARCHITECTURE §3).
builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress),
    Timeout = TimeSpan.FromSeconds(10),
});

builder.Services.AddScoped<CsrfTokenProvider>();
builder.Services.AddScoped<IApiClient, ApiClient>();
builder.Services.AddScoped<ISessionService, SessionService>();

await builder.Build().RunAsync();
