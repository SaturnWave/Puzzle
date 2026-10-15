#nullable enable
using System.Linq;
using Puzzle.Client.Art;
using Puzzle.Client.Canvases;
using Puzzle.Client.Flow;
using Puzzle.Client.Gallery;
using Puzzle.Client.Jigsaws;
using Puzzle.Client.Layout;
using Puzzle.Client.Painting;
using Puzzle.Client.Play;
using Puzzle.Client.Screens;
using Puzzle.Client.Stage;
using Puzzle.Museum;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Hall
{
    /// <summary>
    /// The museum's home, as the design canvas draws it (HomeTR): the hall of the room the player is restoring, with
    /// the room's plaque under the cornice, the painting being restored in its frame on the wall, the painter's
    /// workbench on the floor, and the green button that opens the level the player stands on. While pieces of the
    /// painting wait to be placed, the next of them leans on the workbench among sparkles, and the blue "Tablo"
    /// button stands beside the green one, with a row of pips under its word for the pieces in place and a red badge
    /// that counts the ones that wait: it leads to the painting's jigsaw. On the wall at the workbench's right hangs
    /// the gift frame (<see cref="GiftFrame"/>): the daily reward, with a red dot while it waits. Over the plaque the
    /// museum's stage shows the player's coins and lives (<see cref="WalletRow"/>).
    /// </summary>
    /// <remarks>
    /// The hall has two more faces, which the design canvas does not draw:
    /// <list type="bullet">
    /// <item>When nothing is left to restore (no work is chosen yet for the paintings that follow, or the whole
    /// museum is restored) the frame keeps the painting restored last, whole, and a note over the play button says
    /// which of the two it is.</item>
    /// <item>When the fifth painting of a room has just become whole, the hall shows that room restored before it
    /// moves on: all five paintings sealed on the plaque, the room's last painting in the frame under a running
    /// light and among sparkles, and where the workbench stands a banner that says so and what comes next. Its
    /// green button leads on to the hall as the player's progress has it.</item>
    /// </list>
    /// Left out of the design, because the game has nothing behind them yet: the event frame at the workbench's left,
    /// and on the wallet row the settings' button (the settings' stage brings it).
    /// </remarks>
    public sealed class HomePage : IMuseumPage, IMovingPage, IShopPage
    {
        /// <summary>
        /// The buttons' labels, as the design sets them (HomeTR): "Bölüm 47" at 29 points; "Tablo" at 19 beside a
        /// piece of 38, its line 5.5 points over where a label alone stands, over a row of pips whose middle lies
        /// 9.5 under the face's. The pips' hollows are the design's dark at 55 % over the button's blue, as one colour.
        /// </summary>
        private const float PlayLabelSize = 29f;
        private const float JigsawLabelSize = 19f;
        private const float JigsawMarkSize = 38f;
        private const float JigsawLabelLift = 5.5f;
        private const float PipsUnderMiddle = 9.5f;
        private static readonly Color PipsGround = Tint.Of(0x0F2670);
        private static readonly Color PipsHollow = Tint.Of(0x1A3C94);

        /// <summary>
        /// The piece that waits, leaning on the workbench (HomeTR), in the bench's own points from its feet: its
        /// cell fits a box of 56 by 44, its middle stands here, and it leans 8 degrees back. Around it three
        /// sparkles: where, and how large.
        /// </summary>
        private static readonly Vector2 WaitingPieceBox = new Vector2(56f, 44f);
        private static readonly Vector2 WaitingPieceMiddle = new Vector2(-50f, 144f);
        private const float WaitingPieceLeans = 8f;
        private const float WaitingPieceSoftEdge = 0.5f;
        private const float SparkleDrawnSize = 24f;
        private static readonly (float Across, float Up, float Size)[] SparklesBesideThePiece = { (-13f, 164f, 15f), (-82f, 143f, 10f), (-42f, 177f, 9f) };
        private const float NoteSize = 12.5f;
        private const float NoteSideRoom = 14f;

        /// <summary>A pill's picture is the pill and the room around it: 16 points wider than the pill, 40 high for a pill of 24.</summary>
        private const float PillPictureMargin = 16f;
        private const float PillPictureHeight = 40f;

        /// <summary>A wall lamp hangs with its foot this far under the middle of the frame.</summary>
        private const float LampFootUnderFrameMiddle = 14f;

        /// <summary>
        /// On the banner of a restored room, in its own points from the middle of its body: its nameplate, which is
        /// a result card's (WinTR: 280 points wide, the title at 20.5), and its two lines with the room each has
        /// between the banner's sides.
        /// </summary>
        private const float BannerNameplateWidth = 280f;
        private const float BannerTitleSize = 20.5f;
        private const float BannerLineSize = 15f;
        private const float BannerLineDown = 3f;
        private const float BannerNoteSize = 12f;
        private const float BannerNoteDown = 21f;
        private const float BannerLineRoom = HallLayout.BannerWidth - 2f * 22f;

        /// <summary>The room lights up before the restored room's light runs and its sparkles burst.</summary>
        private const float CelebrationBeginsAfter = 0.4f;

        private readonly Transform _root;
        private readonly PaintingLook _look;
        private readonly Wall _wall;
        private readonly RoomTrim _trim;
        private readonly RoomPlaque _plaque;
        private readonly FramedPainting _painting;
        private readonly Transform _bench;
        private readonly Transform _leftLamp;
        private readonly Transform _rightLamp;
        private readonly PushButton _play;
        private readonly MuseumPress _playLeadsTo;

        /// <summary>The button that leads to the painting's jigsaw, and the pips under its word; nothing while no piece of the painting waits.</summary>
        private readonly PushButton? _toTheJigsaw;
        private readonly SmallPips? _pips;

        /// <summary>The next piece that waits, leaning on the workbench; nothing while none waits.</summary>
        private readonly LoosePiece? _waitingPiece;

        /// <summary>The pill over the play button that says why nothing is being restored; nothing while a painting is.</summary>
        private readonly Transform? _note;

        /// <summary>The gift frame beside the workbench; nothing in the hall of a room that has just been restored, where the bench stands aside too.</summary>
        private readonly GiftFrame? _gift;

        /// <summary>What a room that has just been restored adds to its hall; nothing in the hall as the progress has it.</summary>
        private readonly RoomBanner? _banner;
        private readonly RestoredSparkles? _sparkles;
        private readonly PaintingNumber _shown;

        private HomePage(Transform parent, ArtCatalogue art, PaintingGallery paintings, MuseumVisit visit, RoomNumber? restored)
        {
            _root = new GameObject("Home").transform;
            _root.SetParent(parent, worldPositionStays: false);

            bool isCelebrating = restored.HasValue;
            RoomWall room = RoomWall.Of(restored ?? visit.Room, visit.Progress, paintings.Catalogue);
            _shown = restored is RoomNumber whole ? whole.Paintings.Last() : visit.Painting;
            HungPainting painting = room.Painting(_shown);
            int piecesWaiting = isCelebrating ? 0 : painting.PiecesWaiting;
            _look = paintings.LookOf(_shown);
            _wall = Wall.Create(_root, art);
            _trim = RoomTrim.Create(_root, art);
            _plaque = RoomPlaque.Create(_root, art, paintings, room, isCelebrating ? null : visit.BeingRestored);
            _painting = FramedPainting.Create(_root, art, _look, painting);
            _bench = SceneObjects.AddSprite("Workbench", _root, art.Of(GalleryArt.Workbench), art.SpriteMaterial, DrawOrder.Bench).transform;
            _bench.gameObject.SetActive(!isCelebrating);
            _leftLamp = SceneObjects.AddSprite("Left lamp", _root, art.Of(GalleryArt.Sconce), art.SpriteMaterial, DrawOrder.Sconce).transform;
            _rightLamp = SceneObjects.AddSprite("Right lamp", _root, art.Of(GalleryArt.Sconce), art.SpriteMaterial, DrawOrder.Sconce).transform;

            _playLeadsTo = isCelebrating ? new MuseumPress.GoOnFromTheRestoredRoom() : (MuseumPress)new MuseumPress.Play();
            _play = new PushButton(
                "Play", _root, art, ButtonLook.Green, PlayLabel(visit, isCelebrating),
                piecesWaiting > 0 ? HallLayout.PlayWidthBesideJigsaw : HallLayout.ButtonWidth, PlayLabelSize,
                DrawOrder.PageButton, DrawOrder.PageButtonLettering);
            if (piecesWaiting > 0)
            {
                _toTheJigsaw = new PushButton(
                    "To the jigsaw", _root, art, ButtonLook.Blue, MuseumWords.ToTheJigsaw, HallLayout.JigsawButtonWidth, JigsawLabelSize,
                    DrawOrder.PageButton, DrawOrder.PageButtonLettering, GalleryArt.IconPiece, labelLift: JigsawLabelLift, markSize: JigsawMarkSize);
                _toTheJigsaw.AddBadge(piecesWaiting);
                _pips = new SmallPips("Pips", _toTheJigsaw.Face, art, PipsGround, PipsHollow, DrawOrder.PageButtonLettering);
                _pips.Show(painting.PiecesInPlace.Count, painting.PieceCount);
                float pipsScale = Mathf.Min(1f, _toTheJigsaw.RoomUnderTheLabel / SmallPips.WidthOf(painting.PieceCount));
                _pips.Transform.localPosition = new Vector3(_toTheJigsaw.LabelMiddle, -PipsUnderMiddle, 0f);
                _pips.Transform.localScale = new Vector3(pipsScale, pipsScale, 1f);
                _waitingPiece = LeanOnTheBench(art, visit.Progress.WaitingIn(_shown)[0]);
            }

            if (isCelebrating)
            {
                _banner = new RoomBanner(_root, art, room, MuseumWords.AfterTheRoom(room.Number, paintings.Catalogue));
                _sparkles = RestoredSparkles.Create(_root, art);
            }
            else
            {
                _gift = new GiftFrame(_root, art);
                if (NoteFor(visit.Standing.Task) is string note)
                {
                    _note = AddNote(art, note);
                }
            }
        }

        public Transform Root => _root;

        /// <summary>The middle of the play button, in the layout's points: where a finger presses it.</summary>
        public Vector2 PlayButtonMiddle => _play.Middle;

        /// <summary>The middle of the button that leads to the painting's jigsaw; nothing while no piece waits.</summary>
        public Vector2? JigsawButtonMiddle => _toTheJigsaw?.Middle;

        /// <summary>True while the hall shows a room that has just been restored: its green button leads on, not into a level.</summary>
        public bool ShowsARestoredRoom => _banner != null;

        /// <summary>The gift frame, for whoever presses it or reads what it shows; nothing in a restored room's hall.</summary>
        public GiftFrame? Gift => _gift;

        /// <param name="restored">The room that has just been restored, which the hall then shows; nothing for the hall as the player's progress has it.</param>
        public static HomePage Create(Transform parent, ArtCatalogue art, PaintingGallery paintings, MuseumVisit visit, RoomNumber? restored = null) =>
            new HomePage(parent, art, paintings, visit, restored);

        public void Fit(ScreenShape screen)
        {
            HallLayout layout = HallLayout.Of(screen, _look.Aspect, hasJigsawButton: _toTheJigsaw != null);
            _wall.Fit(layout.Page.View);
            _trim.Show(layout.Page, layout.FloorLine);
            _plaque.Place(layout.Page);
            _painting.Place(layout.Hanging, layout.Page.View);
            _bench.localPosition = new Vector3(0f, layout.BenchFeet, 0f);
            _bench.localScale = new Vector3(layout.BenchScale, layout.BenchScale, 1f);
            _play.Place(new Vector2(layout.Button.MiddleX, layout.Button.MiddleY), layout.ButtonScale);
            if (_toTheJigsaw != null && layout.JigsawButton is Box jigsawButton)
            {
                _toTheJigsaw.Place(new Vector2(jigsawButton.MiddleX, jigsawButton.MiddleY), layout.ButtonScale);
            }

            if (_note != null)
            {
                _note.localPosition = new Vector3(0f, layout.NoteMiddle, 0f);
            }

            _banner?.Place(layout.Banner, layout.BannerScale);
            _sparkles?.Place(layout.Hanging.Frame);
            _gift?.Place(layout);
            HangTheLamps(layout);
        }

        public void AddPressables(PressSet<MuseumPress> presses)
        {
            presses.Add(_play, _playLeadsTo);
            if (_toTheJigsaw != null)
            {
                presses.Add(_toTheJigsaw, new MuseumPress.Restore(_shown));
            }

            if (_gift != null)
            {
                presses.Add(_gift, new MuseumPress.TakeTheGift());
            }
        }

        /// <summary>The gift frame shows whether the day's gift waits.</summary>
        public void Tell(ShopView shop) => _gift?.Show(shop.Gift);

        /// <summary>A restored room's light runs over its last painting once, and its sparkles burst and go on twinkling; the hall as the progress has it stands still.</summary>
        public void Move(float secondsOnThePage)
        {
            if (_sparkles == null)
            {
                return;
            }

            float celebrating = secondsOnThePage - CelebrationBeginsAfter;
            _painting.Canvas.ShowLight(Easing.InOut(Mathf.Clamp01(celebrating / Tempo.RestoredLight)));
            _sparkles.Show(celebrating, secondsOnThePage);
        }

        /// <summary>The hall has no way back: it is where the ways back lead.</summary>
        public bool TryGoBack() => false;

        public void Dispose()
        {
            _wall.Dispose();
            _trim.Dispose();
            _plaque.Dispose();
            _painting.Dispose();
            _sparkles?.Dispose();
            _gift?.Dispose();
            _pips?.Dispose();
            _waitingPiece?.Dispose();
            SceneObjects.DestroyObjectOf(_root);
        }

        /// <summary>The piece that waits, standing on the workbench and leaning back, with sparkles around it; the bench's scale draws it with the bench.</summary>
        private LoosePiece LeanOnTheBench(ArtCatalogue art, int pieceNumber)
        {
            // A cut is the same at every size: the piece's cell on a painting one point wide tells its share of any width.
            var cell = _look.CutAt(new Vector2(1f, 1f / _look.Aspect)).Pieces[pieceNumber].Cell;
            float paintingWidth = Mathf.Min(WaitingPieceBox.x / (float)cell.Width, WaitingPieceBox.y / (float)cell.Height);
            LoosePiece piece = LoosePiece.Create(_bench, art, _look, pieceNumber, paintingWidth, WaitingPieceSoftEdge, DrawOrder.BenchPiece);
            piece.Transform.localPosition = new Vector3(WaitingPieceMiddle.x, WaitingPieceMiddle.y, 0f);
            piece.Transform.localRotation = Quaternion.Euler(0f, 0f, WaitingPieceLeans);
            foreach ((float across, float up, float size) in SparklesBesideThePiece)
            {
                Transform sparkle = SceneObjects.AddSprite("Sparkle", _bench, art.Of(GalleryArt.Sparkle), art.SpriteMaterial, DrawOrder.BenchSparkle).transform;
                sparkle.localPosition = new Vector3(across, up, 0f);
                sparkle.localScale = new Vector3(size / SparkleDrawnSize, size / SparkleDrawnSize, 1f);
            }

            return piece;
        }

        /// <summary>What the green button says: the level it opens, the last level again once all are won, or the way on from a restored room.</summary>
        private static string PlayLabel(MuseumVisit visit, bool isCelebrating) =>
            isCelebrating ? MuseumWords.OnFromTheRoom
            : visit.HasWonEveryLevel ? MuseumWords.PlayAgain
            : MuseumWords.Play(visit.LevelToPlay);

        /// <summary>What the hall tells a player who has nothing to restore just now; nothing for one who has.</summary>
        private static string? NoteFor(HallTask task) => task switch
        {
            HallTask.Restoring => null,
            HallTask.WaitingForPaintings => MuseumWords.MorePaintingsToCome,
            HallTask.MuseumRestored => MuseumWords.MuseumRestored,
            _ => throw new System.ArgumentOutOfRangeException(nameof(task), task, "A task the hall has no note for."),
        };

        private Transform AddNote(ArtCatalogue art, string words)
        {
            SpriteRenderer pill = SceneObjects.AddSprite("Note", _root, art.Of(GalleryArt.Pill), art.SpriteMaterial, DrawOrder.PageButton);
            pill.drawMode = SpriteDrawMode.Tiled; // the picture's middle repeats, its round ends keep their shape
            TextMeshPro line = Lettering.Write("Words", pill.transform, art, LetteringStyle.Label, NoteSize, Lettering.Ink, DrawOrder.PageButtonLettering);
            Lettering.Say(line, words);
            pill.size = new Vector2(Lettering.WidthOf(line, words) + 2f * NoteSideRoom + PillPictureMargin, PillPictureHeight);
            return pill.transform;
        }

        /// <summary>A lamp midway in the wall at each side of the painting, where the screen is wide enough to have such a wall.</summary>
        private void HangTheLamps(HallLayout layout)
        {
            _leftLamp.gameObject.SetActive(layout.HasLamps);
            _rightLamp.gameObject.SetActive(layout.HasLamps);
            if (!layout.HasLamps)
            {
                return;
            }

            float fromMiddle = layout.Frame.Right + layout.WallBesideTheFrame / 2f;
            float foot = layout.Frame.MiddleY - LampFootUnderFrameMiddle;
            _leftLamp.localPosition = new Vector3(-fromMiddle, foot, 0f);
            _rightLamp.localPosition = new Vector3(fromMiddle, foot, 0f);
        }

        /// <summary>
        /// The banner of a room that has just been restored: a cream plaque with the brass nameplate of a result's
        /// card on its top edge, pins and all, which says that the room is restored; on the plaque the room with
        /// its paintings, and what comes next. Sizes are in points around the middle of the plaque's body at its
        /// full size.
        /// </summary>
        private sealed class RoomBanner
        {
            private readonly PagePlaque _plaque;
            private readonly Transform _content;

            public RoomBanner(Transform parent, ArtCatalogue art, RoomWall room, string whatComesNext)
            {
                _plaque = new PagePlaque("Restored room", parent, art);
                _content = new GameObject("Banner").transform;
                _content.SetParent(_plaque.Root, worldPositionStays: false);

                var nameplate = new BrassNameplate(
                    "Nameplate", _content, art, new CardNameplate(MuseumWords.RoomRestored, BannerNameplateWidth, BannerTitleSize),
                    DrawOrder.PlaqueArt, DrawOrder.PlaqueLettering, DrawOrder.PlaqueNameplatePin);
                nameplate.Transform.localPosition = new Vector3(0f, HallLayout.BannerHeight / 2f, 0f);
                nameplate.AddThePins();

                TextMeshPro line = Lettering.Write("Room", _content, art, LetteringStyle.Label, BannerLineSize, Lettering.Ink, DrawOrder.PlaqueLettering);
                line.transform.localPosition = new Vector3(0f, -BannerLineDown, 0f);
                Lettering.SayWithin(line, MuseumWords.RoomAndItsPaintings(room.Name), BannerLineSize, BannerLineRoom);

                TextMeshPro next = Lettering.Write("What comes next", _content, art, LetteringStyle.Soft, BannerNoteSize, Lettering.SoftInk, DrawOrder.PlaqueLettering);
                next.transform.localPosition = new Vector3(0f, -BannerNoteDown, 0f);
                Lettering.SayWithin(next, whatComesNext, BannerNoteSize, BannerLineRoom);
            }

            /// <summary>Stands the banner where the layout has it, drawn at a share of its full size.</summary>
            public void Place(Box body, float scale)
            {
                _plaque.Place(body);
                _content.localScale = new Vector3(scale, scale, 1f);
            }
        }
    }
}
