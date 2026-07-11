using Frontend.Components;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://0.0.0.0:5001");

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

builder.Services.AddHttpClient("DataApi", client => client.BaseAddress = new Uri("http://localhost:5003"));

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("http://localhost:5003") });

WebApplication app = builder.Build();

app.UseAntiforgery();
app.UseStaticFiles();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();