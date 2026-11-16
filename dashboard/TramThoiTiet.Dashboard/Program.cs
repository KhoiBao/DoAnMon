using Microsoft.EntityFrameworkCore;
using TramThoiTiet.Dashboard.Components;
using TramThoiTiet.Dashboard.Data;
using TramThoiTiet.Dashboard.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<WeatherStore>();
builder.Services.AddSingleton<HistoryService>();
builder.Services.AddHostedService<MqttReceiverService>();
builder.Services.AddHttpClient<OpenMeteoService>();
builder.Services.AddHttpClient<AiAdviceService>(c => c.Timeout = TimeSpan.FromSeconds(8));

var dbPath = Path.Combine(builder.Environment.ContentRootPath, "tramthoitiet.db");
builder.Services.AddDbContextFactory<WeatherDbContext>(o => o.UseSqlite($"Data Source={dbPath}"));

var app = builder.Build();
using (var db = app.Services.GetRequiredService<IDbContextFactory<WeatherDbContext>>().CreateDbContext())
{
    db.Database.EnsureCreated();
}

app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.Run();
