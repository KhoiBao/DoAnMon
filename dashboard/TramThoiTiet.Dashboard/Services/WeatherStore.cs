using System.Collections.Concurrent;

namespace TramThoiTiet.Dashboard.Services;

public record WeatherSample(
    DateTime ReceivedAt,
    string Device,
    double NhietDo,
    double DoAm,
    double TocDoGio,
    int HuongGio,
    double Mua,
    double AnhSang,
    bool SensorOk);

public sealed class WeatherStore
{
    private readonly object _lock = new();
    private readonly Queue<WeatherSample> _history = new();
    private const int HistoryCapacity = 200;

    public WeatherSample? Latest { get; private set; }
    public DateTime? LastMessageAt { get; private set; }
    public bool MqttConnected { get; set; }

    public event Action? Changed;

    public IReadOnlyList<WeatherSample> History
    {
        get { lock (_lock) { return _history.ToList(); } }
    }

    public void Add(WeatherSample sample)
    {
        lock (_lock)
        {
            _history.Enqueue(sample);
            while (_history.Count > HistoryCapacity)
                _history.Dequeue();
        }
        Latest = sample;
        LastMessageAt = DateTime.Now;
        Changed?.Invoke();
    }

    public void NotifyChanged() => Changed?.Invoke();
}
