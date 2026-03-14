using MetarPulse.Core.Enums;
using MetarPulse.Core.Models;
using MetarPulse.Core.Services;

namespace MetarPulse.Core.Tests;

public class MetarDiffEngineTests
{
    private static Metar MakeMetar(
        int vis = 10000,
        int ceiling = 99999,
        FlightCategory? category = null,
        int windDir = 0, int windSpeed = 5, int? windGust = null,
        bool varWind = false,
        List<WeatherCondition>? wx = null)
    {
        var clouds = ceiling < 99999
            ? new List<CloudLayer> { new() { Coverage = CloudCoverage.BKN, AltitudeFt = ceiling } }
            : new List<CloudLayer>();

        var m = new Metar
        {
            StationId = "LTFM",
            VisibilityMeters = vis,
            CeilingFeet = ceiling,
            WindDirection = windDir,
            WindSpeed = windSpeed,
            WindGust = windGust,
            IsVariableWind = varWind,
            CloudLayers = clouds,
            WeatherConditions = wx ?? new List<WeatherCondition>()
        };
        m.Category = category ?? FlightCategoryResolver.Resolve(vis, ceiling);
        return m;
    }

    [Fact]
    public void Compare_CategoryChanged_VFR_to_IFR()
    {
        var old = MakeMetar(vis: 10000, ceiling: 5000);
        var neo = MakeMetar(vis: 3000, ceiling: 800);

        var diff = MetarDiffEngine.Compare(old, neo);

        Assert.True(diff.CategoryChanged);
        Assert.Equal(FlightCategory.VFR, diff.OldCategory);
        Assert.Equal(FlightCategory.IFR, diff.NewCategory);
    }

    [Fact]
    public void Compare_IsDeteriorating_WhenCategoryWorsens()
    {
        var old = MakeMetar(vis: 10000, ceiling: 5000);  // VFR
        var neo = MakeMetar(vis: 800, ceiling: 400);      // LIFR

        var diff = MetarDiffEngine.Compare(old, neo);

        Assert.True(diff.IsDeteriorating);
        Assert.False(diff.IsImproving);
    }

    [Fact]
    public void Compare_IsImproving_WhenCategoryImproves()
    {
        var old = MakeMetar(vis: 800, ceiling: 400);      // LIFR
        var neo = MakeMetar(vis: 10000, ceiling: 5000);   // VFR

        var diff = MetarDiffEngine.Compare(old, neo);

        Assert.True(diff.IsImproving);
        Assert.False(diff.IsDeteriorating);
    }

    [Fact]
    public void Compare_VisibilityChanged_WhenDeltaAboveThreshold()
    {
        var old = MakeMetar(vis: 5000);
        var neo = MakeMetar(vis: 3000);  // Δ = 2000m > 1000m eşik

        var diff = MetarDiffEngine.Compare(old, neo);

        Assert.True(diff.VisibilityChanged);
    }

    [Fact]
    public void Compare_VisibilityNotChanged_WhenDeltaBelowThreshold()
    {
        var old = MakeMetar(vis: 5000);
        var neo = MakeMetar(vis: 5200);  // Δ = 200m < 1000m eşik

        var diff = MetarDiffEngine.Compare(old, neo);

        Assert.False(diff.VisibilityChanged);
    }

    [Fact]
    public void Compare_CeilingChanged_WhenDeltaAboveThreshold()
    {
        var old = MakeMetar(ceiling: 3000);
        var neo = MakeMetar(ceiling: 2000);  // Δ = 1000ft > 500ft eşik

        var diff = MetarDiffEngine.Compare(old, neo);

        Assert.True(diff.CeilingChanged);
    }

    [Fact]
    public void Compare_WindChanged_SpeedDelta()
    {
        var old = MakeMetar(windSpeed: 10);
        var neo = MakeMetar(windSpeed: 20);  // Δ = 10kt > 5kt eşik

        var diff = MetarDiffEngine.Compare(old, neo);

        Assert.True(diff.WindChanged);
    }

    [Fact]
    public void Compare_WindChanged_DirectionDelta()
    {
        var old = MakeMetar(windDir: 360, windSpeed: 10);
        var neo = MakeMetar(windDir: 90, windSpeed: 10);  // 90° fark > 30° eşik

        var diff = MetarDiffEngine.Compare(old, neo);

        Assert.True(diff.WindChanged);
    }

    [Fact]
    public void Compare_WindNotChanged_SmallDelta()
    {
        var old = MakeMetar(windDir: 180, windSpeed: 10);
        var neo = MakeMetar(windDir: 190, windSpeed: 12);  // Yön Δ=10°, hız Δ=2kt

        var diff = MetarDiffEngine.Compare(old, neo);

        Assert.False(diff.WindChanged);
    }

    [Fact]
    public void Compare_SignificantWeatherChanged_ThunderstormAdded()
    {
        var old = MakeMetar();
        var neo = MakeMetar(wx: new List<WeatherCondition>
        {
            new() { Descriptor = WeatherDescriptor.TS, Phenomena = new() { WeatherPhenomenon.RA } }
        });

        var diff = MetarDiffEngine.Compare(old, neo);

        Assert.True(diff.SignificantWeatherChanged);
    }

    [Fact]
    public void Compare_SignificantWeatherNotChanged_BRvsHZ()
    {
        // BR ve HZ significant wx listesinde yok
        var old = MakeMetar(wx: new List<WeatherCondition>
        {
            new() { Phenomena = new() { WeatherPhenomenon.BR } }
        });
        var neo = MakeMetar(wx: new List<WeatherCondition>
        {
            new() { Phenomena = new() { WeatherPhenomenon.HZ } }
        });

        var diff = MetarDiffEngine.Compare(old, neo);

        Assert.False(diff.SignificantWeatherChanged);
    }

    [Fact]
    public void Compare_ChangeSummary_NotEmpty_WhenCategoryChanges()
    {
        var old = MakeMetar(vis: 10000, ceiling: 5000);
        var neo = MakeMetar(vis: 500, ceiling: 200);

        var diff = MetarDiffEngine.Compare(old, neo);

        Assert.NotEmpty(diff.ChangeSummary);
        Assert.Contains(diff.ChangeSummary, s => s.Contains("kategori") || s.Contains("Görüş") || s.Contains("Tavan"));
    }

    [Fact]
    public void Compare_NoChanges_WhenIdenticalConditions()
    {
        var old = MakeMetar(vis: 10000, ceiling: 5000, windSpeed: 10, windDir: 180);
        var neo = MakeMetar(vis: 10000, ceiling: 5000, windSpeed: 10, windDir: 180);

        var diff = MetarDiffEngine.Compare(old, neo);

        Assert.False(diff.CategoryChanged);
        Assert.False(diff.WindChanged);
        Assert.False(diff.VisibilityChanged);
        Assert.False(diff.CeilingChanged);
        Assert.False(diff.SignificantWeatherChanged);
        Assert.Empty(diff.ChangeSummary);
    }

    [Fact]
    public void Compare_WindDirection_WrapsAt360()
    {
        // 350° → 010° = 20° fark, eşiğin (30°) altında
        var old = MakeMetar(windDir: 350, windSpeed: 10);
        var neo = MakeMetar(windDir: 10, windSpeed: 10);

        var diff = MetarDiffEngine.Compare(old, neo);

        Assert.False(diff.WindChanged);
    }
}
