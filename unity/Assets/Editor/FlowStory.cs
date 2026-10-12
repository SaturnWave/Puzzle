#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Puzzle.Bot;
using Puzzle.Client.Art;
using Puzzle.Client.Flow;
using Puzzle.Client.Layout;
using Puzzle.Client.Play;
using Puzzle.Client.Stage;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Randomness;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>How a storyboard of the game's flow ends: with a level won, or with one lost.</summary>
    internal enum FlowEnding
    {
        Won,
        Lost,
    }

    /// <summary>
    /// The storyboard of the way from one level to the next, with no game running: a session starts on a level as a
    /// player would find it, a bot plays until the level is won (or lost), the result's card comes down, a finger
    /// presses its button, and the next board comes out of the dark. It is the game's own session on a progress file
    /// of its own (out/unity/flow-progress), so what is pictured is what a phone does, saving included; at the end
    /// the progress is read back from the file and a second session is opened on it, as after a restart.
    /// A level that is won with moves left over has its finale before the card: a few moments of it are pictured
    /// too, and the story requires that the win is on file the moment the winning move is played and that a
    /// restart as the finale begins opens the next level, so closing the game during the finale loses nothing.
    /// Pictures go to out/unity/shots/flow-level-NNNN-SIZE-won-NN-what.png (or -lost-), with one sheet of them all.
    /// </summary>
    internal static class FlowStory
    {
        private const string ProgressFolder = "../out/unity/flow-progress";

        /// <summary>A bot does not always end a level the way the story needs; after this many games it is the wrong bot for the level.</summary>
        private const int MostGames = 8;

        private const ulong BotSeed = 20261007;
        private const ulong BotStream = 11;

        /// <summary>The moment of the board's entrance that is pictured, in seconds after the level opened.</summary>
        private const float EntranceMoment = 0.12f;

        /// <summary>
        /// The moments after the end of the deciding turn that are pictured: the board at rest, the room going dark,
        /// the card on its way down, the card landing, its exhibits popping up.
        /// </summary>
        private static readonly float[] ResultMoments = { 0.3f, 0.52f, 0.62f, 0.74f, 0.9f, 1.06f };

        /// <summary>
        /// The moments of a won game's finale that are pictured, in seconds after its first frame: moves on their
        /// way to the board, the rockets firing, the board coming down. Its own storyboard is FinaleStory's.
        /// </summary>
        private static readonly float[] FinaleMoments = { 0.6f, 1.4f, 2.2f };

        /// <summary>A button is pictured this long after the finger came down on it, a card this long after the finger lifted.</summary>
        private const float PressedMoment = 2f * StoryStudio.FrameSeconds;
        private const float LeavingMoment = 0.14f;

        /// <summary>The moments of the next board's coming out of the dark that are pictured, in seconds after it opened.</summary>
        private static readonly float[] NextMoments = { 0.08f, 0.24f };

        /// <summary>Tells the story of one ending on one level and one screen.</summary>
        /// <param name="player">The bot that plays; a strong one for a level to be won, a weak one for a level to be lost.</param>
        /// <exception cref="InvalidOperationException">The story did not go as the game's rules say it must, or the bot never reached the ending.</exception>
        public static void Tell(IReadOnlyList<LevelDefinition> levels, LevelDefinition level, FlowEnding ending, IBotPolicy player, ScreenShape screen, ArtCatalogue art)
        {
            // A scene of its own that is never saved: nothing of an earlier run is left in this one.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            string name = $"flow-level-{level.Id.Value:D4}-{screen}-{ending.ToString().ToLowerInvariant()}";
            string folder = Path.Combine(ProgressFolder, name);
            StartTheProgressFile(folder, StoryStudio.StandingOn(level, levels));

            using GameSession session = StoryStudio.AtTheBoard(levels, art, new ProgressFile(folder), screen);
            var reel = new Reel(session, screen, name);
            reel.Advance(EntranceMoment);
            reel.Shoot($"start-t{StoryStudio.Seconds(EntranceMoment)}");
            reel.Settle();
            reel.Shoot("start");

            TurnScript decidingTurn = PlayToTheEnding(session, ending, player);
            PlayerProgress decidedWith = session.Progress;
            LevelDefinition decided = session.Stage.Game.Level;
            var restart = new Restart(folder, levels, art, screen);
            if (ending == FlowEnding.Won)
            {
                // The clock stands at the winning move: nothing of it was shown yet, and the win must be on file already.
                restart.RequireTheLevelAfter(decided, "right after the winning move, before any of it was shown");
            }

            reel.Advance(decidingTurn.Seconds);
            if (session.Stage.Game.Status == GameStatus.Won && session.Stage.Game.MovesLeft > 0)
            {
                PictureTheFinale(reel, restart, decided);
            }

            PictureTheResult(reel);
            PictureGoingOn(reel);

            CheckWhereThePlayerStands(session, decided, decidedWith, ending, levels);
            CheckARestart(session, folder, levels, art, screen);
            reel.WriteSheet();
            Debug.Log($"Storyboard: flow {name}: level {decided.Id.Value} {ending.ToString().ToLowerInvariant()}, the player went on to level "
                + $"{session.Stage.Game.Level.Id.Value} ({session.Progress}); the progress file holds the same and a restart opens the same level.");
        }

        /// <summary>A progress file of this story's own, holding where the story begins.</summary>
        private static void StartTheProgressFile(string folder, PlayerProgress start)
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }

            new ProgressFile(folder).Save(start);
        }

        /// <summary>
        /// Plays whole games until one ends as the story needs. A game that ends the other way is gone on from, as a
        /// player would: by the button of its result's card.
        /// </summary>
        /// <returns>The script of the turn that decided the game; the session's clock stands at that turn's move.</returns>
        private static TurnScript PlayToTheEnding(GameSession session, FlowEnding ending, IBotPolicy player)
        {
            var rng = new Rng(BotSeed, BotStream);
            for (int game = 1; game <= MostGames; game++)
            {
                TurnScript decidingTurn = PlayTheGame(session, player, rng);
                Game played = session.Stage.Game;
                bool isTheEnding = ending == FlowEnding.Won ? played.Status == GameStatus.Won : played.Status.IsLoss();
                Debug.Log($"Storyboard: flow game {game}: level {played.Level.Id.Value} ended {played.Status} after {played.MovesUsed} moves.");
                if (isTheEnding)
                {
                    return decidingTurn;
                }

                StoryStudio.AdvanceUntilSettled(session);
                PressTheButton(session);
                session.PointerUp();
                AdvanceToTheNextStage(session);
                StoryStudio.AdvanceUntilSettled(session);
            }

            throw new InvalidOperationException($"{MostGames} games did not end as {ending}; pick another bot or level for this story.");
        }

        /// <summary>Plays the open game to its end, every turn to rest but the last; returns the last turn's script.</summary>
        internal static TurnScript PlayTheGame(GameSession session, IBotPolicy player, Rng rng)
        {
            GameStage stage = session.Stage;
            while (true)
            {
                Move move = player.ChooseMove(stage.Game, stage.Game.FindLegalMoves(), rng);
                if (!session.TryPlay(move) || stage.LastTurn is not TurnScript script)
                {
                    throw new InvalidOperationException($"The stage refused the bot's move {move} at move {stage.Game.MovesUsed + 1} of level {stage.Game.Level.Id.Value}.");
                }

                if (stage.Game.Status != GameStatus.Playing)
                {
                    return script;
                }

                StoryStudio.AdvanceUntilSettled(session);
                RefuseFaults(stage);
            }
        }

        /// <summary>
        /// The finale of a game that was won with moves left over: a few of its moments, and in the middle of it
        /// what a player finds who closes the game there and opens it again. The clock is left at the finale's end.
        /// </summary>
        /// <exception cref="InvalidOperationException">No finale began, it was at fault, or a restart in the middle of it lost the win.</exception>
        private static void PictureTheFinale(Reel reel, Restart restart, LevelDefinition won)
        {
            GameStage stage = reel.Session.Stage;

            // The clock stands at the end of the winning turn's script: only the give of its last landings is still to come.
            StageSteps.AdvanceToTheFinale(stage, () => reel.Session.Advance(StoryStudio.FrameSeconds), turnStillTakes: 0f);
            restart.RequireTheLevelAfter(won, "as the finale began");
            float clock = 0f;
            foreach (float moment in FinaleMoments)
            {
                // Step by step and no further than the finale's end: the card's moments are counted from there.
                for (; stage.IsShowingTheFinale && clock + StoryStudio.FrameSeconds / 2f < moment; clock += StoryStudio.FrameSeconds)
                {
                    reel.Session.Advance(StoryStudio.FrameSeconds);
                }

                if (stage.IsShowingTheFinale)
                {
                    reel.Shoot($"finale-t{StoryStudio.Seconds(moment)}");
                }
            }

            while (stage.IsShowingTheFinale)
            {
                reel.Session.Advance(StoryStudio.FrameSeconds);
            }

            RefuseFaults(stage);
        }

        /// <summary>
        /// The end of the deciding turn, or of the finale that followed it, and what comes then: the room goes dark,
        /// the card comes down, its exhibits pop up.
        /// </summary>
        private static void PictureTheResult(Reel reel)
        {
            float clock = 0f;
            foreach (float moment in ResultMoments)
            {
                clock = reel.AdvanceTo(moment, clock);
                reel.Shoot($"result-t{StoryStudio.Seconds(moment)}");
            }

            reel.Settle();
            RefuseFaults(reel.Session.Stage);
            reel.Shoot("result");
        }

        /// <summary>A finger presses the card's button and lifts; the card goes up, and the next board comes out of the dark.</summary>
        private static void PictureGoingOn(Reel reel)
        {
            PressTheButton(reel.Session);
            reel.Advance(PressedMoment);
            reel.Shoot("pressed");
            reel.Session.PointerUp();
            reel.Advance(LeavingMoment);
            reel.Shoot($"leaving-t{StoryStudio.Seconds(LeavingMoment)}");

            AdvanceToTheNextStage(reel.Session);
            float clock = 0f;
            foreach (float moment in NextMoments)
            {
                clock = reel.AdvanceTo(moment, clock);
                reel.Shoot($"next-t{StoryStudio.Seconds(moment)}");
            }

            reel.Settle();
            reel.Shoot("next");
        }

        /// <exception cref="InvalidOperationException">No result's card hangs at rest on the session's stage.</exception>
        private static void PressTheButton(GameSession session)
        {
            if (session.Stage.ResultButtonPixel is not Vector2 button)
            {
                throw new InvalidOperationException("The stage shows no result's card at rest; there is no button to press.");
            }

            session.PointerDown(button);
        }

        /// <summary>Moves the clock until the session has opened another stage, which it does once the card is gone.</summary>
        /// <exception cref="InvalidOperationException">The session keeps its stage: the button was not pressed.</exception>
        private static void AdvanceToTheNextStage(GameSession session)
        {
            IStage left = session.Front;
            for (float waited = 0f; ReferenceEquals(session.Front, left); waited += StoryStudio.FrameSeconds)
            {
                if (waited > 2f * Tempo.CardGoesUp)
                {
                    throw new InvalidOperationException("The session opened no new stage after the card's button was pressed.");
                }

                session.Advance(StoryStudio.FrameSeconds);
            }
        }

        /// <summary>After a win the player stands on the next level, never tried; after a loss on the same level, with the same count of tries.</summary>
        private static void CheckWhereThePlayerStands(GameSession session, LevelDefinition decided, PlayerProgress decidedWith, FlowEnding ending, IReadOnlyList<LevelDefinition> levels)
        {
            int decidedIndex = StoryStudio.StandingOn(decided, levels).LevelsWon;
            PlayerProgress expected = ending == FlowEnding.Won
                ? new PlayerProgress(Math.Min(decidedIndex + 1, levels.Count), attemptsOnLevel: 0)
                : new PlayerProgress(decidedIndex, decidedWith.AttemptsOnLevel);
            if (!session.Progress.Equals(expected))
            {
                throw new InvalidOperationException($"After level {decided.Id.Value} was {ending} the progress is \"{session.Progress}\", not \"{expected}\".");
            }

            int openLevel = session.Stage.Game.Level.Id.Value;
            int expectedLevel = levels[expected.LevelIndexIn(levels.Count)].Id.Value;
            if (openLevel != expectedLevel)
            {
                throw new InvalidOperationException($"After level {decided.Id.Value} was {ending} level {openLevel} is open, not level {expectedLevel}.");
            }
        }

        /// <summary>What a restart of the game finds: the file holds the session's progress, and a session opened on it opens the same level.</summary>
        private static void CheckARestart(GameSession session, string folder, IReadOnlyList<LevelDefinition> levels, ArtCatalogue art, ScreenShape screen)
        {
            PlayerProgress onFile = new ProgressFile(folder).Load();
            if (session.HasUnsavedProgress || !onFile.Equals(session.Progress))
            {
                throw new InvalidOperationException($"The progress file holds \"{onFile}\" while the session stands at \"{session.Progress}\".");
            }

            using GameSession restarted = StoryStudio.AtTheBoard(levels, art, new ProgressFile(folder), screen);
            if (restarted.Stage.Game.Level.Id.Value != session.Stage.Game.Level.Id.Value)
            {
                throw new InvalidOperationException($"A restart opens level {restarted.Stage.Game.Level.Id.Value}, not level {session.Stage.Game.Level.Id.Value}.");
            }
        }

        /// <exception cref="InvalidOperationException">What the stage showed at the end of its last turn was not the engine's board.</exception>
        private static void RefuseFaults(GameStage stage)
        {
            if (stage.LastTurnFaults.Count > 0)
            {
                throw new InvalidOperationException($"After move {stage.Game.MovesUsed} the stage did not show the engine's board: {string.Join("; ", stage.LastTurnFaults)}");
            }
        }

        /// <summary>
        /// What a player finds who closes the game and opens it again, while the story's own session goes on: the
        /// story's progress file as it stands, and a session of its own opened on it and closed again at once.
        /// </summary>
        private sealed class Restart
        {
            private readonly string _folder;
            private readonly IReadOnlyList<LevelDefinition> _levels;
            private readonly ArtCatalogue _art;
            private readonly ScreenShape _screen;

            public Restart(string folder, IReadOnlyList<LevelDefinition> levels, ArtCatalogue art, ScreenShape screen)
            {
                _folder = folder;
                _levels = levels;
                _art = art;
                _screen = screen;
            }

            /// <summary>A win is the player's from the move that won it: the file holds it, and a restart opens the level after it.</summary>
            /// <exception cref="InvalidOperationException">The file does not hold the win, or a restart opens another level.</exception>
            public void RequireTheLevelAfter(LevelDefinition won, string when)
            {
                int levelsWon = Math.Min(StoryStudio.StandingOn(won, _levels).LevelsWon + 1, _levels.Count);
                PlayerProgress onFile = new ProgressFile(_folder).Load();
                if (onFile.LevelsWon != levelsWon)
                {
                    throw new InvalidOperationException($"Level {won.Id.Value} is won, and {when} the progress file holds \"{onFile}\": a player who closed the game now would lose the win.");
                }

                int next = _levels[onFile.LevelIndexIn(_levels.Count)].Id.Value;
                using GameSession restarted = StoryStudio.AtTheBoard(_levels, _art, new ProgressFile(_folder), _screen);
                if (restarted.Stage.Game.Level.Id.Value != next)
                {
                    throw new InvalidOperationException($"Level {won.Id.Value} is won, and a restart {when} opens level {restarted.Stage.Game.Level.Id.Value}, not level {next}.");
                }

                Debug.Log($"Storyboard: flow: level {won.Id.Value} is won; {when} the progress file holds \"{onFile}\" and a restart opens level {next}.");
            }
        }

        /// <summary>The pictures of one story: numbered in the order they are taken, and gathered on one sheet.</summary>
        private sealed class Reel
        {
            private readonly ScreenShape _screen;
            private readonly string _name;
            private readonly ContactSheet _sheet = new ContactSheet(StoryStudio.SheetShrinksBy);
            private int _shots;

            public Reel(GameSession session, ScreenShape screen, string name)
            {
                Session = session;
                _screen = screen;
                _name = name;
            }

            public GameSession Session { get; }

            public void Advance(float seconds) => StoryStudio.AdvanceBy(seconds, Session);

            public float AdvanceTo(float seconds, float clock) => StoryStudio.AdvanceTo(seconds, Session, clock);

            public void Settle() => StoryStudio.AdvanceUntilSettled(Session);

            public void Shoot(string what)
            {
                _shots++;
                StoryStudio.Shoot(Session, _screen, _sheet, $"{_name}-{_shots:D2}-{what}.png");
            }

            public void WriteSheet() => StoryStudio.WriteSheet(_sheet, $"{_name}-sheet.png");
        }
    }
}
