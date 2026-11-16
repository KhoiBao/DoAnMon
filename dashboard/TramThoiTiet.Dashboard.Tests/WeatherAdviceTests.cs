using TramThoiTiet.Dashboard.Services;
using Xunit;

namespace TramThoiTiet.Dashboard.Tests;

// Rules under test (weights approved 2026-11-16; comment format updated 2026-11-16):
//   T_score = 100 in [24,30] degC, minus 10 per degC outside, clamped [0,100]
//   L_score = 100 for 200-600 lx, 70 for >=600 (glare) or <50 (dark),
//             60 for 50-200 (overcast)
//   S = 0.6*T_score + 0.4*L_score
//   Level: >=75 Thuan loi, >=50 Binh thuong, else Can luu y; NaN -> no verdict
//   Comment: temperature band + one recommendation; light detail not shown
public class WeatherAdviceTests
{
    [Fact]
    public void Comfortable_temp_and_moderate_sun_scores_100()
    {
        var a = WeatherAdvice.Assess(27, 400);
        Assert.Equal(100, a.Score);
        Assert.Equal("Thuận lợi", a.Level);
        Assert.Contains("dễ chịu", a.Comment);
        Assert.DoesNotContain("Ánh sáng", a.Comment);
    }

    [Fact]
    public void Hot_and_glaring_sun_scores_70_normal()
    {
        var a = WeatherAdvice.Assess(33, 820);
        Assert.Equal(70, a.Score);
        Assert.Equal("Bình thường", a.Level);
        Assert.Contains("nóng", a.Comment);
        Assert.Contains("giữa trưa", a.Comment);
        Assert.DoesNotContain("Ánh sáng", a.Comment);
    }

    [Fact]
    public void Very_hot_temp_scores_40_weighted_52()
    {
        var a = WeatherAdvice.Assess(36, 800);
        Assert.Equal(52, a.Score);
        Assert.Equal("Bình thường", a.Level);
        Assert.Contains("rất nóng", a.Comment);
    }

    [Fact]
    public void Cold_and_overcast_scores_30_attention()
    {
        var a = WeatherAdvice.Assess(15, 60);
        Assert.Equal(30, a.Score);
        Assert.Equal("Cần lưu ý", a.Level);
        Assert.Contains("lạnh", a.Comment);
        Assert.Contains("giữ ấm", a.Comment);
        Assert.DoesNotContain("Ánh sáng", a.Comment);
    }

    [Fact]
    public void Dark_night_is_not_penalized_hard()
    {
        var a = WeatherAdvice.Assess(25, 30);
        Assert.Equal(88, a.Score);
        Assert.Equal("Thuận lợi", a.Level);
        Assert.Contains("tối", a.Comment);
    }

    [Fact]
    public void Comfort_band_boundaries_score_100()
    {
        Assert.Equal(100, WeatherAdvice.Assess(24, 200).Score);
        Assert.Equal(100, WeatherAdvice.Assess(30, 599).Score);
    }

    [Fact]
    public void Missing_temperature_gives_no_verdict()
    {
        var a = WeatherAdvice.Assess(double.NaN, 400);
        Assert.Null(a.Score);
        Assert.Equal("Không đủ dữ liệu", a.Level);
    }

    [Fact]
    public void Missing_light_gives_no_verdict()
    {
        var a = WeatherAdvice.Assess(27, double.NaN);
        Assert.Null(a.Score);
        Assert.Equal("Không đủ dữ liệu", a.Level);
    }

    [Fact]
    public void Light_band_buckets_feed_the_ai_cache_key()
    {
        Assert.Equal("tối", WeatherAdvice.LightBand(30));
        Assert.Equal("âm u", WeatherAdvice.LightBand(100));
        Assert.Equal("nắng vừa", WeatherAdvice.LightBand(400));
        Assert.Equal("nắng gắt", WeatherAdvice.LightBand(900));
    }
}
