#nullable enable
using System;
using System.Globalization;
using Puzzle.Client.Words;
using Puzzle.Meta;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// What the card before a level says, in the language the game speaks (<see cref="Speech"/>): in Turkish the
    /// design canvas' words (LevelStartTR), and those of the card that offers a start booster the player holds none
    /// of, which the canvas does not draw.
    /// No Unity dependency.
    /// </summary>
    public static class LevelStartWords
    {
        /// <summary>The headers over the goals and over the boosters, and the label over the moves.</summary>
        public static string Goals => Speech.Say(WordId.StartGoals);
        public static string ChooseBoosters => Speech.Say(WordId.StartChooseBoosters);
        public static string Moves => Speech.Say(WordId.StartMoves);

        /// <summary>The green button that leads into the level.</summary>
        public static string Play => Speech.Say(WordId.Play);

        /// <summary>The green button that buys one booster, and what it says when the coins do not cover the price.</summary>
        public static string Buy => Speech.Say(WordId.Buy);
        public static string CoinsShort => Speech.Say(WordId.ButtonCoinsShort);

        /// <summary>The smaller line of the card that offers a booster: a booster is chosen only once it is held.</summary>
        public static string BuyToChoose => Speech.Say(WordId.GetMoreBuyToChoose);

        /// <summary>The nameplate: the level, as the hall's play button names it.</summary>
        public static string Title(int levelNumber) => MuseumWords.Play(levelNumber);

        /// <summary>
        /// The nameplate's second line: the jigsaw piece winning the level gives, counted among the painting's pieces
        /// ("ÖDÜL · PARÇA 7 / 10"); a level that gives two names both ("PARÇA 7–8 / 10").
        /// </summary>
        public static string Reward(PieceReward reward)
        {
            int first = reward.Pieces[0] + 1;
            int last = reward.Pieces[reward.Pieces.Count - 1] + 1;
            string pieces = first == last ? Number(first) : $"{Number(first)}–{Number(last)}";
            return Speech.Say(WordId.StartReward, ("pieces", pieces), ("all", Number(reward.PieceCount)));
        }

        /// <summary>A start booster's name on one line, as the card that offers it names it.</summary>
        /// <param name="extraMoves">How many moves the booster of extra moves adds: its name says so.</param>
        public static string NameOf(BoosterKind booster, int extraMoves) => OnTheShelf(booster, extraMoves).Replace('\n', ' ');

        /// <summary>A start booster's name under its disc, on the lines the canvas breaks it into.</summary>
        /// <exception cref="ArgumentException">The booster is used during play: the card before a level offers none of them.</exception>
        public static string OnTheShelf(BoosterKind booster, int extraMoves) => booster switch
        {
            BoosterKind.StartRocketAndBomb => Speech.Say(WordId.StartRocketAndBomb),
            BoosterKind.StartColorBomb => Speech.Say(WordId.StartColorBomb),
            BoosterKind.StartExtraMoves => PlusMoves(extraMoves),
            _ => throw NotAStartBooster(booster),
        };

        /// <summary>What a start booster does to the level, for the player who is offered one.</summary>
        /// <exception cref="ArgumentException">The booster is used during play.</exception>
        public static string WhatItDoes(BoosterKind booster, int extraMoves) => booster switch
        {
            BoosterKind.StartRocketAndBomb => Speech.Say(WordId.StartRocketAndBombDoes),
            BoosterKind.StartColorBomb => Speech.Say(WordId.StartColorBombDoes),
            BoosterKind.StartExtraMoves => Speech.SayCounted(WordId.StartExtraMovesDoes, extraMoves, ("moves", Number(extraMoves))),
            _ => throw NotAStartBooster(booster),
        };

        /// <summary>Extra moves as the start booster's disc and the offer's green button name them: "+5 hamle".</summary>
        public static string PlusMoves(int moves) => Speech.SayCounted(WordId.PlusMoves, moves, ("moves", Number(moves)));

        private static string Number(int number) => number.ToString(CultureInfo.InvariantCulture);

        private static ArgumentException NotAStartBooster(BoosterKind booster) =>
            new ArgumentException($"{booster.Name()} is used during play; the card before a level offers none of it.", nameof(booster));
    }
}
