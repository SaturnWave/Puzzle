#nullable enable
using System;
using System.Collections.Generic;

namespace Puzzle.Client.Statistics
{
    /// <summary>
    /// One note of the play statistics: what happened, by its name, and the few numbers or words that go with it. These
    /// are all the notes the game ever sends (docs/GAME_DESIGN.md §20 lists them, and the privacy card and the store
    /// declarations say what they hold): the game's start, a level started, won or lost with its number and the moves
    /// left, a painting and a room completed, the shop opened, and a purchase given, by its product alone. None carries
    /// the phone's id, the player's id, a transaction or a receipt, or anything a player typed, so no note can be told
    /// to come from one phone rather than another. A closed set (private constructor): a new note is a new kind here,
    /// and a change of what the game says it sends.
    /// </summary>
    /// <remarks>
    /// The names and the properties' names are the statistics' own (Aptabase shows them as they are written), in its
    /// manner: lower case, words joined by an underscore. A property is a whole number or a word, the only kinds the
    /// service takes. No Unity dependency.
    /// </remarks>
    public abstract class PlayNote
    {
        private static readonly IReadOnlyDictionary<string, object> Nothing = new Dictionary<string, object>();

        private PlayNote(string name)
        {
            Name = name;
        }

        /// <summary>What happened, as the statistics name it: "level_win".</summary>
        public string Name { get; }

        /// <summary>What goes with it: each a whole number or a word; none for most notes.</summary>
        public virtual IReadOnlyDictionary<string, object> Properties => Nothing;

        public override string ToString()
        {
            var said = new List<string>();
            foreach (KeyValuePair<string, object> property in Properties)
            {
                said.Add($"{property.Key}={property.Value}");
            }

            return said.Count == 0 ? Name : $"{Name} ({string.Join(", ", said)})";
        }

        /// <summary>The game has started: once a run, as soon as the player allows the notes (PlayStatistics).</summary>
        public sealed class GameStarted : PlayNote
        {
            public GameStarted()
                : base("app_start")
            {
            }
        }

        /// <summary>A level's board has opened: its number, as the hall's play button shows it.</summary>
        public sealed class LevelStarted : PlayNote
        {
            public LevelStarted(int level)
                : base("level_start")
            {
                Level = level >= 1 ? level : throw new ArgumentOutOfRangeException(nameof(level), level, "Levels are counted from 1.");
            }

            public int Level { get; }

            public override IReadOnlyDictionary<string, object> Properties => new Dictionary<string, object> { ["level"] = Level };
        }

        /// <summary>An attempt at a level has ended for good, won or lost (out of moves, or left): its number and the moves left unplayed.</summary>
        public sealed class LevelEnded : PlayNote
        {
            public LevelEnded(int level, bool isWon, int movesLeft)
                : base(isWon ? "level_win" : "level_lose")
            {
                Level = level >= 1 ? level : throw new ArgumentOutOfRangeException(nameof(level), level, "Levels are counted from 1.");
                IsWon = isWon;
                MovesLeft = movesLeft >= 0 ? movesLeft : throw new ArgumentOutOfRangeException(nameof(movesLeft), movesLeft, "No board has fewer than no moves left.");
            }

            public int Level { get; }

            public bool IsWon { get; }

            public int MovesLeft { get; }

            public override IReadOnlyDictionary<string, object> Properties => new Dictionary<string, object> { ["level"] = Level, ["moves_left"] = MovesLeft };
        }

        /// <summary>A painting's last piece has come into its place: the painting's number in the museum and its room's.</summary>
        public sealed class PaintingCompleted : PlayNote
        {
            public PaintingCompleted(int painting, int room)
                : base("painting_complete")
            {
                Painting = painting >= 1 ? painting : throw new ArgumentOutOfRangeException(nameof(painting), painting, "Paintings are counted from 1.");
                Room = room >= 1 ? room : throw new ArgumentOutOfRangeException(nameof(room), room, "Rooms are counted from 1.");
            }

            public int Painting { get; }

            public int Room { get; }

            public override IReadOnlyDictionary<string, object> Properties => new Dictionary<string, object> { ["painting"] = Painting, ["room"] = Room };
        }

        /// <summary>A room's fifth painting has become whole: the room is restored. Its number.</summary>
        public sealed class RoomRestored : PlayNote
        {
            public RoomRestored(int room)
                : base("room_complete")
            {
                Room = room >= 1 ? room : throw new ArgumentOutOfRangeException(nameof(room), room, "Rooms are counted from 1.");
            }

            public int Room { get; }

            public override IReadOnlyDictionary<string, object> Properties => new Dictionary<string, object> { ["room"] = Room };
        }

        /// <summary>The museum has turned to the shop's page.</summary>
        public sealed class ShopOpened : PlayNote
        {
            public ShopOpened()
                : base("shop_open")
            {
            }
        }

        /// <summary>
        /// A payment of the store has been given to the player: which product, by the end of its id in both stores
        /// ("coins.pouch"), and nothing else of it: no price, no transaction, no receipt.
        /// </summary>
        public sealed class PurchaseCompleted : PlayNote
        {
            public PurchaseCompleted(string product)
                : base("purchase_complete")
            {
                Product = string.IsNullOrEmpty(product) ? throw new ArgumentException("A purchase is of a product.", nameof(product)) : product;
            }

            public string Product { get; }

            public override IReadOnlyDictionary<string, object> Properties => new Dictionary<string, object> { ["product"] = Product };
        }
    }
}
