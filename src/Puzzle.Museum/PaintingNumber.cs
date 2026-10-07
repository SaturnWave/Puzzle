using System;
using System.Collections.Generic;
using System.Globalization;
using Puzzle.Core.Levels;

namespace Puzzle.Museum
{
    /// <summary>
    /// Which painting of the museum, counted from 1 in the order the paintings are restored. The number says where a
    /// painting hangs and which ten levels give its pieces; which work of art hangs there is the catalogue's to say,
    /// and may not be chosen yet.
    /// </summary>
    public readonly struct PaintingNumber : IEquatable<PaintingNumber>
    {
        /// <summary>Counted from 0, so that the default value is the first painting and not one that does not exist.</summary>
        private readonly int _index;

        /// <exception cref="ArgumentOutOfRangeException">The museum has no painting of that number.</exception>
        public PaintingNumber(int value)
        {
            if (value < 1 || value > MuseumPlan.PaintingCount)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, $"The museum's paintings are numbered 1 to {MuseumPlan.PaintingCount}.");
            }

            _index = value - 1;
        }

        /// <summary>Every painting, in the order they are restored.</summary>
        public static IEnumerable<PaintingNumber> All
        {
            get
            {
                for (int value = 1; value <= MuseumPlan.PaintingCount; value++)
                {
                    yield return new PaintingNumber(value);
                }
            }
        }

        public int Value => _index + 1;

        public RoomNumber Room => new RoomNumber(_index / MuseumPlan.PaintingsPerRoom + 1);

        /// <summary>1 for the first painting of its room, 5 for the room's finale.</summary>
        public int PlaceInRoom => _index % MuseumPlan.PaintingsPerRoom + 1;

        public LevelId FirstLevel => new LevelId(_index * MuseumPlan.LevelsPerPainting + 1);

        public LevelId LastLevel => new LevelId(Value * MuseumPlan.LevelsPerPainting);

        /// <summary>How many pieces the painting is cut into, and how many of them each of its levels gives.</summary>
        public JigsawSize Size => JigsawSize.OfLevel(FirstLevel);

        /// <summary>The painting whose pieces a level gives.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The level lies beyond the museum's last painting.</exception>
        public static PaintingNumber OfLevel(LevelId level)
        {
            if (level.Value > MuseumPlan.LevelCount)
            {
                throw new ArgumentOutOfRangeException(nameof(level), level.Value, $"The museum's paintings cover the levels 1 to {MuseumPlan.LevelCount}.");
            }

            return new PaintingNumber((level.Value - 1) / MuseumPlan.LevelsPerPainting + 1);
        }

        public bool Equals(PaintingNumber other) => _index == other._index;

        public override bool Equals(object? obj) => obj is PaintingNumber other && Equals(other);

        public override int GetHashCode() => _index;

        public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

        public static bool operator ==(PaintingNumber left, PaintingNumber right) => left.Equals(right);

        public static bool operator !=(PaintingNumber left, PaintingNumber right) => !left.Equals(right);
    }
}
