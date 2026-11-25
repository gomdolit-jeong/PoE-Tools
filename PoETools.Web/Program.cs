using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using PoETools.Web;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<PoETools.Web.Core.StarterBuildStore>();
builder.Services.AddScoped<PoETools.Web.Core.TimelessChanges>();
builder.Services.AddScoped<PoETools.Web.Core.CraftDataStore>();

await builder.Build().RunAsync();
