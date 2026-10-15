#nullable enable
using System;
using System.Globalization;
using Puzzle.Meta;

namespace Puzzle.Client.Flow
{
    /// <summary>What the plaques over the board say, in the player's language, in the capitals the design sets them in (BoardTR). No Unity dependency.</summary>
    public static class BoardWords
    {
        /// <summary>The label over the level's number, where the level gives no jigsaw piece.</summary>
        public const string Level = "BÖLÜM";

        /// <summary>The label on the moves' medallion.</summary>
        public const string Moves = "HAMLE";

        /// <summary>The label over the goals, where the level gives no jigsaw piece.</summary>
        public const string Goals = "HEDEF";

        /// <summary>The label over the goals where the level's number has no plaque of its own: "BÖLÜM 47".</summary>
        public static string LevelNumbered(int levelNumber) => $"{Level} {Number(levelNumber)}";

        /// <summary>The label over the painting whose piece the level gives: how many of its pieces the player has once it is won, "PARÇA 7 / 10".</summary>
        public static string PieceOf(PieceReward reward) => $"PARÇA {Number(reward.WonWithThem)} / {Number(reward.PieceCount)}";

        /// <summary>On the disc of a booster used during play that is not open yet: the level it opens on.</summary>
        public static string OpensOn(int levelNumber) => $"Bölüm {Number(levelNumber)}";

        public static string Number(int number) => number.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// What the board says of the boosters used during play, on the pill over the shelf and on the card that offers
    /// one: their names, what each does and what to pick for it, why a use was refused, the word on a booster's first
    /// use and the honest line beside the first of them. The design canvas draws none of it (BoardTR shows the discs
    /// alone); the words follow docs/research/in-level-boosters.md, section 5. No Unity dependency.
    /// </summary>
    public static class BoosterWords
    {
        /// <summary>The line beside the first booster's word on its first use (the genre's honest sentence, the owner's of 2026-10-15): the levels are calibrated without boosters.</summary>
        public const string EveryLevelWithout = "Her bölüm booster'sız geçilebilir";

        /// <summary>The smaller line of the card that offers a booster used during play: using it costs no move.</summary>
        public const string CostsNoMove = "Kullanmak hamle harcamaz";

        /// <summary>The booster's name, on its pill and on the card that offers it.</summary>
        /// <exception cref="ArgumentException">The booster is taken before a level; the board's shelf has none of it.</exception>
        public static string NameOf(BoosterKind booster) => booster switch
        {
            BoosterKind.HitCell => "Spatula",
            BoosterKind.HitRow => "Yatay fırça",
            BoosterKind.HitColumn => "Dikey fırça",
            BoosterKind.Shuffle => "Paleti karıştır",
            _ => throw NotUsedDuringPlay(booster),
        };

        /// <summary>What the booster does, after its name.</summary>
        /// <exception cref="ArgumentException">The booster is taken before a level.</exception>
        public static string WhatItDoes(BoosterKind booster) => booster switch
        {
            BoosterKind.HitCell => "bir taşı ya da bir katı kaldırır",
            BoosterKind.HitRow => "bir satırı boyar",
            BoosterKind.HitColumn => "bir sütunu boyar",
            BoosterKind.Shuffle => "taşları yeniden dizer",
            _ => throw NotUsedDuringPlay(booster),
        };

        /// <summary>The armed booster's pill, its first line: "Spatula · bir taşı ya da bir katı kaldırır".</summary>
        public static string Armed(BoosterKind booster) => $"{NameOf(booster)} · {WhatItDoes(booster)}";

        /// <summary>The armed booster's pill, its second line: what the finger is to pick. The shuffle picks nothing: it is used at once.</summary>
        /// <exception cref="ArgumentException">The booster is the shuffle, or is taken before a level.</exception>
        public static string WhatToPick(BoosterKind booster) => booster switch
        {
            BoosterKind.HitCell => "Bir taş seç",
            BoosterKind.HitRow => "Bir satır seç",
            BoosterKind.HitColumn => "Bir sütun seç",
            BoosterKind.Shuffle => throw new ArgumentException("The shuffle is used at once; nothing is picked for it.", nameof(booster)),
            _ => throw NotUsedDuringPlay(booster),
        };

        /// <summary>Why the engine refused a use, which spent nothing: a strike found nothing to hit, the board has no other arrangement.</summary>
        /// <exception cref="ArgumentException">The booster is taken before a level.</exception>
        public static string Refused(BoosterKind booster) => booster switch
        {
            BoosterKind.HitCell or BoosterKind.HitRow or BoosterKind.HitColumn => "Burada vurulacak bir şey yok",
            BoosterKind.Shuffle => "Tahta karıştırılamıyor",
            _ => throw NotUsedDuringPlay(booster),
        };

        /// <summary>The one sentence on the level a booster opens on, until it is first touched: what it is and how it is used.</summary>
        /// <exception cref="ArgumentException">The booster is taken before a level.</exception>
        public static string FirstUse(BoosterKind booster) => booster switch
        {
            BoosterKind.HitCell => "Yeni: Spatula! Diske dokun, sonra bir taşa.",
            BoosterKind.HitRow => "Yeni: Yatay fırça! Diske dokun, sonra bir satıra.",
            BoosterKind.HitColumn => "Yeni: Dikey fırça! Diske dokun, sonra bir sütuna.",
            BoosterKind.Shuffle => "Yeni: Paleti karıştır! Diske dokun, taşlar yeniden dizilsin.",
            _ => throw NotUsedDuringPlay(booster),
        };

        /// <summary>What the booster does, as the card that offers it says it: a sentence of its own.</summary>
        public static string OnTheCard(BoosterKind booster)
        {
            string does = WhatItDoes(booster);
            return char.ToUpperInvariant(does[0]) + does.Substring(1); // every one begins with a letter that has no Turkish capital of its own
        }

        private static ArgumentException NotUsedDuringPlay(BoosterKind booster) =>
            new ArgumentException($"{booster.Name()} is taken before a level; the board's shelf has none of it.", nameof(booster));
    }

    /// <summary>
    /// What the two plaques beside the moves' medallion show, which the jigsaw piece the level gives decides. A
    /// closed set (private constructor). A level that gives a piece shows it as the design canvas draws the board
    /// (BoardTR): the level over its goals at the left, the painting with the piece lit at the right. A level that
    /// gives none (one won before, or beyond the museum's paintings) keeps the web test page's arrangement: the
    /// level's number at the left, the goals at the right. No Unity dependency.
    /// </summary>
    public abstract class BoardPlaques
    {
        private BoardPlaques(int levelNumber)
        {
            LevelNumber = levelNumber;
        }

        public int LevelNumber { get; }

        /// <summary>The label that stands over the goals, on whichever plaque they hang.</summary>
        public abstract string GoalsLabel { get; }

        /// <summary>The plaques of a level, by the piece winning it gives; nothing for none.</summary>
        public static BoardPlaques Of(int levelNumber, PieceReward? reward) => reward is PieceReward given
            ? new LevelOverGoalsBesideThePiece(levelNumber, given)
            : new LevelBesideGoals(levelNumber);

        /// <summary>The level gives no piece: "BÖLÜM" over its number at the left, "HEDEF" over the goals at the right.</summary>
        public sealed class LevelBesideGoals : BoardPlaques
        {
            public LevelBesideGoals(int levelNumber)
                : base(levelNumber)
            {
            }

            public override string GoalsLabel => BoardWords.Goals;

            public string LevelLabel => BoardWords.Level;

            public string LevelFigure => BoardWords.Number(LevelNumber);
        }

        /// <summary>The level gives a piece: "BÖLÜM 47" over the goals at the left, "PARÇA 7 / 10" over the painting at the right.</summary>
        public sealed class LevelOverGoalsBesideThePiece : BoardPlaques
        {
            public LevelOverGoalsBesideThePiece(int levelNumber, PieceReward reward)
                : base(levelNumber)
            {
                Reward = reward;
            }

            /// <summary>The pieces the level gives: the painting the right plaque shows, and the pieces lit on it.</summary>
            public PieceReward Reward { get; }

            public override string GoalsLabel => BoardWords.LevelNumbered(LevelNumber);

            public string PieceLabel => BoardWords.PieceOf(Reward);
        }
    }
}
