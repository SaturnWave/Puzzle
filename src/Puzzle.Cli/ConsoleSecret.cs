using System.Text;

namespace Puzzle.Cli;

/// <summary>Asks for a secret on the console without showing it.</summary>
internal static class ConsoleSecret
{
    /// <summary>
    /// The line typed after <paramref name="prompt"/>, with nothing of it echoed. When the input is not a keyboard (a
    /// pipe or a file) its next line is taken as it is, so a script can supply the secret without making it a
    /// command-line argument, which other processes could read. Absent when the input has ended.
    /// </summary>
    public static string? ReadLine(string prompt)
    {
        if (Console.IsInputRedirected)
        {
            return Console.In.ReadLine();
        }

        Console.Write(prompt);
        var typed = new StringBuilder();
        while (true)
        {
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);
            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    Console.WriteLine();
                    return typed.ToString();
                case ConsoleKey.Backspace:
                    typed.Length = Math.Max(0, typed.Length - 1);
                    break;
                default:
                    if (!char.IsControl(key.KeyChar))
                    {
                        typed.Append(key.KeyChar);
                    }

                    break;
            }
        }
    }
}
