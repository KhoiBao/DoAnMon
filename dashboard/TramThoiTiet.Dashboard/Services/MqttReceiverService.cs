using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Packets;
using MQTTnet.Protocol;
using TramThoiTiet.Dashboard.Data;

namespace TramThoiTiet.Dashboard.Services;

public sealed class MqttReceiverService : BackgroundService
{
    private readonly WeatherStore _store;
    private readonly IDbContextFactory<WeatherDbContext> _dbFactory;
    private readonly ILogger<MqttReceiverService> _logger;
    private readonly string _broker;
    private readonly int _port;
    private readonly string _topic;
    private readonly string _device;

    public MqttReceiverService(WeatherStore store, IConfiguration config,
        IDbContextFactory<WeatherDbContext> dbFactory, ILogger<MqttReceiverService> logger)
    {
        _store = store;
        _dbFactory = dbFactory;
        _logger = logger;
        _broker = config.GetValue<string>("Mqtt:Broker") ?? "broker.hivemq.com";
        _port = config.GetValue<int>("Mqtt:Port", 1883);
        _topic = config.GetValue<string>("Mqtt:Topic") ?? "v1/devices/me/telemetry";
        _device = config.GetValue<string>("Mqtt:Device") ?? "tram-mini-01";
    }

    // Public broker: any device can publish. JSON null (ArduinoJson serializes
    // NaN as null) or missing fields must yield NaN instead of throwing.
    public static bool TryParse(string json, string device, out WeatherSample sample)
    {
        sample = new WeatherSample(DateTime.Now, device, double.NaN, double.NaN,
            double.NaN, 0, double.NaN, double.NaN, false);
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            // ts is Unix epoch seconds from the device (NTP-synced); 0/absent means
            // the device clock was not synced, so fall back to arrival time.
            var ts = GetNumber(root, "ts");
            var sampleAt = ts is > 1_600_000_000
                ? DateTimeOffset.FromUnixTimeSeconds((long)ts.Value).LocalDateTime
                : DateTime.Now;
            sample = new WeatherSample(
                ReceivedAt: sampleAt,
                Device: device,
                NhietDo: GetNumber(root, "t") ?? double.NaN,
                DoAm: GetNumber(root, "h") ?? double.NaN,
                TocDoGio: GetNumber(root, "ws") ?? double.NaN,
                HuongGio: (int)(GetNumber(root, "wd") ?? 0),
                Mua: GetNumber(root, "r") ?? double.NaN,
                AnhSang: GetNumber(root, "lux") ?? double.NaN,
                SensorOk: root.TryGetProperty("ok", out var ok) &&
                    (ok.ValueKind == JsonValueKind.True ||
                     (ok.ValueKind == JsonValueKind.Number && ok.GetDouble() != 0)));
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static double? GetNumber(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.Number
            ? el.GetDouble() : null;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new MqttFactory();
        using var client = factory.CreateMqttClient();
        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(_broker, _port)
            .Build();

        client.ApplicationMessageReceivedAsync += async e =>
        {
            try
            {
                var payload = e.ApplicationMessage.PayloadSegment;
                if (payload.Array is null || payload.Count == 0) return;
                if (!TryParse(Encoding.UTF8.GetString(payload.Array, payload.Offset, payload.Count), _device, out var sample))
                {
                    _logger.LogWarning("Bad telemetry payload");
                    return;
                }
                _store.Add(sample);
                await using var db = await _dbFactory.CreateDbContextAsync();
                db.WeatherRecords.Add(WeatherRecord.From(sample));
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Khong xu duoc du lieu telemetry");
            }
        };

        client.ConnectedAsync += _ =>
        {
            _store.MqttConnected = true;
            _store.NotifyChanged();
            return Task.CompletedTask;
        };
        client.DisconnectedAsync += _ =>
        {
            _store.MqttConnected = false;
            _store.NotifyChanged();
            return Task.CompletedTask;
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await client.ConnectAsync(options, stoppingToken);
                await client.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
                    .WithTopicFilter(_topic, MqttQualityOfServiceLevel.AtMostOnce)
                    .Build(), stoppingToken);
                _logger.LogInformation("MQTT connected to {Broker}:{Port}, topic {Topic}",
                    _broker, _port, _topic);
                while (!stoppingToken.IsCancellationRequested && client.IsConnected)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MQTT connection failed, retrying in 5 s");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
