#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Puzzle.Client.Art;
using Puzzle.Client.Engine;
using Puzzle.Client.Layout;
using Puzzle.Client.Play;
using Puzzle.Client.Stage;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using UnityEngine;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// A player's way through the levels: the stage of the attempt they are in, and the progress that decides which
    /// attempt comes next. A win opens the next level of the pack, a loss the same level again with the next seed
    /// of its calibrated pool, so a retry is another board of the same measured difficulty (the web test server
    /// deals attempts the same way). Like the stage it owns, the session only moves when it is told the time
    /// (<see cref="Advance"/>): the running game tells it every frame, the Editor's storyboard whenever it wants.
    /// </summary>
    /// <remarks>
    /// Progress is kept in the store the moment it changes: when the first move of an attempt is played (the attempt
    /// then counts, so its board is not dealt again), and when the engine says a level is won (before the win is
    /// even shown, so closing the game during the celebration does not lose it). The attempt in progress itself
    /// lives in memory only: a game that is closed in the middle of a level comes back on that level with its next
    /// board. A store that cannot be written does not stop the game; the progress is kept here and saved again at
    /// the next change, or when <see cref="SaveProgress"/> is called.
    /// </remarks>
    public sealed class GameSession : IDisposable
    {
        private readonly IReadOnlyList<LevelDefinition> _levels;
        private readonly ArtCatalogue _art;
        private readonly IProgressStore _store;

        /// <summary>What is thrown over the board, painted once for all the stages of this session: painting it anew for every level would hold up each level's first frame.</summary>
        private readonly EffectArt _effectArt;
        private ScreenShape _screen;
        private GameStage _stage;
        private PlayerProgress _progress;

        /// <summary>What the store was last given: the progress is saved again when it differs from this.</summary>
        private PlayerProgress _saved;

        /// <summary>How many moves of the open attempt the progress already knows of.</summary>
        private int _movesKept;

        /// <summary>Loads the player's progress and opens the level they stand on, laid out for a screen.</summary>
        /// <param name="levels">The levels in the order they are played.</param>
        /// <exception cref="ArgumentException">There is no level to play.</exception>
        /// <exception cref="IOException">The store's progress is there but cannot be read.</exception>
        /// <exception cref="UnauthorizedAccessException">The game may not read the store.</exception>
        public GameSession(IReadOnlyList<LevelDefinition> levels, ArtCatalogue art, IProgressStore store, ScreenShape screen)
        {
            if (levels.Count == 0)
            {
                throw new ArgumentException("A session needs a level to play.", nameof(levels));
            }

            _levels = levels;
            _art = art;
            _store = store;
            _screen = screen;
            _progress = store.Load();
            _saved = _progress;
            _effectArt = EffectArt.Paint();
            _stage = OpenTheStage(StageOpening.InTheLitRoom);
        }

        /// <summary>The stage of the attempt the player is in. It changes when the player goes on from a result.</summary>
        public GameStage Stage => _stage;

        /// <summary>The player's progress as it stands now, saved or not.</summary>
        public PlayerProgress Progress => _progress;

        /// <summary>True while the store does not hold the progress, because the last save failed.</summary>
        public bool HasUnsavedProgress => !_progress.Equals(_saved);

        /// <summary>Lays the stage out for a screen, and every later stage too; call again whenever the screen changes.</summary>
        public void Fit(ScreenShape screen)
        {
            _screen = screen;
            _stage.Fit(screen);
        }

        /// <summary>
        /// Moves the stage's clock on. A touch that waited for the board to come to rest may play its move here,
        /// which is kept as any other move is. When the player has gone on from a result, the next stage is opened
        /// here: the next level after a win, the same level's next board after a loss.
        /// </summary>
        public void Advance(float deltaSeconds)
        {
            _stage.Advance(deltaSeconds);
            KeepWhatWasPlayed();
            if (_stage.PlayerWentOn)
            {
                _stage.Dispose();
                _stage = OpenTheStage(StageOpening.OutOfTheDark);
            }
        }

        /// <summary>Plays a move on the stage, as <see cref="GameStage.TryPlay"/> does, and keeps what it means for the progress.</summary>
        public bool TryPlay(Move move)
        {
            bool isPlayed = _stage.TryPlay(move);
            KeepWhatWasPlayed();
            return isPlayed;
        }

        /// <summary>A finger came down on the screen. Pixels are counted from the screen's lower left corner.</summary>
        public void PointerDown(Vector2 pixel) => _stage.PointerDown(pixel);

        /// <summary>The finger moved while it was down; a drag towards a neighbouring cell plays a move at once when the board is at rest.</summary>
        public void PointerMoved(Vector2 pixel)
        {
            _stage.PointerMoved(pixel);
            KeepWhatWasPlayed();
        }

        /// <summary>The finger lifted; a tap on a neighbour of the picked piece, or on a special piece, plays a move.</summary>
        public void PointerUp()
        {
            _stage.PointerUp();
            KeepWhatWasPlayed();
        }

        /// <summary>The touch was taken away by the device, or the game lost the screen while a finger was down.</summary>
        public void PointerCancelled() => _stage.PointerCancelled();

        /// <summary>
        /// Saves the progress if the store does not hold it yet. The game calls this when it is put away, so a save
        /// that failed while playing gets another chance before the game may be closed.
        /// </summary>
        public void SaveProgress()
        {
            if (!HasUnsavedProgress)
            {
                return;
            }

            try
            {
                _store.Save(_progress);
                _saved = _progress;
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException)
            {
                // Playing goes on: the progress is still held here, and the next change or the next pause saves it again.
                Debug.LogWarning($"The progress ({_progress}) could not be saved and will be saved again later: {failure.Message}");
            }
        }

        public void Dispose()
        {
            _stage.Dispose();
            _effectArt.Dispose();
        }

        /// <summary>
        /// Opens the stage of the attempt the progress asks for: the level the player stands on, with the seed its
        /// pool has for this try.
        /// </summary>
        private GameStage OpenTheStage(StageOpening opening)
        {
            LevelDefinition level = _levels[_progress.LevelIndexIn(_levels.Count)];
            var brief = new AttemptBrief(_progress.AttemptsOnLevel + 1, _progress.IsOnLastLevelOf(_levels.Count));
            GameStage stage = GameStage.Open(LevelStart.Attempt(level, _progress.AttemptsOnLevel), _art, _effectArt, brief, opening);
            stage.Fit(_screen); // at once: the first frame of a level is drawn before the next step of the clock
            _movesKept = 0;
            return stage;
        }

        /// <summary>
        /// If the stage's game has moved on since the progress last looked: its first move makes the attempt count,
        /// and a move that won the level moves the player on to the next. Either is saved at once.
        /// </summary>
        private void KeepWhatWasPlayed()
        {
            Game game = _stage.Game;
            if (game.MovesUsed == _movesKept)
            {
                return;
            }

            if (_movesKept == 0)
            {
                _progress = _progress.WithAttemptStarted();
            }

            if (game.Status == GameStatus.Won)
            {
                _progress = _progress.WithLevelWon(_levels.Count);
            }

            _movesKept = game.MovesUsed;
            SaveProgress();
        }
    }
}
