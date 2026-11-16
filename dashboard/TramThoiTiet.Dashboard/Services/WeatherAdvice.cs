namespace TramThoiTiet.Dashboard.Services;

public sealed record WeatherAdviceResult(int? Score, string Level, string Comment);

// Weighted verdict for the current conditions: temperature (60%) + light (40%).
// All thresholds live here so the model can be tuned in one place and cited
// verbatim in the course report.
public static class WeatherAdvice
{
    private const double TempWeight = 0.6;
    private const double LightWeight = 0.4;
    private const double TempComfortMin = 24;
    private const double TempComfortMax = 30;
    private const double TempPenaltyPerDeg = 10;
    private const double DarkMaxLux = 50;
    private const double OvercastMaxLux = 200;
    private const double GlareMinLux = 600;

    public static WeatherAdviceResult Assess(double nhietDo, double anhSang)
    {
        if (double.IsNaN(nhietDo) || double.IsNaN(anhSang))
        {
            return new WeatherAdviceResult(null, "Không đủ dữ liệu",
                "Chưa đủ dữ liệu nhiệt độ/ánh sáng.");
        }

        var tScore = TempScore(nhietDo);
        var lScore = LightScore(anhSang);
        var score = (int)Math.Round(TempWeight * tScore + LightWeight * lScore,
            MidpointRounding.AwayFromZero);
        var level = score >= 75 ? "Thuận lợi" : score >= 50 ? "Bình thường" : "Cần lưu ý";
        return new WeatherAdviceResult(score, level,
            $"Nhiệt độ {nhietDo:0.#}°C ({TempBand(nhietDo)}). {Recommend(nhietDo, anhSang)}");
    }

    private static double TempScore(double t)
    {
        if (t >= TempComfortMin && t <= TempComfortMax) return 100;
        var distance = t < TempComfortMin ? TempComfortMin - t : t - TempComfortMax;
        return Math.Clamp(100 - distance * TempPenaltyPerDeg, 0, 100);
    }

    private static double LightScore(double lux) => lux switch
    {
        >= GlareMinLux => 70,
        >= OvercastMaxLux => 100,
        >= DarkMaxLux => 60,
        _ => 70,
    };

    // Public because the AI advice layer uses the bands as its cache key.
    public static string TempBand(double t) => t switch
    {
        < 20 => "lạnh",
        < 25 => "mát",
        < 32 => "dễ chịu",
        < 35 => "nóng",
        _ => "rất nóng",
    };

    public static string LightBand(double lux) => lux switch
    {
        < DarkMaxLux => "tối",
        < OvercastMaxLux => "âm u",
        < GlareMinLux => "nắng vừa",
        _ => "nắng gắt",
    };

    private static string Recommend(double t, double lux)
    {
        if (t >= 32) return "Hạn chế ra ngoài giữa trưa, bổ sung nước.";
        if (t < 20) return "Trời lạnh, giữ ấm khi ra ngoài.";
        if (lux < DarkMaxLux) return "Trời tối — ánh sáng không phản ánh điều kiện ban ngày.";
        if (lux >= GlareMinLux) return "Nắng gắt, nên che chắn khi hoạt động lâu ngoài trời.";
        if (lux < OvercastMaxLux) return "Trời âm u, nhiều mây.";
        return "Điều kiện dễ chịu, thuận lợi cho hoạt động ngoài trời.";
    }
}
