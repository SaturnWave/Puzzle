#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Puzzle.Client.Art;
using Puzzle.Client.Gallery;
using Puzzle.Client.Screens;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Meta;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// What the card before a level shows of the level and offers for it: its number, its goals and its moves, the
    /// jigsaw pieces winning it gives, and the three start boosters with the rules they are chosen and sold by. A
    /// press on a booster is the rules' to answer: one the player holds is chosen, or put back if it was chosen;
    /// one they hold none of is offered at its single price; no more than the settings allow are chosen for one
    /// attempt. Nothing is spent here: what is chosen leaves the shelf with the attempt's first move
    /// (<see cref="AttemptLedger"/>). This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public sealed class LevelBrief
    {
        private readonly StartBoosterRules _choosing;
        private readonly Shop _shop;

        private LevelBrief(int levelNumber, int moveLimit, IReadOnlyList<GoalFigure> goals, PieceReward? reward, int extraMoves, StartBoosterRules choosing, Shop shop)
        {
            LevelNumber = levelNumber;
            MoveLimit = moveLimit;
            Goals = goals;
            Reward = reward;
            ExtraMoves = extraMoves;
            _choosing = choosing;
            _shop = shop;
        }

        /// <summary>The start boosters as the card's shelf holds them, from left to right: in the engine's order.</summary>
        public static IReadOnlyList<BoosterKind> StartBoosters { get; } = BoosterKinds.All.Where(BoosterKinds.IsStart).ToArray();

        /// <summary>The number the player knows the level by.</summary>
        public int LevelNumber { get; }

        /// <summary>The moves the level gives, before a start booster adds any.</summary>
        public int MoveLimit { get; }

        /// <summary>The level's goals, in the order of the board's plaque, each with all of it still to do.</summary>
        public IReadOnlyList<GoalFigure> Goals { get; }

        /// <summary>The jigsaw pieces winning the level gives; nothing when it gives none.</summary>
        public PieceReward? Reward { get; }

        /// <summary>How many moves the start booster of extra moves adds to the level's.</summary>
        public int ExtraMoves { get; }

        /// <summary>The card of a level, for the player who stands on it.</summary>
        /// <param name="reward">What winning the level gives; nothing for a level that gives nothing new.</param>
        /// <param name="economy">The rules start boosters are chosen and sold by.</param>
        /// <param name="engine">The rules the level is played by: what the booster of extra moves adds.</param>
        public static LevelBrief Of(LevelDefinition level, PieceReward? reward, EconomyRules economy, RuleSet engine)
        {
            GoalFigure[] goals = level.Goals.Select(goal => new GoalFigure(PieceArtOf.Goal(goal), goal.Target)).ToArray();
            return new LevelBrief(level.Id.Value, level.MoveLimit, goals, reward, engine.StartBoosterExtraMoves, economy.StartBoosters, economy.Shop);
        }

        /// <summary>The moves the level opens with when these start boosters are taken into it.</summary>
        public int MovesWith(StartChoice choice) => MoveLimit + (choice.Has(BoosterKind.StartExtraMoves) ? ExtraMoves : 0);

        /// <summary>What a start booster's disc on the card shows: how many the player holds, or the plus, and whether it is chosen.</summary>
        /// <exception cref="ArgumentException">The choice holds a booster the player holds none of.</exception>
        public BoosterFace FaceOf(BoosterKind booster, BoosterShelf owned, StartChoice choice) =>
            BoosterFace.Of(owned.Count(booster), choice.Has(booster) ? BoosterChoice.Chosen : BoosterChoice.Waiting);

        /// <summary>What a press on a start booster's disc does to the choice: the settings' rules answer.</summary>
        /// <exception cref="ArgumentException">The booster is used during play.</exception>
        public StartToggle Press(StartChoice choice, BoosterKind booster, BoosterShelf owned) => _choosing.Toggled(choice, booster, owned);

        /// <summary>
        /// The choice with every booster the player no longer holds put back, so a choice can never spend what the
        /// shelf does not have.
        /// </summary>
        public StartChoice HeldOnly(StartChoice choice, BoosterShelf owned)
        {
            StartChoice held = choice;
            foreach (BoosterKind booster in choice.Boosters)
            {
                if (!owned.Has(booster) && Press(held, booster, owned) is StartToggle.Changed putBack)
                {
                    held = putBack.Choice;
                }
            }

            return held;
        }

        /// <summary>What one booster costs on its own: the price the card that offers it asks.</summary>
        public Coins PriceOf(BoosterKind booster) => _shop.PriceOf(booster);

        /// <summary>True when the coins cover one booster of the kind.</summary>
        public bool CanBuy(BoosterKind booster, Coins coins) => coins.Covers(PriceOf(booster));
    }
}
