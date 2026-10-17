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
    /// pill "Dolu" or how long until the next life is back; at each pill's right end the green plus of "get more".
    /// The museum's stage owns the row, as it owns the navigation bar, so a page turns under it and the row stays.
    /// The coin, its pill and its plus are pressed to go to the shop; the heart, its pill and its plus to open the
    /// lives card (no life is sold); the teal gear at the row's right end to open the settings card. In the tester mode
    /// the amount and the count on the heart are "∞" and the lives pill says "Test" (<see cref="WalletWords"/>). Where everything
    /// stands is <see cref="WalletRowLayout"/>'s; sizes are the canvas' points, drawn at the share of them the page
    /// has (<see cref="PageLayout.WalletRowScale"/>).
    /// </summary>
    /// <remarks>
    /// The row has no clock of its own and is no act of the museum's timeline: the stage tells it how much time has
    /// passed, and coins that arrive count up as a function of that time (<see cref="Tempo.CoinsCount"/>), so the
    /// museum listens to the player while they do. The countdown to the next life is what the wallet it is told says.
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
        private readonly DiscButton _coinsPlus;
        private readonly DiscButton _livesPlus;
        private readonly RowButton _coinsButton;
        private readonly RowButton _livesButton;
        private readonly DiscButton _gear;
        private readonly GearButton _gearButton;

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
            _coinsPlus = new DiscButton("Coins plus", _root, art, DiscGlyph.Plus, DrawOrder.WalletButton);
            _livesPlus = new DiscButton("Lives plus", _root, art, DiscGlyph.Plus, DrawOrder.WalletButton);
            _coinsButton = new RowButton(this, layout => layout.CoinsButton, _coinsPill, _coin, _coinsPlus);
            _livesButton = new RowButton(this, layout => layout.LivesButton, _livesPill, _heart, _livesPlus);
            _gear = new DiscButton("Gear", _root, art, DiscGlyph.Gear, DrawOrder.WalletButton);
            _gear.Place(new Vector2(WalletRowLayout.Gear.MiddleX, WalletRowLayout.Gear.MiddleY), WalletRowLayout.GearSize / DiscButton.Size);
            _gearButton = new GearButton(this);

            _countFrom = _countTo = _shown = wallet.Coins;
            Wallet = wallet;
            ShowTheLives();
            ShowTheAmount(evenIfShown: true);
        }

        /// <summary>The wallet as the row was last told of it; the amount shown may still be counting towards its coins.</summary>
        public WalletView Wallet { get; private set; }

        /// <summary>The amount of coins the row shows now: while it counts, where the count has got to.</summary>
        public Coins CoinsShown => _shown;

        /// <summary>The middle of the lives' button, in the layout's points: where a finger presses it. Nothing before the row is placed.</summary>
        public Vector2? LivesButtonMiddle => _page is PageLayout page ? InTheLayout(page, new Vector2(_layout.LivesButton.MiddleX, _layout.LivesButton.MiddleY)) : (Vector2?)null;

        /// <summary>The middle of the coins' plus, in the layout's points: a finger that presses it goes to the shop. Nothing before the row is placed.</summary>
        public Vector2? CoinsPlusMiddle => _page is PageLayout page ? InTheLayout(page, new Vector2(_layout.CoinsPlus.MiddleX, _layout.CoinsPlus.MiddleY)) : (Vector2?)null;

        /// <summary>The middle of the gear, in the layout's points: a finger that presses it opens the settings card. Nothing before the row is placed.</summary>
        public Vector2? GearMiddle => _page is PageLayout page ? InTheLayout(page, _gear.Middle) : (Vector2?)null;

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
        /// one when it differs from what the row was told before; the tester mode turned on or off at once.
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
            bool handlingChanged = wallet.Handling != Wallet.Handling;
            Wallet = wallet;
            if (livesChanged)
            {
                ShowTheLives();
            }

            ShowTheAmount(evenIfShown: handlingChanged);
        }

        /// <summary>Moves the row's clock on: an amount that counts shows where it has got to.</summary>
        public void Advance(float deltaSeconds)
        {
            _clock += deltaSeconds;
            ShowTheAmount(evenIfShown: false);
        }

        /// <summary>Tells a set of pressable controls about the row's: the coins' button asks for the shop, the lives' button for the lives card, the gear for the settings card.</summary>
        public void AddPressables(PressSet<MuseumPress> presses)
        {
            presses.Add(_coinsButton, new MuseumPress.Visit(Destination.Shop));
            presses.Add(_livesButton, new MuseumPress.OpenTheLivesCard());
            presses.Add(_gearButton, new MuseumPress.OpenTheSettings());
        }

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
            string words = WalletWords.AmountOnTheRow(Wallet, now);
            Lettering.ReadyForAFigure(_amount, AmountSize, isASign: Wallet.IsUnlimited);
            _layout = WalletRowLayout.ForAnAmount(Lettering.WidthOf(_amount, words));
            Lettering.SayWithin(_amount, words, _amount.fontSize, _layout.AmountRoom);
            Arrange();
        }

        /// <summary>The count on the heart and the words in its pill, as the wallet the row was told of has them.</summary>
        private void ShowTheLives()
        {
            Lettering.ReadyForAFigure(_heartCount, HeartCountSize, isASign: Wallet.IsUnlimited);
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
            float amountSize = Wallet.IsUnlimited ? AmountSize : _amount.fontSize; // a sign stands on the middle of the figures it stands for
            _amount.transform.localPosition = new Vector3(_layout.AmountMiddle, WordsHeight(coinsPill, LetteringStyle.Figure, amountSize), 0f);
            _coinsPlus.Place(new Vector2(_layout.CoinsPlus.MiddleX, _layout.CoinsPlus.MiddleY));

            Box livesPill = _layout.LivesPillBox;
            _livesPill.transform.localPosition = new Vector3(livesPill.MiddleX, livesPill.MiddleY, 0f);
            _livesPill.size = new Vector2(livesPill.Width + PillPictureMargin, PillPictureHeight);
            _heart.transform.localPosition = new Vector3(_layout.Heart.MiddleX, _layout.Heart.MiddleY, 0f);
            _heartCount.transform.localPosition = new Vector3(
                _layout.Heart.MiddleX, _layout.Heart.MiddleY + HeartCountOverMiddle - Lettering.FigureDrop(LetteringStyle.OnHeart) * HeartCountSize, 0f);
            _wait.transform.localPosition = new Vector3(_layout.WaitMiddle, WordsHeight(livesPill, LetteringStyle.Figure, _wait.fontSize), 0f);
            _livesPlus.Place(new Vector2(_layout.LivesPlus.MiddleX, _layout.LivesPlus.MiddleY));
        }

        /// <summary>Where a pill's words stand upright: as the canvas centres them in the pill, figures seated as a stylesheet seats them.</summary>
        private static float WordsHeight(Box pill, LetteringStyle style, float size) =>
            pill.MiddleY - WordsUnderPillMiddle - Lettering.FigureDrop(style) * size;

        /// <summary>Where a point of the row, in the canvas' points, lies on the page.</summary>
        private static Vector2 InTheLayout(PageLayout page, Vector2 onTheRow) =>
            new Vector2(page.WalletRow.Left + onTheRow.x * page.WalletRowScale, page.WalletRow.MiddleY + onTheRow.y * page.WalletRowScale);

        /// <summary>Where a point of the page lies on the row, in the canvas' points.</summary>
        private static Vector2 OnTheRow(PageLayout page, Vector2 point) =>
            new Vector2((point.x - page.WalletRow.Left) / page.WalletRowScale, (point.y - page.WalletRow.MiddleY) / page.WalletRowScale);

        /// <summary>The gear as a button of the page: a finger on the page is looked for on the row, where the gear stands.</summary>
        private sealed class GearButton : IPressable
        {
            private readonly WalletRow _row;

            public GearButton(WalletRow row)
            {
                _row = row;
            }

            public bool IsUnder(Vector2 point) => _row._page is PageLayout page && _row._gear.IsUnder(OnTheRow(page, point));

            public void ShowPressed(bool isPressed) => _row._gear.ShowPressed(isPressed);
        }

        /// <summary>
        /// A mark, its pill and its plus as one button, as the canvas makes each a button (HomeTR): the coin's leads to
        /// the shop, the heart's opens the lives card. Held, the pill and the mark lose a little light and the plus
        /// goes down onto its lip.
        /// </summary>
        private sealed class RowButton : IPressable
        {
            private readonly WalletRow _row;
            private readonly Func<WalletRowLayout, Box> _box;
            private readonly SpriteRenderer _pill;
            private readonly SpriteRenderer _mark;
            private readonly DiscButton _plus;

            /// <param name="box">Where the button lies on the row, in the row's points, as the row's layout has it for the amount it shows.</param>
            public RowButton(WalletRow row, Func<WalletRowLayout, Box> box, SpriteRenderer pill, SpriteRenderer mark, DiscButton plus)
            {
                _row = row;
                _box = box;
                _pill = pill;
                _mark = mark;
                _plus = plus;
            }

            public bool IsUnder(Vector2 point)
            {
                if (!(_row._page is PageLayout page))
                {
                    return false;
                }

                Vector2 onTheRow = OnTheRow(page, point);
                return _box(_row._layout).Contains(onTheRow.x, onTheRow.y);
            }

            public void ShowPressed(bool isPressed)
            {
                Color light = Dimming.Of(isDimmed: false, isHeld: isPressed);
                _pill.color = light;
                _mark.color = light;
                _plus.ShowPressed(isPressed);
            }
        }
    }
}
