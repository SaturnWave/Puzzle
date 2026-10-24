#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Puzzle.Client.Art;
using Puzzle.Client.Flow;
using Puzzle.Client.Gallery;
using Puzzle.Client.Hall;
using Puzzle.Client.Layout;
using Puzzle.Client.Painting;
using Puzzle.Client.Purchases;
using Puzzle.Client.Screens;
using Puzzle.Meta;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Shops
{
    /// <summary>
    /// The shop, as the design canvas draws it (ShopTR): under the wallet row the brass sign "Mağaza" on its pin
    /// between two lamps, and three cream panels hung under it on wires. "ALTIN" shows the packs of coins in gilt
    /// frames with their amounts, each with a green button that says the store's price as the store words it, the
    /// canvas' "[fiyat]"; while the store has not answered the buttons are dimmed and say "…", a pack the store does
    /// not sell is dimmed and says so, and while the store cannot be reached the buttons give way to the calm line
    /// "Mağaza şu anda açılamıyor". While the starter pack is offered (from its level on, until it is bought, while the
    /// store sells it) it stands in the first pack's place, the better buy at the same price: its coins and its three
    /// boosters in the frame, "BİR KEZ" on the frame's top edge, its amount and its price. A test build sells nothing
    /// for money: its buttons say "Yakında", and a press is answered with a card that says so. A press on a product
    /// for sale brings the card that asks before paying; no press here pays. On the App Store an underlined row at the
    /// foot of the panel asks for the purchases back. "GÜÇLENDİRİCİLER" has a row for each set: its boosters on bare
    /// discs on a wooden shelf with the pills that count them, its name, and the green button with the coin and the
    /// price from the settings, dimmed while the coins do not cover it; a press on a dimmed one is answered by the
    /// rules with what is missing. "ÜCRETSİZ" is the strip of the ad for coins: what one gives and how many are left
    /// today, and the blue "İzle", or "Yarın yine" once the day's ads are watched. Where everything stands is
    /// <see cref="ShopLayout"/>'s; what it shows is the <see cref="ShopView"/> the stage tells it at every step.
    /// </summary>
    public sealed class ShopPage : IMuseumPage, IShopPage
    {
        /// <summary>The sign's title, as the canvas sets it: Young Serif of 23.5.</summary>
        private const float SignTitleSize = 23.5f;

        /// <summary>The sign hangs on a wire from its pin to 50 at each side of its middle, on its top edge; it hangs the first panel on two wires 54 from the middle, and each panel the next on two 123 from it.</summary>
        private const float SignWireEndsAcross = 50f;
        private const float SignWiresAcross = 54f;
        private const float PanelWiresAcross = 123f;

        /// <summary>A picture of the canvas is drawn in a box of 100 points; a sparkle's drawing is 24 across.</summary>
        private const float MarkBox = 100f;
        private const float SparkleDrawn = 24f;

        /// <summary>
        /// A pack's frame: the small gilt frame's picture is 28 wider and 56 higher than its body, drawn over the teal
        /// velvet (a box of 100 drawn as large as the frame's hole, 10 smaller each way).
        /// </summary>
        private const float FramePictureMargin = 28f;
        private const float FramePictureHeightMargin = 56f;
        private const float FrameHole = 10f;

        /// <summary>
        /// The coins of each pack, by its place, as the canvas draws its three: what, how large, how far under the
        /// frame's middle, and how far to its right. The fourth, the largest pack, is the chest with a pile in front.
        /// </summary>
        private static readonly (GalleryArt Picture, float Size, float UnderTheMiddle, float Across)[][] PackPictures =
        {
            new[] { (GalleryArt.CoinPile, 54f, 1.8f, 0f) },
            new[] { (GalleryArt.CoinPouch, 62f, -1.7f, 0f) },
            new[] { (GalleryArt.CoinChest, 80f, -9.4f, 0f) },
            new[] { (GalleryArt.CoinChest, 72f, -12f, 8f), (GalleryArt.CoinPile, 44f, 12f, -22f) },
        };

        /// <summary>
        /// The starter pack in its frame, in the canvas' points around the frame's middle at full size: its pile of
        /// coins over its boosters on small bare discs, side by side.
        /// </summary>
        private const float StarterPileSize = 34f;
        private const float StarterPileOverTheMiddle = 12f;
        private const float StarterDiscSize = 26f;
        private const float StarterDiscsApart = 28f;
        private const float StarterDiscsUnderTheMiddle = 16f;

        /// <summary>"BİR KEZ" on the starter pack's frame: small capitals of 10 on a pill as the amount's drawn at 0.7, its middle 3 over the frame's top edge.</summary>
        private const float OnceSize = 10f;
        private const float OncePillScale = 0.7f;
        private const float OnceOverTheFrame = 3f;

        /// <summary>Two sparkles in each pack's frame: how far from the frame's middle, and how large.</summary>
        private static readonly (float Across, float Up, float Size)[] Sparkles = { (31f, 16f, 11f), (-32f, 6f, 8f) };

        /// <summary>A pack's amount: Paytone One of 14.5 on the pill of 24, 9 from its ends.</summary>
        private const float AmountSize = 14.5f;
        private const float AmountPillPadding = 9f;
        private const float PillPictureMargin = 16f;
        private const float PillPictureHeight = 40f;

        /// <summary>The shelf is the plank drawn at three quarters (its corners of 24 then 18), its picture 28 wider and 68 higher than its body.</summary>
        private const float ShelfScale = 0.75f;
        private const float PlankPictureMargin = 28f;
        private const float PlankPictureHeightMargin = 68f;

        /// <summary>A set's name in Gabarito ExtraBold of 13.5; the small buttons' words at 16, and "İzle" at 18.</summary>
        private const float SetNameSize = 13.5f;
        private const float PriceSize = 16f;
        private const float WatchSize = 18f;

        /// <summary>The line that stands for the packs' buttons while the store cannot be reached: Gabarito ExtraBold of 14, 16 in from the panel's sides.</summary>
        private const float UnreachableSize = 14f;
        private const float UnreachableInFromTheSides = 16f;

        /// <summary>The free strip's two lines: small capitals of 10, apart by 9 hundredths of their size, and Gabarito ExtraBold of 15.</summary>
        private const float FreeLabelSize = 10f;
        private const float FreeLabelSpacing = 9f;
        private const float FreeLineSize = 15f;

        private readonly Transform _root;
        private readonly Wall _wall;
        private readonly RoomTrim _trim;
        private readonly PaintedShape _wires;
        private readonly BrassNameplate _sign;
        private readonly Transform _pin;
        private readonly Transform _leftLamp;
        private readonly Transform _rightLamp;
        private readonly Dictionary<ShopPanel, PagePlaque> _panels = new Dictionary<ShopPanel, PagePlaque>();
        private readonly Dictionary<ShopPanel, Transform> _contents = new Dictionary<ShopPanel, Transform>();
        private readonly List<SectionHeader> _headers = new List<SectionHeader>();
        private readonly List<(StoreProduct Product, Transform Column, SmallButton Button)> _packs = new List<(StoreProduct, Transform, SmallButton)>();
        private readonly (StoreProduct Product, Transform Column, SmallButton Button) _starter;
        private readonly List<(BoosterSet Set, SmallButton Button)> _sets = new List<(BoosterSet, SmallButton)>();
        private readonly TextMeshPro _unreachable;
        private readonly TextLink _restore;
        private readonly TextMeshPro _freeLabel;
        private readonly TextMeshPro _freeLine;
        private readonly SmallButton _watch;
        private readonly SmallButton _tomorrow;
        private ShopLayout? _layout;
        private ScreenShape? _screen;
        private StoreView _store;
        private bool _offersAnAd;

        private ShopPage(Transform parent, ArtCatalogue art, ShopView shop)
        {
            _store = shop.Store;
            _root = new GameObject("Shop").transform;
            _root.SetParent(parent, worldPositionStays: false);
            _wall = Wall.Create(_root, art);
            _trim = RoomTrim.Create(_root, art);
            _wires = PaintedShape.Create("Wires", _root, art.PaintMaterial, DrawOrder.Wire);
            _sign = new BrassNameplate(
                "Sign", _root, art, new CardNameplate(ShopWords.Title, ShopLayout.SignWidth, SignTitleSize), DrawOrder.ShopSign, DrawOrder.ShopSignLettering, DrawOrder.ShopSignStud);
            _pin = SceneObjects.AddSprite("Pin", _root, art.Of(GalleryArt.WirePin), art.SpriteMaterial, DrawOrder.ShopSignStud).transform;
            _leftLamp = SceneObjects.AddSprite("Left lamp", _root, art.Of(GalleryArt.Sconce), art.SpriteMaterial, DrawOrder.Sconce).transform;
            _rightLamp = SceneObjects.AddSprite("Right lamp", _root, art.Of(GalleryArt.Sconce), art.SpriteMaterial, DrawOrder.Sconce).transform;

            foreach (ShopPanel panel in ShopLayout.Panels)
            {
                _panels[panel] = new PagePlaque($"{panel} panel", _root, art);
                var content = new GameObject($"{panel} content").transform;
                content.SetParent(_root, worldPositionStays: false);
                _contents[panel] = content;
            }

            AddTheHeader(art, ShopPanel.Coins, ShopWords.Coins);
            AddTheHeader(art, ShopPanel.Boosters, ShopWords.Boosters);
            IReadOnlyList<ProductOffer> packs = shop.Store.CoinPacks;
            for (int pack = 0; pack < packs.Count; pack++)
            {
                _packs.Add(AddThePack(art, pack, packs.Count, packs[pack].Product));
            }

            _starter = AddTheStarterPack(art, packs.Count, shop.Store.StarterPackProduct);
            for (int set = 0; set < shop.Sets.Count; set++)
            {
                _sets.Add((shop.Sets[set].Set, AddTheSet(art, set, shop.Sets[set])));
            }

            _unreachable = Lettering.Write("Store unreachable", _contents[ShopPanel.Coins], art, LetteringStyle.Label, UnreachableSize, Lettering.Ink, DrawOrder.ShopLettering);
            Lettering.SayWithin(_unreachable, PurchaseWords.StoreUnreachable, UnreachableSize, ShopLayout.PanelWidth - 2f * UnreachableInFromTheSides);
            _unreachable.transform.localPosition = At(ShopLayout.PanelWidth / 2f, ShopLayout.PacksDown + ShopLayout.PackButtonDown(packs.Count));
            _restore = new TextLink("Restore", _root, art, PurchaseWords.Restore, cost: null, DrawOrder.ShopLettering);

            Transform free = _contents[ShopPanel.Free];
            Transform coins = SceneObjects.AddSprite("Coins", free, art.Of(GalleryArt.CoinPile), art.SpriteMaterial, DrawOrder.ShopGoods).transform;
            coins.localPosition = At(ShopLayout.FreeCoinsMiddle, ShopLayout.FreeCoinsMiddle);
            coins.localScale = new Vector3(ShopLayout.FreeCoinsSize / MarkBox, ShopLayout.FreeCoinsSize / MarkBox, 1f);
            _freeLabel = Lettering.Write("Free label", free, art, LetteringStyle.Label, FreeLabelSize, Lettering.LabelInk, DrawOrder.ShopLettering);
            _freeLabel.characterSpacing = FreeLabelSpacing;
            _freeLine = Lettering.Write("Free line", free, art, LetteringStyle.Label, FreeLineSize, Lettering.Ink, DrawOrder.ShopLettering);
            _watch = new SmallButton(
                "Watch", _root, art, SmallButtonColour.Blue, ShopWords.Watch, ShopLayout.ButtonWidth, WatchSize, DrawOrder.ShopButton, DrawOrder.ShopButtonLettering, SmallButtonMark.Play);
            _tomorrow = new SmallButton(
                "Tomorrow", _root, art, SmallButtonColour.Blue, ShopWords.TomorrowAgain, ShopLayout.ButtonWidth, PriceSize, DrawOrder.ShopButton, DrawOrder.ShopButtonLettering);
            _tomorrow.Dim(true);
            Tell(shop);
        }

        public Transform Root => _root;

        /// <summary>True while the free strip's blue button plays an ad: the day has one left.</summary>
        public bool OffersAnAd => _offersAnAd;

        /// <summary>Builds the shop as a view of the rules has it. Call <see cref="Fit"/> before the first picture.</summary>
        public static ShopPage Create(Transform parent, ArtCatalogue art, ShopView shop) => new ShopPage(parent, art, shop);

        /// <summary>
        /// The middle of a pack's button, in the layout's points: where a finger presses it. Nothing before the page is
        /// fitted, while the buttons give way to the line of a store that cannot be reached, and for the first pack while
        /// the starter pack stands in its place.
        /// </summary>
        public Vector2? PackButtonMiddle(int pack) =>
            _layout != null && !(_store.Status is StoreStatus.Unreachable) && !(pack == 0 && ShowsTheStarterPack) ? _packs[pack].Button.Middle : (Vector2?)null;

        /// <summary>True while a pack's button buys: a test build's "Yakında", or the store's price.</summary>
        public bool IsPackForSale(int pack) => IsForSale(pack);

        /// <summary>The middle of the starter pack's button; nothing while it is not offered.</summary>
        public Vector2? StarterButtonMiddle => _layout != null && ShowsTheStarterPack ? _starter.Button.Middle : (Vector2?)null;

        /// <summary>The middle of the row that asks for the purchases back; nothing where it is not shown.</summary>
        public Vector2? RestoreRowMiddle => _layout?.RestoreRow != null ? _restore.Middle : (Vector2?)null;

        /// <summary>The middle of a set's button, lit or dimmed.</summary>
        public Vector2? SetButtonMiddle(int set) => _layout != null ? _sets[set].Button.Middle : (Vector2?)null;

        /// <summary>True while a set's button is lit: the coins cover the set.</summary>
        public bool IsSetButtonLit(int set) => !_sets[set].Button.IsDimmed;

        /// <summary>The middle of the free strip's button: "İzle", or the dimmed "Yarın yine".</summary>
        public Vector2? AdButtonMiddle => _layout != null ? _watch.Middle : (Vector2?)null;

        /// <summary>True while the starter pack stands in the first pack's place.</summary>
        private bool ShowsTheStarterPack => _store.StarterPack != null;

        public void Fit(ScreenShape screen)
        {
            _screen = screen;
            var layout = ShopLayout.Of(screen, _packs.Count, _sets.Count, hasRestore: _store.OffersRestore);
            _layout = layout;
            _wall.Fit(layout.Page.View);
            _trim.Show(layout.Page, floorLine: null, hasARail: false);

            _sign.Transform.localPosition = new Vector3(layout.Sign.MiddleX, layout.Sign.MiddleY, 0f);
            _sign.Transform.localScale = new Vector3(layout.Scale, layout.Scale, 1f);
            _pin.localPosition = new Vector3(layout.PinMiddle.X, layout.PinMiddle.Y, 0f);
            _pin.localScale = new Vector3(layout.Scale, layout.Scale, 1f);
            (float lampX, float lampFoot) = layout.LeftLamp;
            _leftLamp.localPosition = new Vector3(lampX, lampFoot, 0f);
            _rightLamp.localPosition = new Vector3(-lampX, lampFoot, 0f);
            _leftLamp.localScale = _rightLamp.localScale = new Vector3(layout.Scale, layout.Scale, 1f);

            foreach (ShopPanel panel in ShopLayout.Panels)
            {
                Box box = layout.Panel(panel);
                _panels[panel].Place(box);
                _contents[panel].localPosition = new Vector3(box.Left, box.Top, 0f);
                _contents[panel].localScale = new Vector3(layout.Scale, layout.Scale, 1f);
            }

            // The buttons stand on the page itself, in the layout's points, where a finger is told of: each where its panel has it.
            float packButtonsDown = ShopLayout.PacksDown + ShopLayout.PackButtonDown(_packs.Count);
            for (int pack = 0; pack < _packs.Count; pack++)
            {
                PlaceAButton(_packs[pack].Button, layout, ShopPanel.Coins, ShopLayout.PackMiddle(pack, _packs.Count), packButtonsDown);
            }

            PlaceAButton(_starter.Button, layout, ShopPanel.Coins, ShopLayout.PackMiddle(0, _packs.Count), packButtonsDown);
            for (int set = 0; set < _sets.Count; set++)
            {
                PlaceAButton(_sets[set].Button, layout, ShopPanel.Boosters, ShopLayout.SetColumnMiddle, ShopLayout.SetRowTop(set) + ShopLayout.SetButtonDown);
            }

            PlaceAButton(_watch, layout, ShopPanel.Free, ShopLayout.SetColumnMiddle, ShopLayout.FreeButtonDown);
            PlaceAButton(_tomorrow, layout, ShopPanel.Free, ShopLayout.SetColumnMiddle, ShopLayout.FreeButtonDown);
            _restore.Place(layout.RestoreRow is Box row ? new Vector2(row.MiddleX, row.MiddleY) : Vector2.zero);
            _wires.Show(Wires(layout));
            ShowTheStore();
        }

        public void AddPressables(PressSet<MuseumPress> presses)
        {
            for (int pack = 0; pack < _packs.Count; pack++)
            {
                int place = pack;
                presses.Add(new ButtonWhile(_packs[pack].Button, () => IsForSale(place)), new MuseumPress.BuyFromTheStore(_packs[pack].Product));
            }

            presses.Add(new ButtonWhile(_starter.Button, () => _store.StarterPack is { CanBeBought: true }), new MuseumPress.BuyFromTheStore(_starter.Product));
            foreach ((BoosterSet set, SmallButton button) in _sets)
            {
                // Lit, it buys; dimmed, it is pressed all the same, and the rules say what the coins lack.
                presses.Add(button, new MuseumPress.BuyASet(set));
                presses.Add(new ButtonWhile(button, () => button.IsDimmed), new MuseumPress.BuyASet(set));
            }

            presses.Add(_watch, new MuseumPress.WatchAnAdForCoins());
            presses.Add(new RowWhile(_restore, () => _layout?.RestoreRow != null), new MuseumPress.RestorePurchases());
        }

        /// <summary>
        /// Shows the shop as the rules have it now: which sets the coins cover, whether the day has an ad for coins
        /// left, and the store: its prices, or why nothing can be bought, and the starter pack while it is offered.
        /// When the restore row comes or goes, the page is laid out anew.
        /// </summary>
        public void Tell(ShopView shop)
        {
            bool isLaidOutAnew = shop.Store.OffersRestore != _store.OffersRestore;
            _store = shop.Store;
            if (isLaidOutAnew && _screen is ScreenShape screen)
            {
                Fit(screen);
            }
            else
            {
                ShowTheStore();
            }

            for (int set = 0; set < _sets.Count && set < shop.Sets.Count; set++)
            {
                SmallButton button = _sets[set].Button;
                bool isDimmed = !shop.Sets[set].IsCovered;
                if (button.IsDimmed != isDimmed)
                {
                    button.Dim(isDimmed);
                }
            }

            _offersAnAd = shop.CanWatchAnAd;
            _watch.Show(shop.CanWatchAnAd);
            _tomorrow.Show(!shop.CanWatchAnAd);

            // Told at every step: the strip's lines are written anew only when what they say changes.
            string label = ShopWords.FreeToday(shop.AdsLeftToday);
            if (label != _freeLabel.text)
            {
                SayFromTheLeft(_freeLabel, label, FreeLabelSize, ShopLayout.FreeLabelDown);
            }

            string line = ShopWords.WatchForCoins(shop.CoinsForAnAd);
            if (line != _freeLine.text)
            {
                SayFromTheLeft(_freeLine, line, FreeLineSize, ShopLayout.FreeLineDown);
            }
        }

        /// <summary>The shop has no way back of its own: the way back leads to the hall.</summary>
        public bool TryGoBack() => false;

        public void Dispose()
        {
            _wall.Dispose();
            _trim.Dispose();
            _wires.Dispose();
            _restore.Dispose();
            foreach (SectionHeader header in _headers)
            {
                header.Dispose();
            }

            SceneObjects.DestroyObjectOf(_root);
        }

        /// <summary>A point of a panel's content, given in the canvas' points across from its left edge and down from its top.</summary>
        private static Vector3 At(float across, float down) => new Vector3(across, -down, 0f);

        private static void PlaceAButton(SmallButton button, ShopLayout layout, ShopPanel panel, float across, float down)
        {
            (float x, float y) = layout.InPanel(panel, across, down);
            button.Place(new Vector2(x, y), layout.Scale);
        }

        /// <summary>True while a pack's button buys: in a test build (whose press says it sells nothing), or while the store gives its price; never the first while the starter pack stands in its place.</summary>
        private bool IsForSale(int pack) =>
            !(pack == 0 && ShowsTheStarterPack) && (_store.Status is StoreStatus.NotSold || _store.CoinPacks[pack].CanBeBought);

        /// <summary>
        /// The products' columns and buttons as the store stands: the starter pack in the first pack's place while it
        /// is offered; "Yakında" in a test build, "…" while the store has not answered, the store's price, or "Satışta
        /// değil", each written anew only when it changes; and the calm line in the buttons' place while the store
        /// cannot be reached. The restore row shows where it is offered.
        /// </summary>
        private void ShowTheStore()
        {
            bool isUnreachable = _store.Status is StoreStatus.Unreachable;
            _unreachable.gameObject.SetActive(isUnreachable);
            bool showsTheStarter = ShowsTheStarterPack;
            _starter.Column.gameObject.SetActive(showsTheStarter);
            _starter.Button.Show(showsTheStarter && !isUnreachable);
            for (int pack = 0; pack < _packs.Count; pack++)
            {
                (_, Transform column, SmallButton button) = _packs[pack];
                bool isInThePlaceOfTheStarter = pack == 0 && showsTheStarter;
                column.gameObject.SetActive(!isInThePlaceOfTheStarter);
                button.Show(!isUnreachable && !isInThePlaceOfTheStarter);
                ProductOffer offer = _store.CoinPacks[pack];
                (string words, bool isLit) = _store.Status switch
                {
                    StoreStatus.NotSold _ => (ShopWords.ComingSoon, true),
                    StoreStatus.Open _ when offer.Price is StorePrice price => (price.Words, true),
                    StoreStatus.Open _ => (PurchaseWords.PackNotOnSale, false),
                    _ => (PurchaseWords.PriceComing, false),
                };
                Show(button, words, isLit);
            }

            if (_store.StarterPack is ProductOffer starter)
            {
                Show(_starter.Button, starter.Price?.Words ?? PurchaseWords.PriceComing, starter.CanBeBought);
            }

            _restore.Show(_layout?.RestoreRow != null);
        }

        /// <summary>A button with these words, lit or dimmed; written anew only when they change.</summary>
        private static void Show(SmallButton button, string words, bool isLit)
        {
            if (button.Label != words)
            {
                button.Say(words);
            }

            if (button.IsDimmed == isLit)
            {
                button.Dim(!isLit);
            }
        }

        private void AddTheHeader(ArtCatalogue art, ShopPanel panel, string words)
        {
            var header = new SectionHeader($"{panel} header", _contents[panel], art, words, DrawOrder.PlaqueArt, DrawOrder.PlaqueLettering);
            header.Transform.localPosition = At(ShopLayout.PanelWidth / 2f, ShopLayout.HeaderDown);
            _headers.Add(header);
        }

        /// <summary>
        /// A pack of coins: its gilt frame over the teal velvet with the coins and two sparkles in it, its amount's
        /// pill, and its button, all of its column drawn at the column's share for so many packs but the pill and the
        /// button's height.
        /// </summary>
        private (StoreProduct, Transform, SmallButton) AddThePack(ArtCatalogue art, int place, int packs, StoreProduct pack)
        {
            Transform column = AddAFramedColumn(art, $"Pack {place + 1}", place, packs, out float across, out float frameDown);
            float scale = ShopLayout.PackScale(packs);
            foreach ((GalleryArt picture, float size, float underTheMiddle, float sideways) in PackPictures[Math.Min(place, PackPictures.Length - 1)])
            {
                Transform drawn = SceneObjects.AddSprite("Coins", column, art.Of(picture), art.SpriteMaterial, DrawOrder.ShopGoods).transform;
                drawn.localPosition = At(across + sideways * scale, frameDown + underTheMiddle * scale);
                drawn.localScale = new Vector3(size * scale / MarkBox, size * scale / MarkBox, 1f);
            }

            AddAPill(art, column, NumberWords.Amount(pack.Coins.Amount), LetteringStyle.Figure, AmountSize, At(across, ShopLayout.PacksDown + ShopLayout.PackPillDownFor(packs)), scale: 1f);
            var button = new SmallButton(
                $"Pack {place + 1}", _root, art, SmallButtonColour.Green, PurchaseWords.PriceComing, ShopLayout.PackButtonWidth(packs), PriceSize,
                DrawOrder.ShopButton, DrawOrder.ShopButtonLettering);
            return (pack, column, button);
        }

        /// <summary>
        /// The starter pack, in the first pack's column: its pile of coins over its boosters on small bare discs in the
        /// frame, "BİR KEZ" on the frame's top edge, its amount's pill and its button.
        /// </summary>
        private (StoreProduct, Transform, SmallButton) AddTheStarterPack(ArtCatalogue art, int packs, StoreProduct starter)
        {
            Transform column = AddAFramedColumn(art, "Starter pack", place: 0, packs, out float across, out float frameDown);
            float scale = ShopLayout.PackScale(packs);
            Transform pile = SceneObjects.AddSprite("Coins", column, art.Of(GalleryArt.CoinPile), art.SpriteMaterial, DrawOrder.ShopGoods).transform;
            pile.localPosition = At(across, frameDown - StarterPileOverTheMiddle * scale);
            pile.localScale = new Vector3(StarterPileSize * scale / MarkBox, StarterPileSize * scale / MarkBox, 1f);

            BoosterKind[] boosters = BoosterKinds.All.Where(starter.Boosters.Has).ToArray();
            for (int place = 0; place < boosters.Length; place++)
            {
                var disc = new BoosterDisc(
                    boosters[place].Name(), column, art, BoosterPictures.Of(boosters[place]), StarterDiscSize * scale, DrawOrder.ShopDisc, BoosterFace.Goods);
                float sideways = (place - (boosters.Length - 1) / 2f) * StarterDiscsApart;
                disc.Transform.localPosition = At(across + sideways * scale, frameDown + StarterDiscsUnderTheMiddle * scale);
            }

            AddAPill(art, column, PurchaseWords.StarterOnce, LetteringStyle.Label, OnceSize, At(across, ShopLayout.PacksDown - OnceOverTheFrame), OncePillScale);
            AddAPill(art, column, NumberWords.Amount(starter.Coins.Amount), LetteringStyle.Figure, AmountSize, At(across, ShopLayout.PacksDown + ShopLayout.PackPillDownFor(packs)), scale: 1f);
            var button = new SmallButton(
                "Starter pack", _root, art, SmallButtonColour.Green, PurchaseWords.PriceComing, ShopLayout.PackButtonWidth(packs), PriceSize,
                DrawOrder.ShopButton, DrawOrder.ShopButtonLettering);
            return (starter, column, button);
        }

        /// <summary>A product's column in the coins' panel, with its gilt frame over the teal velvet and two sparkles in it: where its middle stands across, and its frame's middle down.</summary>
        private Transform AddAFramedColumn(ArtCatalogue art, string name, int place, int packs, out float across, out float frameDown)
        {
            var column = new GameObject(name).transform;
            column.SetParent(_contents[ShopPanel.Coins], worldPositionStays: false);
            float scale = ShopLayout.PackScale(packs);
            across = ShopLayout.PackMiddle(place, packs);
            float frameWidth = ShopLayout.PackFrameWidth * scale;
            float frameHeight = ShopLayout.PackFrameHeight * scale;
            frameDown = ShopLayout.PacksDown + frameHeight / 2f;
            Vector3 frameMiddle = At(across, frameDown);

            SpriteRenderer velvet = SceneObjects.AddSprite("Velvet", column, art.Of(GalleryArt.Velvet), art.SpriteMaterial, DrawOrder.ShopVelvet);
            velvet.transform.localPosition = frameMiddle;
            velvet.transform.localScale = new Vector3((frameWidth - FrameHole) / MarkBox, (frameHeight - FrameHole) / MarkBox, 1f);
            SpriteRenderer frame = SceneObjects.AddSprite("Frame", column, art.Of(GalleryArt.SmallFrame), art.SpriteMaterial, DrawOrder.ShopFrame);
            frame.drawMode = SpriteDrawMode.Sliced; // the frame's middle stretches both ways; its corners keep their shape
            frame.size = new Vector2(frameWidth + FramePictureMargin, frameHeight + FramePictureHeightMargin);
            frame.transform.localPosition = frameMiddle;

            foreach ((float sparkleAcross, float up, float sparkleSize) in Sparkles)
            {
                Transform sparkle = SceneObjects.AddSprite("Sparkle", column, art.Of(GalleryArt.Sparkle), art.SpriteMaterial, DrawOrder.ShopSparkle).transform;
                sparkle.localPosition = At(across + sparkleAcross * scale, frameDown - up * scale);
                sparkle.localScale = new Vector3(sparkleSize * scale / SparkleDrawn, sparkleSize * scale / SparkleDrawn, 1f);
            }

            return column;
        }

        /// <summary>A set's row: its boosters on bare discs on the wooden shelf, with the pills that count them; its name; and its button with the coin and the price.</summary>
        private SmallButton AddTheSet(ArtCatalogue art, int row, SetOffer offer)
        {
            Transform boosters = _contents[ShopPanel.Boosters];
            float top = ShopLayout.SetRowTop(row);

            SpriteRenderer plank = SceneObjects.AddSprite("Shelf", boosters, art.Of(GalleryArt.Plank), art.SpriteMaterial, DrawOrder.ShopShelf);
            plank.drawMode = SpriteDrawMode.Sliced; // the picture's middle stretches both ways; its round corners keep their shape
            plank.size = new Vector2(ShopLayout.ShelfWidth / ShelfScale + PlankPictureMargin, ShopLayout.ShelfHeight / ShelfScale + PlankPictureHeightMargin);
            plank.transform.localScale = new Vector3(ShelfScale, ShelfScale, 1f);
            plank.transform.localPosition = At(ShopLayout.ShelfLeft + ShopLayout.ShelfWidth / 2f, top + ShopLayout.ShelfHeight / 2f);

            IReadOnlyList<(BoosterKind Kind, int Count)> held = offer.Boosters;
            GoodsItem[] items = held.Select(booster => (GoodsItem)new GoodsItem.Booster(booster.Kind, ShopWords.Times(booster.Count))).ToArray();
            float step = ShopLayout.DiscMiddle(1, items.Length) - ShopLayout.DiscMiddle(0, items.Length);
            var discs = new GoodsRow(
                $"Set {row + 1} boosters", boosters, art, items, ShopLayout.DiscSize, step, pillScale: 0.75f, DrawOrder.ShopDisc, DrawOrder.ShopPill, DrawOrder.ShopPillLettering);
            float rowMiddle = (ShopLayout.DiscMiddle(0, items.Length) + ShopLayout.DiscMiddle(items.Length - 1, items.Length)) / 2f;
            discs.Place(At(rowMiddle, top + ShopLayout.DiscDown));

            TextMeshPro name = Lettering.Write($"Set {row + 1} name", boosters, art, LetteringStyle.Label, SetNameSize, Lettering.Ink, DrawOrder.ShopLettering);
            Lettering.SayWithin(name, ShopWords.NameOf(offer.Set), SetNameSize, ShopLayout.ButtonWidth);
            name.transform.localPosition = At(ShopLayout.SetColumnMiddle, top + ShopLayout.SetNameDown);

            return new SmallButton(
                $"Set {row + 1}", _root, art, SmallButtonColour.Green, NumberWords.Amount(offer.Set.Price.Amount), ShopLayout.ButtonWidth, PriceSize,
                DrawOrder.ShopButton, DrawOrder.ShopButtonLettering, SmallButtonMark.Coin);
        }

        /// <summary>A cream pill with words in it, as the canvas draws a pack's amount: 24 high, its words 9 from its ends; drawn at a share of that.</summary>
        private static void AddAPill(ArtCatalogue art, Transform parent, string words, LetteringStyle style, float size, Vector3 middle, float scale)
        {
            SpriteRenderer pill = SceneObjects.AddSprite("Pill", parent, art.Of(GalleryArt.Pill), art.SpriteMaterial, DrawOrder.ShopPill);
            pill.drawMode = SpriteDrawMode.Tiled; // the picture's middle repeats, its round ends keep their shape
            pill.transform.localPosition = middle;
            pill.transform.localScale = new Vector3(scale, scale, 1f);
            TextMeshPro said = Lettering.Write("Words", pill.transform, art, style, size, Lettering.Ink, DrawOrder.ShopPillLettering);
            Lettering.Say(said, words);
            said.transform.localPosition = new Vector3(0f, -Lettering.FigureDrop(style) * size, 0f);
            pill.size = new Vector2(Lettering.WidthOf(said, words) + 2f * AmountPillPadding + PillPictureMargin, PillPictureHeight);
        }

        /// <summary>A line of the free strip that begins where the strip's lines begin, at a size that fits their room.</summary>
        private static void SayFromTheLeft(TextMeshPro line, string words, float size, float down)
        {
            Lettering.SayWithin(line, words, size, ShopLayout.FreeLinesRoom);
            line.transform.localPosition = At(ShopLayout.FreeLinesLeft + Lettering.WidthOf(line, words) / 2f, down);
        }

        /// <summary>
        /// The wires the shop hangs on, in the layout's points: the sign's from its pin to its top edge, two from the
        /// sign down to the first row of panels under it, and two from each row to the next: from the left and the
        /// right panel of a row of two, from the one panel of a row of one.
        /// </summary>
        private static Mesh Wires(ShopLayout layout)
        {
            var sketch = new MeshSketch();
            float scale = layout.Scale;
            var pin = new Vector2(layout.PinMiddle.X, layout.PinMiddle.Y);
            IReadOnlyList<IReadOnlyList<ShopPanel>> rows = layout.Rows;
            foreach (float side in new[] { -1f, 1f })
            {
                PictureWire.AddSlanting(sketch, pin, new Vector2(side * SignWireEndsAcross * scale, layout.Sign.Top));
                float signWire = side * SignWiresAcross * scale;
                PictureWire.Add(sketch, signWire - PictureWire.Width / 2f, PanelUnder(layout, rows[0], side).Top - 1f, layout.Sign.Bottom + 1f);
                float panelWire = side * PanelWiresAcross * scale;
                for (int row = 1; row < rows.Count; row++)
                {
                    Box above = PanelUnder(layout, rows[row - 1], side);
                    Box below = PanelUnder(layout, rows[row], side);
                    PictureWire.Add(sketch, panelWire - PictureWire.Width / 2f, below.Top - 1f, above.Bottom + 1f);
                }
            }

            return sketch.ToMesh("Shop wires");
        }

        /// <summary>The panel of a row a wire on this side hangs from or to: the row's one panel, or of two the one on its side.</summary>
        private static Box PanelUnder(ShopLayout layout, IReadOnlyList<ShopPanel> row, float side) =>
            layout.Panel(row.Count == 1 || side < 0f ? row[0] : row[row.Count - 1]);

        /// <summary>A button that a finger presses only while a condition holds: a set's while it is dimmed, a product's while it is for sale.</summary>
        private sealed class ButtonWhile : IPressable
        {
            private readonly SmallButton _button;
            private readonly Func<bool> _holds;

            public ButtonWhile(SmallButton button, Func<bool> holds)
            {
                _button = button;
                _holds = holds;
            }

            public bool IsUnder(Vector2 point) => _holds() && _button.Covers(point);

            public void ShowPressed(bool isPressed) => _button.ShowPressed(isPressed);
        }

        /// <summary>The restore row, which a finger presses only while it is shown.</summary>
        private sealed class RowWhile : IPressable
        {
            private readonly TextLink _row;
            private readonly Func<bool> _holds;

            public RowWhile(TextLink row, Func<bool> holds)
            {
                _row = row;
                _holds = holds;
            }

            public bool IsUnder(Vector2 point) => _holds() && _row.IsUnder(point);

            public void ShowPressed(bool isPressed) => _row.ShowPressed(isPressed);
        }
    }
}
