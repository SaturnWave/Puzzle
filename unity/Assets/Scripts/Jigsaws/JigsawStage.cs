#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Puzzle.Client.Art;
using Puzzle.Client.Canvases;
using Puzzle.Client.Flow;
using Puzzle.Client.Gallery;
using Puzzle.Client.Hall;
using Puzzle.Client.Layout;
using Puzzle.Client.Play;
using Puzzle.Client.Screens;
using Puzzle.Client.Stage;
using Puzzle.Museum;
using Puzzle.Museum.Jigsaw;
using UnityEngine;

namespace Puzzle.Client.Jigsaws
{
    /// <summary>
    /// A painting's jigsaw, as the design canvas draws that screen (JigsawTR): the painting in its frame with the
    /// pieces that are in place, the pieces the player has won waiting in the wooden tray, and the player's finger
    /// that carries a piece from the tray to its place. A piece let go near its place drops in; let go anywhere
    /// else it goes back to the tray, so no piece is ever lost. The blue button places every waiting piece for the
    /// player. When the last piece is in, a light runs over the painting and sparkles burst around its frame.
    /// </summary>
    /// <remarks>
    /// Like the other stages it has a clock that only <see cref="Advance"/> moves and one camera that draws all of
    /// it. The stage does not keep the player's progress: it begins with the painting as the progress has it, and
    /// whoever owns the progress reads <see cref="Table"/> after every step and keeps what was placed. When the
    /// player has pressed the button that leads back and the room has gone dark, <see cref="PlayerLeft"/> is true.
    /// <para>
    /// The player's finger is not held up by a piece that is still settling: while one piece drops in, lights up,
    /// or goes back to the tray, the next one can be picked up already. Only what takes the screen over stops the
    /// finger: the room lighting up or going dark, every piece being placed at once, and the celebration.
    /// </para>
    /// </remarks>
    public sealed class JigsawStage : IStage
    {
        /// <summary>What the player can press on this screen. A closed set.</summary>
        private enum JigsawPress
        {
            /// <summary>The round button, and the green one once nothing waits: back to where the player came from.</summary>
            Leave,

            /// <summary>The blue button: every waiting piece goes to its place.</summary>
            PlaceThemAll,
        }

        /// <summary>A carried piece is held this far over the fingertip, so the finger does not hide it, and never leaves the screen by more than its middle.</summary>
        private const float CarriedOverTheFinger = 30f;

        /// <summary>A piece that flies to its place by itself rises this far over the straight way, and comes out of the tray at this share of its tray size.</summary>
        private const float FlightRise = 34f;
        private const float ComesOutAt = 0.4f;

        /// <summary>The frame of a painting that is whole again swells once by this share of its size.</summary>
        private const float RestoredSwell = 0.035f;

        /// <summary>
        /// The buttons' labels. The design sets "Otomatik tamamla" at 18 points on a button of 260 (JigsawTR); on
        /// the game's button, which is drawn smaller than its measures, it is as large as fits beside its mark.
        /// Its two words stand as far apart as the design sets them (word-spacing 4 px at 18 px): both of the
        /// outline's widths given back to the gap, where most labels get one, so that blue shows between the words.
        /// </summary>
        private const float PlaceAllLabelSize = 20f;
        private const float PlaceAllWordsApart = 4f / 18f;
        private const float DoneLabelSize = 29f;

        private readonly GameObject _root;
        private readonly PaintingLook _look;
        private readonly Wall _wall;
        private readonly RoomTrim _trim;
        private readonly PieceCounter _counter;
        private readonly FramedPainting _painting;
        private readonly ProgressPips _pips;
        private readonly PieceTray _tray;
        private readonly RestoredSparkles _sparkles;
        private readonly DiscButton _back;
        private readonly PushButton _placeAll;
        private readonly PushButton _done;
        private readonly Veil _veil;
        private readonly Timeline _timeline = new Timeline();
        private readonly PressSet<JigsawPress> _presses = new PressSet<JigsawPress>();

        /// <summary>The pieces that are on their way to their place, and the ones that arrived or came back to the tray in the step of the clock that is on.</summary>
        private readonly HashSet<int> _flying = new HashSet<int>();
        private readonly List<int> _landed = new List<int>();
        private readonly List<int> _cameBack = new List<int>();

        /// <summary>The pieces something on the timeline is moving just now (to their place, back to the tray, or popping up in it): no finger picks one of them up.</summary>
        private readonly HashSet<int> _moving = new HashSet<int>();

        /// <summary>The lights on the pieces that have just dropped in, each fading on the timeline.</summary>
        private readonly List<PieceGlow> _glows = new List<PieceGlow>();

        private ScreenShape? _screen;
        private JigsawLayout? _layout;
        private JigsawCut? _cut;
        private TrayRow _row;
        private Carry? _carried;
        private float _clock;

        /// <summary>How long the painting has been whole; the burst of sparkles follows it. Nothing while pieces are missing.</summary>
        private float? _wholeFor;
        private bool _isLeaving;

        /// <summary>True while the room lights up as the screen opens.</summary>
        private bool _isOpening;

        /// <summary>True from the press on the blue button until the last of the pieces it sends has dropped in.</summary>
        private bool _isPlacingThemAll;

        /// <summary>True when the blue button was pressed while a piece was still moving: the others leave once it is at rest.</summary>
        private bool _waitsToPlaceThemAll;

        private JigsawStage(PaintingNumber painting, MuseumProgress progress, PaintingGallery paintings, ArtCatalogue art)
        {
            Table = new JigsawTable(painting, progress);
            _row = new TrayRow(Table.Waiting, placeCount: 1);
            _look = paintings.LookOf(painting);
            _root = new GameObject("Jigsaw");
            Camera = StageCamera.Add(_root.transform);
            _wall = Wall.Create(_root.transform, art);
            _trim = RoomTrim.Create(_root.transform, art);
            _counter = PieceCounter.Create(_root.transform, art);
            _painting = FramedPainting.Create(_root.transform, art, _look);
            _pips = ProgressPips.Create(_root.transform, art, Table.PieceCount);
            _tray = PieceTray.Create(_root.transform, art, _look);
            _sparkles = RestoredSparkles.Create(_root.transform, art);
            _back = new DiscButton("Back", _root.transform, art, DiscGlyph.Back, DrawOrder.PageButton);
            _placeAll = new PushButton(
                "Place them all", _root.transform, art, ButtonLook.Blue, MuseumWords.PlaceThemAll, JigsawLayout.ButtonWidth, PlaceAllLabelSize,
                DrawOrder.PageButton, DrawOrder.PageButtonLettering, GalleryArt.IconPiece, PlaceAllWordsApart);
            _done = new PushButton(
                "Done", _root.transform, art, ButtonLook.Green, MuseumWords.Done, JigsawLayout.ButtonWidth, DoneLabelSize,
                DrawOrder.PageButton, DrawOrder.PageButtonLettering);
            _veil = Veil.Create(_root.transform, art);
            _presses.Add(_back, JigsawPress.Leave);
            _presses.Add(_placeAll, JigsawPress.PlaceThemAll);
            _presses.Add(_done, JigsawPress.Leave);

            // A painting that is whole when the screen opens has had its celebration: its sparkles are out already.
            _wholeFor = Table.IsWhole ? Tempo.PauseBeforeRestored + RestoredSparkles.BurstSeconds : (float?)null;
        }

        public Camera Camera { get; }

        /// <summary>The painting's jigsaw as it stands: which pieces are in place and which still wait.</summary>
        public JigsawTable Table { get; }

        /// <summary>True when no piece is on its way, no light or sparkle is still coming, and the room is neither lighting up nor going dark.</summary>
        public bool IsSettled => _timeline.IsDone && (_carried == null || _carried.Lift >= 1f) && !IsCelebrating && !_waitsToPlaceThemAll;

        /// <summary>
        /// True while pieces are on their way to their place, or about to leave for it: whoever keeps the player's
        /// progress waits for them and then keeps them all at once.
        /// </summary>
        public bool HasPiecesOnTheirWay => _flying.Count > 0 || _waitsToPlaceThemAll;

        /// <summary>True once the player has pressed a button that leads back and the room has gone dark: this stage is over.</summary>
        public bool PlayerLeft { get; private set; }

        /// <summary>The piece a finger is carrying; nothing while none is carried.</summary>
        public int? PieceInTheHand => _carried?.Piece;

        /// <summary>The pieces the tray shows, from left to right: the ones a finger can pick up.</summary>
        public IReadOnlyList<int> PiecesInTheTray => _row.Shown.ToArray();

        /// <summary>The pixel of the screen in the middle of the round button that leads back; nothing while the stage does not listen.</summary>
        public Vector2? BackButtonPixel => ListensToThePlayer ? PixelOf(_back.Middle) : null;

        /// <summary>
        /// The pixel of the screen in the middle of the button under the tray (the blue one that places every piece
        /// while pieces wait, the green one that leads back once none does); nothing while the stage does not listen.
        /// </summary>
        public Vector2? TrayButtonPixel => ListensToThePlayer ? PixelOf(Table.Waiting.Count > 0 ? _placeAll.Middle : _done.Middle) : null;

        private bool ListensToThePlayer => !_isOpening && !_isPlacingThemAll && !IsCelebrating && !_isLeaving && !PlayerLeft;

        private bool IsCelebrating => _wholeFor is float seconds && seconds < Tempo.PauseBeforeRestored + RestoredSparkles.BurstSeconds;

        /// <summary>Builds the jigsaw of a painting as a player's progress has it. Call <see cref="Fit"/> before the first picture.</summary>
        /// <param name="opening">In the lit room, as a picture command shows it; or out of the dark the stage before left behind.</param>
        public static JigsawStage Open(
            PaintingNumber painting, MuseumProgress progress, PaintingGallery paintings, ArtCatalogue art, StageOpening opening = StageOpening.InTheLitRoom)
        {
            var stage = new JigsawStage(painting, progress, paintings, art);
            if (opening == StageOpening.OutOfTheDark)
            {
                stage._veil.Darken(1f);
                stage._isOpening = true;
                stage._timeline.Add(
                    0f, Tempo.RoomLightens, progressOfTheLight => stage._veil.Darken(1f - Easing.Out(progressOfTheLight)), () => stage._isOpening = false);
            }

            return stage;
        }

        /// <summary>
        /// Lays the screen out. Whatever was moving is brought to its end first: a piece on its way is in its
        /// place, a piece in the player's hand is back in the tray.
        /// </summary>
        public void Fit(ScreenShape screen)
        {
            if (_layout != null)
            {
                EndWhatIsMoving();
            }

            _screen = screen;
            JigsawLayout layout = JigsawLayout.Of(screen, _look.Aspect, Table.Painting.Size, _look.Orientation);
            _layout = layout;
            ScreenView view = layout.Page.View;
            Box picture = layout.Hanging.Picture;
            _cut = _look.CutAt(new Vector2(picture.Width, picture.Height));

            StageCamera.Show(Camera, view);
            _wall.Fit(view);
            _trim.Show(layout.Page, floorLine: null);
            _counter.Place(layout);
            _painting.Place(layout.Hanging, view);
            _pips.Place(layout);
            _sparkles.Place(layout.Hanging.Frame);
            _tray.Place(layout, softEdge: 1f / view.PixelsPerPoint);
            _back.Place(new Vector2(layout.Back.MiddleX, layout.Back.MiddleY), layout.BackScale);
            _placeAll.Place(new Vector2(layout.Button.MiddleX, layout.Button.MiddleY), layout.ButtonScale);
            _done.Place(new Vector2(layout.Button.MiddleX, layout.Button.MiddleY), layout.ButtonScale);
            _veil.Fit(view);

            _row = new TrayRow(Table.Waiting, layout.TrayPlaces);
            _tray.Lay(_row);
            ShowTheTable();
            ShowTheSparkles();
        }

        public void Advance(float deltaSeconds)
        {
            _clock += deltaSeconds;
            if (_carried is Carry carried && carried.Lift < 1f)
            {
                carried.Lift = Mathf.Min(1f, carried.Lift + deltaSeconds / Tempo.PieceLifts);
                ShowInTheHand(carried);
            }

            _timeline.Advance(deltaSeconds);
            TakeInWhatArrived();
            if (_waitsToPlaceThemAll && _moving.Count == 0)
            {
                SendEveryPieceHome();
            }

            if (_wholeFor is float wholeFor)
            {
                _wholeFor = wholeFor + deltaSeconds;
            }

            ShowTheSparkles();
        }

        /// <summary>The pixel of the screen at which a piece lies in the tray: where a finger picks it up. Nothing for a piece the tray does not show.</summary>
        public Vector2? PixelOfTheTrayPiece(int piece) => _row.PlaceOf(piece) is null ? null : PixelOf(_tray.RestOf(piece));

        /// <summary>
        /// The pixel of the screen at which a finger lets a carried piece go for it to hang over a point of the
        /// painting: the piece is held a little over the fingertip.
        /// </summary>
        /// <param name="piece">Which piece's own place is meant.</param>
        /// <param name="away">How far from that place, in points: nothing drops the piece exactly in.</param>
        public Vector2? PixelToLetGoAt(int piece, Vector2 away) => PixelOf(HomeOf(piece) + away - new Vector2(0f, CarriedOverTheFinger));

        public void PointerDown(Vector2 pixel)
        {
            if (!ListensToThePlayer || _carried != null || PointAt(pixel) is not Vector2 point)
            {
                return;
            }

            _presses.FingerDown(point);
            if (!_presses.IsHolding && _tray.PieceAt(point) is int piece && !_moving.Contains(piece))
            {
                PickUp(piece, point);
            }
        }

        public void PointerMoved(Vector2 pixel)
        {
            if (PointAt(pixel) is not Vector2 point)
            {
                return;
            }

            if (_carried is Carry carried)
            {
                carried.Finger = point;
                ShowInTheHand(carried);
            }
            else if (ListensToThePlayer)
            {
                _presses.FingerMovedTo(point);
            }
        }

        public void PointerUp()
        {
            if (_carried is Carry carried)
            {
                LetGo(carried);
            }
            else if (ListensToThePlayer && _presses.FingerUp(out JigsawPress press))
            {
                Follow(press);
            }
        }

        /// <summary>The touch was taken away: a button that was held is not pressed, a piece that was carried goes back to the tray.</summary>
        public void PointerCancelled()
        {
            _presses.FingerGone();
            if (_carried is Carry carried)
            {
                _carried = null;
                SendBack(carried.Piece);
                ShowTheTable();
            }
        }

        /// <summary>The device's way back: a piece in the player's hand goes back to the tray; otherwise the jigsaw is left, as by its round button.</summary>
        public void GoBack()
        {
            if (_carried != null)
            {
                PointerCancelled();
            }
            else if (ListensToThePlayer)
            {
                Leave();
            }
        }

        public void Dispose()
        {
            TakeTheGlowsAway();
            _wall.Dispose();
            _trim.Dispose();
            _counter.Dispose();
            _painting.Dispose();
            _pips.Dispose();
            _tray.Dispose();
            _sparkles.Dispose();
            _veil.Dispose();
            SceneObjects.Destroy(_root);
        }

        private void Follow(JigsawPress press)
        {
            switch (press)
            {
                case JigsawPress.Leave:
                    Leave();
                    break;
                case JigsawPress.PlaceThemAll:
                    PlaceThemAll();
                    break;
                default:
                    throw new InvalidOperationException($"A press of the kind {press} leads nowhere.");
            }
        }

        /// <summary>The room goes dark; once it is, this stage is over.</summary>
        private void Leave()
        {
            _isLeaving = true;
            _presses.FingerGone();
            _timeline.Add(0f, Tempo.RoomDarkens, progress => _veil.Darken(Easing.Out(progress)), () => PlayerLeft = true);
        }

        /// <summary>The finger takes a piece out of the tray: from now on the piece follows it.</summary>
        private void PickUp(int piece, Vector2 finger)
        {
            LoosePiece loose = _tray.PieceOf(piece);
            loose.DrawFrom(DrawOrder.Carried);
            var carried = new Carry(piece, grabbedAt: (Vector2)loose.Transform.localPosition - finger, finger);
            _carried = carried;
            ShowInTheHand(carried);
            ShowTheTable();
        }

        /// <summary>
        /// A carried piece where the finger has it: as it is lifted it grows to its size in the painting, stands
        /// straight, and comes to lie a little over the fingertip, where the player sees it.
        /// </summary>
        private void ShowInTheHand(Carry carried)
        {
            if (_layout is not JigsawLayout layout)
            {
                return;
            }

            float lifted = Easing.Out(carried.Lift);
            ScreenView view = layout.Page.View;
            Vector2 held = carried.Finger + carried.GrabbedAt * (1f - lifted) + new Vector2(0f, CarriedOverTheFinger * lifted);
            held = new Vector2(Mathf.Clamp(held.x, view.Left, view.Right), Mathf.Clamp(held.y, view.Bottom, view.Top));
            float scale = Mathf.Lerp(_tray.RestScale, 1f, lifted);

            LoosePiece loose = _tray.PieceOf(carried.Piece);
            loose.Transform.localPosition = new Vector3(held.x, held.y, 0f);
            loose.Transform.localScale = new Vector3(scale, scale, 1f);
            loose.Transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(PieceTray.RestTurnOf(carried.Piece), 0f, lifted));
            loose.Lift(lifted);
        }

        /// <summary>The finger lets the piece go: near its place it drops in, anywhere else it goes back to the tray.</summary>
        private void LetGo(Carry carried)
        {
            _carried = null;
            int piece = carried.Piece;
            Vector2 away = (Vector2)_tray.PieceOf(piece).Transform.localPosition - HomeOf(piece);
            PictureRect cell = CutOnThisScreen().Pieces[piece].Cell;
            if (JigsawSnap.Catches(away.x, away.y, (float)cell.Width, (float)cell.Height))
            {
                FlyHome(piece, startsIn: 0f, Tempo.PieceDropsIn, rise: 0f);
            }
            else
            {
                SendBack(piece);
            }

            ShowTheTable();
        }

        /// <summary>
        /// A piece goes from where it is to its place in the painting, growing to its size there and standing
        /// straight as it goes; when it has arrived it is taken in after the step of the clock that brought it.
        /// </summary>
        /// <param name="rise">How far over the straight way the piece rises in the middle of its flight.</param>
        private void FlyHome(int piece, float startsIn, float seconds, float rise)
        {
            LoosePiece loose = _tray.PieceOf(piece);
            Transform moved = loose.Transform;
            Vector2 from = moved.localPosition;
            Vector2 home = HomeOf(piece);
            float fromScale = moved.localScale.x;
            float fromTurn = Mathf.DeltaAngle(0f, moved.localEulerAngles.z);
            _flying.Add(piece);
            _moving.Add(piece);
            _timeline.Add(
                startsIn,
                seconds,
                progress =>
                {
                    float gone = Easing.InOut(progress);
                    Vector2 place = Vector2.Lerp(from, home, gone) + new Vector2(0f, rise * Easing.Arch(progress));
                    float scale = Mathf.Lerp(fromScale, 1f, gone);
                    moved.gameObject.SetActive(true);
                    moved.localPosition = new Vector3(place.x, place.y, 0f);
                    moved.localScale = new Vector3(scale, scale, 1f);
                    moved.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(fromTurn, 0f, gone));
                    loose.Lift(1f - gone);
                },
                () => _landed.Add(piece));
        }

        /// <summary>A piece goes back to its place in the tray, shrinking to its size there.</summary>
        private void SendBack(int piece)
        {
            LoosePiece loose = _tray.PieceOf(piece);
            Transform moved = loose.Transform;
            Vector2 from = moved.localPosition;
            Vector2 rest = _tray.RestOf(piece);
            float fromScale = moved.localScale.x;
            float fromTurn = Mathf.DeltaAngle(0f, moved.localEulerAngles.z);
            float restTurn = PieceTray.RestTurnOf(piece);
            _moving.Add(piece);
            _timeline.Add(
                0f,
                Tempo.PieceGoesBack,
                progress =>
                {
                    float gone = Easing.Out(progress);
                    Vector2 place = Vector2.Lerp(from, rest, gone);
                    float scale = Mathf.Lerp(fromScale, _tray.RestScale, gone);
                    moved.localPosition = new Vector3(place.x, place.y, 0f);
                    moved.localScale = new Vector3(scale, scale, 1f);
                    moved.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(fromTurn, restTurn, gone));
                },
                () => _cameBack.Add(piece));
        }

        /// <summary>
        /// The blue button: from now on the screen is the pieces' until the last of them has dropped in. They leave
        /// at once when nothing is moving; a piece that is still settling is waited for first.
        /// </summary>
        private void PlaceThemAll()
        {
            _isPlacingThemAll = true;
            _presses.FingerGone();
            _waitsToPlaceThemAll = true;
            if (_moving.Count == 0)
            {
                SendEveryPieceHome();
            }
        }

        /// <summary>
        /// Every waiting piece flies to its place, one after another: first the ones the tray shows, then the ones
        /// that waited behind, which come out of the middle of the well.
        /// </summary>
        private void SendEveryPieceHome()
        {
            _waitsToPlaceThemAll = false;
            int[] waiting = Table.Waiting.ToArray();
            if (_layout is not JigsawLayout layout || waiting.Length == 0)
            {
                _isPlacingThemAll = false; // nothing leaves, so nothing will arrive to give the screen back
                return;
            }

            float every = Mathf.Min(Tempo.PiecesLeaveEvery, Tempo.PiecesLeaveWithin / waiting.Length);
            for (int turn = 0; turn < waiting.Length; turn++)
            {
                int piece = waiting[turn];
                LoosePiece loose = _row.PlaceOf(piece) is null ? ComingOutOfTheWell(piece, layout) : _tray.PieceOf(piece);
                loose.DrawFrom(DrawOrder.Carried);
                FlyHome(piece, turn * every, Tempo.PieceFlies, FlightRise);
            }

            // The pieces are on their way: the tray's places are empty, and nothing waits behind them.
            _row = new TrayRow(Array.Empty<int>(), layout.TrayPlaces);
            _tray.ForgetTheRow();
            ShowTheTable();
        }

        /// <summary>A piece that waited behind the tray's row: small in the middle of the well, out of sight until its flight begins.</summary>
        private LoosePiece ComingOutOfTheWell(int piece, JigsawLayout layout)
        {
            LoosePiece loose = _tray.Add(piece);
            float scale = ComesOutAt * _tray.RestScale;
            loose.Transform.localPosition = new Vector3(layout.Well.MiddleX, layout.Well.MiddleY, 0f);
            loose.Transform.localScale = new Vector3(scale, scale, 1f);
            loose.Transform.localRotation = Quaternion.Euler(0f, 0f, PieceTray.RestTurnOf(piece));
            loose.Transform.gameObject.SetActive(false);
            return loose;
        }

        /// <summary>
        /// What the last step of the clock brought: a piece that arrived is in its place from now on, and the tray's
        /// next piece comes into the place it left; a piece that came back lies in the tray again. Done here, after
        /// the step, because both begin new movements, which the timeline does not take while it is being advanced.
        /// </summary>
        private void TakeInWhatArrived()
        {
            if (_landed.Count == 0 && _cameBack.Count == 0)
            {
                return;
            }

            int[] landed = _landed.ToArray();
            foreach (int piece in landed)
            {
                PutInPlace(piece);
            }

            foreach (int piece in _cameBack)
            {
                _tray.Rest(_tray.PieceOf(piece));
                _moving.Remove(piece);
            }

            _landed.Clear();
            _cameBack.Clear();
            _isPlacingThemAll = _isPlacingThemAll && _flying.Count > 0;
            ShowTheTable();

            // After the canvas is drawn with the pieces in place: the light lies on what is seen there now.
            foreach (int piece in landed)
            {
                LightUp(piece);
            }
        }

        /// <summary>A piece that has just dropped in is lit for a moment: its light fades on the timeline and is then taken away.</summary>
        private void LightUp(int piece)
        {
            PieceGlow glow = _painting.Canvas.LightUp(piece);
            _glows.Add(glow);
            _timeline.Add(
                0f,
                Tempo.PieceLightsUp,
                progress => glow.Show(1f - Easing.In(progress)),
                () =>
                {
                    _glows.Remove(glow);
                    glow.Dispose();
                });
        }

        private void TakeTheGlowsAway()
        {
            foreach (PieceGlow glow in _glows)
            {
                glow.Dispose();
            }

            _glows.Clear();
        }

        /// <summary>A piece is in its place: the table knows it, its loose object is gone, and the tray's row closes up behind it.</summary>
        private void PutInPlace(int piece)
        {
            Table.Place(piece);
            _flying.Remove(piece);
            _moving.Remove(piece);
            _tray.Remove(piece);
            if (_row.PlaceOf(piece) is int)
            {
                CloseUpTheRow(piece);
            }

            if (Table.IsWhole && _wholeFor == null)
            {
                Celebrate();
            }
        }

        /// <summary>A piece has left its place in the tray: the next one that waited behind, if one did, pops up there.</summary>
        private void CloseUpTheRow(int piece)
        {
            if (_row.Take(piece) is int next)
            {
                LoosePiece coming = _tray.Add(next);
                _tray.Rest(coming);
                float restScale = _tray.RestScale;
                _moving.Add(next);
                _timeline.Add(
                    0f,
                    Tempo.TrayRefills,
                    progress =>
                    {
                        float scale = restScale * Easing.Overshoot(progress);
                        coming.Transform.localScale = new Vector3(scale, scale, 1f);
                    },
                    () => _moving.Remove(next));
            }

            _tray.ShowBehind(_row.Behind);
        }

        /// <summary>The painting is whole: after a breath a light runs over it and its frame swells once; the sparkles follow the time it has been whole.</summary>
        private void Celebrate()
        {
            _wholeFor = 0f;
            _timeline.Add(Tempo.PauseBeforeRestored, Tempo.RestoredLight, progress => _painting.Canvas.ShowLight(Easing.InOut(progress)));
            _timeline.Add(Tempo.PauseBeforeRestored, Tempo.RestoredSwell, progress => Swell(1f + RestoredSwell * Easing.Arch(progress)));
        }

        /// <summary>The sparkles of a painting that is whole, as long after its last piece as it is; none around one that is not.</summary>
        private void ShowTheSparkles() => _sparkles.Show((_wholeFor ?? 0f) - Tempo.PauseBeforeRestored, _clock);

        /// <summary>Draws the painting larger or smaller around the middle of its frame.</summary>
        private void Swell(float scale)
        {
            if (_layout is JigsawLayout layout)
            {
                var middle = new Vector2(layout.Hanging.Frame.MiddleX, layout.Hanging.Frame.MiddleY);
                Vector2 moved = middle * (1f - scale);
                _painting.Root.localPosition = new Vector3(moved.x, moved.y, 0f);
                _painting.Root.localScale = new Vector3(scale, scale, 1f);
            }
        }

        /// <summary>
        /// Brings everything that moves to its end at once, for a screen that changes under it: pieces on their
        /// way are in their place, a carried piece is no longer carried (the tray is laid anew by whoever calls),
        /// the light and the swell of a restored painting are over, and the room is as light or dark as it was
        /// going to be.
        /// </summary>
        private void EndWhatIsMoving()
        {
            _timeline.Clear();
            _presses.FingerGone();
            _carried = null;
            foreach (int piece in _flying.ToArray())
            {
                Table.Place(piece);
            }

            _flying.Clear();
            _moving.Clear();
            _landed.Clear();
            _cameBack.Clear();
            TakeTheGlowsAway();
            _isOpening = false;
            _isPlacingThemAll = false;
            _waitsToPlaceThemAll = false;
            if (Table.IsWhole)
            {
                _wholeFor = Mathf.Max(_wholeFor ?? 0f, Tempo.PauseBeforeRestored + RestoredSparkles.BurstSeconds);
            }

            _painting.Canvas.ShowLight(0f);
            Swell(1f);
            _veil.Darken(_isLeaving ? 1f : 0f);
            PlayerLeft = _isLeaving;
        }

        /// <summary>Shows the table as it stands everywhere it is told: on the counter, the pips, the canvas, the tray's hint, and in which of the two buttons under the tray is there to be pressed.</summary>
        private void ShowTheTable()
        {
            _counter.Show(Table);
            _pips.Show(Table);
            _painting.Show(Table.InPlace.ToArray(), new CanvasHelp(showsGhost: true, PlacesAskedFor()));
            _tray.Say(Table.IsWhole ? MuseumWords.PaintingRestored : Table.Waiting.Count > 0 ? MuseumWords.DragHint(Table.Waiting.Count) : MuseumWords.NoPieceWaits);

            bool canPlaceAll = Table.Waiting.Count > _flying.Count;
            _placeAll.Show(canPlaceAll && !_isPlacingThemAll);
            _done.Show(!canPlaceAll && !_isPlacingThemAll);
        }

        /// <summary>The places the canvas marks: the place of the piece in the player's hand, or else the places of the pieces the tray shows.</summary>
        private IReadOnlyCollection<int> PlacesAskedFor() =>
            _carried is Carry carried ? new[] { carried.Piece } : _row.Shown.Where(piece => !_flying.Contains(piece)).ToArray();

        /// <summary>The middle of a piece's own place in the painting, in the layout's points.</summary>
        private Vector2 HomeOf(int piece)
        {
            JigsawLayout layout = _layout ?? throw NoScreenYet();
            Box picture = layout.Hanging.Picture;
            Vector2 inThePainting = JigsawShapes.InScene(CutOnThisScreen().Pieces[piece].Home, new Vector2(picture.Width, picture.Height));
            return new Vector2(picture.MiddleX, picture.MiddleY) + inThePainting;
        }

        private JigsawCut CutOnThisScreen() => _cut ?? throw NoScreenYet();

        private static InvalidOperationException NoScreenYet() => new InvalidOperationException("The jigsaw has no screen yet; call Fit first.");

        private Vector2? PointAt(Vector2 pixel)
        {
            if (_screen is not ScreenShape screen)
            {
                return null;
            }

            (float x, float y) = ScreenView.Of(screen).PointAt(pixel.x, pixel.y);
            return new Vector2(x, y);
        }

        private Vector2? PixelOf(Vector2 point)
        {
            if (_screen is not ScreenShape screen)
            {
                return null;
            }

            (float pixelX, float pixelY) = ScreenView.Of(screen).PixelOfPoint(point.x, point.y);
            return new Vector2(pixelX, pixelY);
        }

        /// <summary>A piece in the player's hand.</summary>
        private sealed class Carry
        {
            public Carry(int piece, Vector2 grabbedAt, Vector2 finger)
            {
                Piece = piece;
                GrabbedAt = grabbedAt;
                Finger = finger;
            }

            public int Piece { get; }

            /// <summary>Where the piece's middle lay from the finger when the finger came down on it.</summary>
            public Vector2 GrabbedAt { get; }

            /// <summary>Where the finger is, in the layout's points.</summary>
            public Vector2 Finger { get; set; }

            /// <summary>How far the piece is lifted out of the tray: 0 as the finger comes down, 1 a moment later.</summary>
            public float Lift { get; set; }
        }
    }
}
