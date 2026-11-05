using System.Security.Cryptography;
using System.Text.Json;
using Puzzle.Museum.Places;
using Puzzle.Tooling.WorldMap;

namespace Puzzle.Cli.Commands;

/// <summary>
/// <c>worldmap build</c>: the world map's data file for the game, from Natural Earth's 1:50m land and lakes, with the
/// record of where they came from and under what licence (art/map/sources.json). <c>--fetch</c> first downloads the
/// two source files into the source folder; a source whose checksum differs from the record is refused unless the
/// record is written anew (<c>--rerecord</c>), so the map never changes by accident.
/// </summary>
internal static class WorldMapCommand
{
    private const string DefaultSource = "out/naturalearth";
    private const string DefaultOut = "unity/Assets/Resources/WorldMap.bytes";
    private const string DefaultRecord = "art/map/sources.json";
    private const string Licence = "Public domain (Natural Earth: \"All versions of Natural Earth raster + vector map data found on this website are in the public domain.\")";
    private const string LicenceUrl = "https://www.naturalearthdata.com/about/terms-of-use/";
    private const string Repository = "https://raw.githubusercontent.com/nvkelso/natural-earth-vector/master/geojson/";

    private static readonly string[] SourceFiles = { "ne_50m_land.geojson", "ne_50m_lakes.geojson" };
    /// <summary>The record is read by people: camelCase keys as the other records of art/ have them, and quotes and signs written as they are.</summary>
    private static readonly JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static int Run(CliArgs args)
    {
        string action = args.Required(0, "build");
        if (action != "build")
        {
            throw new UsageException($"worldmap knows only 'build', not '{action}'.");
        }

        string source = args.Text("source", DefaultSource);
        string output = args.Text("out", DefaultOut);
        string recordPath = args.Text("record", DefaultRecord);
        if (args.Flag("fetch"))
        {
            Fetch(source);
        }

        Dictionary<string, SourceFile> files = SourceFiles.ToDictionary(name => name, name => Describe(Path.Combine(source, name)));
        SourceRecord? before = File.Exists(recordPath) ? JsonSerializer.Deserialize<SourceRecord>(File.ReadAllText(recordPath), Indented) : null;
        if (before != null && !args.Flag("rerecord"))
        {
            foreach (SourceFile recorded in before.Sources)
            {
                if (files.TryGetValue(recorded.File, out SourceFile? now) && now.Sha256 != recorded.Sha256)
                {
                    Console.Error.WriteLine($"{recorded.File} is not the file {recordPath} records (sha256 {now.Sha256}, recorded {recorded.Sha256}); pass --rerecord to take it.");
                    return 1;
                }
            }
        }

        WorldMapSettings settings = WorldMapSettings.OfTheDesign;
        WorldMapBuild build = WorldMapBuilder.Build(
            File.ReadAllText(Path.Combine(source, SourceFiles[0])), File.ReadAllText(Path.Combine(source, SourceFiles[1])), settings);
        byte[] bytes = WorldMapFormat.Write(build.Map);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllBytes(output, bytes);

        var record = new SourceRecord(
            About: "The world map of the game's map page (#4): Natural Earth 1:50m land and lakes, projected by Natural Earth I, thinned and triangulated by `dotnet run --project src/Puzzle.Cli -- worldmap build`.",
            Licence: Licence,
            LicenceUrl: LicenceUrl,
            Sources: files.Values.ToList(),
            Settings: settings,
            Output: new OutputFile(output.Replace('\\', '/'), bytes.Length, Sha256Of(bytes)),
            Figures: new Figures(
                build.Map.Land.Rings.Count, build.Map.Land.Points.Count, build.Map.Land.Triangles.Count / 3,
                build.Map.Lakes.Rings.Count, build.Map.Lakes.Points.Count, build.Map.Lakes.Triangles.Count / 3,
                build.PolygonsDropped, build.RingsDropped,
                Math.Round(build.AreaAsTriangles, 1), Math.Round(build.AreaAsRings, 1)));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(recordPath))!);
        File.WriteAllText(recordPath, JsonSerializer.Serialize(record, Indented) + "\n");

        Console.WriteLine(
            $"land: {record.Figures.LandRings} rings, {record.Figures.LandPoints} points, {record.Figures.LandTriangles} triangles; "
            + $"lakes: {record.Figures.LakeRings} rings, {record.Figures.LakePoints} points, {record.Figures.LakeTriangles} triangles; "
            + $"dropped {build.PolygonsDropped} polygons and {build.RingsDropped} holes; area as triangles {build.AreaAsTriangles:0.0} of {build.AreaAsRings:0.0} as rings");
        Console.WriteLine($"{output}: {bytes.Length} bytes, sha256 {record.Output.Sha256}; record -> {recordPath}");
        double mismatch = Math.Abs(build.AreaAsTriangles - build.AreaAsRings) / build.AreaAsRings;
        if (mismatch > 0.002)
        {
            Console.Error.WriteLine($"The triangles cover {mismatch:P2} more or less than the rings enclose: a ring crosses itself.");
            return 1;
        }

        return 0;
    }

    private static void Fetch(string folder)
    {
        Directory.CreateDirectory(folder);
        using var client = new HttpClient();
        foreach (string name in SourceFiles)
        {
            byte[] bytes = client.GetByteArrayAsync(Repository + name).GetAwaiter().GetResult();
            File.WriteAllBytes(Path.Combine(folder, name), bytes);
            Console.WriteLine($"{Repository}{name} -> {folder} ({bytes.Length} bytes)");
        }
    }

    private static SourceFile Describe(string path)
    {
        if (!File.Exists(path))
        {
            throw new UsageException($"{path} is missing: pass --fetch to download it, or --source to name the folder that has it.");
        }

        byte[] bytes = File.ReadAllBytes(path);
        string name = Path.GetFileName(path);
        return new SourceFile(name, Repository + name, bytes.Length, Sha256Of(bytes));
    }

    private static string Sha256Of(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private sealed record SourceRecord(string About, string Licence, string LicenceUrl, List<SourceFile> Sources, WorldMapSettings Settings, OutputFile Output, Figures Figures);

    private sealed record SourceFile(string File, string FetchedFrom, long Bytes, string Sha256);

    private sealed record OutputFile(string File, long Bytes, string Sha256);

    private sealed record Figures(
        int LandRings, int LandPoints, int LandTriangles, int LakeRings, int LakePoints, int LakeTriangles, int PolygonsDropped, int HolesDropped,
        double AreaAsTriangles, double AreaAsRings);
}
