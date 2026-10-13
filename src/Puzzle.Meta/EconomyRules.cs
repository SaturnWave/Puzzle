using System;

namespace Puzzle.Meta
{
    /// <summary>
    /// Every rule of the game's economy with the numbers of one settings file in it: what lives, wins, rewards, the
    /// extra moves, boosters and the shop give and cost. It is built from the packed settings
    /// (<see cref="Settings.EconomyFileMapper"/>) and holds no number of its own, so two builds with the same file
    /// play by the same economy.
    /// </summary>
    public sealed class EconomyRules
    {
        public EconomyRules(
            LifeRules lives,
            WinReward win,
            PaintingReward paintingCompleted,
            DailyReward daily,
            ExtraMovesRules extraMoves,
            StartBoosterRules startBoosters,
            Shop shop,
            Wallet startingWallet,
            AdSource ads)
        {
            Lives = lives ?? throw new ArgumentNullException(nameof(lives));
            Win = win ?? throw new ArgumentNullException(nameof(win));
            PaintingCompleted = paintingCompleted ?? throw new ArgumentNullException(nameof(paintingCompleted));
            Daily = daily ?? throw new ArgumentNullException(nameof(daily));
            ExtraMoves = extraMoves ?? throw new ArgumentNullException(nameof(extraMoves));
            StartBoosters = startBoosters ?? throw new ArgumentNullException(nameof(startBoosters));
            Shop = shop ?? throw new ArgumentNullException(nameof(shop));
            StartingWallet = startingWallet ?? throw new ArgumentNullException(nameof(startingWallet));
            Ads = ads;
        }

        public LifeRules Lives { get; }

        public WinReward Win { get; }

        public PaintingReward PaintingCompleted { get; }

        public DailyReward Daily { get; }

        public ExtraMovesRules ExtraMoves { get; }

        public StartBoosterRules StartBoosters { get; }

        /// <summary>The single prices of the boosters, the sets, the coin packs and the coins of an ad; it also says that nothing is sold for money.</summary>
        public Shop Shop { get; }

        /// <summary>What a new player holds: the settings' starting coins and boosters, every life, nothing used.</summary>
        public Wallet StartingWallet { get; }

        /// <summary>Where this build's ads come from: what the client shows when a rule asks for one.</summary>
        public AdSource Ads { get; }
    }
}
