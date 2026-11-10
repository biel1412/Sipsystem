using System.Globalization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Sip.Client;
using Sip.Client.Servicos;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// A API está no mesmo endereço do site, então o cookie de login vai junto automaticamente.
builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress),
    Timeout = TimeSpan.FromMinutes(3),
});
builder.Services.AddScoped<ApiCliente>();
builder.Services.AddScoped<SessaoAuth>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<SessaoAuth>());
builder.Services.AddScoped<Avisos>();
builder.Services.AddScoped<Navegador>();
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();

var ptBr = new CultureInfo("pt-BR");
CultureInfo.DefaultThreadCurrentCulture = ptBr;
CultureInfo.DefaultThreadCurrentUICulture = ptBr;

await builder.Build().RunAsync();
