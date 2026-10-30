using Puzzle.Core.Model;

namespace Puzzle.Core.Engine
{
    /// <summary>What happened during one move (or a whole game, for totals). Immutable snapshot.</summary>
    public sealed class PlayStats
    {
        public static PlayStats None { get; } = new PlayStats();

        public int GemsCleared { get; init; }

        public int RocketsCreated { get; init; }

        /// <summary>Small (3x3) bombs.</summary>
        public int BombsCreated { get; init; }

        public int BigBombsCreated { get; init; }

        public int SeekersCreated { get; init; }

        public int ColorBombsCreated { get; init; }

        public int SpecialsCreated => RocketsCreated + BombsCreated + BigBombsCreated + SeekersCreated + ColorBombsCreated;

        public int SpecialsActivated { get; init; }

        /// <summary>Match rounds after the first one, i.e. chain reactions caused by falling tiles.</summary>
        public int Cascades { get; init; }

        public int BoxHpRemoved { get; init; }

        public int BoxesDestroyed { get; init; }

        public int JellyLayersRemoved { get; init; }

        public int JellyCleared { get; init; }

        public int ChainHpRemoved { get; init; }

        public int ChainsBroken { get; init; }

        public int ItemsCollected { get; init; }

        public int Shuffles { get; init; }
    }

    /// <summary>Mutable accumulator behind <see cref="PlayStats"/>.</summary>
    internal sealed class StatsCounter
    {
        public int GemsCleared;
        public int RocketsCreated;
        public int BombsCreated;
        public int BigBombsCreated;
        public int SeekersCreated;
        public int ColorBombsCreated;
        public int SpecialsActivated;
        public int Cascades;
        public int BoxHpRemoved;
        public int BoxesDestroyed;
        public int JellyLayersRemoved;
        public int JellyCleared;
        public int ChainHpRemoved;
        public int ChainsBroken;
        public int ItemsCollected;
        public int Shuffles;

        public void RecordSpecialCreated(TileKind kind)
        {
            switch (kind)
            {
                case TileKind.RocketH:
                case TileKind.RocketV:
                    RocketsCreated++;
                    break;
                case TileKind.Bomb:
                    BombsCreated++;
                    break;
                case TileKind.BigBomb:
                    BigBombsCreated++;
                    break;
                case TileKind.Seeker:
                    SeekersCreated++;
                    break;
                case TileKind.ColorBomb:
                    ColorBombsCreated++;
                    break;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(kind), kind, "Not a special tile kind.");
            }
        }

        public void AddTo(StatsCounter total)
        {
            total.GemsCleared += GemsCleared;
            total.RocketsCreated += RocketsCreated;
            total.BombsCreated += BombsCreated;
            total.BigBombsCreated += BigBombsCreated;
            total.SeekersCreated += SeekersCreated;
            total.ColorBombsCreated += ColorBombsCreated;
            total.SpecialsActivated += SpecialsActivated;
            total.Cascades += Cascades;
            total.BoxHpRemoved += BoxHpRemoved;
            total.BoxesDestroyed += BoxesDestroyed;
            total.JellyLayersRemoved += JellyLayersRemoved;
            total.JellyCleared += JellyCleared;
            total.ChainHpRemoved += ChainHpRemoved;
            total.ChainsBroken += ChainsBroken;
            total.ItemsCollected += ItemsCollected;
            total.Shuffles += Shuffles;
        }

        public void Reset()
        {
            GemsCleared = 0;
            RocketsCreated = 0;
            BombsCreated = 0;
            BigBombsCreated = 0;
            SeekersCreated = 0;
            ColorBombsCreated = 0;
            SpecialsActivated = 0;
            Cascades = 0;
            BoxHpRemoved = 0;
            BoxesDestroyed = 0;
            JellyLayersRemoved = 0;
            JellyCleared = 0;
            ChainHpRemoved = 0;
            ChainsBroken = 0;
            ItemsCollected = 0;
            Shuffles = 0;
        }

        public StatsCounter Clone() => (StatsCounter)MemberwiseClone();

        public PlayStats ToSnapshot() => new PlayStats
        {
            GemsCleared = GemsCleared,
            RocketsCreated = RocketsCreated,
            BombsCreated = BombsCreated,
            BigBombsCreated = BigBombsCreated,
            SeekersCreated = SeekersCreated,
            ColorBombsCreated = ColorBombsCreated,
            SpecialsActivated = SpecialsActivated,
            Cascades = Cascades,
            BoxHpRemoved = BoxHpRemoved,
            BoxesDestroyed = BoxesDestroyed,
            JellyLayersRemoved = JellyLayersRemoved,
            JellyCleared = JellyCleared,
            ChainHpRemoved = ChainHpRemoved,
            ChainsBroken = ChainsBroken,
            ItemsCollected = ItemsCollected,
            Shuffles = Shuffles,
        };
    }
}
