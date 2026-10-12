using RVM.UI.Tutor.Samples.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();
// /health: o e2e.yml espera por ele antes de rodar o Playwright (ADR-008).
builder.Services.AddHealthChecks();
// Textos da biblioteca em pt-BR fixo: o E2E confere "Próximo", "1 de 4" em qualquer máquina.
builder.Services.AddRvmTutor(o => o.Language = "pt-BR");
builder.Services.AddScoped<RVM.UI.Tutor.Samples.SampleTheme>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapHealthChecks("/health");
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(RVM.UI.Tutor.Samples.Client._Imports).Assembly);

app.Run();
