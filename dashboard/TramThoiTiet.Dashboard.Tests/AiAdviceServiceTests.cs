using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TramThoiTiet.Dashboard.Services;
using Xunit;

namespace TramThoiTiet.Dashboard.Tests;

// AI advice rules (approved 2026-11-16, amended: on-demand button, OpenRouter provider):
//   Provider: OpenRouter chat completions, Bearer key, OpenAI response format
//   Model: liquid/lfm-3-1.7b:free via Ai:Model
//   Prompt: assess ONLY the provided measurements; no outside information
//   No ApiKey -> NotConfigured, no HTTP call
//   NaN temperature or light -> Failed, no HTTP call
//   API error -> Failed; retrying the same condition within 30 s makes no second HTTP call
//   Valid response -> Ok with text; same condition within 5 min is served from cache (1 call total)
//   Different condition -> new HTTP call
public class AiAdviceServiceTests
{
    private static WeatherSample Sample(double t = 29.1, double lux = 450, bool ok = true)
        => new(DateTime.Now, "tram-mini-01", t, 66, 3.5, 185, 0, lux, ok);

    private static AiAdviceService Service(IConfiguration config, HttpMessageHandler handler)
        => new(new HttpClient(handler), config, NullLogger<AiAdviceService>.Instance);

    private static IConfiguration Config(string? apiKey = "test-key")
        => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Ai:ApiKey"] = apiKey,
            ["Ai:Model"] = "test-model",
        }).Build();

    private sealed class CountingHandler(Func<HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls;
        public HttpRequestMessage? Last;
        public string LastBody = "";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            Last = request;
            if (request.Content is { } content)
                LastBody = await content.ReadAsStringAsync(ct);
            return respond();
        }
    }

    private const string ValidOpenAiJson =
        """{"choices":[{"message":{"content":"Trời đang dễ chịu, phù hợp để ra ngoài. Nên mang theo nước."}}]}""";

    [Fact]
    public void Prompt_contains_readings_score_and_level()
    {
        var prompt = AiAdviceService.BuildPrompt(Sample(), 100, "Thuận lợi");
        Assert.Contains("29.1", prompt);
        Assert.Contains("66", prompt);
        Assert.Contains("3.5", prompt);
        Assert.Contains("450", prompt);
        Assert.Contains("100/100", prompt);
        Assert.Contains("Thuận lợi", prompt);
    }

    [Fact]
    public void Prompt_flags_broken_sensor()
    {
        var prompt = AiAdviceService.BuildPrompt(Sample(ok: false), 40, "Cần lưu ý");
        Assert.Contains("LỖI", prompt);
    }

    [Fact]
    public void Prompt_limits_assessment_to_given_readings()
    {
        var prompt = AiAdviceService.BuildPrompt(Sample(), 100, "Thuận lợi");
        Assert.Contains("CHỈ đánh giá dựa trên các số liệu đã nêu", prompt);
        Assert.Contains("không tra cứu", prompt);
    }

    [Fact]
    public void ParseResponse_extracts_text()
    {
        Assert.Equal("Trời đang dễ chịu, phù hợp để ra ngoài. Nên mang theo nước.",
            AiAdviceService.ParseResponse(ValidOpenAiJson));
    }

    [Fact]
    public void ParseResponse_returns_null_for_malformed_or_missing()
    {
        Assert.Null(AiAdviceService.ParseResponse("not json"));
        Assert.Null(AiAdviceService.ParseResponse("""{"choices":[]}"""));
        Assert.Null(AiAdviceService.ParseResponse("""{"choices":[{"message":{}}]}"""));
    }

    [Fact]
    public async Task No_api_key_returns_not_configured_without_call()
    {
        var handler = new CountingHandler(() => throw new InvalidOperationException("must not call"));
        var svc = Service(Config(null), handler);

        var result = await svc.GetAdviceAsync(Sample(), 100, "Thuận lợi");

        Assert.Equal(AiAdviceStatus.NotConfigured, result.Status);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Request_targets_openrouter_with_bearer_key()
    {
        var handler = new CountingHandler(() => new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent(ValidOpenAiJson) });
        var svc = Service(Config(), handler);

        await svc.GetAdviceAsync(Sample(), 100, "Thuận lợi");

        var request = handler.Last!;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Contains("openrouter.ai/api/v1/chat/completions",
            request.RequestUri!.ToString());
        Assert.Equal("Bearer test-key",
            string.Join(' ', request.Headers.GetValues("Authorization")));

        // Liquid free is a reasoning model: reasoning tokens count against the
        // completion budget, so it must exceed the ~1300 tokens a real call uses.
        Assert.Contains("\"max_tokens\":2500", handler.LastBody);
    }

    [Fact]
    public async Task Api_error_fails_and_throttles_retry()
    {
        var handler = new CountingHandler(() => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var svc = Service(Config(), handler);

        var first = await svc.GetAdviceAsync(Sample(), 100, "Thuận lợi");
        var second = await svc.GetAdviceAsync(Sample(), 100, "Thuận lợi");

        Assert.Equal(AiAdviceStatus.Failed, first.Status);
        Assert.Equal(AiAdviceStatus.Failed, second.Status);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Valid_response_is_ok_and_cached_for_same_condition()
    {
        var handler = new CountingHandler(() => new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent(ValidOpenAiJson) });
        var svc = Service(Config(), handler);

        var first = await svc.GetAdviceAsync(Sample(), 100, "Thuận lợi");
        var second = await svc.GetAdviceAsync(Sample(), 100, "Thuận lợi");

        Assert.Equal(AiAdviceStatus.Ok, first.Status);
        Assert.Equal("Trời đang dễ chịu, phù hợp để ra ngoài. Nên mang theo nước.", first.Text);
        Assert.Equal(first.Text, second.Text);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Changed_condition_calls_api_again()
    {
        var handler = new CountingHandler(() => new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent(ValidOpenAiJson) });
        var svc = Service(Config(), handler);

        await svc.GetAdviceAsync(Sample(), 100, "Thuận lợi");
        await svc.GetAdviceAsync(Sample(t: 15, lux: 60), 30, "Cần lưu ý");

        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task Missing_readings_fail_without_call()
    {
        var handler = new CountingHandler(() => throw new InvalidOperationException("must not call"));
        var svc = Service(Config(), handler);

        var result = await svc.GetAdviceAsync(Sample(t: double.NaN), 40, "Cần lưu ý");

        Assert.Equal(AiAdviceStatus.Failed, result.Status);
        Assert.Equal(0, handler.Calls);
    }
}
