using TramThoiTiet.Dashboard.Components;
using TramThoiTiet.Dashboard.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<WeatherStore>();
builder.Services.AddHostedService<MqttReceiverService>();
builder.Services.AddHttpClient<OpenMeteoService>();

var app = builder.Build();
app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.Run();
