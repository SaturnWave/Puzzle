#nullable enable
using System.Linq;
using Puzzle.Client.Art;
using Puzzle.Client.Canvases;
using Puzzle.Client.Flow;
using Puzzle.Client.Gallery;
using Puzzle.Client.Jigsaws;
using Puzzle.Client.Layout;
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
    /// painting wait to be placed, the blue "Tablo" button stands beside the green one, with a red badge that counts
    /// them: it leads to the painting's jigsaw.
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
    /// Left out of the design, because the game has nothing behind them yet: the gold and the lives with their
    /// plus buttons, the settings, and the event and gift frames beside the workbench. The piece that leans on the
    /// workbench while it waits is not drawn either; the badge on the blue button says that pieces wait.
    /// </remarks>
    public sealed class HomePage : IMuseumPage, IMovingPage
    {
        private const float PlayLabelSize = 29f;
        private const float JigsawLabelSize = 25f;
        private const float NoteSize = 12.5f;
        private const float NoteSideRoom = 14f;

        /// <summary>A pill's picture is the pill and the room around it: 16 points wider than the pill, 40 high for a pill of 24.</summary>
        private const float PillPictureMargin = 16f;
        private const float PillPictureHeight = 40f;

        /// <summary>A wall lamp hangs with its foot this far under the middle of the frame.</summary>
        private const float LampFootUnderFrameMiddle = 14f;

        /// <summary>On the banner of a restored room, in its own points from the middle of its body: its two lines, and the room each has between its sides.</summary>
        private const float BannerTitleSize = 19.5f;
        private const float BannerTitleRoom = 232f;
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

        /// <summary>The button that leads to the painting's jigsaw; nothing while no piece of the painting waits.</summary>
        private readonly PushButton? _toTheJigsaw;

        /// <summary>The pill over the play button that says why nothing is being restored; nothing while a painting is.</summary>
        private readonly Transform? _note;

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
                    DrawOrder.PageButton, DrawOrder.PageButtonLettering, GalleryArt.IconPiece);
                _toTheJigsaw.AddBadge(piecesWaiting);
            }

            if (isCelebrating)
            {
                _banner = new RoomBanner(_root, art, room, MuseumWords.AfterTheRoom(room.Number, paintings.Catalogue));
                _sparkles = RestoredSparkles.Create(_root, art);
            }
            else if (NoteFor(visit.Standing.Task) is string note)
            {
                _note = AddNote(art, note);
            }
        }

        public Transform Root => _root;

        /// <summary>The middle of the play button, in the layout's points: where a finger presses it.</summary>
        public Vector2 PlayButtonMiddle => _play.Middle;

        /// <summary>The middle of the button that leads to the painting's jigsaw; nothing while no piece waits.</summary>
        public Vector2? JigsawButtonMiddle => _toTheJigsaw?.Middle;

        /// <summary>True while the hall shows a room that has just been restored: its green button leads on, not into a level.</summary>
        public bool ShowsARestoredRoom => _banner != null;

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
            HangTheLamps(layout);
        }

        public void AddPressables(PressSet<MuseumPress> presses)
        {
            presses.Add(_play, _playLeadsTo);
            if (_toTheJigsaw != null)
            {
                presses.Add(_toTheJigsaw, new MuseumPress.Restore(_shown));
            }
        }

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
            SceneObjects.DestroyObjectOf(_root);
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
        /// The banner of a room that has just been restored: a cream plaque with the gilt nameplate of a result's
        /// card on its top edge, which says that the room is restored; on the plaque the room with its paintings,
        /// and what comes next. Sizes are in points around the middle of the plaque's body at its full size.
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

                Transform nameplate = SceneObjects.AddSprite("Nameplate", _content, art.Of(GalleryArt.Nameplate), art.SpriteMaterial, DrawOrder.PlaqueArt).transform;
                nameplate.localPosition = new Vector3(0f, HallLayout.BannerHeight / 2f, 0f);
                TextMeshPro title = Lettering.Write("Title", nameplate, art, LetteringStyle.Label, BannerTitleSize, Lettering.Ink, DrawOrder.PlaqueLettering);
                Lettering.SayWithin(title, MuseumWords.RoomRestored, BannerTitleSize, BannerTitleRoom);

                TextMeshPro line = Lettering.Write("Room", _content, art, LetteringStyle.Label, BannerLineSize, Lettering.Ink, DrawOrder.PlaqueLettering);
                line.transform.localPosition = new Vector3(0f, -BannerLineDown, 0f);
                Lettering.SayWithin(line, MuseumWords.RoomAndItsPaintings(room.Name), BannerLineSize, BannerLineRoom);

                TextMeshPro next = Lettering.Write("What comes next", _content, art, LetteringStyle.Label, BannerNoteSize, Lettering.SoftInk, DrawOrder.PlaqueLettering);
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
