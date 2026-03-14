using MetarPulse.Core.Enums;
using MetarPulse.Core.Services;

namespace MetarPulse.Core.Tests;

public class TafParserTests
{
    [Fact]
    public void Parse_StationId()
    {
        var t = TafParser.Parse("TAF LTFM 121100Z 1212/1318 02010KT 9999 FEW030");
        Assert.Equal("LTFM", t.StationId);
    }

    [Fact]
    public void Parse_BasePeriod_Wind()
    {
        var t = TafParser.Parse("TAF LTFM 121100Z 1212/1318 02010KT 9999 FEW030");
        Assert.Single(t.Periods);
        var p = t.Periods[0];
        Assert.Equal(20, p.WindDirection);
        Assert.Equal(10, p.WindSpeed);
    }

    [Fact]
    public void Parse_BasePeriod_Visibility()
    {
        var t = TafParser.Parse("TAF LTFM 121100Z 1212/1318 02010KT 9999 FEW030");
        Assert.Equal(10000, t.Periods[0].VisibilityMeters);
    }

    [Fact]
    public void Parse_BasePeriod_Clouds()
    {
        var t = TafParser.Parse("TAF LTFM 121100Z 1212/1318 02010KT 9999 FEW030 SCT080");
        var p = t.Periods[0];
        Assert.Equal(2, p.CloudLayers.Count);
        Assert.Equal(CloudCoverage.FEW, p.CloudLayers[0].Coverage);
        Assert.Equal(3000, p.CloudLayers[0].AltitudeFt);
        Assert.Equal(CloudCoverage.SCT, p.CloudLayers[1].Coverage);
    }

    [Fact]
    public void Parse_BecmgGroup_Present()
    {
        var t = TafParser.Parse(
            "TAF LTFM 121100Z 1212/1318 02010KT 9999 FEW030 " +
            "BECMG 1215/1217 05015G25KT 7000 SCT020");

        Assert.Equal(2, t.Periods.Count);
        var becmg = t.Periods[1];
        Assert.Equal("BECMG", becmg.ChangeIndicator);
        Assert.Equal(50, becmg.WindDirection);
        Assert.Equal(15, becmg.WindSpeed);
        Assert.Equal(25, becmg.WindGust);
    }

    [Fact]
    public void Parse_TempoGroup_Present()
    {
        var t = TafParser.Parse(
            "TAF EGLL 121100Z 1212/1318 28010KT 9999 FEW025 " +
            "TEMPO 1215/1221 4000 TSRA BKN020CB");

        var tempo = t.Periods.First(p => p.ChangeIndicator == "TEMPO");
        Assert.NotNull(tempo);
        Assert.Equal(4000, tempo.VisibilityMeters);
        Assert.Single(tempo.WeatherConditions);
        Assert.Equal(WeatherDescriptor.TS, tempo.WeatherConditions[0].Descriptor);
        Assert.Single(tempo.CloudLayers);
        Assert.Equal(CloudType.CB, tempo.CloudLayers[0].Type);
    }

    [Fact]
    public void Parse_FmGroup_Present()
    {
        var t = TafParser.Parse(
            "TAF UUEE 121100Z 1212/1318 27015MPS 9999 SCT030 " +
            "FM121800 27010MPS 9999 FEW020");

        var fm = t.Periods.First(p => p.ChangeIndicator == "FM");
        Assert.NotNull(fm);
        // 10 MPS → ~19 KT
        Assert.True(fm.WindSpeed >= 18 && fm.WindSpeed <= 21);
    }

    [Fact]
    public void Parse_Prob30Tempo()
    {
        var t = TafParser.Parse(
            "TAF KJFK 121100Z 1212/1318 28015KT 9999 FEW040 " +
            "PROB30 TEMPO 1218/1224 3000 TSRA BKN025CB");

        var prob = t.Periods.FirstOrDefault(p => p.Probability == 30);
        Assert.NotNull(prob);
        Assert.Equal(30, prob!.Probability);
        Assert.Equal("TEMPO", prob.ChangeIndicator);
    }

    [Fact]
    public void Parse_MultipleChangeGroups()
    {
        var t = TafParser.Parse(
            "TAF LTFM 121100Z 1212/1318 02010KT 9999 FEW030 " +
            "BECMG 1214/1216 05020KT " +
            "TEMPO 1218/1224 3000 TSRA SCT020CB " +
            "BECMG 1300/1302 02010KT 9999 SKC");

        // BASE + 2x BECMG + 1x TEMPO = 4
        Assert.Equal(4, t.Periods.Count);
    }

    [Fact]
    public void Parse_Amd_Prefix()
    {
        var t = TafParser.Parse("TAF AMD LTFM 121100Z 1212/1318 00000KT CAVOK");
        Assert.Equal("LTFM", t.StationId);
    }

    [Fact]
    public void Parse_ValidityPeriod_Parsed()
    {
        var t = TafParser.Parse("TAF LTFM 121100Z 1212/1318 02010KT 9999 FEW030");
        Assert.Equal(12, t.ValidFrom.Hour);
        Assert.Equal(18, t.ValidTo.Hour);
    }
}
