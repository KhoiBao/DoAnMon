using System.Text.Json;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Packets;
using MQTTnet.Protocol;

namespace TramThoiTiet.Dashboard.Services;

public sealed class MqttReceiverService : BackgroundService
{
    private readonly WeatherStore _store;
    private readonly ILogger<MqttReceiverService> _logger;
    private readonly string _broker;
    private readonly int _port;
    private readonly string _topic;

    public MqttReceiverService(WeatherStore store, IConfiguration config,
        ILogger<MqttReceiverService> logger)
    {
        _store = store;
        _logger = logger;
        _broker = config.GetValue<string>("Mqtt:Broker") ?? "broker.hivemq.com";
        _port = config.GetValue<int>("Mqtt:Port", 1883);
        _topic = config.GetValue<string>("Mqtt:Topic") ?? "v1/devices/me/telemetry";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new MqttFactory();
        using var client = factory.CreateMqttClient();
        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(_broker, _port)
            .Build();

        client.ApplicationMessageReceivedAsync += e =>
        {
            try
            {
                var payload = e.ApplicationMessage.PayloadSegment;
                if (payload.Array is null || payload.Count == 0) return Task.CompletedTask;
                using var doc = JsonDocument.Parse(payload);
                var root = doc.RootElement;
                _store.Add(new WeatherSample(
                    ReceivedAt: DateTime.Now,
                    Device: root.TryGetProperty("device", out var d) ? d.GetString() ?? "" : "",
                    NhietDo: root.TryGetProperty("nhiet_do", out var t) ? t.GetDouble() : double.NaN,
                    DoAm: root.TryGetProperty("do_am", out var h) ? h.GetDouble() : double.NaN,
                    ApSuat: root.TryGetProperty("ap_suat", out var p) ? p.GetDouble() : double.NaN,
                    TocDoGio: root.TryGetProperty("toc_do_gio", out var ws) ? ws.GetDouble() : double.NaN,
                    HuongGio: root.TryGetProperty("huong_gio", out var wd) ? wd.GetInt32() : 0,
                    Mua: root.TryGetProperty("mua", out var r) ? r.GetDouble() : double.NaN,
                    SensorOk: root.TryGetProperty("sensor_ok", out var ok) && ok.GetBoolean()));
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Bad telemetry payload");
            }
            return Task.CompletedTask;
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
