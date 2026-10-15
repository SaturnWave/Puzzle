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
using Puzzle.Client.Screens;
using Puzzle.Meta;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Shops
{
    /// <summary>
    /// The shop, as the design canvas draws it (ShopTR): under the wallet row the brass sign "Mağaza" on its pin
    /// between two lamps, and three cream panels hung under it on wires. "ALTIN" shows the packs of coins in gilt
    /// frames with their amounts; a test build sells nothing for money, so each pack's button says "Yakında" and a
    /// press is answered by the rules with a card that says so. "GÜÇLENDİRİCİLER" has a row for each set: its boosters
    /// on bare discs on a wooden shelf with the pills that count them, its name, and the green button with the coin
    /// and the price from the settings, dimmed while the coins do not cover it; a press on a dimmed one is answered by
    /// the rules with what is missing. "ÜCRETSİZ" is the strip of the ad for coins: what one gives and how many are
    /// left today, and the blue "İzle", or "Yarın yine" once the day's ads are watched. Where everything stands is
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

        /// <summary>The coins of each pack, by its place, as the canvas draws them: what, how large, and how far under the frame's middle.</summary>
        private static readonly (GalleryArt Picture, float Size, float UnderTheMiddle)[] PackPictures =
        {
            (GalleryArt.CoinPile, 54f, 1.8f), (GalleryArt.CoinPouch, 62f, -1.7f), (GalleryArt.CoinChest, 80f, -9.4f),
        };

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
        private readonly List<(CoinPack Pack, SmallButton Button)> _packs = new List<(CoinPack, SmallButton)>();
        private readonly List<(BoosterSet Set, SmallButton Button)> _sets = new List<(BoosterSet, SmallButton)>();
        private readonly TextMeshPro _freeLabel;
        private readonly TextMeshPro _freeLine;
        private readonly SmallButton _watch;
        private readonly SmallButton _tomorrow;
        private ShopLayout? _layout;
        private bool _offersAnAd;

        private ShopPage(Transform parent, ArtCatalogue art, ShopView shop)
        {
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
            for (int pack = 0; pack < shop.CoinPacks.Count; pack++)
            {
                _packs.Add((shop.CoinPacks[pack], AddThePack(art, pack, shop.CoinPacks.Count, shop.CoinPacks[pack])));
            }

            for (int set = 0; set < shop.Sets.Count; set++)
            {
                _sets.Add((shop.Sets[set].Set, AddTheSet(art, set, shop.Sets[set])));
            }

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

        /// <summary>The middle of a pack's button, in the layout's points: where a finger presses it. Nothing before the page is fitted.</summary>
        public Vector2? PackButtonMiddle(int pack) => _layout != null ? _packs[pack].Button.Middle : (Vector2?)null;

        /// <summary>The middle of a set's button, lit or dimmed.</summary>
        public Vector2? SetButtonMiddle(int set) => _layout != null ? _sets[set].Button.Middle : (Vector2?)null;

        /// <summary>True while a set's button is lit: the coins cover the set.</summary>
        public bool IsSetButtonLit(int set) => !_sets[set].Button.IsDimmed;

        /// <summary>The middle of the free strip's button: "İzle", or the dimmed "Yarın yine".</summary>
        public Vector2? AdButtonMiddle => _layout != null ? _watch.Middle : (Vector2?)null;

        public void Fit(ScreenShape screen)
        {
            var layout = ShopLayout.Of(screen, _packs.Count, _sets.Count);
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
            for (int pack = 0; pack < _packs.Count; pack++)
            {
                PlaceAButton(_packs[pack].Button, layout, ShopPanel.Coins, ShopLayout.PackMiddle(pack, _packs.Count), ShopLayout.PacksDown + ShopLayout.PackButtonDown);
            }

            for (int set = 0; set < _sets.Count; set++)
            {
                PlaceAButton(_sets[set].Button, layout, ShopPanel.Boosters, ShopLayout.SetColumnMiddle, ShopLayout.SetRowTop(set) + ShopLayout.SetButtonDown);
            }

            PlaceAButton(_watch, layout, ShopPanel.Free, ShopLayout.SetColumnMiddle, ShopLayout.FreeButtonDown);
            PlaceAButton(_tomorrow, layout, ShopPanel.Free, ShopLayout.SetColumnMiddle, ShopLayout.FreeButtonDown);
            _wires.Show(Wires(layout));
        }

        public void AddPressables(PressSet<MuseumPress> presses)
        {
            foreach ((CoinPack pack, SmallButton button) in _packs)
            {
                presses.Add(button, new MuseumPress.BuyACoinPack(pack));
            }

            foreach ((BoosterSet set, SmallButton button) in _sets)
            {
                // Lit, it buys; dimmed, it is pressed all the same, and the rules say what the coins lack.
                presses.Add(button, new MuseumPress.BuyASet(set));
                presses.Add(new DimmedButton(button), new MuseumPress.BuyASet(set));
            }

            presses.Add(_watch, new MuseumPress.WatchAnAdForCoins());
        }

        /// <summary>Shows the shop as the rules have it now: which sets the coins cover, and whether the day has an ad for coins left.</summary>
        public void Tell(ShopView shop)
        {
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

        private void AddTheHeader(ArtCatalogue art, ShopPanel panel, string words)
        {
            var header = new SectionHeader($"{panel} header", _contents[panel], art, words, DrawOrder.PlaqueArt, DrawOrder.PlaqueLettering);
            header.Transform.localPosition = At(ShopLayout.PanelWidth / 2f, ShopLayout.HeaderDown);
            _headers.Add(header);
        }

        /// <summary>A pack of coins: its gilt frame over the teal velvet with the coins and two sparkles in it, its amount's pill, and its button.</summary>
        private SmallButton AddThePack(ArtCatalogue art, int place, int packs, CoinPack pack)
        {
            Transform coins = _contents[ShopPanel.Coins];
            float across = ShopLayout.PackMiddle(place, packs);
            float frameDown = ShopLayout.PacksDown + ShopLayout.PackFrameHeight / 2f;
            Vector3 frameMiddle = At(across, frameDown);

            SpriteRenderer velvet = SceneObjects.AddSprite("Velvet", coins, art.Of(GalleryArt.Velvet), art.SpriteMaterial, DrawOrder.ShopVelvet);
            velvet.transform.localPosition = frameMiddle;
            velvet.transform.localScale = new Vector3((ShopLayout.PackFrameWidth - FrameHole) / MarkBox, (ShopLayout.PackFrameHeight - FrameHole) / MarkBox, 1f);
            SpriteRenderer frame = SceneObjects.AddSprite("Frame", coins, art.Of(GalleryArt.SmallFrame), art.SpriteMaterial, DrawOrder.ShopFrame);
            frame.drawMode = SpriteDrawMode.Sliced; // the frame's middle stretches both ways; its corners keep their shape
            frame.size = new Vector2(ShopLayout.PackFrameWidth + FramePictureMargin, ShopLayout.PackFrameHeight + FramePictureHeightMargin);
            frame.transform.localPosition = frameMiddle;

            (GalleryArt picture, float size, float underTheMiddle) = PackPictures[Math.Min(place, PackPictures.Length - 1)];
            Transform drawn = SceneObjects.AddSprite("Coins", coins, art.Of(picture), art.SpriteMaterial, DrawOrder.ShopGoods).transform;
            drawn.localPosition = At(across, frameDown + underTheMiddle);
            drawn.localScale = new Vector3(size / MarkBox, size / MarkBox, 1f);
            foreach ((float sparkleAcross, float up, float sparkleSize) in Sparkles)
            {
                Transform sparkle = SceneObjects.AddSprite("Sparkle", coins, art.Of(GalleryArt.Sparkle), art.SpriteMaterial, DrawOrder.ShopSparkle).transform;
                sparkle.localPosition = At(across + sparkleAcross, frameDown - up);
                sparkle.localScale = new Vector3(sparkleSize / SparkleDrawn, sparkleSize / SparkleDrawn, 1f);
            }

            AddAPill(art, coins, NumberWords.Amount(pack.Coins.Amount), At(across, ShopLayout.PacksDown + ShopLayout.PackPillDown));
            return new SmallButton(
                $"Pack {place + 1}", _root, art, SmallButtonColour.Green, ShopWords.ComingSoon, ShopLayout.ButtonWidth, PriceSize, DrawOrder.ShopButton, DrawOrder.ShopButtonLettering);
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

        /// <summary>A cream pill with an amount in it, as the canvas draws a pack's: 24 high, its words 9 from its ends.</summary>
        private static void AddAPill(ArtCatalogue art, Transform parent, string words, Vector3 middle)
        {
            SpriteRenderer pill = SceneObjects.AddSprite("Amount", parent, art.Of(GalleryArt.Pill), art.SpriteMaterial, DrawOrder.ShopPill);
            pill.drawMode = SpriteDrawMode.Tiled; // the picture's middle repeats, its round ends keep their shape
            pill.transform.localPosition = middle;
            TextMeshPro amount = Lettering.Write("Words", pill.transform, art, LetteringStyle.Figure, AmountSize, Lettering.Ink, DrawOrder.ShopPillLettering);
            Lettering.Say(amount, words);
            amount.transform.localPosition = new Vector3(0f, -Lettering.FigureDrop(LetteringStyle.Figure) * AmountSize, 0f);
            pill.size = new Vector2(Lettering.WidthOf(amount, words) + 2f * AmountPillPadding + PillPictureMargin, PillPictureHeight);
        }

        /// <summary>A line of the free strip that begins where the strip's lines begin, at a size that fits their room.</summary>
        private static void SayFromTheLeft(TextMeshPro line, string words, float size, float down)
        {
            Lettering.SayWithin(line, words, size, ShopLayout.FreeLinesRoom);
            line.transform.localPosition = At(ShopLayout.FreeLinesLeft + Lettering.WidthOf(line, words) / 2f, down);
        }

        /// <summary>
        /// The wires the shop hangs on, in the layout's points: the sign's from its pin to its top edge, two from the
        /// sign down to the panels under it, and two from each panel to the free strip, or to the panel under it.
        /// </summary>
        private static Mesh Wires(ShopLayout layout)
        {
            var sketch = new MeshSketch();
            float scale = layout.Scale;
            var pin = new Vector2(layout.PinMiddle.X, layout.PinMiddle.Y);
            foreach (float side in new[] { -1f, 1f })
            {
                PictureWire.AddSlanting(sketch, pin, new Vector2(side * SignWireEndsAcross * scale, layout.Sign.Top));
                float signWire = side * SignWiresAcross * scale;
                PictureWire.Add(sketch, signWire - PictureWire.Width / 2f, layout.Panel(ShopPanel.Coins).Top - 1f, layout.Sign.Bottom + 1f);
                float panelWire = side * PanelWiresAcross * scale;
                Box free = layout.Panel(ShopPanel.Free);
                Box above = layout.IsTwoColumns
                    ? layout.Panel(side < 0f ? ShopPanel.Coins : ShopPanel.Boosters)
                    : layout.Panel(ShopPanel.Boosters);
                PictureWire.Add(sketch, panelWire - PictureWire.Width / 2f, free.Top - 1f, above.Bottom + 1f);
                if (!layout.IsTwoColumns)
                {
                    Box coins = layout.Panel(ShopPanel.Coins);
                    PictureWire.Add(sketch, panelWire - PictureWire.Width / 2f, above.Top - 1f, coins.Bottom + 1f);
                }
            }

            return sketch.ToMesh("Shop wires");
        }

        /// <summary>A set's button while it is dimmed: a finger still presses it, and the rules then say what the coins lack.</summary>
        private sealed class DimmedButton : IPressable
        {
            private readonly SmallButton _button;

            public DimmedButton(SmallButton button)
            {
                _button = button;
            }

            public bool IsUnder(Vector2 point) => _button.IsDimmed && _button.Covers(point);

            public void ShowPressed(bool isPressed) => _button.ShowPressed(isPressed);
        }
    }
}
