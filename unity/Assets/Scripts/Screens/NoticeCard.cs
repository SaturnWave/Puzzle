#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Client.Art;
using Puzzle.Client.Flow;
using Puzzle.Client.Gallery;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Screens
{
    /// <summary>What a notice card shows over its lines. A closed set (private constructor).</summary>
    public abstract class NoticeExhibit
    {
        private NoticeExhibit()
        {
        }

        /// <summary>One of the canvas' marks, drawn this large: the chest of coins, the pile.</summary>
        public sealed class Mark : NoticeExhibit
        {
            public Mark(GalleryArt drawing, float size)
            {
                Drawing = drawing;
                Size = size;
            }

            public GalleryArt Drawing { get; }

            public float Size { get; }
        }

        /// <summary>A picture of the canvas in the small gilt frame it hangs in on the hall's wall (the gift's), the frame this large.</summary>
        public sealed class Framed : NoticeExhibit
        {
            public Framed(GalleryArt picture, float size)
            {
                Picture = picture;
                Size = size;
            }

            public GalleryArt Picture { get; }

            public float Size { get; }
        }

        /// <summary>What the player received: each thing with the pill that says how many.</summary>
        public sealed class Goods : NoticeExhibit
        {
            public Goods(IReadOnlyList<GoodsItem> items)
            {
                Items = items ?? throw new ArgumentNullException(nameof(items));
            }

            public IReadOnlyList<GoodsItem> Items { get; }
        }
    }

    /// <summary>How a notice card is answered. A closed set.</summary>
    public enum NoticeAnswers
    {
        /// <summary>The green "Tamam" and the round button close it: it tells something and asks nothing.</summary>
        Done,

        /// <summary>
        /// Two answers side by side where "Tamam" stands, as the question of the quit card has them: the blue "Vazgeç"
        /// closes the card, the green "Satın al" opens the store's payment sheet (<see cref="MuseumPress.PayInTheStore"/>).
        /// The round button closes it too. The card that asks before paying for real money.
        /// </summary>
        BuyOrDecline,

        /// <summary>No answer and no round button: the card stands while something outside the game (the store's payment) runs, and goes when it ends.</summary>
        None,
    }

    /// <summary>
    /// A card that tells the player something over a museum page: what a request of the shop or of the gift frame
    /// came to, or where a purchase for real money stands. Its title is on its nameplate; under it what it is about (a
    /// mark, or the goods with their counts), one line and a smaller one, and its answers (<see cref="NoticeAnswers"/>):
    /// mostly the green "Tamam", which, with its round button and the way back, closes it.
    /// </summary>
    /// <remarks>
    /// The design canvas draws no such card. It is built in the language of the cards it does draw (LevelStartTR,
    /// SettingsTR) and of the lives card, and is the owner's to look at and to redraw. Its sizes are the canvas'
    /// points around the middle of its body, and it is drawn at the share of them the screen's card place gives it.
    /// </remarks>
    public sealed class NoticeCard : IMuseumCard
    {
        /// <summary>The body, and the nameplate as wide as the card's before a level, with one line.</summary>
        private static readonly Vector2 Body = new Vector2(300f, 300f);
        private const float NameplateWidth = 212f;
        private const float TitleSize = 22f;

        /// <summary>A mark's middle this far under the body's top edge; goods stand a little higher, their pills under them.</summary>
        private const float MarkDown = 100f;
        private const float GoodsDown = 88f;
        private const float MarkBox = 100f;

        /// <summary>The small gilt frame's picture is 28 wider and 56 higher than the frame, drawn over its picture, which is 10 smaller each way.</summary>
        private const float FramePictureMargin = 28f;
        private const float FramePictureHeightMargin = 56f;
        private const float FrameHole = 10f;

        /// <summary>A few goods on discs as large as a card's offer has them, more on smaller ones, at these steps.</summary>
        private const float LargeDisc = 84f;
        private const float LargeStep = 104f;
        private const float SmallDisc = 60f;
        private const float SmallStep = 68f;
        private const int MostLargeDiscs = 2;

        /// <summary>The two lines, as a block between the exhibit and the button.</summary>
        private const float FirstLineDown = 180f;
        private const float SecondLineDown = 204f;
        private const float FirstLineSize = 15f;
        private const float SecondLineSize = 13f;
        private const float LinesInFromSides = 22f;

        /// <summary>The green "Tamam", as the lives card has it.</summary>
        private const float ButtonWidth = 180f;
        private const float ButtonDown = 256f;
        private const float ButtonLabelSize = 28f;

        /// <summary>The two answers of a card that asks before paying: small buttons as wide as the room allows, with their middles this far apart, labelled as the quit card's.</summary>
        private const float AnswerWidth = 128f;
        private const float AnswersApart = 140f;
        private const float AnswerLabelSize = 16f;

        private readonly HangingCard<MuseumPress> _card;
        private readonly PushButton? _done;
        private readonly SmallButton? _buy;
        private readonly SmallButton? _decline;

        private NoticeCard(
            Transform parent, ArtCatalogue art, string title, NoticeExhibit exhibit, string firstLine, string? secondLine, int raisedBy, NoticeAnswers answers)
        {
            _card = new HangingCard<MuseumPress>(
                "Notice card", parent, art, Body, new CardNameplate(title, NameplateWidth, TitleSize), CardHanging.FromPinsInTheWall, raisedBy: raisedBy);
            if (answers != NoticeAnswers.None)
            {
                _card.AddCloseButton(new MuseumPress.CloseTheCard());
            }

            Title = title;
            Answers = answers;
            Show(exhibit, art, raisedBy);

            float room = Body.x - 2f * LinesInFromSides;
            TextMeshPro first = Lettering.Write("First line", _card.Content, art, LetteringStyle.Label, FirstLineSize, Lettering.Ink, DrawOrder.CardLettering + raisedBy);
            Lettering.SayWithin(first, firstLine, FirstLineSize, room);
            first.transform.localPosition = _card.At(secondLine == null ? (FirstLineDown + SecondLineDown) / 2f : FirstLineDown);
            if (secondLine != null)
            {
                TextMeshPro second = Lettering.Write("Second line", _card.Content, art, LetteringStyle.Soft, SecondLineSize, Lettering.SoftInk, DrawOrder.CardLettering + raisedBy);
                Lettering.SayWithin(second, secondLine, SecondLineSize, room);
                second.transform.localPosition = _card.At(SecondLineDown);
            }

            switch (answers)
            {
                case NoticeAnswers.Done:
                    _done = new PushButton(
                        "Done", _card.Content, art, ButtonLook.Green, MuseumWords.Done, ButtonWidth, ButtonLabelSize, DrawOrder.CardArt + raisedBy, DrawOrder.CardLettering + raisedBy);
                    _done.Place(_card.At(ButtonDown));
                    _card.Listen(_done, new MuseumPress.CloseTheCard());
                    break;
                case NoticeAnswers.BuyOrDecline:
                    Vector3 answersAt = _card.At(ButtonDown);
                    _decline = new SmallButton(
                        "Decline", _card.Content, art, SmallButtonColour.Blue, PurchaseWords.Decline, AnswerWidth, AnswerLabelSize, DrawOrder.CardArt + raisedBy, DrawOrder.CardLettering + raisedBy);
                    _decline.Place(answersAt + new Vector3(-AnswersApart / 2f, 0f, 0f));
                    _buy = new SmallButton(
                        "Buy", _card.Content, art, SmallButtonColour.Green, PurchaseWords.Buy, AnswerWidth, AnswerLabelSize, DrawOrder.CardArt + raisedBy, DrawOrder.CardLettering + raisedBy);
                    _buy.Place(answersAt + new Vector3(AnswersApart / 2f, 0f, 0f));
                    _card.Listen(_decline, new MuseumPress.CloseTheCard());
                    _card.Listen(_buy, new MuseumPress.PayInTheStore());
                    break;
                case NoticeAnswers.None:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(answers), answers, "A notice card is answered in a way it cannot draw.");
            }
        }

        /// <summary>The card itself: where it hangs, how far it has come down, and what a finger on it presses.</summary>
        public HangingCard<MuseumPress> Card => _card;

        /// <summary>What its nameplate says.</summary>
        public string Title { get; }

        /// <summary>How the card is answered.</summary>
        public NoticeAnswers Answers { get; }

        /// <summary>True when the player can close the card: by an answer, its round button or the way back.</summary>
        public bool CanBeClosed => Answers != NoticeAnswers.None;

        /// <summary>The middle of "Tamam", in the layout's points: where a finger presses it; nothing on a card answered otherwise.</summary>
        public Vector2? DoneButtonMiddle => _done != null ? _card.InTheLayout(_done.Middle) : (Vector2?)null;

        /// <summary>The middles of the green "Satın al" and the blue "Vazgeç" of a card that asks before paying; nothing on another card.</summary>
        public Vector2? BuyButtonMiddle => _buy != null ? _card.InTheLayout(_buy.Middle) : (Vector2?)null;

        public Vector2? DeclineButtonMiddle => _decline != null ? _card.InTheLayout(_decline.Middle) : (Vector2?)null;

        /// <summary>Builds the card, answered by "Tamam", out of sight above the screen. Place it before the first picture.</summary>
        /// <param name="secondLine">A smaller line under the first; nothing for a card of one line, which stands in the middle of the two places.</param>
        /// <param name="raisedBy">Added to the card's places in the drawing order: <see cref="DrawOrder.OverACard"/> for a card in front of another card.</param>
        public static NoticeCard Create(Transform parent, ArtCatalogue art, string title, NoticeExhibit exhibit, string firstLine, string? secondLine, int raisedBy = 0) =>
            new NoticeCard(parent, art, title, exhibit, firstLine, secondLine, raisedBy, NoticeAnswers.Done);

        /// <summary>Builds the card, answered as it says, out of sight above the screen. Place it before the first picture.</summary>
        public static NoticeCard Create(
            Transform parent, ArtCatalogue art, string title, NoticeExhibit exhibit, string firstLine, string? secondLine, int raisedBy, NoticeAnswers answers) =>
            new NoticeCard(parent, art, title, exhibit, firstLine, secondLine, raisedBy, answers);

        /// <summary>The card tells of nothing that changes while it hangs.</summary>
        public void Tell(WalletView wallet)
        {
        }

        public void Dispose() => _card.Dispose();

        private void Show(NoticeExhibit exhibit, ArtCatalogue art, int raisedBy)
        {
            switch (exhibit)
            {
                case NoticeExhibit.Mark mark:
                    Transform drawn = SceneObjects.AddSprite("Exhibit", _card.Content, art.Of(mark.Drawing), art.SpriteMaterial, DrawOrder.CardExhibit + raisedBy).transform;
                    drawn.localPosition = _card.At(MarkDown);
                    drawn.localScale = new Vector3(mark.Size / MarkBox, mark.Size / MarkBox, 1f);
                    break;
                case NoticeExhibit.Framed framed:
                    Vector3 middle = _card.At(MarkDown);
                    float pictureSize = framed.Size - FrameHole;
                    Transform picture = SceneObjects.AddSprite("Exhibit", _card.Content, art.Of(framed.Picture), art.SpriteMaterial, DrawOrder.CardArt + raisedBy).transform;
                    picture.localPosition = middle;
                    picture.localScale = new Vector3(pictureSize / MarkBox, pictureSize / MarkBox, 1f);
                    SpriteRenderer frame = SceneObjects.AddSprite("Frame", _card.Content, art.Of(GalleryArt.SmallFrame), art.SpriteMaterial, DrawOrder.CardExhibit + raisedBy);
                    frame.drawMode = SpriteDrawMode.Sliced; // the frame's middle stretches both ways; its corners keep their shape
                    frame.size = new Vector2(framed.Size + FramePictureMargin, framed.Size + FramePictureHeightMargin);
                    frame.transform.localPosition = middle;
                    break;
                case NoticeExhibit.Goods goods:
                    bool isLarge = goods.Items.Count <= MostLargeDiscs;
                    var row = new GoodsRow(
                        "Goods", _card.Content, art, goods.Items, isLarge ? LargeDisc : SmallDisc, isLarge ? LargeStep : SmallStep, pillScale: 1f,
                        DrawOrder.CardDisc + raisedBy, DrawOrder.CardBadge + raisedBy, DrawOrder.CardBadgeLettering + raisedBy);
                    row.Place(_card.At(GoodsDown));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(exhibit), exhibit, "A notice card cannot show that.");
            }
        }
    }
}
