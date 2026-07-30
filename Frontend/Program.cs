using System.Reflection;
using Frontend;
using Frontend.Components;

WebApplication CreateApp()
{
    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
    builder.WebHost.UseUrls("http://0.0.0.0:5001");

    builder.Services.AddRazorComponents().AddInteractiveServerComponents();
    builder.Services.AddSignalR(options =>
    {
        options.MaximumReceiveMessageSize = 256 * 1024;
    });

    var dataApiBaseUrl = builder.Configuration["DataApi:BaseUrl"] ?? "http://localhost:5003";
    builder.Services.AddHttpClient("DataApi", client => client.BaseAddress = new Uri(dataApiBaseUrl));
    builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(dataApiBaseUrl) });

    builder.Services.AddSingleton<BlogService>();
    builder.Services.AddHealthChecks();

    WebApplication app = builder.Build();

    app.UseAntiforgery();
    app.UseStaticFiles();

    app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        ResponseWriter = async (context, report) =>
        {
            var assembly = typeof(Program).Assembly;
            var version = assembly.GetName().Version?.ToString() ?? "0.0.0.0";
            var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? version;
            var response = new
            {
                status = report.Status.ToString(),
                application = "Frontend",
                version,
                informationalVersion = infoVersion,
                framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription
            };
            context.Response.ContentType = "application/json";
            await System.Text.Json.JsonSerializer.SerializeAsync(context.Response.Body, response).ConfigureAwait(false);
        }
    });

    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    return app;
}

PortBindRetrier.Run(CreateApp);

namespace Frontend
{
    partial class Program { }
}