using TramThoiTiet.Dashboard.Services;
using Xunit;

namespace TramThoiTiet.Dashboard.Tests;

// Semantics under test (docs §6.1): the topic is public, so payloads from
// unknown devices with null or missing fields must still parse into a row
// (measurements as NaN) instead of killing the MQTT receive handler.
public sealed class MqttReceiverServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 23, 14, 0, 0);

    [Fact]
    public void Null_fields_parse_as_nan_and_keep_the_row()
    {
        // Exact payload seen from the simulator when DHT22 fails.
        const string json = "{\"t\":null,\"h\":null,\"ws\":0,\"wd\":0,\"r\":0,\"lux\":44.69,\"ok\":false,\"ts\":474}";

        Assert.True(MqttReceiverService.TryParse(json, "tram-mini-01", out var s));

        Assert.True(double.IsNaN(s.NhietDo));
        Assert.True(double.IsNaN(s.DoAm));
        Assert.Equal(0, s.TocDoGio);
        Assert.Equal(0, s.HuongGio);
        Assert.Equal(0, s.Mua);
        Assert.Equal(44.69, s.AnhSang);
        Assert.False(s.SensorOk);
        Assert.Equal("tram-mini-01", s.Device);
        // ts=474 is not a Unix epoch, so the sample falls back to arrival time.
        Assert.True(s.ReceivedAt > DateTime.Now.AddMinutes(-1));
    }

    [Fact]
    public void Missing_fields_parse_as_nan_and_keep_the_row()
    {
        const string json = "{\"wd\":120}";

        Assert.True(MqttReceiverService.TryParse(json, "tram-mini-01", out var s));

        Assert.True(double.IsNaN(s.NhietDo));
        Assert.True(double.IsNaN(s.DoAm));
        Assert.True(double.IsNaN(s.TocDoGio));
        Assert.Equal(120, s.HuongGio);
        Assert.True(double.IsNaN(s.Mua));
        Assert.True(double.IsNaN(s.AnhSang));
        Assert.False(s.SensorOk);
    }

    [Fact]
    public void Valid_payload_maps_all_fields()
    {
        // Documented example payload (docs §5.3).
        const string json = "{\"t\":29.5,\"h\":62,\"ws\":3.4,\"wd\":120,\"r\":0,\"lux\":450,\"ok\":true,\"ts\":1794846520}";

        Assert.True(MqttReceiverService.TryParse(json, "tram-mini-01", out var s));

        Assert.Equal(29.5, s.NhietDo);
        Assert.Equal(62, s.DoAm);
        Assert.Equal(3.4, s.TocDoGio);
        Assert.Equal(120, s.HuongGio);
        Assert.Equal(0, s.Mua);
        Assert.Equal(450, s.AnhSang);
        Assert.True(s.SensorOk);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1794846520).LocalDateTime, s.ReceivedAt);
    }

    [Fact]
    public void Non_json_payload_is_rejected()
    {
        Assert.False(MqttReceiverService.TryParse("not json", "tram-mini-01", out var s));
        Assert.True(double.IsNaN(s.NhietDo));
    }
}
