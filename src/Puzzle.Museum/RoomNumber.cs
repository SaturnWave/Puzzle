using System;
using System.Collections.Generic;
using System.Globalization;
using Puzzle.Core.Levels;

namespace Puzzle.Museum
{
    /// <summary>
    /// Which room of the museum, counted from 1 in the order the rooms open. A room holds five paintings and spans
    /// fifty levels.
    /// </summary>
    public readonly struct RoomNumber : IEquatable<RoomNumber>
    {
        /// <summary>Counted from 0, so that the default value is the first room and not a room that does not exist.</summary>
        private readonly int _index;

        /// <exception cref="ArgumentOutOfRangeException">The museum has no room of that number.</exception>
        public RoomNumber(int value)
        {
            if (value < 1 || value > MuseumPlan.RoomCount)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, $"The museum's rooms are numbered 1 to {MuseumPlan.RoomCount}.");
            }

            _index = value - 1;
        }

        /// <summary>Every room, in the order they open.</summary>
        public static IEnumerable<RoomNumber> All
        {
            get
            {
                for (int value = 1; value <= MuseumPlan.RoomCount; value++)
                {
                    yield return new RoomNumber(value);
                }
            }
        }

        public int Value => _index + 1;

        public LevelId FirstLevel => new LevelId(LevelsWonToOpen + 1);

        public LevelId LastLevel => new LevelId(Value * MuseumPlan.LevelsPerRoom);

        /// <summary>
        /// A room opens when every level of the rooms before it is won, whether or not their pieces are placed yet:
        /// the levels never wait for the jigsaw. The first room is open from the start.
        /// </summary>
        public int LevelsWonToOpen => _index * MuseumPlan.LevelsPerRoom;

        /// <summary>The room's five paintings, in the order they are restored.</summary>
        public IEnumerable<PaintingNumber> Paintings
        {
            get
            {
                for (int place = 1; place <= MuseumPlan.PaintingsPerRoom; place++)
                {
                    yield return new PaintingNumber(_index * MuseumPlan.PaintingsPerRoom + place);
                }
            }
        }

        public bool Equals(RoomNumber other) => _index == other._index;

        public override bool Equals(object? obj) => obj is RoomNumber other && Equals(other);

        public override int GetHashCode() => _index;

        public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

        public static bool operator ==(RoomNumber left, RoomNumber right) => left.Equals(right);

        public static bool operator !=(RoomNumber left, RoomNumber right) => !left.Equals(right);
    }
}
