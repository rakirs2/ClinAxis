using Frontend.Components;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://0.0.0.0:5001");

builder.Services.AddRazorComponents();

WebApplication app = builder.Build();

app.UseAntiforgery();
app.MapStaticAssets();

app.MapGet("/api-proxy/studies", async (int? page, int? pageSize, string? search, string? status, string? phase) =>
{
    using HttpClient client = new() { BaseAddress = new Uri("http://localhost:5003") };
    var query = $"?page={page ?? 1}&pageSize={pageSize ?? 20}&search={Uri.EscapeDataString(search ?? "")}&status={Uri.EscapeDataString(status ?? "")}&phase={Uri.EscapeDataString(phase ?? "")}";
    HttpResponseMessage response = await client.GetAsync(new Uri("/api/studies" + query, UriKind.Relative));
    return Results.Content(await response.Content.ReadAsStringAsync(), "application/json");
});

app.MapRazorComponents<App>();

app.Run();