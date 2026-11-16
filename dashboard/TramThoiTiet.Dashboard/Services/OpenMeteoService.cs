using System.Text.Json;

namespace TramThoiTiet.Dashboard.Services;

public record ApiWeather(
    DateTime Time,
    double Temperature,
    double Humidity,
    double WindSpeed,
    int WindDirection);

public record DailyForecast(
    DateTime Date,
    double TempMax,
    double TempMin,
    double PrecipitationSum,
    int PrecipitationProbability,
    int WeatherCode);

public sealed class OpenMeteoService
{
    private readonly HttpClient _http;
    private readonly IHttpClientFactory _factory;
    private readonly string _baseUrl;
    private readonly int _cacheMinutes;
    private (double Lat, double Lon)? _cacheKey;
    private ApiWeather? _cache;
    private DateTime _cacheAt;
    private (double Lat, double Lon)? _placeKey;
    private string? _placeCache;

    public OpenMeteoService(HttpClient http, IHttpClientFactory factory, IConfiguration config)
    {
        _http = http;
        _factory = factory;
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

        var lat = latitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var lon = longitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var url = $"{_baseUrl}?latitude={lat}&longitude={lon}" +
                  "&current=temperature_2m,relative_humidity_2m," +
                  "wind_speed_10m,wind_direction_10m" +
                  "&daily=temperature_2m_max,temperature_2m_min," +
                  "precipitation_sum,precipitation_probability_max,weathercode" +
                  "&forecast_days=5&timezone=auto";
        try
        {
            using var doc = JsonDocument.Parse(await _http.GetStringAsync(url, ct));
            var root = doc.RootElement;
            var current = root.GetProperty("current");
            var daily = root.GetProperty("daily");
            var weather = new ApiWeather(
                Time: current.GetProperty("time").GetDateTime(),
                Temperature: current.GetProperty("temperature_2m").GetDouble(),
                Humidity: current.GetProperty("relative_humidity_2m").GetDouble(),
                WindSpeed: current.GetProperty("wind_speed_10m").GetDouble() / 3.6, // km/h -> m/s
                WindDirection: current.GetProperty("wind_direction_10m").GetInt32());
            Forecast = ParseDaily(daily);
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

    private static List<DailyForecast> ParseDaily(JsonElement daily)
    {
        var dates = daily.GetProperty("time").EnumerateArray().Select(e => e.GetDateTime()).ToList();
        var tmax = daily.GetProperty("temperature_2m_max").EnumerateArray().Select(e => e.GetDouble()).ToList();
        var tmin = daily.GetProperty("temperature_2m_min").EnumerateArray().Select(e => e.GetDouble()).ToList();
        var rain = daily.GetProperty("precipitation_sum").EnumerateArray().Select(e => e.GetDouble()).ToList();
        var prob = daily.GetProperty("precipitation_probability_max").EnumerateArray().Select(e => e.GetInt32()).ToList();
        var code = daily.GetProperty("weathercode").EnumerateArray().Select(e => e.GetInt32()).ToList();
        var list = new List<DailyForecast>();
        for (int i = 0; i < dates.Count; i++)
        {
            list.Add(new DailyForecast(dates[i], tmax[i], tmin[i], rain[i], prob[i], code[i]));
        }
        return list;
    }

    public List<DailyForecast> Forecast { get; private set; } = new();

    public async Task<string?> GetPlaceNameAsync(double latitude, double longitude,
        CancellationToken ct = default)
    {
        if (_placeCache is not null && _placeKey == (latitude, longitude))
        {
            return _placeCache;
        }

        var lat = latitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var lon = longitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var url = $"https://nominatim.openstreetmap.org/reverse?lat={lat}&lon={lon}" +
                  "&format=jsonv2&accept-language=vi&zoom=18";
        try
        {
            var client = _factory.CreateClient("nominatim");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("TramThoiTiet-Dashboard/1.0");
            using var doc = JsonDocument.Parse(await client.GetStringAsync(url, ct));
            var address = doc.RootElement.GetProperty("address");
            var parts = new List<string>();
            foreach (var key in new[] { "road", "neighbourhood", "quarter", "suburb",
                                        "city_district", "district", "city", "town",
                                        "village", "commune", "county", "state", "country" })
            {
                if (address.TryGetProperty(key, out var v)
                    && v.GetString() is { } s && s.Length > 0
                    && !parts.Contains(s, StringComparer.OrdinalIgnoreCase))
                {
                    parts.Add(s);
                }
            }
            var result = parts.Count > 0 ? string.Join(", ", parts.Take(4)) : null;
            _placeCache = result;
            _placeKey = (latitude, longitude);
            return result;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static string DescribeWeatherCode(int code) => code switch
    {
        0 => "Trời quang",
        1 => "Chủ yếu quang",
        2 => "Mây rải rác",
        3 => "Nhiều mây",
        45 => "Sương mù",
        48 => "Sương mù đóng băng",
        51 => "Mưa phùn nhẹ",
        53 => "Mưa phùn vừa",
        55 => "Mưa phùn dày",
        56 or 57 => "Mưa phùn đóng băng",
        61 => "Mưa nhẹ",
        63 => "Mưa vừa",
        65 => "Mưa to",
        66 or 67 => "Mưa đóng băng",
        71 => "Tuyết nhẹ",
        73 => "Tuyết vừa",
        75 => "Tuyết dày",
        77 => "Hạt tuyết",
        80 => "Mưa rào nhẹ",
        81 => "Mưa rào vừa",
        82 => "Mưa rào dữ dội",
        85 or 86 => "Mưa tuyết rào",
        95 => "Dông",
        96 => "Dông kèm mưa đá nhẹ",
        99 => "Dông kèm mưa đá to",
        _ => "Không xác định"
    };
}
