using System.Text.Json;
using Microsoft.Extensions.Options;

namespace TramThoiTiet.Dashboard.Services;

public record ApiWeather(
    DateTime Time,
    double Temperature,
    double Humidity,
    double Pressure,
    double WindSpeed,
    int WindDirection);

public sealed class OpenMeteoService
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly int _cacheMinutes;
    private (double Lat, double Lon)? _cacheKey;
    private ApiWeather? _cache;
    private DateTime _cacheAt;

    public OpenMeteoService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _baseUrl = config.GetValue<string>("OpenMeteo:BaseUrl")
            ?? "https://api.open-meteo.com/v1/forecast";
        _cacheMinutes = config.GetValue<int>("OpenMeteo:CacheMinutes", 10);
    }

    public async Task<ApiWeather?> GetCurrentAsync(double latitude, double longitude,
        CancellationToken ct = default)
    {
        if (_cache is not null && _cacheKey == (latitude, longitude)
            && DateTime.UtcNow - _cacheAt < TimeSpan.FromMinutes(_cacheMinutes))
        {
            return _cache;
        }

        var url = $"{_baseUrl}?latitude={latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
                  $"&longitude={longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
                  "&current=temperature_2m,relative_humidity_2m,surface_pressure," +
                  "wind_speed_10m,wind_direction_10m&timezone=auto";
        try
        {
            using var doc = JsonDocument.Parse(await _http.GetStringAsync(url, ct));
            var current = doc.RootElement.GetProperty("current");
            var weather = new ApiWeather(
                Time: current.GetProperty("time").GetDateTime(),
                Temperature: current.GetProperty("temperature_2m").GetDouble(),
                Humidity: current.GetProperty("relative_humidity_2m").GetDouble(),
                Pressure: current.GetProperty("surface_pressure").GetDouble(),
                WindSpeed: current.GetProperty("wind_speed_10m").GetDouble() / 3.6, // km/h -> m/s
                WindDirection: current.GetProperty("wind_direction_10m").GetInt32());
            _cache = weather;
            _cacheKey = (latitude, longitude);
            _cacheAt = DateTime.UtcNow;
            return weather;
        }
        catch (Exception)
        {
            return _cache;
        }
    }
}
