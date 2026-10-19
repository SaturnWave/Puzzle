#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Puzzle.Client.Flow;

namespace Puzzle.Client.Play
{
    /// <summary>A cue at its moment of a script: seconds after the move, the booster or the finale began, on the script's own clock.</summary>
    public readonly struct SoundNote : IEquatable<SoundNote>
    {
        public SoundNote(float at, SoundCue cue)
        {
            At = at;
            Cue = cue;
        }

        public float At { get; }

        public SoundCue Cue { get; }

        public bool Equals(SoundNote other) => At.Equals(other.At) && Cue == other.Cue;

        public override bool Equals(object? obj) => obj is SoundNote other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(At, Cue);

        public override string ToString() => $"{Cue} at {At:0.###} s";
    }

    /// <summary>
    /// Which sound falls at which moment of the game, and what of it reaches the player. A turn's script, a booster's
    /// and a won game's finale are heard as they are seen: a trade as the pieces start to slide, a pop as a piece
    /// bursts, a special piece as it appears, a blast as a special piece's effect sets out, a booster as its tool
    /// strikes, a move of the finale as it leaves the medallion. A card is heard as it comes down: the card of a won
    /// level as the win, of a level lost for good as the loss, every other as a card. The phone buzzes under five cues
    /// only (<see cref="TickOf"/>), and one of them, a hint shown, is felt and not heard (<see cref="IsHeard"/>). This
    /// file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    /// <remarks>
    /// A script is not heard cue by cue: a cascade pops a dozen pieces in a few hundredths of a second, a finale's
    /// volley throws its moves and fires its rockets many to a tenth of a second, and every one of them heard is a
    /// rattle. One cue is heard again only once <see cref="SameCueApart"/> has passed since it was last heard in the
    /// same script; what falls sooner is heard in the one before it. Different cues do not wait for each other. In a
    /// finale the rocket each thrown move makes is heard as its throw, not again as it appears; a booster's own sound
    /// stands for the shuffle's dealing.
    /// </remarks>
    public static class SoundScore
    {
        /// <summary>
        /// How far apart, in a script's seconds, two sounds of one cue are at least: about the time a finale's moves
        /// leave the medallion apart (<see cref="Tempo.MovesLeaveEvery"/>), so that a volley is heard as a run of
        /// plucks and a cascade as a few pops, not as a buzz.
        /// </summary>
        public const float SameCueApart = 0.12f;

        /// <summary>What is heard of a move: the trade, the pops, the special pieces made and gone off, a board dealt anew.</summary>
        public static IReadOnlyList<SoundNote> OfATurn(TurnScript script) => Thinned(script.Cues.SelectMany(NotesOf));

        /// <summary>
        /// What is heard of a booster used during play: the booster itself as its tool strikes the board (as it is
        /// used, for the shuffle, which has no tool and whose dealing it stands for), then what it hits as in a move.
        /// </summary>
        public static IReadOnlyList<SoundNote> OfABooster(TurnScript script)
        {
            Cue.ToolUsed? tool = script.Cues.OfType<Cue.ToolUsed>().FirstOrDefault();
            var strike = new SoundNote(tool?.StrikesAt ?? 0f, SoundCue.Booster);
            IEnumerable<SoundNote> hits = script.Cues.Where(cue => !(cue is Cue.Reshuffle)).SelectMany(NotesOf);
            return Thinned(hits.Prepend(strike));
        }

        /// <summary>
        /// What is heard of a won game's finale: every move left over as it leaves the medallion, the rockets as they
        /// go off, and what they and the pieces that fall in pop. The rocket a move makes is heard as its throw.
        /// </summary>
        public static IReadOnlyList<SoundNote> OfTheFinale(TurnScript script) =>
            Thinned(script.Cues.Where(cue => !(cue is Cue.Appear)).SelectMany(NotesOf));

        /// <summary>What is heard as a card comes down in front of the board: the win, the loss for good, or a card (the question to leave, the offer of moves).</summary>
        public static SoundCue OfACard(LevelResult tells) => tells switch
        {
            LevelResult.Won _ => SoundCue.Win,
            LevelResult.Lost _ => SoundCue.Lose,
            LevelResult.Leaving _ => SoundCue.CardDown,
            LevelResult.OutOfMoves _ => SoundCue.CardDown,
            _ => throw new ArgumentOutOfRangeException(nameof(tells), tells, "A card the score does not know."),
        };

        /// <summary>
        /// How the phone buzzes under a cue; nothing for most. A special piece made and a booster's strike tick
        /// lightly, a won level and a painting made whole firmly: the moments the player made happen, not the pops
        /// that follow them. A hint ticks lightly too: it is the one cue that comes while nothing moves.
        /// </summary>
        public static Tick? TickOf(SoundCue cue) => cue switch
        {
            SoundCue.SpecialMade => Tick.Light,
            SoundCue.Booster => Tick.Light,
            SoundCue.Win => Tick.Firm,
            SoundCue.PaintingWhole => Tick.Firm,
            SoundCue.Hint => Tick.Light,
            SoundCue.Press => null,
            SoundCue.Refuse => null,
            SoundCue.Swap => null,
            SoundCue.Match => null,
            SoundCue.Blast => null,
            SoundCue.MoveThrown => null,
            SoundCue.CardDown => null,
            SoundCue.Lose => null,
            SoundCue.Coin => null,
            SoundCue.PiecePlaced => null,
            SoundCue.Toggle => null,
            _ => throw new ArgumentOutOfRangeException(nameof(cue), cue, "A cue the score does not know."),
        };

        /// <summary>
        /// Whether a cue has a sound. A hint has none: it comes while the player thinks, and only a tick goes with it.
        /// </summary>
        public static bool IsHeard(SoundCue cue) => cue != SoundCue.Hint;

        /// <summary>
        /// What of a cue reaches the player under the settings as they are at the moment it falls: its sound (where it
        /// has one) unless sound is off, its tick (where it has one) unless vibration is off.
        /// </summary>
        public static (SoundCue? Sound, Tick? Tick) Reaching(SoundCue cue, DeviceSettings settings) =>
            (settings.Sound && IsHeard(cue) ? cue : (SoundCue?)null, settings.Vibration ? TickOf(cue) : null);

        /// <summary>The cue a cue of a script sounds, at its beginning; none for what is seen and not heard.</summary>
        /// <exception cref="InvalidOperationException">A kind of cue the score does not know.</exception>
        private static IEnumerable<SoundNote> NotesOf(Cue cue)
        {
            switch (cue)
            {
                case Cue.Slide { Manner: SlideManner.Displaced } _:
                    break; // the piece that makes room slides with the one the player moved: one trade, one sound
                case Cue.Slide slide:
                    yield return new SoundNote(slide.Start, SoundCue.Swap);
                    break;
                case Cue.Reshuffle reshuffle:
                    yield return new SoundNote(reshuffle.Start, SoundCue.Swap);
                    break;
                case Cue.Leave leave when leave.Manner == LeaveManner.Pop || leave.Manner == LeaveManner.Gather || leave.Manner == LeaveManner.Break:
                    yield return new SoundNote(leave.Start, SoundCue.Match);
                    break;
                case Cue.Reface reface:
                    yield return new SoundNote(reface.Start, SoundCue.Match);
                    break;
                case Cue.Appear appear:
                    yield return new SoundNote(appear.Start, SoundCue.SpecialMade);
                    break;
                case Cue.Effect effect:
                    yield return new SoundNote(effect.Start, SoundCue.Blast);
                    break;
                case Cue.MoveThrown thrown:
                    yield return new SoundNote(thrown.Start, SoundCue.MoveThrown);
                    break;
                case Cue.Leave _: // a special piece that fires is heard in its effect; an item that drops out with the pops around it
                case Cue.MoveSpent _:
                case Cue.GoalTick _:
                case Cue.Fall _:
                case Cue.Enter _:
                case Cue.Slip _:
                case Cue.TapeHit _: // a tape or a slab is hit where a piece pops
                case Cue.SlabHit _:
                case Cue.Flight _:  // a seeker is heard where it lands
                case Cue.Zap _:     // a colour bomb's rays in its light
                case Cue.ToolUsed _: // a booster's tool by OfABooster, at its strike
                case Cue.FinaleOpens _:
                    break;
                default:
                    throw new InvalidOperationException($"The score has no sound for a {cue.GetType().Name}.");
            }
        }

        /// <summary>The notes in the order they fall, each cue heard again only <see cref="SameCueApart"/> after it was last.</summary>
        private static IReadOnlyList<SoundNote> Thinned(IEnumerable<SoundNote> notes)
        {
            var heard = new List<SoundNote>();
            var lastHeard = new Dictionary<SoundCue, float>();
            foreach (SoundNote note in notes.OrderBy(note => note.At))
            {
                if (lastHeard.TryGetValue(note.Cue, out float last) && note.At - last < SameCueApart)
                {
                    continue;
                }

                lastHeard[note.Cue] = note.At;
                heard.Add(note);
            }

            return heard;
        }
    }
}
