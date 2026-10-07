#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Puzzle.Bot;
using Puzzle.Client.Art;
using Puzzle.Client.Engine;
using Puzzle.Client.Flow;
using Puzzle.Client.Layout;
using Puzzle.Client.Play;
using Puzzle.Client.Stage;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Randomness;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// Checks from the command line, with no game running, what the stage makes of a finger that touches the board
    /// while a turn is still being shown. A player who is a little ahead of the pieces must not have to touch
    /// twice: a swipe made shortly before the board comes to rest is played the moment it rests; a finger that came
    /// down while the pieces moved and drags once they rest plays at once; and a swipe made long before the end of
    /// a long turn, at a board that has changed since, is forgotten. The touches go through the session as a
    /// phone's do, and the clock is moved in the steps of a 60 Hz screen.
    /// usage: Unity.exe -batchmode -quit -projectPath unity -executeMethod Puzzle.Client.EditorTools.TouchWhileMovingCheck.Run
    /// </summary>
    public static class TouchWhileMovingCheck
    {
        /// <summary>A level with moves enough for every part of the check and room for long cascades.</summary>
        private const int LevelNumber = 40;

        private const ulong BotSeed = 20261007;
        private const ulong BotStream = 13;

        /// <summary>The early swipe is made this long before the turn's pieces are at rest: well inside the time a touch waits.</summary>
        private const float ShortlyBeforeRest = 0.3f;

        /// <summary>A turn is long enough to forget a touch made at its beginning when it outlasts the wait by this much.</summary>
        private const float ClearlyLonger = 0.3f;

        /// <summary>No turn takes this long; a move that has not been played by then never will be.</summary>
        private const float LongestWaitSeconds = 12f;

        private static readonly ScreenShape Screen = new ScreenShape(1080, 1920);

        public static void Run()
        {
            try
            {
                ArtCatalogue art = AssetDatabase.LoadAssetAtPath<ArtCatalogue>(ArtCatalogueBuilder.CataloguePath);
                if (art == null)
                {
                    throw new InvalidOperationException($"{ArtCatalogueBuilder.CataloguePath} is missing; run ProjectSetup.Run first.");
                }

                IReadOnlyList<LevelDefinition> levels = LevelPack.LoadPacked();
                LevelDefinition level = LevelPack.Numbered(levels, LevelNumber);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var progress = new ProgressInMemory(StoryStudio.StandingOn(level, levels));
                using var session = new GameSession(levels, art, progress, Screen);
                if (!BotPolicies.TryCreate(SkillProfile.Expert.Name, out IBotPolicy bot))
                {
                    throw new InvalidOperationException($"The bots' list has no {SkillProfile.Expert.Name}.");
                }

                var player = new Player(session, bot, new Rng(BotSeed, BotStream));

                ASwipeDuringTheEntranceIsPlayedWhenTheBoardRests(player, progress);
                ASwipeShortlyBeforeRestIsPlayedWhenTheBoardRests(player);
                AFingerThatCameDownWhileMovingPlaysWhenItDragsAtRest(player);
                AnOldSwipeIsForgotten(player);
                Debug.Log("Touch check passed: a swipe made while the board moved was played when it came to rest (during the entrance and during a turn), "
                    + "a finger that came down during a turn played by dragging at rest, and a swipe older than the wait was forgotten.");
            }
            catch (Exception failure) when (failure is InvalidOperationException || failure is ArgumentException
                || failure is IOException || failure is LevelFormatException)
            {
                Debug.LogError($"Touch check failed: {failure.Message}");
                EditorApplication.Exit(1);
            }
        }

        private static void ASwipeDuringTheEntranceIsPlayedWhenTheBoardRests(Player player, ProgressInMemory progress)
        {
            player.Swipe(player.ALegalTrade());
            player.Expect(movesPlayed: 0, "right after a swipe made while the board came on");

            player.WaitUntilPlayed(moves: 1, "a swipe made while the board came on");
            player.ComeToRest("the turn of a swipe made while the board came on");
            if (progress.Load().AttemptsOnLevel != 1)
            {
                throw new InvalidOperationException($"The move a waiting swipe played was not kept as the attempt's first: the progress is \"{progress.Load()}\".");
            }
        }

        private static void ASwipeShortlyBeforeRestIsPlayedWhenTheBoardRests(Player player)
        {
            int before = player.MovesPlayed;
            TurnScript turn = player.PlayTheBotsMove();
            player.Advance(Mathf.Max(0f, turn.Seconds - ShortlyBeforeRest));
            player.Swipe(player.ALegalTrade());
            player.Expect(before + 1, "right after a swipe made during a turn");

            player.WaitUntilPlayed(before + 2, "a swipe made shortly before the board came to rest");
            player.ComeToRest("the turn of a swipe made during a turn");
        }

        private static void AFingerThatCameDownWhileMovingPlaysWhenItDragsAtRest(Player player)
        {
            int before = player.MovesPlayed;
            player.PlayTheBotsMove();
            Move trade = player.ALegalTrade();
            player.Session.PointerDown(player.Session.Stage.PixelOf(trade.From));
            player.ComeToRest("a turn during which a finger came down");
            player.Expect(before + 1, "while a finger that came down during a turn rests on its piece");

            player.Session.PointerMoved(player.Session.Stage.PixelOf(trade.To));
            player.Session.PointerUp();
            player.Expect(before + 2, "right after that finger dragged, with the board at rest");
            player.ComeToRest("the turn of a drag that began during the turn before");
        }

        private static void AnOldSwipeIsForgotten(Player player)
        {
            TurnScript turn = player.PlayTheBotsMove();
            while (turn.Seconds < Tempo.TouchWaits + ClearlyLonger)
            {
                player.ComeToRest("a turn too short to forget a touch in");
                turn = player.PlayTheBotsMove();
            }

            int before = player.MovesPlayed;
            player.Swipe(player.ALegalTrade());
            player.ComeToRest($"a turn of {StoryStudio.Seconds(turn.Seconds)} s with a swipe made at its beginning");
            player.Advance(0.5f);
            player.Expect(before, $"after a swipe made {StoryStudio.Seconds(turn.Seconds)} s before the board came to rest");
        }

        /// <summary>The session of the check with a bot to make moves and a finger to touch the board.</summary>
        private sealed class Player
        {
            private readonly IBotPolicy _bot;
            private readonly Rng _rng;

            public Player(GameSession session, IBotPolicy bot, Rng rng)
            {
                Session = session;
                _bot = bot;
                _rng = rng;
            }

            public GameSession Session { get; }

            public int MovesPlayed => Game.MovesUsed;

            private Game Game => Session.Stage.Game;

            public void Advance(float seconds) => StoryStudio.AdvanceBy(seconds, Session);

            /// <summary>A trade of two neighbours that the engine would play on its board as it stands, which is the board the pieces come to rest on.</summary>
            /// <exception cref="InvalidOperationException">The game is over, or its board has only taps left.</exception>
            public Move ALegalTrade()
            {
                RefuseADecidedGame();
                foreach (Move move in Game.FindLegalMoves())
                {
                    if (move.Kind == MoveKind.Swap)
                    {
                        return move;
                    }
                }

                throw new InvalidOperationException($"Level {LevelNumber} has no trade to make at move {Game.MovesUsed}; pick another level for this check.");
            }

            /// <summary>The finger comes down in the middle of one cell, goes to the middle of its neighbour and lifts.</summary>
            public void Swipe(Move trade)
            {
                Session.PointerDown(Session.Stage.PixelOf(trade.From));
                Session.PointerMoved(Session.Stage.PixelOf(trade.To));
                Session.PointerUp();
            }

            /// <summary>The bot's move, played on the stage at rest; the clock stands at the move.</summary>
            public TurnScript PlayTheBotsMove()
            {
                RefuseADecidedGame();
                Move move = _bot.ChooseMove(Game, Game.FindLegalMoves(), _rng);
                if (!Session.TryPlay(move) || Session.Stage.LastTurn is not TurnScript script)
                {
                    throw new InvalidOperationException($"The stage refused the bot's move {move} at move {Game.MovesUsed + 1}.");
                }

                return script;
            }

            /// <exception cref="InvalidOperationException">The game has played another number of moves.</exception>
            public void Expect(int movesPlayed, string when)
            {
                if (Game.MovesUsed != movesPlayed)
                {
                    throw new InvalidOperationException($"{Game.MovesUsed} moves were played {when}; {movesPlayed} were expected.");
                }
            }

            /// <summary>Moves the clock on until the game has played so many moves.</summary>
            /// <exception cref="InvalidOperationException">The move was never played.</exception>
            public void WaitUntilPlayed(int moves, string what)
            {
                float waited = 0f;
                while (Game.MovesUsed < moves)
                {
                    if (waited > LongestWaitSeconds)
                    {
                        throw new InvalidOperationException($"The move of {what} was not played: the game stands at move {Game.MovesUsed}.");
                    }

                    Session.Advance(StoryStudio.FrameSeconds);
                    waited += StoryStudio.FrameSeconds;
                }

                Expect(moves, $"once {what} was played");
            }

            /// <exception cref="InvalidOperationException">What the stage shows at rest is not the engine's board.</exception>
            public void ComeToRest(string after)
            {
                StoryStudio.AdvanceUntilSettled(Session);
                if (Session.Stage.LastTurnFaults.Count > 0)
                {
                    throw new InvalidOperationException($"After {after} the stage did not show the engine's board: {string.Join("; ", Session.Stage.LastTurnFaults)}");
                }
            }

            private void RefuseADecidedGame()
            {
                if (Game.Status != GameStatus.Playing)
                {
                    throw new InvalidOperationException($"Level {LevelNumber} was {Game.Status} after {Game.MovesUsed} moves, before the check was through; pick another level for it.");
                }
            }
        }
    }
}
