using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace TramThoiTiet.Dashboard.Services;

public enum AiAdviceStatus
{
    Ok,
    NotConfigured,
    Failed,
}

public sealed record AiAdviceResult(AiAdviceStatus Status, string? Text);

// On-demand AI commentary: the UI calls GetAdviceAsync when the user clicks
// the advice button. The weighted WeatherAdvice score stays the deterministic
// backbone; this layer only phrases the sentence. Provider is OpenRouter
// (OpenAI-compatible chat completions). Missing key or any API failure yields
// a status so the UI can fall back to the template comment.
public sealed class AiAdviceService(HttpClient http, IConfiguration config,
    ILogger<AiAdviceService> logger)
{
    private static readonly TimeSpan SuccessCacheTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan FailedRetryDelay = TimeSpan.FromSeconds(30);
    private const string Endpoint = "https://openrouter.ai/api/v1/chat/completions";
    private const string DefaultModel = "liquid/lfm-3-1.7b:free";

    private readonly object _sync = new();
    private string? _cacheKey;
    private string? _cacheText;
    private DateTime _cacheAtUtc;

    public async Task<AiAdviceResult> GetAdviceAsync(WeatherSample sample, int score,
        string level, CancellationToken ct = default)
    {
        var apiKey = config["Ai:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey)) return new(AiAdviceStatus.NotConfigured, null);
        if (double.IsNaN(sample.NhietDo) || double.IsNaN(sample.AnhSang))
            return new(AiAdviceStatus.Failed, null);

        var key = $"{WeatherAdvice.TempBand(sample.NhietDo)}|" +
                  $"{WeatherAdvice.LightBand(sample.AnhSang)}|{level}";
        var nowUtc = DateTime.UtcNow;
        lock (_sync)
        {
            if (_cacheKey == key && nowUtc - _cacheAtUtc <
                (_cacheText is null ? FailedRetryDelay : SuccessCacheTtl))
                return new(_cacheText is null ? AiAdviceStatus.Failed : AiAdviceStatus.Ok, _cacheText);
        }

        try
        {
            var model = config["Ai:Model"];
            if (string.IsNullOrWhiteSpace(model)) model = DefaultModel;
            var text = await FetchAsync(apiKey!, model, sample, score, level, ct)
                ?? throw new InvalidOperationException("AI response was empty");
            lock (_sync)
            {
                _cacheKey = key;
                _cacheText = text;
                _cacheAtUtc = DateTime.UtcNow;
            }
            return new(AiAdviceStatus.Ok, text);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException
            or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "AI advice call failed");
            lock (_sync)
            {
                _cacheKey = key;
                _cacheText = null;
                _cacheAtUtc = DateTime.UtcNow;
            }
            return new(AiAdviceStatus.Failed, null);
        }
    }

    private async Task<string?> FetchAsync(string apiKey, string model, WeatherSample sample,
        int score, string level, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = JsonContent.Create(new
            {
                model,
                messages = new[] { new { role = "user", content = BuildPrompt(sample, score, level) } },
                temperature = 0.7,
                max_tokens = 2500,
            }),
        };
        request.Headers.Add("Authorization", $"Bearer {apiKey}");
        request.Headers.Add("X-Title", "TramThoiTiet-Dashboard");
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);
        return ParseResponse(json);
    }

    public static string BuildPrompt(WeatherSample s, int score, string level)
    {
        var sensorState = s.SensorOk ? "hoạt động bình thường" : "LỖI";
        return FormattableString.Invariant($"""
            Bạn là trợ lý thời tiết của một trạm IoT. Dữ liệu đo hiện tại: nhiệt độ {s.NhietDo:0.#}°C, độ ẩm {s.DoAm:0.#}%, gió {s.TocDoGio:0.#} m/s hướng {s.HuongGio}°, mưa {s.Mua:0.#} mm/h, ánh sáng {s.AnhSang:0.#} lx, cảm biến {sensorState}. Mô hình trọng số của hệ thống chấm: {score}/100, mức "{level}".
            CHỈ đánh giá dựa trên các số liệu đã nêu ở trên. Không tự bịa thêm số liệu, không tra cứu hay dùng thông tin bên ngoài, không nêu dự báo hay so sánh với địa điểm khác.
            Viết MỘT nhận xét bằng tiếng Việt gồm 1-2 câu: câu đầu mô tả tình trạng thời tiết kèm dải nhiệt độ (lạnh/mát/dễ chịu/nóng/rất nóng), câu sau nêu MỘT khuyến nghị hành động cụ thể cho người dùng. Không dùng markdown, không dùng dấu ngoặc kép, không nêu lại giá trị số của ánh sáng, không thêm tiêu đề hay lời dẫn.
            """);
    }

    public static string? ParseResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var content = doc.RootElement.GetProperty("choices")[0]
                .GetProperty("message").GetProperty("content").GetString();
            return string.IsNullOrWhiteSpace(content) ? null : content.Trim();
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException
            or InvalidOperationException or ArgumentOutOfRangeException or IndexOutOfRangeException)
        {
            return null;
        }
    }
}
