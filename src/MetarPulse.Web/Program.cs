using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MetarPulse.Web;
using MetarPulse.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// API BaseAddress — development'ta API ayrı port'ta çalışıyor
var apiBase = builder.Configuration["ApiBaseUrl"]
    ?? "http://localhost:5000/";

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBase) });
builder.Services.AddScoped<ReadStateService>();

await builder.Build().RunAsync();
