using Puzzle.Bank.Assembling;
using Puzzle.Core.Levels;
using Puzzle.Tooling;

namespace Puzzle.Cli.Commands;

/// <summary>
/// <c>patch make --shipped &lt;folder&gt; --current &lt;folder&gt; (--previous &lt;pack.json&gt; | --first) --out &lt;pack.json&gt;</c>:
/// the next patch pack, the one that brings every release to the levels as they are now
/// (<see cref="PatchPackBuilder"/>). The pack published before must be named, or <c>--first</c> must say there is
/// none: a pack made without it would give content versions that are already taken.
/// <c>patch apply &lt;pack.json&gt; --shipped &lt;folder&gt; --out &lt;folder&gt;</c>: the levels a player of that release has
/// with the pack, written as level files to look at, validate or play in the lab; it is also how the level folder of
/// the next release is made, since the files carry their content versions.
/// Exit codes: 0 done, 1 an invalid level or pack, 2 usage.
/// </summary>
internal static class PatchCommand
{
    public static int Run(CliArgs args) => args.Required(0, "make|apply") switch
    {
        "make" => Make(args),
        "apply" => Apply(args),
        string other => throw new UsageException($"Unknown patch command '{other}' (use make or apply)."),
    };

    private static int Make(CliArgs args)
    {
        IReadOnlyList<LevelDefinition> shipped = LevelsIn(RequiredOption(args, "shipped"));
        IReadOnlyList<LevelDefinition> current = LevelsIn(RequiredOption(args, "current"));
        PatchPack previous = PreviousPack(args);
        string outPath = RequiredOption(args, "out");

        PatchPackDraft draft = PatchPackBuilder.Build(shipped, current, previous);
        LevelJson.SavePatchPack(draft.Pack, outPath);

        Console.WriteLine($"release {shipped.Count} levels, now {current.Count} levels, previous pack {previous.Sequence} with {previous.Levels.Count} corrections");
        Console.WriteLine($"changed, new content version: {Listed(draft.Changed)}");
        Console.WriteLine($"kept as published:            {Listed(draft.Kept)}");
        if (draft.MissingNow.Count > 0)
        {
            Console.WriteLine($"MISSING now (left as published): {Listed(draft.MissingNow)}");
        }

        if (draft.NotPublished.Count > 0)
        {
            Console.WriteLine($"NOT in the release (a pack never adds levels): {Listed(draft.NotPublished)}");
        }

        Console.WriteLine($"wrote {outPath}: pack {draft.Pack.Sequence}, {draft.Pack.Levels.Count} corrections, {new FileInfo(outPath).Length} bytes");
        return 0;
    }

    private static int Apply(CliArgs args)
    {
        PatchPack pack = LevelJson.LoadPatchPack(args.Required(1, "pack.json"));
        IReadOnlyList<LevelDefinition> shipped = LevelsIn(RequiredOption(args, "shipped"));
        string outFolder = RequiredOption(args, "out");

        PatchedLevels patched = pack.ApplyTo(shipped);
        Directory.CreateDirectory(outFolder);
        foreach (LevelDefinition level in patched.Levels)
        {
            LevelJson.Save(level, Path.Combine(outFolder, LevelFolder.FileNameOf(level.Id.Value)));
        }

        Console.WriteLine($"pack {pack.Sequence} over {shipped.Count} levels");
        Console.WriteLine($"replaced: {Listed(patched.Replaced)}");
        Console.WriteLine($"left aside, the release is as new or newer: {Listed(patched.AlreadyCurrent)}");
        Console.WriteLine($"left aside, not a level of the release: {Listed(patched.Unknown)}");
        Console.WriteLine($"wrote {patched.Levels.Count} levels to {outFolder}");
        return 0;
    }

    /// <exception cref="UsageException">Neither the previous pack nor <c>--first</c> was given, or both were.</exception>
    private static PatchPack PreviousPack(CliArgs args)
    {
        string? previousPath = args.OptionalText("previous");
        if ((previousPath is null) != args.Flag("first"))
        {
            throw new UsageException("Name the pack published before with --previous <pack.json>, or say --first when none was.");
        }

        return previousPath is null ? PatchPack.None : LevelJson.LoadPatchPack(previousPath);
    }

    /// <exception cref="LevelFormatException">A file of the folder is not a valid level, or two files hold the same level number.</exception>
    private static LevelDefinition[] LevelsIn(string folder)
    {
        LevelDefinition[] levels = LevelFiles.Under(folder).Select(LevelJson.Load).ToArray();
        return levels.GroupBy(level => level.Id).FirstOrDefault(group => group.Count() > 1) is { } twice
            ? throw new LevelFormatException($"'{folder}' holds level {twice.Key} {twice.Count()} times.")
            : levels;
    }

    private static string RequiredOption(CliArgs args, string key) =>
        args.OptionalText(key) ?? throw new UsageException($"Missing --{key}.");

    private static string Listed(IReadOnlyList<LevelId> levels) =>
        levels.Count == 0 ? "none" : $"{levels.Count} ({string.Join(' ', levels)})";
}
