using System.Globalization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using RVM.UI.Tutor.Samples;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// No WASM os textos pt da biblioteca vêm de um assembly satélite, que só é baixado para a cultura
// corrente na subida: fixar pt-BR ANTES do RunAsync (docs/uso.md § WebAssembly).
var ptBR = CultureInfo.GetCultureInfo("pt-BR");
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = ptBR;
CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = ptBR;

builder.Services.AddRvmTutor(o => o.Language = "pt-BR");
builder.Services.AddScoped<SampleTheme>();

await builder.Build().RunAsync();
