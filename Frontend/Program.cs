using Frontend.Components;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://0.0.0.0:5001");

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddHttpClient("DataApi", client => client.BaseAddress = new Uri("http://localhost:5000"));
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("DataApi"));

WebApplication app = builder.Build();

app.UseStaticFiles();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();