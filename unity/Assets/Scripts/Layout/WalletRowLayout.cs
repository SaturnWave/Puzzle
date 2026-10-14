#nullable enable
using System;

namespace Puzzle.Client.Layout
{
    /// <summary>
    /// Where everything of the wallet row stands, as the design canvas lays the row out over a museum page's plaque
    /// (HomeTR, MapTR, CollectionTR): as wide as the plaque, from its left end the coins' pill with the coin standing
    /// on the pill's left end, after it the lives' pill with the heart on its left end, and at the row's right end
    /// the round button of the settings. Each pill has a green plus at its right end, which stands over the pill.
    /// A longer amount of coins widens the coins' pill and moves the lives along, up to the room the settings'
    /// button leaves. Lengths are the canvas' points, across from the row's left edge and upright from the middle of
    /// the pills; whoever places the row draws it at a share of that (<see cref="PageLayout.WalletRowScale"/>).
    /// No Unity dependency.
    /// </summary>
    public sealed class WalletRowLayout
    {
        /// <summary>The row is as wide as the canvas' plaque, and as high as the coin and the heart, which reach over the pills' edges.</summary>
        public const float Width = 362f;
        public const float Height = 46f;

        /// <summary>The pills' middle lies this far under the middle of the row: the coin and the heart stand a point higher than the pills they sit on.</summary>
        public const float PillsUnderMiddle = 1f;

        /// <summary>A pill is 36 points high; the coin and the heart are 46 across and stand on its left end, which begins this far from theirs.</summary>
        public const float PillHeight = 36f;
        public const float MarkSize = 46f;
        public const float PillInFromMark = 22f;

        /// <summary>The coins' pill holds "1.250" at 100 points and is widened for a longer amount, up to what leaves the settings' button its room.</summary>
        public const float NarrowestCoinsPill = 100f;
        public const float LivesPill = 92f;

        /// <summary>A pill's words keep this far from its left end, where the mark covers it, and from its right end, where the plus stands.</summary>
        public const float WordsInFromLeft = 26f;
        public const float WordsInFromRight = 20f;

        /// <summary>The green plus of "get more": 26 points across, its middle a point in from its pill's right end.</summary>
        public const float PlusSize = 26f;
        public const float PlusInFromPillEnd = 1f;

        /// <summary>The lives' button begins this far after the coins' plus ends.</summary>
        public const float GapBetweenButtons = 16f;

        /// <summary>The settings' round button: 44 across, its middle this far from the row's left edge (the plaque's right end, less 20).</summary>
        public const float GearSize = 44f;
        public const float GearMiddle = 342f;

        /// <summary>The lives' plus keeps at least this far from the settings' button.</summary>
        public const float LeastGapToGear = 8f;

        private WalletRowLayout(float coinsPill)
        {
            CoinsPill = coinsPill;
        }

        /// <summary>The widest the coins' pill is drawn: the lives then stand as near the settings' button as they may.</summary>
        public static float WidestCoinsPill =>
            GearMiddle - GearSize / 2f - LeastGapToGear - (PillInFromMark + PlusSize / 2f - PlusInFromPillEnd + GapBetweenButtons + PillInFromMark + LivesPill + PlusSize / 2f - PlusInFromPillEnd);

        /// <summary>How wide the coins' pill is drawn.</summary>
        public float CoinsPill { get; }

        /// <summary>The coin: its box, upright around the row's middle.</summary>
        public static Box Coin => Box.Around(MarkSize / 2f, 0f, MarkSize, MarkSize);

        /// <summary>The coins' pill.</summary>
        public Box CoinsPillBox => new Box(PillInFromMark, -PillsUnderMiddle - PillHeight / 2f, CoinsPill, PillHeight);

        /// <summary>The middle of the room the amount has in its pill, and how wide that room is.</summary>
        public float AmountMiddle => CoinsPillBox.Left + WordsInFromLeft + AmountRoom / 2f;

        public float AmountRoom => CoinsPill - WordsInFromLeft - WordsInFromRight;

        /// <summary>The place of the coins' plus.</summary>
        public Box CoinsPlus => Box.Around(CoinsPillBox.Right - PlusInFromPillEnd, CoinsPillBox.MiddleY, PlusSize, PlusSize);

        /// <summary>The heart, the first thing of the lives' button.</summary>
        public Box Heart => Box.Around(CoinsPlus.Right + GapBetweenButtons + MarkSize / 2f, 0f, MarkSize, MarkSize);

        public Box LivesPillBox => new Box(Heart.Left + PillInFromMark, -PillsUnderMiddle - PillHeight / 2f, LivesPill, PillHeight);

        /// <summary>The middle of the room the wait for the next life (or "Dolu") has in its pill, and how wide that room is.</summary>
        public float WaitMiddle => LivesPillBox.Left + WordsInFromLeft + WaitRoom / 2f;

        public static float WaitRoom => LivesPill - WordsInFromLeft - WordsInFromRight;

        public Box LivesPlus => Box.Around(LivesPillBox.Right - PlusInFromPillEnd, LivesPillBox.MiddleY, PlusSize, PlusSize);

        /// <summary>All that a finger presses for the lives: the heart, its pill and its plus.</summary>
        public Box LivesButton => new Box(Heart.Left, -Height / 2f, LivesPlus.Right - Heart.Left, Height);

        /// <summary>The place of the settings' round button.</summary>
        public static Box Gear => Box.Around(GearMiddle, -PillsUnderMiddle, GearSize, GearSize);

        /// <summary>The row with a coins' pill wide enough for words of this width, as far as the row has room; wider words are set smaller to fit.</summary>
        /// <param name="amountWidth">How wide the amount is written at its full size, in the canvas' points.</param>
        /// <exception cref="ArgumentOutOfRangeException">A width less than nothing.</exception>
        public static WalletRowLayout ForAnAmount(float amountWidth)
        {
            if (!(amountWidth >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(amountWidth), amountWidth, "Words are nothing or more wide.");
            }

            float wished = amountWidth + WordsInFromLeft + WordsInFromRight;
            return new WalletRowLayout(Math.Min(WidestCoinsPill, Math.Max(NarrowestCoinsPill, wished)));
        }
    }
}
