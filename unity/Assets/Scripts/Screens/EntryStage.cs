#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Client.Art;
using Puzzle.Client.Flow;
using Puzzle.Client.Gallery;
using Puzzle.Client.Layout;
using Puzzle.Client.Painting;
using Puzzle.Client.Play;
using Puzzle.Client.Stage;
using Puzzle.Meta;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Screens
{
    /// <summary>
    /// The entry page the game opens on at every cold start, before the museum's hall (EntryTR, the kind "logo and tap
    /// to start" the owner chose, drawn anew for the app icon F): a cream ground like the icon's with the gallery's
    /// damask printed on it, warm light falling from above and glowing behind the palette, the icon's palette with
    /// four of the game's gems floating beside it, the title in gilt with the badge that says it is a working name,
    /// the line of what the game is about, and the gilt bar that fills while the game starts. Once the bar is full it
    /// gives way to "Başlamak için dokun", which pulses; a touch anywhere then darkens the page, and the hall comes out
    /// of that dark as it does after a level. The studio's name and the version stand at the foot.
    /// </summary>
    /// <remarks>
    /// The page does not start the game itself: whoever does tells it how much of the work is done
    /// (<see cref="Tell"/>), and the bar follows that work by <see cref="LoadingBar"/>'s rule, never faster than it is
    /// seen to fill; told that the work failed (<see cref="TellTheStartUpFailed"/>), the bar's line says that the game
    /// cannot open instead of its share. The page only says when the player has left it (<see cref="HasLeft"/>), in
    /// the dark the hall opens in. Like every stage it has a clock that only <see cref="Advance"/> moves.
    /// <para>
    /// The device's way back brings down the hall's own card that asks whether to close the game (<see cref="QuitCard"/>),
    /// while the bar fills too: the entry page has no way further back, as the hall has none. Its "Vazgeç" or the way
    /// back again takes it up; its "Çık" is passed on to the page's owner (<see cref="TakeTheWishToQuit"/>), which
    /// closes the game on Android.
    /// </para>
    /// </remarks>
    public sealed class EntryStage : IStage, ITouchable
    {
        /// <summary>
        /// Where the page is built: this far under the place every other stage is built at. The hall is opened behind
        /// the page while it shows, with its camera off, and the page's camera, which draws whatever lies in its view,
        /// must see nothing of it; at this distance a point is still drawn to well under a hundredth of a pixel.
        /// </summary>
        private static readonly Vector3 Apart = new Vector3(0f, -40000f, 0f);

        /// <summary>The icon's cream (art/icon/tools/palette.py, background("cream")), from the light's middle (0) to the edge of its ellipse (1).</summary>
        private static readonly ColourRamp Ground = new ColourRamp((0f, Tint.Of(0xFFF8E6)), (0.6f, Tint.Of(0xF6E4BC)), (1f, Tint.Of(0xD9B77A)));

        /// <summary>The ground's light: its middle this far down the screen, its ellipse these parts of the screen's width and height.</summary>
        private const float GroundMiddleDown = 0.34f;
        private const float GroundRadiusAcross = 1.05f;
        private const float GroundRadiusUpright = 0.74f;
        private const int GroundColumns = 12;
        private const int GroundRows = 24;

        /// <summary>The damask is the wall's (<see cref="Wall"/>), printed in the brown of the gilt's line instead of white.</summary>
        private static readonly Color DamaskInk = Tint.Of(0x7A4A12);
        private const float DamaskMotifWidth = 48f;
        private const float DamaskPictureSide = 288f;

        /// <summary>
        /// The two rays that fall from the screen's top edge to the title (EntryTR: 90 points wide at the top, 270 at
        /// their foot, and a narrower one in them), white on the cream, fading to nothing at their foot.
        /// </summary>
        private static readonly Color RayTop = Tint.Of(0xFFFFFF, 0.5f);
        private const float RayTopWidth = 90f;
        private const float RayFootWidth = 270f;
        private const float InnerRayTopWidth = 40f;
        private const float InnerRayFootWidth = 170f;
        private const float InnerRayStrength = 0.7f;

        /// <summary>The glow behind the palette, white from its middle (0) to nothing at its edge (1), 200 canvas points around.</summary>
        private static readonly ColourRamp Glow = new ColourRamp((0f, Tint.Of(0xFFFFFF, 0.62f)), (0.55f, Tint.Of(0xFFFDF4, 0.2f)), (1f, Tint.Of(0xFFFDF4, 0f)));
        private const float GlowRadius = 200f;
        private const int GlowFaces = 16;

        /// <summary>
        /// The soft shade the palette throws on the cream under it (EntryTR: drop-shadow 0 18px 22px), warm as the
        /// ground is: an ellipse a little smaller than the palette, this far under its middle, from its middle (0) to
        /// nothing at its edge (1).
        /// </summary>
        private static readonly ColourRamp PaletteShade = new ColourRamp((0f, Tint.Of(0x5A3A12, 0.24f)), (0.6f, Tint.Of(0x5A3A12, 0.13f)), (1f, Tint.Of(0x5A3A12, 0f)));
        private const float ShadeDown = 22f;
        private const float ShadeAcross = 0.5f;
        private const float ShadeUpright = 0.44f;

        /// <summary>
        /// The gems that float beside the palette, in the order of <see cref="EntryLayout.Gems"/>: none of the three the
        /// palette holds, each tilted as EntryTR tilts its four, the lower ones a little fainter as further away.
        /// </summary>
        private static readonly (PieceArt Art, float TiltDegrees, float Strength, float PhaseSeconds)[] GemLooks =
        {
            (PieceArt.GemPurple, -14f, 0.95f, 1.2f),
            (PieceArt.GemCyan, 12f, 0.93f, 2.6f),
            (PieceArt.GemPink, -8f, 0.85f, 0.4f),
            (PieceArt.GemGreen, 10f, 0.82f, 3.4f),
        };

        /// <summary>A gem rises this many canvas points and sinks back in this long (EntryTR: entry-float, 10 px in 4.6 s).</summary>
        private const float FloatHeight = 10f;
        private const float FloatSeconds = 4.6f;

        /// <summary>The colours of the lines on the cream: the design's ink, its softer brown, and the gilt's brown of the badge.</summary>
        private static readonly Color SubtitleInk = Tint.Of(0x5A3412);
        private static readonly Color FooterInk = Tint.Of(0x7A4A12, 0.75f);
        private static readonly Color BadgeInk = Tint.Of(0x8A5A2B);
        private static readonly Color BadgeLine = Tint.Of(0xB07A2A, 0.6f);
        private static readonly Color LoadingInk = Tint.Of(0x7A4A12);

        /// <summary>The title's gilt, from the top of its letters to their foot (EntryTR: #FFF6D2, #FFDB6E, #F0A92E), a little deeper so it holds on cream.</summary>
        private static readonly VertexGradient TitleGilt = new VertexGradient(
            Tint.Of(0xFFEFB8), Tint.Of(0xFFEFB8), Tint.Of(0xE8981F), Tint.Of(0xE8981F));

        /// <summary>The spacing between letters, as the canvas sets it, in hundredths of the type size.</summary>
        private const float TitleSpacing = 1f;
        private const float BadgeSpacing = 8f;
        private const float LoadingSpacing = 6f;

        /// <summary>The badge's line, a canvas point wide with a pixel's soft edge.</summary>
        private const float BadgeLineWidth = 1f;
        private const float SoftEdgePixels = 1f;
        private const int BadgeEndSteps = 10;

        /// <summary>The bar in its own canvas points: its picture is 12 points wider and 10 higher than its body; the green runs in the well 3 points in.</summary>
        private const float BarPictureMargin = 12f;
        private const float BarPictureHeight = 32f;
        private const float WellInset = 3f;
        private const float FillHeight = 16f;
        private const float FillPictureMargin = 8f;
        private const float FillPictureHeight = 24f;

        /// <summary>The light that runs over the green (EntryTR: entry-shine, a band 40 % of the green wide, across it in 2.4 s).</summary>
        private const float SheenSeconds = 2.4f;
        private const float SheenShare = 0.4f;
        private const float SheenStrength = 0.45f;
        private const int SheenColumns = 8;

        /// <summary>Once the bar is full it rests this long, then gives way to the line to touch over this long.</summary>
        private const float FullBarRests = 0.15f;
        private const float ReadyFades = 0.3f;

        /// <summary>The line to touch breathes (EntryTR: entry-breathe, 1.8 s): from this strength to full and from its size to 4 % more.</summary>
        private const float BreathSeconds = 1.8f;
        private const float BreathFaintest = 0.55f;
        private const float BreathGrowth = 0.04f;

        private readonly ArtCatalogue _art;
        private readonly string _gameVersion;
        private readonly GameObject _root;
        private readonly Timeline _timeline = new Timeline();
        private readonly CuesSounded _sounded = new CuesSounded();
        private readonly LoadingBar _bar = new LoadingBar();

        private readonly PaintedShape _ground;
        private readonly PaintedShape _damask;
        private readonly PaintedShape _light;
        private readonly PaintedShape _logo;
        private readonly PaintedShape _badge;
        private readonly SpriteRenderer[] _gems;
        private readonly TextMeshPro _title;
        private readonly TextMeshPro _subtitle;
        private readonly TextMeshPro _badgeWords;
        private readonly TextMeshPro _loadingLine;
        private readonly TextMeshPro _tapLine;
        private readonly TextMeshPro _studio;
        private readonly TextMeshPro _version;

        /// <summary>The bar with its green and the light on it, drawn in the canvas' points at the page's scale.</summary>
        private readonly Transform _barGroup;
        private readonly SpriteRenderer _barBody;
        private readonly SpriteRenderer _barFill;
        private readonly PaintedShape _sheen;
        private readonly Veil _veil;

        private EntryLayout? _layout;
        private ScreenShape? _screen;
        private float _clock;

        /// <summary>
        /// What the build sells for money, once the start-up has read the economy: the foot then says "test sürümü"
        /// after the version of a build that sells nothing. Nothing before: the foot says the version alone.
        /// </summary>
        private RealMoneySales? _sales;

        /// <summary>How much of the start-up work is done, as the page was last told.</summary>
        private float _workDone;

        /// <summary>The share the bar's line says now; nothing before it first said one, and after the page was laid out anew.</summary>
        private int? _percentSaid;

        /// <summary>True once the page was told that the start-up failed: the bar's line says the game cannot open, and the page is never ready.</summary>
        private bool _hasTheStartUpFailed;

        /// <summary>When the bar was first shown full; nothing before.</summary>
        private float? _fullSince;

        /// <summary>The card the way back brought down; nothing while none hangs.</summary>
        private QuitCard? _quitCard;

        /// <summary>Where the finger that came down on the page (not on a card) is still a tap; nothing while no such finger is down.</summary>
        private TapSlop? _tap;

        private bool _isLeaving;
        private bool _isAskedToQuit;

        private EntryStage(ArtCatalogue art, string gameVersion)
        {
            _art = art;
            _gameVersion = gameVersion;
            _root = new GameObject("Entry");
            Transform root = _root.transform;
            root.localPosition = Apart;
            Camera = StageCamera.Add(root);
            Camera.backgroundColor = Ground.At(1f);

            _ground = PaintedShape.Create("Ground", root, art.PaintMaterial, DrawOrder.EntryGround);
            _damask = PaintedShape.Create("Damask", root, art.PaintMaterial, DrawOrder.EntryDamask, art.Damask);
            _light = PaintedShape.Create("Light", root, art.PaintMaterial, DrawOrder.EntryLight);
            _logo = PaintedShape.Create("Logo", root, art.PaintMaterial, DrawOrder.EntryLogo, art.EntryLogo);
            _badge = PaintedShape.Create("Badge", root, art.PaintMaterial, DrawOrder.EntryBadge);

            _gems = new SpriteRenderer[GemLooks.Length];
            for (int gem = 0; gem < GemLooks.Length; gem++)
            {
                _gems[gem] = SceneObjects.AddSprite($"Gem {gem + 1}", root, art.Of(GemLooks[gem].Art), art.SpriteMaterial, DrawOrder.EntryGem);
                _gems[gem].color = new Color(1f, 1f, 1f, GemLooks[gem].Strength);
            }

            _title = Lettering.Write("Title", root, art, LetteringStyle.GiltTitle, EntryLayout.TitleSize, Color.white, DrawOrder.EntryLettering);
            _title.enableVertexGradient = true;
            _title.colorGradient = TitleGilt;
            _title.characterSpacing = TitleSpacing;
            _subtitle = Lettering.Write("Subtitle", root, art, LetteringStyle.Soft, EntryLayout.SubtitleSize, SubtitleInk, DrawOrder.EntryLettering);
            _badgeWords = Lettering.Write("Badge", root, art, LetteringStyle.Label, EntryLayout.BadgeSize, BadgeInk, DrawOrder.EntryLettering);
            _badgeWords.characterSpacing = BadgeSpacing;
            _loadingLine = Lettering.Write("Loading", root, art, LetteringStyle.Label, EntryLayout.LoadingLineSize, LoadingInk, DrawOrder.EntryLettering);
            _loadingLine.characterSpacing = LoadingSpacing;
            _tapLine = Lettering.Write("Tap to start", root, art, LetteringStyle.OnCream, EntryLayout.TapSize, Color.white, DrawOrder.EntryLettering);
            _tapLine.alpha = 0f;
            _studio = Lettering.Write("Studio", root, art, LetteringStyle.Soft, EntryLayout.FooterSize, FooterInk, DrawOrder.EntryLettering);
            _version = Lettering.Write("Version", root, art, LetteringStyle.Soft, EntryLayout.FooterSize, FooterInk, DrawOrder.EntryLettering);

            _barGroup = new GameObject("Bar").transform;
            _barGroup.SetParent(root, worldPositionStays: false);
            _barBody = SceneObjects.AddSprite("Body", _barGroup, art.Of(GalleryArt.EntryBar), art.SpriteMaterial, DrawOrder.EntryBar);
            _barBody.drawMode = SpriteDrawMode.Sliced;
            _barBody.size = new Vector2(EntryLayout.BarWidth + BarPictureMargin, BarPictureHeight);
            _barFill = SceneObjects.AddSprite("Fill", _barGroup, art.Of(GalleryArt.EntryBarFill), art.SpriteMaterial, DrawOrder.EntryBarFill);
            _barFill.drawMode = SpriteDrawMode.Sliced;
            _sheen = PaintedShape.Create("Sheen", _barGroup, art.PaintMaterial, DrawOrder.EntryBarSheen);
            _veil = Veil.Create(root, art);
        }

        public Camera Camera { get; }

        /// <summary>
        /// True when nothing moves but what only floats and breathes: the bar is full and its line has given way, or the
        /// bar has come to where a failed start-up stopped; and no card or dark is on its way.
        /// </summary>
        public bool IsSettled => _timeline.IsDone && (ReadyShare >= 1f || (_hasTheStartUpFailed && _bar.Shown >= Mathf.Clamp01(_workDone)));

        /// <summary>True once the bar is full and the line to touch shows: a touch anywhere then leaves the page.</summary>
        public bool IsReady => _fullSince is float full && _clock - full >= FullBarRests;

        /// <summary>True once the player has touched the ready page and it has gone dark: whoever runs the game opens the hall out of this dark.</summary>
        public bool HasLeft { get; private set; }

        /// <summary>How full the bar is shown, from 0 to 1.</summary>
        public float BarShown => _bar.Shown;

        /// <summary>What the line under the bar says now: the share started, or that the game cannot open. For the Editor's checks.</summary>
        public string LineUnderTheBar => _loadingLine.text;

        /// <summary>How strongly the line to touch is drawn now, from 0 (not yet) to 1 (at the height of its breath): for the Editor's pictures.</summary>
        public float TapLineStrength => _tapLine.alpha;

        /// <summary>What the card the way back brought down asks; nothing while none hangs.</summary>
        public string? QuitQuestionShown => _quitCard?.Question;

        /// <summary>The pixels of the card's "Vazgeç" and "Çık", where a finger presses them; nothing while no card hangs or it is on its way.</summary>
        public Vector2? StayInTheGamePixel => _quitCard is QuitCard card && _timeline.IsDone ? PixelOf(card.StayButtonMiddle) : (Vector2?)null;

        public Vector2? QuitTheGamePixel => _quitCard is QuitCard card && _timeline.IsDone ? PixelOf(card.QuitButtonMiddle) : (Vector2?)null;

        /// <summary>The pixel in the middle of the screen: a touch there is a touch anywhere on the page.</summary>
        public Vector2 MiddlePixel => _screen is ScreenShape screen
            ? new Vector2(screen.PixelWidth / 2f, screen.PixelHeight / 2f)
            : throw new InvalidOperationException("The entry page has no screen yet; call Fit first.");

        /// <summary>How far the line to touch has come in for the bar, from 0 (the bar shows) to 1 (the line does).</summary>
        private float ReadyShare => _fullSince is float full ? Mathf.Clamp01((_clock - full - FullBarRests) / ReadyFades) : 0f;

        /// <summary>True while a touch may press something: no card and no dark is on its way, and the page is not being left.</summary>
        private bool ListensToThePlayer => _timeline.IsDone && !_isLeaving;

        /// <summary>Builds the page with an empty bar, laid out for a screen. <paramref name="gameVersion"/> is what its foot says, "1.0".</summary>
        public static EntryStage Open(ArtCatalogue art, string gameVersion, ScreenShape screen)
        {
            var stage = new EntryStage(art, gameVersion);
            stage.Fit(screen);
            stage.ShowTheBar();
            return stage;
        }

        /// <summary>Tells the page how much of the start-up is done, from 0 to 1; the bar follows it as the clock moves.</summary>
        public void Tell(float workDone) => _workDone = workDone;

        /// <summary>
        /// Tells the page what the build sells for money, once the start-up has read the economy: the foot's version
        /// line says "Sürüm 1.0 · test sürümü" for a build that sells nothing, and "Sürüm 1.0", as before, for one that
        /// sells through the platform's store.
        /// </summary>
        public void TellTheBuild(RealMoneySales sales)
        {
            _sales = sales;
            if (_layout is EntryLayout layout)
            {
                SayTheVersion(layout);
            }
        }

        /// <summary>
        /// Tells the page that the start-up failed and goes no further: the bar stays where it was and its line says,
        /// in its place, that the game cannot open and is to be started again. The page never becomes ready, so a touch
        /// leads nowhere; the way back still brings down the card that closes the game.
        /// </summary>
        public void TellTheStartUpFailed()
        {
            _hasTheStartUpFailed = true;
            if (_layout is EntryLayout layout)
            {
                SayTheFailure(layout);
            }
        }

        /// <summary>True once since this was asked last when the player answered the card's question with "Çık": the game is to close.</summary>
        public bool TakeTheWishToQuit()
        {
            bool isWished = _isAskedToQuit;
            _isAskedToQuit = false;
            return isWished;
        }

        public void Fit(ScreenShape screen)
        {
            _screen = screen;
            PointerCancelled(); // a finger on the screen before has no place on this one, nor on the card's buttons
            EntryLayout layout = EntryLayout.Of(screen);
            _layout = layout;
            ScreenView view = layout.View;
            StageCamera.Show(Camera, view);
            var whole = new Rect(view.Left, view.Bottom, view.Width, view.Height);
            _ground.Show(BuildGround(whole));
            _damask.Show(BuildDamask(whole));
            _light.Show(BuildLight(layout));
            _logo.Show(BuildLogo(layout.Picture));
            float scale = layout.Scale;

            Lettering.SayWithin(_title, EntryWords.Title, EntryLayout.TitleSize * scale, layout.LineRoom);
            _title.transform.localPosition = new Vector3(0f, layout.TitleMiddle, 0f);
            Lettering.SayWithin(_subtitle, EntryWords.Subtitle, EntryLayout.SubtitleSize * scale, layout.LineRoom);
            _subtitle.transform.localPosition = new Vector3(0f, layout.SubtitleMiddle, 0f);
            Lettering.SayWithin(_badgeWords, EntryWords.WorkingTitle, EntryLayout.BadgeSize * scale, layout.LineRoom - 2f * EntryLayout.BadgeEnds * scale);
            _badgeWords.transform.localPosition = new Vector3(0f, layout.BadgeMiddle, 0f);
            _badge.Show(BuildBadge(Lettering.WidthOf(_badgeWords, EntryWords.WorkingTitle), layout, view));
            Lettering.SayWithin(_tapLine, EntryWords.TapToStart, EntryLayout.TapSize * scale, layout.LineRoom);
            _tapLine.transform.localPosition = new Vector3(0f, layout.TapMiddle, 0f);
            _loadingLine.fontSize = EntryLayout.LoadingLineSize * scale;
            _loadingLine.transform.localPosition = new Vector3(0f, layout.LoadingLineMiddle, 0f);
            _percentSaid = null; // said again at the new size
            if (_hasTheStartUpFailed)
            {
                SayTheFailure(layout);
            }

            _studio.fontSize = EntryLayout.FooterSize * scale;
            Lettering.SayFromLeft(_studio, EntryWords.Studio, layout.FooterLeft, layout.FooterMiddle);
            _version.fontSize = EntryLayout.FooterSize * scale;
            SayTheVersion(layout);

            _barGroup.localPosition = new Vector3(0f, layout.BarMiddle, 0f);
            _barGroup.localScale = new Vector3(scale, scale, 1f);
            _veil.Fit(view);
            if (_quitCard is QuitCard card)
            {
                PlaceTheCard(card, screen);
            }

            ShowTheBar();
            FloatTheGems();
        }

        public void Advance(float deltaSeconds)
        {
            _clock += deltaSeconds;
            _timeline.Advance(deltaSeconds);
            _bar.Advance(deltaSeconds, _workDone);
            if (_bar.IsFull && _fullSince == null)
            {
                _fullSince = _clock;
            }

            ShowTheBar();
            FloatTheGems();
        }

        public void PointerDown(Vector2 pixel)
        {
            if (!ListensToThePlayer || !(_screen is ScreenShape screen))
            {
                return;
            }

            (float x, float y) = ScreenView.Of(screen).PointAt(pixel.x, pixel.y);
            if (_quitCard is QuitCard card)
            {
                card.Card.FingerDown(new Vector2(x, y));
            }
            else
            {
                _tap = TapSlop.Down(x, y);
            }
        }

        public void PointerMoved(Vector2 pixel)
        {
            if (!ListensToThePlayer || !(_screen is ScreenShape screen))
            {
                return;
            }

            (float x, float y) = ScreenView.Of(screen).PointAt(pixel.x, pixel.y);
            if (_quitCard is QuitCard card)
            {
                card.Card.FingerMovedTo(new Vector2(x, y));
            }
            else if (_tap is TapSlop tap)
            {
                _tap = tap.MovedTo(x, y);
            }
        }

        public void PointerUp()
        {
            bool wasATap = _tap is TapSlop tap && tap.IsATap;
            _tap = null;
            if (!ListensToThePlayer)
            {
                return;
            }

            if (_quitCard is QuitCard card)
            {
                if (card.Card.FingerUp(out MuseumPress press))
                {
                    _sounded.Add(SoundCue.Press);
                    Answer(press);
                }
            }
            else if (wasATap && IsReady)
            {
                Leave();
            }
        }

        public void PointerCancelled()
        {
            _tap = null;
            _quitCard?.Card.FingerGone();
        }

        /// <summary>The page follows one finger and listens to no other.</summary>
        public void AnotherFingerDown()
        {
        }

        /// <summary>
        /// The device's way back: with no card hanging, the card comes down that asks whether to close the game, as in
        /// the hall; with the card hanging, it goes up. While a card or the dark is on its way, nothing.
        /// </summary>
        public void GoBack()
        {
            if (!ListensToThePlayer)
            {
                return;
            }

            if (_quitCard != null)
            {
                TakeTheCardUp();
            }
            else
            {
                HangTheQuitCard();
            }
        }

        public IReadOnlyList<SoundCue> TakeWhatSounded() => _sounded.TakeAll();

        public void Dispose()
        {
            _quitCard?.Dispose();
            _veil.Dispose();
            _sheen.Dispose();
            _badge.Dispose();
            _logo.Dispose();
            _light.Dispose();
            _damask.Dispose();
            _ground.Dispose();
            SceneObjects.Destroy(_root);
        }

        /// <summary>The ready page was touched: it goes dark as a room does under a card, and is left once it is.</summary>
        private void Leave()
        {
            _isLeaving = true;
            _sounded.Add(SoundCue.Press);
            _timeline.Add(0f, Tempo.RoomDarkens, progress => _veil.Darken(Easing.Out(progress)), () => HasLeft = true);
        }

        private void HangTheQuitCard()
        {
            _tap = null;
            QuitCard card = QuitCard.Create(_root.transform, _art);
            _quitCard = card;
            if (_screen is ScreenShape screen)
            {
                PlaceTheCard(card, screen);
            }

            _timeline.Add(0f, Tempo.RoomDarkens, progress => _veil.Darken(Easing.Out(progress)));
            _timeline.At(Tempo.CardAfterDark, () => _sounded.Add(SoundCue.CardDown));
            _timeline.Add(Tempo.CardAfterDark, Tempo.CardComesDown, card.Card.ComeDown);
        }

        private void TakeTheCardUp()
        {
            QuitCard card = _quitCard ?? throw new InvalidOperationException("No card hangs to be taken up.");
            card.Card.FingerGone();
            _timeline.Add(0f, Tempo.CardGoesUp, card.Card.GoUp);
            _timeline.Add(
                Tempo.CardGoesUp / 2f,
                Tempo.RoomLightens,
                progress => _veil.Darken(1f - Easing.Out(progress)),
                () =>
                {
                    card.Dispose();
                    _quitCard = null;
                });
        }

        /// <summary>What a press on the card asks: its "Vazgeç" takes it up; its "Çık" is passed on, and the card stays as it hangs.</summary>
        private void Answer(MuseumPress press)
        {
            switch (press)
            {
                case MuseumPress.CloseTheCard _:
                    TakeTheCardUp();
                    break;
                case MuseumPress.QuitTheGame _:
                    _isAskedToQuit = true;
                    break;
                default:
                    throw new InvalidOperationException($"The entry page's card asks for nothing like \"{press}\".");
            }
        }

        private static void PlaceTheCard(QuitCard card, ScreenShape screen)
        {
            PageLayout layout = PageLayout.Of(screen);
            card.Card.Place(layout.PlaceOfACard(card.Card.Width, card.Card.Height), layout.View);
        }

        /// <summary>The bar as full as it is shown, its line saying so; once full, the line to touch coming in for them.</summary>
        private void ShowTheBar()
        {
            const float wellWidth = EntryLayout.BarWidth - 2f * WellInset;
            float filled = _bar.Shown * wellWidth;

            // The green is never drawn narrower than it is high, which would break its round ends: it fades in up to that width instead.
            float drawn = Mathf.Max(FillHeight, filled);
            float ready = ReadyShare;
            float barStrength = 1f - ready;
            _barFill.size = new Vector2(drawn + FillPictureMargin, FillPictureHeight);
            _barFill.transform.localPosition = new Vector3(-wellWidth / 2f + drawn / 2f, 0f, 0f);
            _barFill.color = new Color(1f, 1f, 1f, barStrength * Mathf.Clamp01(filled / FillHeight));
            _barBody.color = new Color(1f, 1f, 1f, barStrength);
            _sheen.Show(BuildSheen(-wellWidth / 2f, -wellWidth / 2f + drawn));
            _sheen.Fade(barStrength * Mathf.Clamp01(filled / FillHeight));

            int percent = _bar.Percent;
            if (!_hasTheStartUpFailed && _percentSaid != percent)
            {
                Lettering.Say(_loadingLine, EntryWords.Loading(percent));
                _percentSaid = percent;
            }

            _loadingLine.alpha = barStrength;

            // The line to touch breathes from the moment it comes in.
            float breath = Easing.Arch(Mathf.Repeat(_clock, BreathSeconds) / BreathSeconds);
            _tapLine.alpha = ready * Mathf.Lerp(BreathFaintest, 1f, breath);
            float grown = 1f + BreathGrowth * breath;
            _tapLine.transform.localScale = new Vector3(grown, grown, 1f);
        }

        /// <summary>The bar's line says that the game cannot open, at the line's size or smaller where a language's words are longer.</summary>
        private void SayTheVersion(EntryLayout layout)
        {
            string version = _sales is RealMoneySales sales ? EntryWords.Version(_gameVersion, sales) : EntryWords.VersionAlone(_gameVersion);
            Lettering.SayUpToRight(_version, version, layout.FooterRight, layout.FooterMiddle);
        }

        private void SayTheFailure(EntryLayout layout) =>
            Lettering.SayWithin(_loadingLine, EntryWords.CouldNotStart, EntryLayout.LoadingLineSize * layout.Scale, layout.LineRoom);

        /// <summary>Each gem at its place, risen as far as its own beat has it, tilted as it is.</summary>
        private void FloatTheGems()
        {
            if (!(_layout is EntryLayout layout))
            {
                return;
            }

            for (int gem = 0; gem < _gems.Length; gem++)
            {
                Box place = layout.Gems[gem];
                SpriteRenderer sprite = _gems[gem];
                // Up and down again in one beat, slow at both ends as the canvas' ease-in-out has it.
                float beat = Mathf.Repeat(_clock + GemLooks[gem].PhaseSeconds, FloatSeconds) / FloatSeconds;
                float rise = FloatHeight * layout.Scale * (1f - Mathf.Cos(2f * Mathf.PI * beat)) / 2f;
                sprite.transform.localPosition = new Vector3(place.MiddleX, place.MiddleY + rise, 0f);
                sprite.transform.localRotation = Quaternion.Euler(0f, 0f, -GemLooks[gem].TiltDegrees);
                float side = sprite.sprite.bounds.size.x;
                float scale = place.Width / side;
                sprite.transform.localScale = new Vector3(scale, scale, 1f);
            }
        }

        private Vector2 PixelOf(Vector2 point)
        {
            ScreenShape screen = _screen ?? throw new InvalidOperationException("The entry page has no screen yet; call Fit first.");
            (float x, float y) = ScreenView.Of(screen).PixelOfPoint(point.x, point.y);
            return new Vector2(x, y);
        }

        /// <summary>The cream of the icon's ground over the whole screen, lightest a third of the way down.</summary>
        private static Mesh BuildGround(Rect screen)
        {
            var middle = new Vector2(screen.center.x, screen.yMax - GroundMiddleDown * screen.height);
            var radii = new Vector2(GroundRadiusAcross * screen.width, GroundRadiusUpright * screen.height);
            var sketch = new MeshSketch();
            sketch.AddNet(screen, GroundColumns, GroundRows, point => Ground.At(FromMiddle(point, middle, radii)));
            return sketch.ToMesh("Entry ground");
        }

        /// <summary>One face with the damask repeated over it, laid as the wall lays it: a motif stands in the middle of the screen's width, and one begins at its top.</summary>
        private static Mesh BuildDamask(Rect screen)
        {
            float left = (screen.xMin + DamaskMotifWidth / 2f) / DamaskPictureSide;
            var inPicture = new Rect(left, 1f - screen.height / DamaskPictureSide, screen.width / DamaskPictureSide, screen.height / DamaskPictureSide);
            var sketch = new MeshSketch();
            sketch.AddRectangle(screen, DamaskInk, DamaskInk, inPicture);
            return sketch.ToMesh("Entry damask");
        }

        /// <summary>The two rays from the screen's top edge down to the title, the glow behind the palette and the shade it throws.</summary>
        private static Mesh BuildLight(EntryLayout layout)
        {
            var sketch = new MeshSketch();
            float top = layout.View.Top;
            float foot = layout.TitleMiddle;
            float scale = layout.Scale;
            AddRay(sketch, top, foot, RayTopWidth * scale, RayFootWidth * scale, RayTop);
            AddRay(sketch, top, foot, InnerRayTopWidth * scale, InnerRayFootWidth * scale, new Color(RayTop.r, RayTop.g, RayTop.b, RayTop.a * InnerRayStrength));

            float radius = GlowRadius * scale;
            var middle = new Vector2(layout.Palette.MiddleX, layout.Palette.MiddleY);
            var area = new Rect(middle.x - radius, middle.y - radius, 2f * radius, 2f * radius);
            sketch.AddNet(area, GlowFaces, GlowFaces, point => Glow.At(Vector2.Distance(point, middle) / radius));

            // The palette's shade lies over the glow and under the palette.
            Box palette = layout.Palette;
            var shadeMiddle = new Vector2(palette.MiddleX, palette.MiddleY - ShadeDown * scale);
            var shadeRadii = new Vector2(ShadeAcross * palette.Width, ShadeUpright * palette.Height);
            var shadeArea = new Rect(shadeMiddle - shadeRadii, 2f * shadeRadii);
            sketch.AddNet(shadeArea, GlowFaces, GlowFaces, point => PaletteShade.At(FromMiddle(point, shadeMiddle, shadeRadii)));
            return sketch.ToMesh("Entry light");
        }

        private static void AddRay(MeshSketch sketch, float top, float foot, float topWidth, float footWidth, Color colour)
        {
            var gone = new Color(colour.r, colour.g, colour.b, 0f);
            int topLeft = sketch.AddCorner(new Vector2(-topWidth / 2f, top), colour);
            int topRight = sketch.AddCorner(new Vector2(topWidth / 2f, top), colour);
            int footRight = sketch.AddCorner(new Vector2(footWidth / 2f, foot), gone);
            int footLeft = sketch.AddCorner(new Vector2(-footWidth / 2f, foot), gone);
            sketch.AddQuad(footLeft, topLeft, topRight, footRight);
        }

        private static Mesh BuildLogo(Box picture)
        {
            var sketch = new MeshSketch();
            sketch.AddRectangle(new Rect(picture.Left, picture.Bottom, picture.Width, picture.Height), Color.white, Color.white, new Rect(0f, 0f, 1f, 1f));
            return sketch.ToMesh("Entry logo");
        }

        /// <summary>The thin gilt line of the badge around its words: a pill as high as the badge, its ends round.</summary>
        private static Mesh BuildBadge(float wordsWidth, EntryLayout layout, ScreenView view)
        {
            float scale = layout.Scale;
            float radius = EntryLayout.BadgeHeight * scale / 2f;
            float straight = wordsWidth + 2f * EntryLayout.BadgeEnds * scale - 2f * radius;
            float middle = layout.BadgeMiddle;
            var points = new List<Vector2>();
            for (int step = 0; step <= BadgeEndSteps; step++)
            {
                float angle = Mathf.PI / 2f - Mathf.PI * step / BadgeEndSteps;
                points.Add(new Vector2(straight / 2f + radius * Mathf.Cos(angle), middle + radius * Mathf.Sin(angle)));
            }

            for (int step = 0; step <= BadgeEndSteps; step++)
            {
                float angle = -Mathf.PI / 2f - Mathf.PI * step / BadgeEndSteps;
                points.Add(new Vector2(-straight / 2f + radius * Mathf.Cos(angle), middle + radius * Mathf.Sin(angle)));
            }

            var sketch = new MeshSketch();
            Stroke.Add(sketch, points, isClosed: true, BadgeLineWidth * scale, SoftEdgePixels / view.PixelsPerPoint, BadgeLine);
            return sketch.ToMesh("Entry badge");
        }

        /// <summary>
        /// The light that runs over the green, in the bar's own points: a band of white that fades in and out across
        /// its width, crossing the green from before its left end to past its right end, drawn only where the green's
        /// sides are straight so it never shows beside its round ends.
        /// </summary>
        private Mesh BuildSheen(float greenLeft, float greenRight)
        {
            var sketch = new MeshSketch();
            float straightLeft = greenLeft + FillHeight / 2f;
            float straightRight = greenRight - FillHeight / 2f;
            float bandWidth = SheenShare * (greenRight - greenLeft);
            float travelled = Mathf.Repeat(_clock, SheenSeconds) / SheenSeconds;

            // EntryTR: translateX from -120 % to 220 % of the band's own width, eased in and out.
            float bandLeft = greenLeft + bandWidth * Mathf.Lerp(-1.2f, 2.2f, Easing.InOut(travelled));
            float left = Mathf.Max(straightLeft, bandLeft);
            float right = Mathf.Min(straightRight, bandLeft + bandWidth);
            if (right > left && bandWidth > 0f)
            {
                var area = new Rect(left, -FillHeight / 2f, right - left, FillHeight);
                float bandMiddle = bandLeft + bandWidth / 2f;
                sketch.AddNet(area, SheenColumns, 1, point => new Color(1f, 1f, 1f, SheenStrength * (1f - Mathf.Clamp01(Mathf.Abs(point.x - bandMiddle) / (bandWidth / 2f)))));
            }

            return sketch.ToMesh("Entry sheen");
        }

        /// <summary>How far a point is from the middle of an ellipse: 0 in the middle, 1 on its edge.</summary>
        private static float FromMiddle(Vector2 point, Vector2 middle, Vector2 radii) =>
            new Vector2((point.x - middle.x) / radii.x, (point.y - middle.y) / radii.y).magnitude;
    }
}
