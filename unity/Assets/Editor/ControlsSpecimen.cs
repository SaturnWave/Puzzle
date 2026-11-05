#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Client.Art;
using Puzzle.Client.Flow;
using Puzzle.Client.Gallery;
using Puzzle.Client.Layout;
using Puzzle.Client.Screens;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>Which of the game's shared controls a specimen shows. A closed set.</summary>
    internal enum ControlsPage
    {
        /// <summary>The large and the small buttons, in every face, and the settings' switches.</summary>
        Buttons,

        /// <summary>The round buttons, the boosters' discs in every face, the badges and a row of pips.</summary>
        Discs,
    }

    /// <summary>What a specimen of the controls lies on: the three grounds the game draws on. A closed set.</summary>
    internal enum ControlsGround
    {
        /// <summary>The gallery's wall.</summary>
        Wall,

        /// <summary>The cream of a card that hangs in front of the darkened room: the game's own hanging card, with a nameplate of two lines and the button that closes it.</summary>
        Cream,

        /// <summary>The wood of the shelf.</summary>
        Wood,
    }

    /// <summary>
    /// A specimen of the controls several screens share, built from the game's own classes and nothing else: every
    /// control in every face it has, laid out in the design canvas' points as wide as the canvas' cards (362), and
    /// drawn on a screen as a card over a museum page is, so a narrow screen shows it at the share of the design
    /// its cards have there. It is the picture to lay beside the canvas' own after a control was added or changed.
    /// </summary>
    internal sealed class ControlsSpecimen : IDisposable
    {
        /// <summary>The specimen is as wide as the canvas' cards; each page is as high as its rows need.</summary>
        private const float Width = 362f;
        private const float ButtonsHeight = 556f;
        private const float DiscsHeight = 528f;

        /// <summary>A row of the canvas' cards is this wide (WinTR: its row of buttons), and its first thing lies this far under a card's top edge, clear of the nameplate.</summary>
        private const float RowWidth = 328f;
        private const float FirstHeaderDown = 44f;

        /// <summary>From a header's middle down to the top of what it names, and from the foot of that to the next header's middle.</summary>
        private const float UnderAHeader = 15f;
        private const float OverAHeader = 16f;
        private const float BetweenRows = 10f;

        private readonly List<IDisposable> _owned = new List<IDisposable>();
        private readonly Transform _content;
        private readonly ArtCatalogue _art;
        private readonly float _height;

        private ControlsSpecimen(Transform content, ArtCatalogue art, float height)
        {
            _content = content;
            _art = art;
            _height = height;
        }

        /// <summary>What a press on the specimen's card would ask for; nothing listens.</summary>
        private enum SpecimenPress
        {
            Close,
        }

        /// <summary>Builds a page of the specimen on a ground, laid out for a screen, under a root that has nothing else on it.</summary>
        public static ControlsSpecimen Build(Transform root, ArtCatalogue art, ControlsPage page, ControlsGround ground, ScreenShape screen)
        {
            PageLayout layout = PageLayout.Of(screen);
            var body = new Vector2(Width, page == ControlsPage.Buttons ? ButtonsHeight : DiscsHeight);
            var owned = new List<IDisposable>();
            Wall wall = Wall.Create(root, art);
            wall.Fit(layout.View);
            owned.Add(wall);

            Transform content = ground switch
            {
                ControlsGround.Wall => Bare(root, body, layout),
                ControlsGround.Cream => OnACard(root, art, body, layout, owned),
                ControlsGround.Wood => OnWood(root, art, body, layout),
                _ => throw new ArgumentOutOfRangeException(nameof(ground), ground, "A ground the specimen is not shown on."),
            };

            var specimen = new ControlsSpecimen(content, art, body.y);
            specimen._owned.AddRange(owned);
            switch (page)
            {
                case ControlsPage.Buttons:
                    specimen.LayTheButtons();
                    break;
                case ControlsPage.Discs:
                    specimen.LayTheDiscs();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(page), page, "A page the specimen does not have.");
            }

            return specimen;
        }

        public void Dispose()
        {
            foreach (IDisposable thing in _owned)
            {
                thing.Dispose();
            }

            // On a card the controls went with the card; on the wall and on wood they are taken away here.
            SceneObjects.DestroyObjectOf(_content);
        }

        /// <summary>The specimen's own points on the bare wall: where a card of its size would hang, and as large.</summary>
        private static Transform Bare(Transform root, Vector2 body, PageLayout layout)
        {
            CardPlace place = layout.PlaceOfACard(body.x, body.y);
            Transform content = new GameObject("Specimen").transform;
            content.SetParent(root, worldPositionStays: false);
            content.localPosition = new Vector3(0f, place.Middle, 0f);
            content.localScale = new Vector3(place.Scale, place.Scale, 1f);
            return content;
        }

        /// <summary>The game's hanging card in front of the dark, as the canvas hangs the card before a level (LevelStartTR).</summary>
        private static Transform OnACard(Transform root, ArtCatalogue art, Vector2 body, PageLayout layout, List<IDisposable> owned)
        {
            Veil veil = Veil.Create(root, art);
            veil.Fit(layout.View);
            veil.Darken(1f);
            owned.Add(veil);

            var card = new HangingCard<SpecimenPress>(
                "Specimen card", root, art, body,
                new CardNameplate("Bölüm 47", width: 212f, titleSize: 22f, secondLine: "ÖDÜL · PARÇA 7 / 10"), CardHanging.FromPinsInTheWall);
            card.AddCloseButton(SpecimenPress.Close);
            card.Place(layout.PlaceOfACard(card.Width, card.Height), layout.View);
            card.ComeDown(1f);
            owned.Add(card);
            return card.Content;
        }

        /// <summary>A plank as large as the specimen, on the wall.</summary>
        private static Transform OnWood(Transform root, ArtCatalogue art, Vector2 body, PageLayout layout)
        {
            Transform content = Bare(root, body, layout);
            AddPlank("Ground", content, art, Vector2.zero, body, DrawOrder.Card);
            return content;
        }

        /// <summary>The wooden plank with its body of a given size around a middle; its picture has 28 points of room at its sides and 68 above and below.</summary>
        private static void AddPlank(string name, Transform parent, ArtCatalogue art, Vector2 middle, Vector2 body, DrawOrder order)
        {
            SpriteRenderer plank = SceneObjects.AddSprite(name, parent, art.Of(GalleryArt.Plank), art.SpriteMaterial, order);
            plank.drawMode = SpriteDrawMode.Sliced; // the picture's middle stretches both ways; its round corners keep their shape
            plank.size = body + new Vector2(28f, 68f);
            plank.transform.localPosition = new Vector3(middle.x, middle.y, 0f);
        }

        /// <summary>A place of the specimen: so far to the right of its middle, so far under its top edge.</summary>
        private Vector2 At(float right, float down) => new Vector2(right, _height / 2f - down);

        /// <returns>How far under the specimen's top edge what the header names begins.</returns>
        private float Header(string words, float down)
        {
            var header = new SectionHeader(words, _content, _art, words, DrawOrder.CardArt, DrawOrder.CardLettering);
            header.Transform.localPosition = At(0f, down);
            _owned.Add(header);
            return down + UnderAHeader;
        }

        private PushButton Large(string name, ButtonLook look, string label, float width, float labelSize, float left, float top, GalleryArt? mark = null)
        {
            var button = new PushButton(name, _content, _art, look, label, width, labelSize, DrawOrder.CardArt, DrawOrder.CardLettering, mark);
            button.Place(At(-RowWidth / 2f + left + width / 2f, top + PushButton.Height / 2f));
            return button;
        }

        private PushButton WithAPrice(string name, string label, string price, float width, float left, float top)
        {
            // LoseTR: "+5 hamle" at 20 points on a button of 152.
            PushButton button = PushButton.WithAPrice(name, _content, _art, ButtonLook.Green, label, price, width, labelSize: 20f, DrawOrder.CardArt, DrawOrder.CardLettering);
            button.Place(At(-RowWidth / 2f + left + width / 2f, top + PushButton.Height / 2f));
            return button;
        }

        private SmallButton Small(string name, SmallButtonColour colour, string label, float labelSize, int place, float top, SmallButtonMark mark = SmallButtonMark.None)
        {
            // ShopTR: three small buttons of 100 points side by side, 14 apart.
            const float width = 100f;
            const float apart = 114f;
            var button = new SmallButton(name, _content, _art, colour, label, width, labelSize, DrawOrder.CardArt, DrawOrder.CardLettering, mark);
            button.Place(At((place - 1) * apart, top + SmallButton.Height / 2f));
            return button;
        }

        /// <summary>The large buttons (WinTR, LoseTR, the hall's), the small ones (ShopTR), each face once, and the switches (SettingsTR).</summary>
        private void LayTheButtons()
        {
            // WinTR's own row: green beside blue with its mark and two lines.
            float top = Header("BÜYÜK DÜĞMELER", FirstHeaderDown);
            Large("Green", ButtonLook.Green, "Devam", width: 150f, labelSize: 28f, left: 0f, top);
            Large("Blue with a mark", ButtonLook.Blue, "Parçayı\nyerleştir", width: 166f, labelSize: 17.5f, left: 162f, top, GalleryArt.IconPiece);

            // LoseTR's offer as it is, and as it is when the coins do not cover it: said anew, and dimmed.
            top += PushButton.Height + PushButton.Lip + BetweenRows;
            WithAPrice("With a price", "+5 hamle", NumberWords.Amount(900), width: 152f, left: 6f, top);
            PushButton tooDear = WithAPrice("With a price, dimmed", "+5 hamle", NumberWords.Amount(900), width: 152f, left: 170f, top);
            tooDear.Say("Altın yetmiyor");
            tooDear.SayPrice(NumberWords.Amount(1250));
            tooDear.Dim(true);

            // The result card's teal button with a badge of two figures, and a green one with two lines, held down.
            top += PushButton.Height + PushButton.Lip + BetweenRows;
            Large("Teal with a badge", ButtonLook.Teal, "Müze", width: 118f, labelSize: 21f, left: 24f, top).AddBadge(12);
            Large("Green, held", ButtonLook.Green, "Tekrar\noyna", width: 150f, labelSize: 17.5f, left: 166f, top).ShowPressed(true);

            top = Header("KÜÇÜK DÜĞMELER", top + PushButton.Height + PushButton.Lip + OverAHeader);
            Small("Small green", SmallButtonColour.Green, "Yakında", labelSize: 16f, place: 0, top);
            Small("Small green with a coin", SmallButtonColour.Green, NumberWords.Amount(2100), labelSize: 16f, place: 1, top, SmallButtonMark.Coin);
            Small("Small blue with the triangle", SmallButtonColour.Blue, "İzle", labelSize: 18f, place: 2, top, SmallButtonMark.Play);

            top += SmallButton.Height + SmallButton.Lip + BetweenRows;
            Small("Small green, dimmed", SmallButtonColour.Green, NumberWords.Amount(6300), labelSize: 16f, place: 0, top, SmallButtonMark.Coin).Dim(true);
            Small("Small green, held", SmallButtonColour.Green, "Satın al", labelSize: 16f, place: 1, top).ShowPressed(true);
            SmallButton tomorrow = Small("Small blue, said anew", SmallButtonColour.Blue, "İzle", labelSize: 18f, place: 2, top, SmallButtonMark.Play);
            tomorrow.Say("Yarın yine");
            tomorrow.Dim(true);

            // SettingsTR's switches, each in its 70 by 44 button: on, off, and on while it is held.
            top = Header("ANAHTARLAR", top + SmallButton.Height + SmallButton.Lip + OverAHeader);
            float middle = top + Toggle.ButtonHeight / 2f;
            new Toggle("Switch on", _content, _art, isOn: true, DrawOrder.CardDisc).Place(At(-100f, middle));
            new Toggle("Switch off", _content, _art, isOn: false, DrawOrder.CardDisc).Place(At(0f, middle));
            var held = new Toggle("Switch on, held", _content, _art, isOn: true, DrawOrder.CardDisc);
            held.Place(At(100f, middle));
            held.ShowPressed(true);
        }

        private DiscButton Round(string name, DiscGlyph glyph, float right, float middleDown, float scale = 1f)
        {
            var button = new DiscButton(name, _content, _art, glyph, DrawOrder.CardCloseButton);
            button.Place(At(right, middleDown), scale);
            return button;
        }

        private BoosterDisc Booster(string name, BoosterPicture picture, float diameter, float right, float middleDown, BoosterFace face)
        {
            var disc = new BoosterDisc(name, _content, _art, picture, diameter, DrawOrder.CardDisc, face);
            disc.Place(At(right, middleDown));
            return disc;
        }

        /// <summary>The round buttons, the boosters' discs on their planks (LevelStartTR, BoardTR), the badges and a row of pips (WinTR).</summary>
        private void LayTheDiscs()
        {
            float top = Header("YUVARLAK DÜĞMELER", FirstHeaderDown);
            float row = top + 26f;
            Round("Back", DiscGlyph.Back, right: -140f, row);
            Round("Pause", DiscGlyph.Pause, right: -84f, row, scale: 46f / DiscButton.Size); // BoardTR draws the pause disc 46 across
            Round("Close", DiscGlyph.Close, right: -28f, row);
            Round("Gear", DiscGlyph.Gear, right: 28f, row);
            Round("Get more", DiscGlyph.Plus, right: 76f, row);
            Round("Forward, leading nowhere", DiscGlyph.Forward, right: 124f, row).LeadSomewhere(false);

            // LevelStartTR's shelf: a plank of 328 by 88 with three discs of 66 in three equal columns, 8 points in from its ends.
            top = Header("GÜÇLENDİRİCİLER", row + 30f + OverAHeader);
            const float column = (RowWidth - 2f * 8f) / 3f;
            AddPlank("Shelf before a level", _content, _art, At(0f, top + 44f), new Vector2(RowWidth, 88f), DrawOrder.CardArt);
            Booster("Chosen", BoosterPicture.BrushAndSplat, diameter: 66f, right: -column, top + 42.5f, BoosterFace.Of(2, BoosterChoice.Chosen));
            Booster("Owned", BoosterPicture.Of(PieceArt.ColorBomb), diameter: 66f, right: 0f, top + 42.5f, BoosterFace.Of(1, BoosterChoice.Waiting));
            Booster("None owned", BoosterPicture.Of(GalleryArt.IconThreeMoves), diameter: 66f, right: column, top + 42.5f, BoosterFace.Of(0, BoosterChoice.Waiting));

            // BoardTR's shelf, 78 high, with discs of 60 every 73 points: owned, owned in two figures, armed, held.
            top += 88f + 7f + BetweenRows;
            AddPlank("Shelf of the board", _content, _art, At(0f, top + 39f), new Vector2(RowWidth, 78f), DrawOrder.CardArt);
            Booster("Spatula", BoosterPicture.Of(GalleryArt.IconSpatula), diameter: 60f, right: -109.5f, top + 37.5f, BoosterFace.Of(3, BoosterChoice.Waiting));
            Booster("Lying brush, twelve", BoosterPicture.Of(PieceArt.RocketHorizontal), diameter: 60f, right: -36.5f, top + 37.5f, BoosterFace.Of(12, BoosterChoice.Waiting));
            Booster("Standing brush, armed", BoosterPicture.Of(PieceArt.RocketVertical), diameter: 60f, right: 36.5f, top + 37.5f, BoosterFace.Of(2, BoosterChoice.Armed));
            Booster("Shuffle, held", BoosterPicture.Of(GalleryArt.IconShuffle), diameter: 60f, right: 109.5f, top + 37.5f, BoosterFace.Of(1, BoosterChoice.Waiting)).ShowPressed(true);

            // The same shelf while another booster is aimed: everything set aside, and beside it a plus that is not.
            top += 78f + 7f + BetweenRows;
            AddPlank("Shelf set aside", _content, _art, At(0f, top + 39f), new Vector2(RowWidth, 78f), DrawOrder.CardArt);
            Booster("Spatula, set aside", BoosterPicture.Of(GalleryArt.IconSpatula), diameter: 60f, right: -109.5f, top + 37.5f, BoosterFace.Of(3, BoosterChoice.SetAside));
            Booster("Lying brush, set aside", BoosterPicture.Of(PieceArt.RocketHorizontal), diameter: 60f, right: -36.5f, top + 37.5f, BoosterFace.Of(12, BoosterChoice.SetAside));
            Booster("Shuffle, none and set aside", BoosterPicture.Of(GalleryArt.IconShuffle), diameter: 60f, right: 36.5f, top + 37.5f, BoosterFace.Of(0, BoosterChoice.SetAside));
            Booster("Shuffle, none", BoosterPicture.Of(GalleryArt.IconShuffle), diameter: 60f, right: 109.5f, top + 37.5f, BoosterFace.Of(0, BoosterChoice.Waiting));

            top = Header("ROZETLER VE PARÇALAR", top + 78f + 7f + OverAHeader);
            float badges = top + 11f;
            BadgeFace[] faces = { BadgeFace.Waiting(1), BadgeFace.Waiting(12), BadgeFace.Owned(3), BadgeFace.Owned(12), BadgeFace.GetMore };
            for (int badge = 0; badge < faces.Length; badge++)
            {
                Vector2 middle = At(-RowWidth / 2f + 20f + badge * 38f, badges);
                CountBadge.Add($"Badge {badge + 1}", _content, _art, faces[badge], DrawOrder.CardBadge).Transform.localPosition = new Vector3(middle.x, middle.y, 0f);
            }

            // WinTR: ten pieces, seven won, the seventh just now; a row of pips is as wide as it is, so it stands under the badges.
            new PipRow("Pips", _content, _art, PipRowPlan.Of(pieceCount: 10, won: 7, justWon: 1), DrawOrder.CardArt).Place(At(0f, badges + 11f + BetweenRows + 8f));
        }
    }
}
