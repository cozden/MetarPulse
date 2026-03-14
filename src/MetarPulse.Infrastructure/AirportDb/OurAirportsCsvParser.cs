using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using MetarPulse.Core.Models;
using System.Globalization;

namespace MetarPulse.Infrastructure.AirportDb;

/// <summary>
/// OurAirports (ourairports.com/data/) CSV formatını parse eder.
/// airports.csv ve runways.csv desteklenir.
/// </summary>
public static class OurAirportsCsvParser
{
    private static readonly CsvConfiguration CsvConfig = new(CultureInfo.InvariantCulture)
    {
        HasHeaderRecord = true,
        MissingFieldFound = null,
        BadDataFound = null,
        HeaderValidated = null,
        TrimOptions = TrimOptions.Trim
    };

    public static IEnumerable<Airport> ParseAirports(Stream csvStream)
    {
        using var reader = new StreamReader(csvStream);
        using var csv = new CsvReader(reader, CsvConfig);

        csv.Context.RegisterClassMap<AirportCsvMap>();

        foreach (var record in csv.GetRecords<AirportCsvRecord>())
        {
            // Sadece meydanları al (helipads, seaplane vb. hariç)
            if (string.IsNullOrWhiteSpace(record.Ident)) continue;

            yield return new Airport
            {
                Ident = record.Ident.ToUpper(),
                Type = record.Type ?? "small_airport",
                Name = record.Name ?? record.Ident,
                LatitudeDeg = record.LatitudeDeg,
                LongitudeDeg = record.LongitudeDeg,
                ElevationFt = ParseNullableInt(record.ElevationFt),
                IsoCountry = record.IsoCountry ?? string.Empty,
                Municipality = record.Municipality,
                IataCode = string.IsNullOrWhiteSpace(record.IataCode) ? null : record.IataCode.ToUpper(),
                MagneticVariation = ParseNullableDouble(record.MagneticVariation),
                LastSynced = DateTime.UtcNow
            };
        }
    }

    public static IEnumerable<Runway> ParseRunways(Stream csvStream)
    {
        using var reader = new StreamReader(csvStream);
        using var csv = new CsvReader(reader, CsvConfig);

        csv.Context.RegisterClassMap<RunwayCsvMap>();

        foreach (var record in csv.GetRecords<RunwayCsvRecord>())
        {
            if (string.IsNullOrWhiteSpace(record.AirportIdent)) continue;

            yield return new Runway
            {
                AirportIdent = record.AirportIdent.ToUpper()[..Math.Min(record.AirportIdent.Length, 10)],
                LengthFt = ParseNullableInt(record.LengthFt),
                WidthFt = ParseNullableInt(record.WidthFt),
                Surface = record.Surface is { } s ? s[..Math.Min(s.Length, 50)] : null,
                IsLighted = record.Lighted == "1",
                IsClosed = record.Closed == "1",
                LeIdent = record.LeIdent is { } le ? le[..Math.Min(le.Length, 5)] : null,
                LeHeadingDegT = ParseNullableDouble(record.LeHeadingDegT),
                HeIdent = record.HeIdent is { } he ? he[..Math.Min(he.Length, 5)] : null,
                HeHeadingDegT = ParseNullableDouble(record.HeHeadingDegT)
            };
        }
    }

    private static int? ParseNullableInt(string? value)
        => int.TryParse(value, out var result) ? result : null;

    private static double? ParseNullableDouble(string? value)
        => double.TryParse(value, CultureInfo.InvariantCulture, out var result) ? result : null;
}

// ─── CSV Record sınıfları ──────────────────────────────────────────────────

internal class AirportCsvRecord
{
    [Name("ident")] public string? Ident { get; set; }
    [Name("type")] public string? Type { get; set; }
    [Name("name")] public string? Name { get; set; }
    [Name("latitude_deg")] public double LatitudeDeg { get; set; }
    [Name("longitude_deg")] public double LongitudeDeg { get; set; }
    [Name("elevation_ft")] public string? ElevationFt { get; set; }
    [Name("iso_country")] public string? IsoCountry { get; set; }
    [Name("municipality")] public string? Municipality { get; set; }
    [Name("iata_code")] public string? IataCode { get; set; }
    [Name("magnetic_variation")] public string? MagneticVariation { get; set; }
}

internal class RunwayCsvRecord
{
    [Name("airport_ident")] public string? AirportIdent { get; set; }
    [Name("length_ft")] public string? LengthFt { get; set; }
    [Name("width_ft")] public string? WidthFt { get; set; }
    [Name("surface")] public string? Surface { get; set; }
    [Name("lighted")] public string? Lighted { get; set; }
    [Name("closed")] public string? Closed { get; set; }
    [Name("le_ident")] public string? LeIdent { get; set; }
    [Name("le_heading_degT")] public string? LeHeadingDegT { get; set; }
    [Name("he_ident")] public string? HeIdent { get; set; }
    [Name("he_heading_degT")] public string? HeHeadingDegT { get; set; }
}

// ─── CsvHelper ClassMaps ───────────────────────────────────────────────────

internal class AirportCsvMap : ClassMap<AirportCsvRecord>
{
    public AirportCsvMap() => AutoMap(CultureInfo.InvariantCulture);
}

internal class RunwayCsvMap : ClassMap<RunwayCsvRecord>
{
    public RunwayCsvMap() => AutoMap(CultureInfo.InvariantCulture);
}
