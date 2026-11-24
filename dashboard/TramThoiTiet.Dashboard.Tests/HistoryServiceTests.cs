using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TramThoiTiet.Dashboard.Data;
using TramThoiTiet.Dashboard.Services;
using Xunit;

namespace TramThoiTiet.Dashboard.Tests;

// Semantics under test:
//   Hour1  -> raw rows inside the last hour, oldest first
//   Hours24 -> one point per minute bucket, mean of non-null values
//   Days7  -> one point per 10-minute bucket, mean of non-null values
public sealed class HistoryServiceTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ServiceProvider _provider;

    public HistoryServiceTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var services = new ServiceCollection();
        services.AddDbContextFactory<WeatherDbContext>(o => o.UseSqlite(_conn));
        _provider = services.BuildServiceProvider();
        using var db = _provider.GetRequiredService<IDbContextFactory<WeatherDbContext>>()
            .CreateDbContext();
        db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _provider.Dispose();
        _conn.Dispose();
    }

    private async Task SeedAsync(params WeatherSample[] samples)
    {
        var factory = _provider.GetRequiredService<IDbContextFactory<WeatherDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        db.WeatherRecords.AddRange(samples.Select(WeatherRecord.From));
        await db.SaveChangesAsync();
    }

    private static WeatherSample S(DateTime at, double t = 25, double lux = 400, double mua = 0) =>
        new(at, "tram-mini-01", t, 60, 5, 180, mua, lux, true);

    private HistoryService MakeService() =>
        new(_provider.GetRequiredService<IDbContextFactory<WeatherDbContext>>());

    private static readonly DateTime Now = new(2026, 11, 16, 12, 0, 0);

    [Fact]
    public async Task Hour_range_keeps_raw_points_only()
    {
        await SeedAsync(S(Now.AddHours(-2)), S(Now.AddMinutes(-30)), S(Now.AddMinutes(-10)));
        var pts = await MakeService().GetRangeAsync(HistoryRange.Hour1, Now);
        Assert.Equal(2, pts.Count);
        Assert.All(pts, p => Assert.True(p.ReceivedAt > Now.AddHours(-1)));
        Assert.True(pts[0].ReceivedAt < pts[1].ReceivedAt);
    }

    [Fact]
    public async Task Day_range_buckets_per_minute_with_mean()
    {
        await SeedAsync(
            S(Now.AddMinutes(-2).AddSeconds(10), 12),
            S(Now.AddMinutes(-2).AddSeconds(40), 14),
            S(Now.AddMinutes(-1), 16));
        var pts = await MakeService().GetRangeAsync(HistoryRange.Hours24, Now);
        Assert.Equal(2, pts.Count);
        Assert.Equal(13, pts[0].NhietDo);
        Assert.Equal(16, pts[1].NhietDo);
    }

    [Fact]
    public async Task Week_range_buckets_per_10_minutes()
    {
        await SeedAsync(S(Now.AddHours(-1)), S(Now.AddHours(-1).AddMinutes(5)), S(Now.AddHours(-1).AddMinutes(15)));
        var pts = await MakeService().GetRangeAsync(HistoryRange.Days7, Now);
        Assert.Equal(2, pts.Count);
        Assert.Equal(25, pts[0].NhietDo);
    }

    [Fact]
    public async Task Bucket_of_all_null_values_maps_to_nan()
    {
        var bad = new WeatherSample(Now.AddMinutes(-1), "tram-mini-01",
            double.NaN, double.NaN, double.NaN, 0, double.NaN, double.NaN, false);
        await SeedAsync(bad);
        var pts = await MakeService().GetRangeAsync(HistoryRange.Hours24, Now);
        Assert.Single(pts);
        Assert.Equal(double.NaN, pts[0].NhietDo);
        Assert.Equal(double.NaN, pts[0].AnhSang);
        Assert.False(pts[0].SensorOk);
    }

    [Fact]
    public async Task Empty_database_returns_empty_list()
    {
        var pts = await MakeService().GetRangeAsync(HistoryRange.Days7, Now);
        Assert.Empty(pts);
    }
}
