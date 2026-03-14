using MetarPulse.Core.Enums;
using MetarPulse.Core.Services;

namespace MetarPulse.Core.Tests;

public class MetarParserTests
{
    // ── Temel alanlar ────────────────────────────────────────────────────────

    [Fact]
    public void Parse_StationId_IsExtractedCorrectly()
    {
        var m = MetarParser.Parse("METAR LTFM 121150Z 00000KT 9999 SKC 20/10 Q1015");
        Assert.Equal("LTFM", m.StationId);
    }

    [Fact]
    public void Parse_IsSpeci_WhenSpeciPrefix()
    {
        var m = MetarParser.Parse("SPECI LTAI 121200Z 00000KT 9999 SKC 25/14 Q1013");
        Assert.True(m.IsSpeci);
    }

    [Fact]
    public void Parse_NotSpeci_WhenMetarPrefix()
    {
        var m = MetarParser.Parse("METAR LTFM 121150Z 00000KT 9999 SKC 20/10 Q1015");
        Assert.False(m.IsSpeci);
    }

    // ── Rüzgar ───────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_Wind_DirectionAndSpeed()
    {
        var m = MetarParser.Parse("METAR LTFM 121150Z 02010KT 9999 FEW020 20/10 Q1015");
        Assert.Equal(20, m.WindDirection);
        Assert.Equal(10, m.WindSpeed);
        Assert.Null(m.WindGust);
    }

    [Fact]
    public void Parse_Wind_WithGust()
    {
        var m = MetarParser.Parse("METAR EGLL 121550Z 28015G28KT 9999 SCT025 17/09 Q1012");
        Assert.Equal(280, m.WindDirection);
        Assert.Equal(15, m.WindSpeed);
        Assert.Equal(28, m.WindGust);
    }

    [Fact]
    public void Parse_Wind_Variable()
    {
        var m = MetarParser.Parse("METAR LTFM 121150Z VRB03KT 9999 SKC 22/14 Q1014");
        Assert.True(m.IsVariableWind);
        Assert.Equal(3, m.WindSpeed);
    }

    [Fact]
    public void Parse_Wind_VariableSector()
    {
        var m = MetarParser.Parse("METAR LTFM 121150Z 02008KT 350V050 9999 FEW020 20/10 Q1015");
        Assert.True(m.IsVariableWind);
        Assert.Equal(350, m.VariableWindFrom);
        Assert.Equal(50, m.VariableWindTo);
    }

    [Fact]
    public void Parse_Wind_Calm()
    {
        var m = MetarParser.Parse("METAR LTFM 121150Z 00000KT 9999 SKC 18/08 Q1018");
        Assert.Equal(0, m.WindDirection);
        Assert.Equal(0, m.WindSpeed);
    }

    [Fact]
    public void Parse_Wind_MpsConverted()
    {
        // 10 MPS ≈ 19 KT
        var m = MetarParser.Parse("METAR UUEE 121150Z 27010MPS 9999 SCT030 05/M02 Q1025");
        Assert.True(m.WindSpeed >= 18 && m.WindSpeed <= 21);
    }

    // ── Görüş ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_Visibility_9999()
    {
        var m = MetarParser.Parse("METAR LTFM 121150Z 00000KT 9999 SKC 20/10 Q1015");
        Assert.Equal(10000, m.VisibilityMeters);
    }

    [Fact]
    public void Parse_Visibility_Cavok()
    {
        var m = MetarParser.Parse("METAR LTAI 121200Z 00000KT CAVOK 25/14 Q1013");
        Assert.Equal(10000, m.VisibilityMeters);
    }

    [Fact]
    public void Parse_Visibility_Low()
    {
        var m = MetarParser.Parse("METAR EGLL 121150Z 00000KT 0200 FG OVC001 10/09 Q1008");
        Assert.Equal(200, m.VisibilityMeters);
    }

    [Fact]
    public void Parse_Visibility_SmFormat()
    {
        // 10SM = 16093m
        var m = MetarParser.Parse("METAR KJFK 121151Z 00000KT 10SM SKC 20/10 A3005");
        Assert.True(m.VisibilityMeters >= 16000);
    }

    [Fact]
    public void Parse_Visibility_FractionalSm()
    {
        // 1/4SM = ~402m
        var m = MetarParser.Parse("METAR KJFK 121151Z 00000KT 1/4SM FG OVC002 10/09 A3000");
        Assert.True(m.VisibilityMeters >= 300 && m.VisibilityMeters <= 500);
    }

    // ── Bulut ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_Clouds_MultipleLayers()
    {
        var m = MetarParser.Parse("METAR LTFM 121150Z 00000KT 9999 FEW020 SCT060 BKN100 20/10 Q1015");
        Assert.Equal(3, m.CloudLayers.Count);
        Assert.Equal(CloudCoverage.FEW, m.CloudLayers[0].Coverage);
        Assert.Equal(2000, m.CloudLayers[0].AltitudeFt);
        Assert.Equal(CloudCoverage.SCT, m.CloudLayers[1].Coverage);
        Assert.Equal(6000, m.CloudLayers[1].AltitudeFt);
        Assert.Equal(CloudCoverage.BKN, m.CloudLayers[2].Coverage);
        Assert.Equal(10000, m.CloudLayers[2].AltitudeFt);
    }

    [Fact]
    public void Parse_Clouds_CbType()
    {
        var m = MetarParser.Parse("METAR LTFM 121150Z 18010KT 4000 TSRA SCT025CB 22/18 Q1005");
        Assert.Single(m.CloudLayers);
        Assert.Equal(CloudType.CB, m.CloudLayers[0].Type);
        Assert.Equal(2500, m.CloudLayers[0].AltitudeFt);
    }

    [Fact]
    public void Parse_Clouds_TcuType()
    {
        var m = MetarParser.Parse("METAR LTAI 121200Z 00000KT 8000 FEW030TCU 28/18 Q1012");
        Assert.Equal(CloudType.TCU, m.CloudLayers[0].Type);
    }

    [Fact]
    public void Parse_Clouds_Overcast()
    {
        var m = MetarParser.Parse("METAR EGLL 121150Z 00000KT 0200 FG OVC001 10/09 Q1008");
        Assert.Equal(CloudCoverage.OVC, m.CloudLayers[0].Coverage);
        Assert.Equal(100, m.CloudLayers[0].AltitudeFt);
    }

    // ── Sıcaklık ─────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_Temperature_Positive()
    {
        var m = MetarParser.Parse("METAR LTFM 121150Z 00000KT 9999 SKC 20/10 Q1015");
        Assert.Equal(20, m.Temperature);
        Assert.Equal(10, m.DewPoint);
    }

    [Fact]
    public void Parse_Temperature_Negative()
    {
        var m = MetarParser.Parse("METAR UUEE 121150Z 00000KT 9999 SKC M05/M12 Q1035");
        Assert.Equal(-5, m.Temperature);
        Assert.Equal(-12, m.DewPoint);
    }

    [Fact]
    public void Parse_Temperature_MixedSign()
    {
        var m = MetarParser.Parse("METAR EGLL 121150Z 00000KT 9999 SCT020 02/M01 Q1008");
        Assert.Equal(2, m.Temperature);
        Assert.Equal(-1, m.DewPoint);
    }

    // ── Basınç ────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_Altimeter_QNH()
    {
        var m = MetarParser.Parse("METAR LTFM 121150Z 00000KT 9999 SKC 20/10 Q1015");
        Assert.Equal(1015m, m.AltimeterHpa);
        Assert.Null(m.AltimeterInHg);
    }

    [Fact]
    public void Parse_Altimeter_InHg()
    {
        var m = MetarParser.Parse("METAR KJFK 121151Z 00000KT 10SM SKC 20/10 A2992");
        Assert.Equal(29.92m, m.AltimeterInHg);
        Assert.Null(m.AltimeterHpa);
    }

    // ── Hava durumu fenomenleri ───────────────────────────────────────────────

    [Fact]
    public void Parse_Weather_Rain()
    {
        var m = MetarParser.Parse("METAR LTFM 121150Z 00000KT 5000 -RA BKN020 18/15 Q1010");
        Assert.Single(m.WeatherConditions);
        Assert.Equal(WeatherIntensity.Light, m.WeatherConditions[0].Intensity);
        Assert.Contains(WeatherPhenomenon.RA, m.WeatherConditions[0].Phenomena);
    }

    [Fact]
    public void Parse_Weather_HeavyThunderstorm()
    {
        var m = MetarParser.Parse("METAR LTFM 121150Z 18010KT 2000 +TSRA BKN015CB 22/20 Q1003");
        Assert.Single(m.WeatherConditions);
        Assert.Equal(WeatherIntensity.Heavy, m.WeatherConditions[0].Intensity);
        Assert.Equal(WeatherDescriptor.TS, m.WeatherConditions[0].Descriptor);
        Assert.Contains(WeatherPhenomenon.RA, m.WeatherConditions[0].Phenomena);
    }

    [Fact]
    public void Parse_Weather_Fog()
    {
        var m = MetarParser.Parse("METAR EGLL 121150Z 00000KT 0200 FG OVC001 10/09 Q1008");
        Assert.Single(m.WeatherConditions);
        Assert.Contains(WeatherPhenomenon.FG, m.WeatherConditions[0].Phenomena);
    }

    [Fact]
    public void Parse_Weather_FreezingFog()
    {
        var m = MetarParser.Parse("METAR UUEE 121150Z 00000KT 0100 FZFG OVC001 M01/M02 Q1030");
        Assert.Single(m.WeatherConditions);
        Assert.Equal(WeatherDescriptor.FZ, m.WeatherConditions[0].Descriptor);
        Assert.Contains(WeatherPhenomenon.FG, m.WeatherConditions[0].Phenomena);
    }

    [Fact]
    public void Parse_Weather_Snow()
    {
        var m = MetarParser.Parse("METAR UUEE 121150Z 00000KT 1500 SN OVC008 M03/M05 Q1028");
        Assert.Single(m.WeatherConditions);
        Assert.Contains(WeatherPhenomenon.SN, m.WeatherConditions[0].Phenomena);
    }

    [Fact]
    public void Parse_Weather_MultiplePhenomena()
    {
        var m = MetarParser.Parse("METAR LTFM 121150Z 00000KT 4000 RASN SCT020 05/03 Q1010");
        Assert.Single(m.WeatherConditions);
        Assert.Contains(WeatherPhenomenon.RA, m.WeatherConditions[0].Phenomena);
        Assert.Contains(WeatherPhenomenon.SN, m.WeatherConditions[0].Phenomena);
    }

    [Fact]
    public void Parse_Weather_Vicinity()
    {
        var m = MetarParser.Parse("METAR LTAI 121200Z 00000KT 9999 VCTS FEW030CB 28/18 Q1012");
        Assert.Single(m.WeatherConditions);
        Assert.Equal(WeatherIntensity.Vicinity, m.WeatherConditions[0].Intensity);
    }

    // ── Uçuş kategorisi ──────────────────────────────────────────────────────

    [Fact]
    public void Parse_Category_VFR()
    {
        var m = MetarParser.Parse("METAR LTFM 121150Z 00000KT 9999 FEW050 20/10 Q1015");
        Assert.Equal(FlightCategory.VFR, m.Category);
    }

    [Fact]
    public void Parse_Category_MVFR_Ceiling()
    {
        // Tavan 2500ft → MVFR
        var m = MetarParser.Parse("METAR LTFM 121150Z 00000KT 9999 BKN025 15/12 Q1012");
        Assert.Equal(FlightCategory.MVFR, m.Category);
    }

    [Fact]
    public void Parse_Category_IFR_Ceiling()
    {
        // Tavan 800ft → IFR
        var m = MetarParser.Parse("METAR EGLL 121150Z 00000KT 5000 BKN008 10/09 Q1008");
        Assert.Equal(FlightCategory.IFR, m.Category);
    }

    [Fact]
    public void Parse_Category_LIFR_Fog()
    {
        // Görüş 200m + OVC 100ft → LIFR
        var m = MetarParser.Parse("METAR EGLL 121150Z 00000KT 0200 FG OVC001 10/09 Q1008");
        Assert.Equal(FlightCategory.LIFR, m.Category);
    }

    [Fact]
    public void Parse_Category_CAVOK_IsVFR()
    {
        var m = MetarParser.Parse("METAR LTAI 121200Z 00000KT CAVOK 25/14 Q1013");
        Assert.Equal(FlightCategory.VFR, m.Category);
    }

    // ── Trend ────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_Trend_Nosig()
    {
        var m = MetarParser.Parse("METAR LTFM 121150Z 00000KT 9999 SKC 20/10 Q1015 NOSIG");
        Assert.Equal("NOSIG", m.Trend);
    }

    [Fact]
    public void Parse_Trend_Becmg()
    {
        var m = MetarParser.Parse("METAR EGLL 121150Z 00000KT 5000 BR SCT010 12/10 Q1010 BECMG 9999 NSC");
        Assert.Equal("BECMG", m.Trend);
    }

    // ── RVR ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_Rvr_Stored()
    {
        // ICAO formatı: görüş → RVR → weather → bulut
        var m = MetarParser.Parse("METAR EGLL 121150Z 00000KT 0600 R28R/0600FT FG OVC002 10/09 Q1008");
        Assert.NotNull(m.RvrRaw);
        Assert.Contains("R28R", m.RvrRaw);
    }

    // ── Global istasyonlar ───────────────────────────────────────────────────

    [Fact]
    public void Parse_YSSY_SydneyAustralia()
    {
        var m = MetarParser.Parse("METAR YSSY 121130Z 12015KT 9999 FEW030 22/14 Q1018");
        Assert.Equal("YSSY", m.StationId);
        Assert.Equal(120, m.WindDirection);
        Assert.Equal(15, m.WindSpeed);
        Assert.Equal(FlightCategory.VFR, m.Category);
    }

    [Fact]
    public void Parse_RJTT_TokyoJapan()
    {
        var m = MetarParser.Parse("METAR RJTT 121200Z 23010KT 200V260 9999 FEW015 SCT030 18/10 Q1016");
        Assert.Equal("RJTT", m.StationId);
        Assert.True(m.IsVariableWind);
        Assert.Equal(200, m.VariableWindFrom);
        Assert.Equal(260, m.VariableWindTo);
    }

    [Fact]
    public void Parse_AUTO_TokenSkipped()
    {
        var m = MetarParser.Parse("METAR EGLL 121150Z AUTO 00000KT 9999 SKC 12/08 Q1012");
        Assert.Equal("EGLL", m.StationId);
        Assert.Equal(0, m.WindSpeed);
    }

    [Fact]
    public void Parse_RemarksSectionIgnored()
    {
        // RMK bölümünden sonraki içerik yoksayılmalı
        var m = MetarParser.Parse("METAR KJFK 121151Z 25015KT 10SM FEW035 22/10 A2998 RMK AO2 SLP143");
        Assert.Equal("KJFK", m.StationId);
        Assert.Equal(250, m.WindDirection);
        Assert.Equal(15, m.WindSpeed);
    }
}
