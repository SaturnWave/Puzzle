#nullable enable
using System;
using System.Collections.Generic;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// A moment of the game the player hears: a closed set, each with one short sound (unity/Assets/Art/Sounds, where
    /// each came from in art/sounds/licences.json; stand-ins until the owner has chosen the game's own). Which cue falls
    /// at which moment is the score's (Play/SoundScore.cs); whether it is heard and felt, the player's settings' at
    /// that moment. This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public enum SoundCue
    {
        /// <summary>A finger pressed a button, a disc or a place on the bar.</summary>
        Press,

        /// <summary>What the finger asked for is not done: two pieces that do not trade, a booster that would change nothing.</summary>
        Refuse,

        /// <summary>Two pieces trade places, or the board's gems are dealt anew.</summary>
        Swap,

        /// <summary>Pieces pop: a match, a hit that takes a piece, a crate that takes a hit.</summary>
        Match,

        /// <summary>A special piece appears.</summary>
        SpecialMade,

        /// <summary>A special piece goes off: a rocket's stroke, a bomb's blast, a colour bomb's light.</summary>
        Blast,

        /// <summary>A booster's tool strikes the board, or the shuffle deals the gems anew.</summary>
        Booster,

        /// <summary>A move left over leaves the medallion in a won game's finale.</summary>
        MoveThrown,

        /// <summary>A card comes down on its wires.</summary>
        CardDown,

        /// <summary>The card of a won level comes down.</summary>
        Win,

        /// <summary>The card of a level lost for good comes down.</summary>
        Lose,

        /// <summary>Coins arrive in the museum's row, or leave it.</summary>
        Coin,

        /// <summary>A jigsaw piece drops into its place.</summary>
        PiecePlaced,

        /// <summary>A painting is whole: its light runs over it.</summary>
        PaintingWhole,

        /// <summary>A switch of the settings card is flipped.</summary>
        Toggle,

        /// <summary>A hint shows itself on a board the player has left untouched: felt as a light tick and not heard, so it does not break into their thinking.</summary>
        Hint,
    }

    /// <summary>How the phone's short buzz under a cue feels. A closed set.</summary>
    public enum Tick
    {
        /// <summary>A light click: a special piece made, a booster's strike, a hint shown.</summary>
        Light,

        /// <summary>A firm click: a level won, a painting made whole.</summary>
        Firm,
    }

    /// <summary>
    /// Where the cues the player is to hear and feel go: the phone's speaker and vibrator in the game (GameRoot's sound
    /// player), nowhere in the Editor's tools (<see cref="CuePlayer.Silent"/>). Whoever calls it has asked the player's
    /// settings first: what reaches this is to be heard, or felt.
    /// </summary>
    public interface ICuePlayer
    {
        /// <summary>Plays a cue's sound once, over whatever else sounds.</summary>
        void Sound(SoundCue cue);

        /// <summary>Lets the phone buzz once, shortly.</summary>
        void Buzz(Tick tick);
    }

    /// <summary>The cue players there are besides the phone's.</summary>
    public static class CuePlayer
    {
        /// <summary>Hears and feels nothing: the Editor's tools, whose sessions play whole games in a moment, and the tests.</summary>
        public static ICuePlayer Silent { get; } = new Silence();

        private sealed class Silence : ICuePlayer
        {
            public void Sound(SoundCue cue)
            {
            }

            public void Buzz(Tick tick)
            {
            }
        }
    }

    /// <summary>
    /// The cues a stage has sounded that its owner has not taken yet, in the order they fell. Bounded: a stage nobody
    /// takes them from (the Editor's tools drive stages without a session) keeps the latest <see cref="Kept"/> and lets
    /// the older ones go unheard, as they would be.
    /// </summary>
    public sealed class CuesSounded
    {
        /// <summary>More than any step of the clock sounds: a step that sounded more keeps its latest.</summary>
        public const int Kept = 32;

        private readonly Queue<SoundCue> _cues = new Queue<SoundCue>();

        public void Add(SoundCue cue)
        {
            if (_cues.Count == Kept)
            {
                _cues.Dequeue();
            }

            _cues.Enqueue(cue);
        }

        /// <summary>Every cue sounded since the last time, the earliest first; none are kept after.</summary>
        public IReadOnlyList<SoundCue> TakeAll()
        {
            if (_cues.Count == 0)
            {
                return Array.Empty<SoundCue>();
            }

            SoundCue[] taken = _cues.ToArray();
            _cues.Clear();
            return taken;
        }
    }
}
