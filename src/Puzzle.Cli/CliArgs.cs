using System.Globalization;

namespace Puzzle.Cli;

/// <summary>Minimal "command positional --key value" parser; the CLI has too few options to justify a package.</summary>
internal sealed class CliArgs
{
    private readonly Dictionary<string, string> _options;

    private CliArgs(string command, IReadOnlyList<string> positionals, Dictionary<string, string> options)
    {
        Command = command;
        Positionals = positionals;
        _options = options;
    }

    public string Command { get; }

    public IReadOnlyList<string> Positionals { get; }

    public static CliArgs Parse(string[] args)
    {
        var positionals = new List<string>();
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i].StartsWith("--", StringComparison.Ordinal))
            {
                string key = args[i][2..];
                bool hasValue = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal);
                options[key] = hasValue ? args[++i] : "true";
            }
            else
            {
                positionals.Add(args[i]);
            }
        }

        return new CliArgs(args.Length > 0 ? args[0] : "help", positionals, options);
    }

    public string Text(string key, string fallback) => _options.TryGetValue(key, out string? value) ? value : fallback;

    /// <summary>The option's value; absent when the option was not given.</summary>
    public string? OptionalText(string key) => _options.TryGetValue(key, out string? value) ? value : null;

    public bool Flag(string key) => _options.ContainsKey(key);

    /// <exception cref="UsageException">The value is not an integer.</exception>
    public int Int(string key, int fallback) => _options.TryGetValue(key, out string? value)
        ? int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : throw new UsageException($"--{key} expects an integer, got '{value}'.")
        : fallback;

    /// <exception cref="UsageException">The value is not an unsigned integer.</exception>
    public ulong ULong(string key, ulong fallback) => _options.TryGetValue(key, out string? value)
        ? ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong parsed)
            ? parsed
            : throw new UsageException($"--{key} expects a non-negative integer, got '{value}'.")
        : fallback;

    /// <exception cref="UsageException">The positional argument is missing.</exception>
    public string Required(int index, string name) => index < Positionals.Count
        ? Positionals[index]
        : throw new UsageException($"Missing <{name}>.");
}

/// <summary>The command line was wrong; the message is shown to the user as-is.</summary>
internal sealed class UsageException : Exception
{
    public UsageException(string message)
        : base(message)
    {
    }
}
