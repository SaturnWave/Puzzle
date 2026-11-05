#nullable enable
using System.Collections.Generic;
using System.Linq;
using Puzzle.Client.Flow;
using Puzzle.Core.Levels;
using Puzzle.Museum;

namespace Puzzle.Client.Maps
{
    /// <summary>How a painting, or an era, stands on the world map for the player. A closed set.</summary>
    public enum MapMark
    {
        /// <summary>Whole again: a gilt dot, a pin with a green tick, a gilt medallion with a tick.</summary>
        Restored,

        /// <summary>The painting the player restores now, and its era: a red dot, a dimmed pin with the pieces won, a teal medallion in a red ring.</summary>
        Here,

        /// <summary>Still to come: a cream dot, a veiled pin, an ivory medallion with a lock. Its era, place, title and painter are seen; its picture is not.</summary>
        Ahead,
    }

    /// <summary>
    /// What the world map shows of a player's progress (#4): which paintings and eras are restored, which painting the
    /// player restores now and how many of its pieces winning the level at hand gives them, as the board's plaque
    /// counts them ("7 / 10"), and which level that is. No Unity dependency.
    /// </summary>
    public sealed class MapStanding
    {
        private readonly MuseumVisit _visit;

        private MapStanding(MuseumVisit visit)
        {
            _visit = visit;
        }

        public static MapStanding Of(MuseumVisit visit) => new MapStanding(visit);

        /// <summary>The painting the player restores now; nothing once every painting there is to restore hangs whole.</summary>
        public PaintingNumber? Here => _visit.BeingRestored;

        /// <summary>The era the player stands in: the camera glides there as the map opens.</summary>
        public RoomNumber Room => _visit.Room;

        /// <summary>The level the play button opens.</summary>
        public int Level => _visit.LevelToPlay;

        public MapMark MarkOf(PaintingNumber painting)
        {
            if (_visit.Progress.IsComplete(painting))
            {
                return MapMark.Restored;
            }

            return painting == Here ? MapMark.Here : MapMark.Ahead;
        }

        public MapMark MarkOf(RoomNumber room)
        {
            if (_visit.Progress.IsRestored(room))
            {
                return MapMark.Restored;
            }

            return room == Room ? MapMark.Here : MapMark.Ahead;
        }

        /// <summary>How many of an era's paintings hang whole.</summary>
        public int RestoredIn(RoomNumber room) => room.Paintings.Count(painting => MarkOf(painting) == MapMark.Restored);

        /// <summary>
        /// The pieces of the painting the player restores now, as its pin's badge and its card count them: how many
        /// they hold once the level at hand is won, of all its pieces; or, where that level gives pieces of another
        /// painting (a player who played on without placing), how many they hold already. Nothing while the player
        /// restores no painting.
        /// </summary>
        public (int Held, int All)? PiecesHere
        {
            get
            {
                if (!(Here is PaintingNumber here))
                {
                    return null;
                }

                int all = here.Size.PieceCount;
                var level = new LevelId(_visit.LevelToPlay);
                bool levelGivesItsPieces = !_visit.HasWonEveryLevel && _visit.LevelToPlay <= MuseumPlan.LevelCount && PaintingNumber.OfLevel(level) == here;
                if (levelGivesItsPieces)
                {
                    IReadOnlyList<int> given = MuseumProgress.PiecesGivenBy(level);
                    return (given[given.Count - 1] + 1, all);
                }

                return (_visit.Progress.PiecesWonIn(here), all);
            }
        }
    }
}
