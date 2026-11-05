using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Tooling;

namespace Puzzle.Cli.Commands;

/// <summary><c>validate &lt;dir|file&gt;</c>: every level must parse and produce a starting board.</summary>
internal static class ValidateCommand
{
    private const int SeedsPerLevel = 20;

    public static int Run(CliArgs args)
    {
        string[] files = LevelFiles.Under(args.Required(0, "dir|file"));

        int failures = 0;
        foreach (string file in files)
        {
            string? problem = Check(file);
            Console.WriteLine(problem is null ? $"ok    {file}" : $"FAIL  {file}: {problem}");
            failures += problem is null ? 0 : 1;
        }

        Console.WriteLine($"{files.Length - failures}/{files.Length} levels valid.");
        return failures == 0 ? 0 : 1;
    }

    /// <returns>The problem, or absent when the level is valid.</returns>
    private static string? Check(string file)
    {
        try
        {
            LevelDefinition level = LevelJson.Load(file);
            for (ulong seed = 0; seed < SeedsPerLevel; seed++)
            {
                Game.Start(level, seed);
            }

            return null;
        }
        catch (LevelFormatException ex)
        {
            return ex.Message;
        }
        catch (LevelInitializationException ex)
        {
            return ex.Message;
        }
    }
}
