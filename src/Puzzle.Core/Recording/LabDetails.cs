using System;
using System.Collections.Generic;
using System.Linq;
using Puzzle.Core.Engine;

namespace Puzzle.Core.Recording
{
    /// <summary>
    /// What only the web test lab records about an attempt: enough to replay it exactly and to tell help from the
    /// lab's tools (DDA assist, hints, the solver, bot moves) apart from a player's own play.
    /// </summary>
    public sealed class LabDetails
    {
        public const int MaxAssistPercent = 100;

        /// <param name="levelKey">The lab's name for the level file, e.g. "generated/level_0012".</param>
        /// <param name="levelFingerprint">Identifies the exact level content played, so a sample or a regenerated level is never mistaken for another with the same id.</param>
        /// <param name="assistPercent">The DDA assist the attempt ran with, 0..100 (the released game always plays with 0).</param>
        /// <param name="hintsShown">Moves suggested to the player: hint and solver requests.</param>
        /// <param name="botMoves">Moves a bot played for the player.</param>
        /// <param name="moves">Every accepted move in order, whoever chose it.</param>
        /// <exception cref="ArgumentException">A value is out of range or missing.</exception>
        public LabDetails(string levelKey, string levelFingerprint, int assistPercent, int hintsShown, int botMoves, IReadOnlyList<Move> moves)
        {
            LevelKey = string.IsNullOrWhiteSpace(levelKey) ? throw new ArgumentException("A level key is required.", nameof(levelKey)) : levelKey;
            LevelFingerprint = string.IsNullOrWhiteSpace(levelFingerprint)
                ? throw new ArgumentException("A level fingerprint is required.", nameof(levelFingerprint))
                : levelFingerprint;
            AssistPercent = assistPercent >= 0 && assistPercent <= MaxAssistPercent
                ? assistPercent
                : throw new ArgumentOutOfRangeException(nameof(assistPercent), assistPercent, $"Assist is 0..{MaxAssistPercent}.");
            HintsShown = hintsShown >= 0 ? hintsShown : throw new ArgumentOutOfRangeException(nameof(hintsShown), hintsShown, "Counts cannot be negative.");
            BotMoves = botMoves >= 0 ? botMoves : throw new ArgumentOutOfRangeException(nameof(botMoves), botMoves, "Counts cannot be negative.");
            Moves = Array.AsReadOnly((moves ?? throw new ArgumentNullException(nameof(moves))).ToArray());
        }

        /// <summary>The lab's name for the level file, e.g. "generated/level_0012".</summary>
        public string LevelKey { get; }

        /// <summary>Identifies the exact level content played.</summary>
        public string LevelFingerprint { get; }

        public int AssistPercent { get; }

        /// <summary>Moves suggested to the player: hint and solver requests.</summary>
        public int HintsShown { get; }

        /// <summary>Moves a bot played for the player.</summary>
        public int BotMoves { get; }

        /// <summary>Every accepted move in order, whoever chose it.</summary>
        public IReadOnlyList<Move> Moves { get; }

        /// <summary>Whether anything but the player's own judgment shaped the attempt.</summary>
        public bool IsAssisted => IsAssistedBy(AssistPercent, HintsShown, BotMoves);

        /// <summary>
        /// The rule behind <see cref="IsAssisted"/>, for an attempt still being played: the lab shows it to the player
        /// before the record exists.
        /// </summary>
        public static bool IsAssistedBy(int assistPercent, int hintsShown, int botMoves) => assistPercent > 0 || hintsShown > 0 || botMoves > 0;
    }
}
