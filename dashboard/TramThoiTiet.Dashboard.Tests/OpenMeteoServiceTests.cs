using System.Net;
using Microsoft.Extensions.Configuration;
using TramThoiTiet.Dashboard.Services;
using Xunit;

namespace TramThoiTiet.Dashboard.Tests;

public class OpenMeteoServiceTests
{
    private sealed class CountingHandler(Func<int, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var callIndex = Interlocked.Increment(ref Calls);
            return Task.FromResult(respond(callIndex));
        }
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    private static IConfiguration Config(int? cacheMinutes = null)
    {
        var values = new Dictionary<string, string?>();
        if (cacheMinutes.HasValue)
        {
            values["OpenMeteo:CacheMinutes"] = cacheMinutes.Value.ToString();
        }
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static OpenMeteoService CreateService(IConfiguration config, HttpMessageHandler handler)
        => new(new HttpClient(handler), new StubHttpClientFactory(), config);

    private static string SampleJson(double temp = 30.5, double windKmh = 10.8, int windDir = 180)
        => FormattableString.Invariant($$"""
        {
          "current": {
            "time": "2026-11-23T08:30",
            "temperature_2m": {{temp}},
            "relative_humidity_2m": 70,
            "wind_speed_10m": {{windKmh}},
            "wind_direction_10m": {{windDir}}
          },
          "daily": {
            "time": ["2026-11-23", "2026-11-24"],
            "temperature_2m_max": [33.0, 32.5],
            "temperature_2m_min": [25.0, 24.5],
            "precipitation_sum": [1.2, 0.0],
            "precipitation_probability_max": [40, 10],
            "weathercode": [2, 1]
          }
        }
        """);

    [Fact]
    public void Default_refresh_interval_is_five_minutes()
    {
        var handler = new CountingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var defaultSvc = CreateService(Config(), handler);
        var clampedSvc = CreateService(Config(0), handler);

        // Mặc định là 5 phút; nếu cấu hình <= 0 thì kẹp về tối thiểu 1 phút
        Assert.Equal(TimeSpan.FromMinutes(5), defaultSvc.RefreshInterval);
        Assert.Equal(TimeSpan.FromMinutes(1), clampedSvc.RefreshInterval);
    }

    [Fact]
    public async Task GetCurrentAsync_uses_cache_within_interval()
    {
        var handler = new CountingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(SampleJson(temp: 31.2, windKmh: 10.8, windDir: 90))
        });
        var svc = CreateService(Config(5), handler);

        var first = await svc.GetCurrentAsync(10.82, 106.63);
        var second = await svc.GetCurrentAsync(10.82, 106.63);

        Assert.NotNull(first);
        Assert.Equal(31.2, first.Temperature);
        Assert.Equal(3.0, first.WindSpeed, precision: 4);
        Assert.Equal(2, svc.Forecast.Count);
        Assert.Equal(first, second);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task GetCurrentAsync_force_refresh_bypasses_cache()
    {
        var handler = new CountingHandler(call => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(SampleJson(temp: call == 1 ? 29.0 : 32.0))
        });
        var svc = CreateService(Config(5), handler);

        var first = await svc.GetCurrentAsync(10.82, 106.63);
        var second = await svc.GetCurrentAsync(10.82, 106.63, forceRefresh: true);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(29.0, first.Temperature);
        Assert.Equal(32.0, second.Temperature);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task GetCurrentAsync_force_refresh_failure_keeps_cached_value()
    {
        var handler = new CountingHandler(call => call == 1
            ? new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SampleJson(temp: 28.4))
            }
            : new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var svc = CreateService(Config(5), handler);

        var first = await svc.GetCurrentAsync(10.82, 106.63);
        var second = await svc.GetCurrentAsync(10.82, 106.63, forceRefresh: true);

        // Khi làm mới định kỳ bị lỗi mạng/API, vẫn giữ dữ liệu cache cũ để UI không bị trống
        Assert.NotNull(first);
        Assert.Equal(first, second);
        Assert.Equal(2, handler.Calls);
    }
}
