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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new MqttFactory();
        using var client = factory.CreateMqttClient();
        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(_broker, _port)
            .Build();

        client.ApplicationMessageReceivedAsync += async e =>
        {
            WeatherSample sample;
            try
            {
                var payload = e.ApplicationMessage.PayloadSegment;
                if (payload.Array is null || payload.Count == 0) return;
                using var doc = JsonDocument.Parse(payload);
                var root = doc.RootElement;
                // ts is Unix epoch seconds from the device (NTP-synced); 0/absent means
                // the device clock was not synced, so fall back to arrival time.
                var ts = root.TryGetProperty("ts", out var tsEl) && tsEl.ValueKind == JsonValueKind.Number
                    ? tsEl.GetInt64() : 0;
                var sampleAt = ts > 1_600_000_000L
                    ? DateTimeOffset.FromUnixTimeSeconds(ts).LocalDateTime
                    : DateTime.Now;
                sample = new WeatherSample(
                    ReceivedAt: sampleAt,
                    Device: _device,
                    NhietDo: root.TryGetProperty("t", out var t) ? t.GetDouble() : double.NaN,
                    DoAm: root.TryGetProperty("h", out var h) ? h.GetDouble() : double.NaN,
                    TocDoGio: root.TryGetProperty("ws", out var ws) ? ws.GetDouble() : double.NaN,
                    HuongGio: root.TryGetProperty("wd", out var wd) ? wd.GetInt32() : 0,
                    Mua: root.TryGetProperty("r", out var r) ? r.GetDouble() : double.NaN,
                    AnhSang: root.TryGetProperty("lux", out var lux) ? lux.GetDouble() : double.NaN,
                    SensorOk: root.TryGetProperty("ok", out var ok) && ok.GetBoolean());
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Bad telemetry payload");
                return;
            }
            _store.Add(sample);
            try
            {
                await using var db = await _dbFactory.CreateDbContextAsync();
                db.WeatherRecords.Add(WeatherRecord.From(sample));
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Khong luu du lieu telemetry vao SQLite");
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
