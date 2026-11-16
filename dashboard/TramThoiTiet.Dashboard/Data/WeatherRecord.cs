using TramThoiTiet.Dashboard.Services;

namespace TramThoiTiet.Dashboard.Data;

// One persisted telemetry row. Missing values are stored as NULL:
// SQLite cannot hold NaN in a REAL column.
public class WeatherRecord
{
    public int Id { get; set; }
    public DateTime ReceivedAt { get; set; }
    public string Device { get; set; } = "";
    public double? NhietDo { get; set; }
    public double? DoAm { get; set; }
    public double? TocDoGio { get; set; }
    public int HuongGio { get; set; }
    public double? Mua { get; set; }
    public double? AnhSang { get; set; }
    public bool SensorOk { get; set; }

    public static WeatherRecord From(WeatherSample s) => new()
    {
        ReceivedAt = s.ReceivedAt,
        Device = s.Device,
        NhietDo = double.IsNaN(s.NhietDo) ? null : s.NhietDo,
        DoAm = double.IsNaN(s.DoAm) ? null : s.DoAm,
        TocDoGio = double.IsNaN(s.TocDoGio) ? null : s.TocDoGio,
        HuongGio = s.HuongGio,
        Mua = double.IsNaN(s.Mua) ? null : s.Mua,
        AnhSang = double.IsNaN(s.AnhSang) ? null : s.AnhSang,
        SensorOk = s.SensorOk,
    };

    public WeatherSample ToSample() => new(
        ReceivedAt,
        Device,
        NhietDo ?? double.NaN,
        DoAm ?? double.NaN,
        TocDoGio ?? double.NaN,
        HuongGio,
        Mua ?? double.NaN,
        AnhSang ?? double.NaN,
        SensorOk);
}
