using Microsoft.EntityFrameworkCore;
using TramThoiTiet.Dashboard.Data;

namespace TramThoiTiet.Dashboard.Services;

public enum HistoryRange { Hour1, Hours24, Days7 }

public sealed class HistoryService
{
    private readonly IDbContextFactory<WeatherDbContext> _factory;

    public HistoryService(IDbContextFactory<WeatherDbContext> factory) => _factory = factory;

    public async Task<IReadOnlyList<WeatherSample>> GetRangeAsync(HistoryRange range, DateTime now,
        CancellationToken ct = default)
    {
        var from = range switch
        {
            HistoryRange.Hour1 => now.AddHours(-1),
            HistoryRange.Hours24 => now.AddHours(-24),
            _ => now.AddDays(-7),
        };

        await using var db = await _factory.CreateDbContextAsync(ct);
        var rows = await db.WeatherRecords
            .Where(r => r.ReceivedAt >= from && r.ReceivedAt <= now)
            .OrderBy(r => r.ReceivedAt)
            .ToListAsync(ct);

        if (range == HistoryRange.Hour1)
            return rows.Select(r => r.ToSample()).ToList();

        var bucketTicks = range == HistoryRange.Hours24
            ? TimeSpan.FromMinutes(1).Ticks
            : TimeSpan.FromMinutes(10).Ticks;

        return rows
            .GroupBy(r => r.ReceivedAt.Ticks / bucketTicks)
            .Select(g => BucketSample(g, bucketTicks))
            .ToList();
    }

    private static WeatherSample BucketSample(IGrouping<long, WeatherRecord> g, long bucketTicks)
    {
        var first = g.First();
        return new WeatherSample(
            ReceivedAt: new DateTime(g.Key * bucketTicks),
            Device: first.Device,
            NhietDo: Mean(g.Select(r => r.NhietDo)),
            DoAm: Mean(g.Select(r => r.DoAm)),
            TocDoGio: Mean(g.Select(r => r.TocDoGio)),
            HuongGio: (int)Math.Round(g.Average(r => (double)r.HuongGio)),
            Mua: Mean(g.Select(r => r.Mua)),
            AnhSang: Mean(g.Select(r => r.AnhSang)),
            SensorOk: g.All(r => r.SensorOk));
    }

    private static double Mean(IEnumerable<double?> values)
    {
        var present = values
            .Select(v => v ?? double.NaN)
            .Where(v => !double.IsNaN(v))
            .ToList();
        return present.Count == 0 ? double.NaN : present.Average();
    }
}
