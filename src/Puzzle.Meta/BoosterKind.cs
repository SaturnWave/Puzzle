using System;
using System.Collections.Generic;
using Puzzle.Core.Engine;

namespace Puzzle.Meta
{
    /// <summary>
    /// The seven boosters a player can hold (docs/GAME_DESIGN.md section 18), in the engine's order: the three taken
    /// before a level starts, then the four used during play. Closed set.
    /// </summary>
    public enum BoosterKind
    {
        StartRocketAndBomb,
        StartColorBomb,
        StartExtraMoves,
        HitCell,
        HitRow,
        HitColumn,
        Shuffle,
    }

    public static class BoosterKinds
    {
        public static IReadOnlyList<BoosterKind> All { get; } = (BoosterKind[])Enum.GetValues(typeof(BoosterKind));

        /// <summary>
        /// The name the booster goes by in attempt records, and so in the settings file and in a saved wallet. It is
        /// the engine's own (<see cref="BoosterNames"/>) and never changes.
        /// </summary>
        public static string Name(this BoosterKind kind) => kind switch
        {
            BoosterKind.StartRocketAndBomb => BoosterNames.Of(StartBooster.RocketAndBomb),
            BoosterKind.StartColorBomb => BoosterNames.Of(StartBooster.ColorBomb),
            BoosterKind.StartExtraMoves => BoosterNames.Of(StartBooster.ExtraMoves),
            BoosterKind.HitCell => BoosterNames.Of(new BoosterUse.HitCell(default)),
            BoosterKind.HitRow => BoosterNames.Of(new BoosterUse.HitRow(default)),
            BoosterKind.HitColumn => BoosterNames.Of(new BoosterUse.HitColumn(default)),
            BoosterKind.Shuffle => BoosterNames.Of(new BoosterUse.Shuffle()),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown booster."),
        };

        /// <summary>Taken before the level starts.</summary>
        public static bool IsStart(this BoosterKind kind) => kind switch
        {
            BoosterKind.StartRocketAndBomb or BoosterKind.StartColorBomb or BoosterKind.StartExtraMoves => true,
            BoosterKind.HitCell or BoosterKind.HitRow or BoosterKind.HitColumn or BoosterKind.Shuffle => false,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown booster."),
        };

        /// <summary>Used during play, at no cost of a move.</summary>
        public static bool IsInLevel(this BoosterKind kind) => !kind.IsStart();

        /// <summary>The booster a player gives up when the engine takes this one into an attempt.</summary>
        public static BoosterKind Of(StartBooster booster) => booster switch
        {
            StartBooster.RocketAndBomb => BoosterKind.StartRocketAndBomb,
            StartBooster.ColorBomb => BoosterKind.StartColorBomb,
            StartBooster.ExtraMoves => BoosterKind.StartExtraMoves,
            _ => throw new ArgumentOutOfRangeException(nameof(booster), booster, "Unknown start booster."),
        };

        /// <summary>The booster a player gives up when the engine uses this one during play.</summary>
        public static BoosterKind Of(BoosterUse use) => use switch
        {
            BoosterUse.HitCell _ => BoosterKind.HitCell,
            BoosterUse.HitRow _ => BoosterKind.HitRow,
            BoosterUse.HitColumn _ => BoosterKind.HitColumn,
            BoosterUse.Shuffle _ => BoosterKind.Shuffle,
            _ => throw new InvalidOperationException($"Unhandled booster {use.GetType().Name}."),
        };

        /// <summary>What the engine calls a booster that is taken before the level starts.</summary>
        /// <exception cref="ArgumentException">The booster is used during play: the engine takes none of its kind into an attempt.</exception>
        public static StartBooster AsStartBooster(this BoosterKind kind) => kind switch
        {
            BoosterKind.StartRocketAndBomb => StartBooster.RocketAndBomb,
            BoosterKind.StartColorBomb => StartBooster.ColorBomb,
            BoosterKind.StartExtraMoves => StartBooster.ExtraMoves,
            BoosterKind.HitCell or BoosterKind.HitRow or BoosterKind.HitColumn or BoosterKind.Shuffle =>
                throw new ArgumentException($"{kind.Name()} is used during play, not taken before a level starts.", nameof(kind)),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown booster."),
        };

        /// <summary>The booster a record name names; absent for any other text.</summary>
        public static BoosterKind? Find(string name)
        {
            foreach (BoosterKind kind in All)
            {
                if (kind.Name() == name)
                {
                    return kind;
                }
            }

            return null;
        }
    }
}
