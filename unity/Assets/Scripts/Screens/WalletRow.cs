#nullable enable
using System;
using Puzzle.Client.Art;
using Puzzle.Client.Flow;
using Puzzle.Client.Gallery;
using Puzzle.Client.Layout;
using Puzzle.Client.Play;
using Puzzle.Client.Stage;
using Puzzle.Meta;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Screens
{
    /// <summary>
    /// The row of the player's coins and lives over a museum page's plaque, as the design canvas draws it (HomeTR,
    /// MapTR, CollectionTR): a cream pill in a gilt rim with the coin standing on its left end and the amount in it,
    /// and a second pill with the heart on its left end, the lives that are there written on the heart, and in the
    /// pill "Dolu" or how long until the next life is back. The museum's stage owns the row, as it owns the
    /// navigation bar, so a page turns under it and the row stays. The heart's pill is pressed to open the lives
    /// card. Where everything stands is <see cref="WalletRowLayout"/>'s; sizes are the canvas' points, drawn at the
    /// share of them the page has (<see cref="PageLayout.WalletRowScale"/>).
    /// </summary>
    /// <remarks>
    /// The row has no clock of its own and is no act of the museum's timeline: the stage tells it how much time has
    /// passed, and coins that arrive count up as a function of that time (<see cref="Tempo.CoinsCount"/>), so the
    /// museum listens to the player while they do. The countdown to the next life is what the wallet it is told says.
    /// Not drawn yet, their places kept: the green plus at each pill's right end (the coins' leads to the shop) and
    /// the round button of the settings at the row's right end.
    /// </remarks>
    public sealed class WalletRow : IDisposable
    {
        /// <summary>The coin and the heart are drawn in a box of 100 points; the pill's picture is the pill and the room around it for its rim and drop: 28 points wider, 68 high.</summary>
        private const float MarkBox = 100f;
        private const float PillPictureMargin = 28f;
        private const float PillPictureHeight = 68f;

        /// <summary>The canvas' type: the amount at 16.5 points, "Dolu" and the wait at 15.5, the count on the heart at 18.</summary>
        private const float AmountSize = 16.5f;
        private const float WaitSize = 15.5f;
        private const float HeartCountSize = 18f;

        /// <summary>The words of a pill stand half a point under its middle (the canvas' margin of one point over them); the count stands 6 points over the heart's middle, in its round top.</summary>
        private const float WordsUnderPillMiddle = 0.5f;
        private const float HeartCountOverMiddle = 6f;

        private readonly Transform _root;
        private readonly SpriteRenderer _coinsPill;
        private readonly SpriteRenderer _coin;
        private readonly TextMeshPro _amount;
        private readonly SpriteRenderer _livesPill;
        private readonly SpriteRenderer _heart;
        private readonly TextMeshPro _heartCount;
        private readonly TextMeshPro _wait;
        private readonly LivesButton _livesButton;

        private WalletRowLayout _layout = WalletRowLayout.ForAnAmount(0f);

        /// <summary>Where the row was last placed on the page, and how large; nothing before <see cref="Place"/>.</summary>
        private PageLayout? _page;

        /// <summary>The time the row has been told of, and the count of coins from the amount shown when the told amount changed.</summary>
        private float _clock;
        private Coins _countFrom;
        private Coins _countTo;
        private float _countBegan;
        private Coins _shown;

        private WalletRow(Transform parent, ArtCatalogue art, WalletView wallet)
        {
            _root = new GameObject("Wallet row").transform;
            _root.SetParent(parent, worldPositionStays: false);

            _coinsPill = AddPill("Coins pill", art);
            _coin = AddMark("Coin", art, GalleryArt.IconCoin);
            _amount = Lettering.Write("Amount", _root, art, LetteringStyle.Figure, AmountSize, Lettering.Ink, DrawOrder.WalletLettering);

            _livesPill = AddPill("Lives pill", art);
            _heart = AddMark("Heart", art, GalleryArt.IconHeart);
            _heartCount = Lettering.Write("Lives", _root, art, LetteringStyle.OnHeart, HeartCountSize, Color.white, DrawOrder.WalletLettering);
            _wait = Lettering.Write("Next life", _root, art, LetteringStyle.Figure, WaitSize, Lettering.Ink, DrawOrder.WalletLettering);
            _livesButton = new LivesButton(this);

            _countFrom = _countTo = _shown = wallet.Coins;
            Wallet = wallet;
            ShowTheLives();
            ShowTheAmount(evenIfShown: true);
        }

        /// <summary>The wallet as the row was last told of it; the amount shown may still be counting towards its coins.</summary>
        public WalletView Wallet { get; private set; }

        /// <summary>The middle of the lives' button, in the layout's points: where a finger presses it. Nothing before the row is placed.</summary>
        public Vector2? LivesButtonMiddle => _page is PageLayout page ? InTheLayout(page, new Vector2(_layout.LivesButton.MiddleX, _layout.LivesButton.MiddleY)) : (Vector2?)null;

        /// <summary>Builds the row showing a wallet as it is, with nothing counting. Call <see cref="Place"/> before the first picture.</summary>
        public static WalletRow Create(Transform parent, ArtCatalogue art, WalletView wallet) => new WalletRow(parent, art, wallet);

        /// <summary>Lays the row over the plaque of a page; call again whenever the screen changes.</summary>
        public void Place(PageLayout page)
        {
            _page = page;
            _root.localPosition = new Vector3(page.WalletRow.Left, page.WalletRow.MiddleY, 0f);
            _root.localScale = new Vector3(page.WalletRowScale, page.WalletRowScale, 1f);
        }

        /// <summary>
        /// Shows the wallet as it is now: the lives at once, the coins counting from the amount shown to the new
        /// one when it differs from what the row was told before.
        /// </summary>
        public void Tell(WalletView wallet)
        {
            if (wallet.Coins != _countTo)
            {
                _countFrom = _shown;
                _countTo = wallet.Coins;
                _countBegan = _clock;
            }

            bool livesChanged = wallet.WithCoins(Wallet.Coins) != Wallet;
            Wallet = wallet;
            if (livesChanged)
            {
                ShowTheLives();
            }

            ShowTheAmount(evenIfShown: false);
        }

        /// <summary>Moves the row's clock on: an amount that counts shows where it has got to.</summary>
        public void Advance(float deltaSeconds)
        {
            _clock += deltaSeconds;
            ShowTheAmount(evenIfShown: false);
        }

        /// <summary>Tells a set of pressable controls about the row's: the lives' button asks for the lives card.</summary>
        public void AddPressables(PressSet<MuseumPress> presses) => presses.Add(_livesButton, new MuseumPress.OpenTheLivesCard());

        public void Dispose() => SceneObjects.DestroyObjectOf(_root);

        private SpriteRenderer AddPill(string name, ArtCatalogue art)
        {
            SpriteRenderer pill = SceneObjects.AddSprite(name, _root, art.Of(GalleryArt.WalletPill), art.SpriteMaterial, DrawOrder.WalletPill);
            pill.drawMode = SpriteDrawMode.Sliced; // the picture's middle stretches; its round ends keep their shape
            return pill;
        }

        private SpriteRenderer AddMark(string name, ArtCatalogue art, GalleryArt drawing)
        {
            SpriteRenderer mark = SceneObjects.AddSprite(name, _root, art.Of(drawing), art.SpriteMaterial, DrawOrder.WalletMark);
            mark.transform.localScale = new Vector3(WalletRowLayout.MarkSize / MarkBox, WalletRowLayout.MarkSize / MarkBox, 1f);
            return mark;
        }

        /// <summary>The amount where the count has got to; a longer amount widens its pill and moves the lives along.</summary>
        /// <param name="evenIfShown">True to write the amount and lay the row out although the amount is the one shown.</param>
        private void ShowTheAmount(bool evenIfShown)
        {
            float counted = _countTo == _countFrom ? 1f : Mathf.Clamp01((_clock - _countBegan) / Tempo.CoinsCount);
            var now = new Coins(_countFrom.Amount + (long)Math.Round((_countTo.Amount - _countFrom.Amount) * (double)Easing.Out(counted)));
            if (now == _shown && !evenIfShown)
            {
                return;
            }

            _shown = now;
            string words = WalletWords.Amount(now);
            _amount.fontSize = AmountSize;
            _layout = WalletRowLayout.ForAnAmount(Lettering.WidthOf(_amount, words));
            Lettering.SayWithin(_amount, words, AmountSize, _layout.AmountRoom);
            Arrange();
        }

        /// <summary>The count on the heart and the words in its pill, as the wallet the row was told of has them.</summary>
        private void ShowTheLives()
        {
            Lettering.Say(_heartCount, WalletWords.OnTheHeart(Wallet));
            Lettering.SayWithin(_wait, WalletWords.LivesPill(Wallet), WaitSize, WalletRowLayout.WaitRoom);
            Arrange();
        }

        /// <summary>Puts every part where the row's layout has it for the amount it shows.</summary>
        private void Arrange()
        {
            Box coinsPill = _layout.CoinsPillBox;
            _coinsPill.transform.localPosition = new Vector3(coinsPill.MiddleX, coinsPill.MiddleY, 0f);
            _coinsPill.size = new Vector2(coinsPill.Width + PillPictureMargin, PillPictureHeight);
            _coin.transform.localPosition = new Vector3(WalletRowLayout.Coin.MiddleX, WalletRowLayout.Coin.MiddleY, 0f);
            _amount.transform.localPosition = new Vector3(_layout.AmountMiddle, WordsHeight(coinsPill, LetteringStyle.Figure, _amount), 0f);

            Box livesPill = _layout.LivesPillBox;
            _livesPill.transform.localPosition = new Vector3(livesPill.MiddleX, livesPill.MiddleY, 0f);
            _livesPill.size = new Vector2(livesPill.Width + PillPictureMargin, PillPictureHeight);
            _heart.transform.localPosition = new Vector3(_layout.Heart.MiddleX, _layout.Heart.MiddleY, 0f);
            _heartCount.transform.localPosition = new Vector3(
                _layout.Heart.MiddleX, _layout.Heart.MiddleY + HeartCountOverMiddle - Lettering.FigureDrop(LetteringStyle.OnHeart) * _heartCount.fontSize, 0f);
            _wait.transform.localPosition = new Vector3(_layout.WaitMiddle, WordsHeight(livesPill, LetteringStyle.Figure, _wait), 0f);
        }

        /// <summary>Where a pill's words stand upright: as the canvas centres them in the pill, figures seated as a stylesheet seats them.</summary>
        private static float WordsHeight(Box pill, LetteringStyle style, TextMeshPro words) =>
            pill.MiddleY - WordsUnderPillMiddle - Lettering.FigureDrop(style) * words.fontSize;

        /// <summary>Where a point of the row, in the canvas' points, lies on the page.</summary>
        private static Vector2 InTheLayout(PageLayout page, Vector2 onTheRow) =>
            new Vector2(page.WalletRow.Left + onTheRow.x * page.WalletRowScale, page.WalletRow.MiddleY + onTheRow.y * page.WalletRowScale);

        /// <summary>The heart, its pill and the plus's place: one button that opens the lives card.</summary>
        private sealed class LivesButton : IPressable
        {
            private readonly WalletRow _row;

            public LivesButton(WalletRow row)
            {
                _row = row;
            }

            public bool IsUnder(Vector2 point)
            {
                if (!(_row._page is PageLayout page))
                {
                    return false;
                }

                var onTheRow = new Vector2((point.x - page.WalletRow.Left) / page.WalletRowScale, (point.y - page.WalletRow.MiddleY) / page.WalletRowScale);
                return _row._layout.LivesButton.Contains(onTheRow.x, onTheRow.y);
            }

            public void ShowPressed(bool isPressed)
            {
                Color light = Dimming.Of(isDimmed: false, isHeld: isPressed);
                _row._livesPill.color = light;
                _row._heart.color = light;
            }
        }
    }
}
