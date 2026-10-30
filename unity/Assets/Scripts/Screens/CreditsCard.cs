#nullable enable
using System.Collections.Generic;
using System.Linq;
using Puzzle.Client.Art;
using Puzzle.Client.Flow;
using Puzzle.Client.Gallery;
using Puzzle.Client.Layout;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Screens
{
    /// <summary>
    /// The card the settings card's row "Künye" opens, in front of it: who made the pictures, typefaces and sounds the
    /// game uses, and under which licences (<see cref="CreditWords"/>). A small line says that the licence lines stay in
    /// English; then the parts, each a heading in the language the game speaks over its lines in English: the painter
    /// (his head, his body, his textures) and his body's MIT notice whole, the paintings, the typefaces, the sounds. The
    /// text is set on as many pages as it needs, turned by two arrows with "2 / 4" between them, and the green "Tamam"
    /// under them closes the card, as its round button and the way back do.
    /// </summary>
    /// <remarks>
    /// The design canvas draws no such card. It is built as the privacy card is, in the language of the settings card
    /// it hangs in front of: as wide, its words in the canvas' interface type, its arrows the chevrons of the settings
    /// card's rows. A heading never ends a page: it stands with the first line under it, and a part's other lines may
    /// go on to the next page, a line never broken over two. Every page is as high as the highest, so the arrows and
    /// "Tamam" stay where they are as the pages turn. Sizes are the canvas' points around the middle of its body.
    /// </remarks>
    public sealed class CreditsCard : IMuseumCard
    {
        private const float NameplateWidth = 236f;
        private const float TitleSize = 22f;

        /// <summary>The text begins this far under the body's top edge; its blocks stand this far apart, a heading this far over its first line.</summary>
        private const float TextTop = 40f;
        private const float BlockGap = 7f;
        private const float HeadingGap = 2f;

        /// <summary>The first line and the licences' lines in the soft interface type, the headings in the labels' type.</summary>
        private const float FirstLineSize = 11.5f;
        private const float HeadingSize = 14f;
        private const float LineSize = 11f;

        /// <summary>The band of the arrows under the text: its middle this far under the text, the arrows this far apart, the count between them at this size.</summary>
        private const float PagerBand = 38f;
        private const float PagerUnderText = 21f;
        private const float ArrowsApart = 120f;
        private const float PageCountSize = 13f;

        /// <summary>"Tamam" as the support card has it: its middle this far under the text's band, the card's foot this far under that.</summary>
        private const float ButtonUnderTheBand = 44f;
        private const float BodyUnderTheButton = 54f;
        private const float ButtonWidth = 180f;
        private const float ButtonLabelSize = 28f;

        /// <summary>The most the text may take on a page: the card is then as high as a card in front may be (<see cref="SettingsLayout.CreditsBodyHeight"/>).</summary>
        private const float MostTextHeight = SettingsLayout.CreditsBodyHeight - TextTop - PagerBand - ButtonUnderTheBand - BodyUnderTheButton;

        private static float TextWidth => SettingsLayout.RowsRight - SettingsLayout.RowsLeft;

        private readonly HangingCard<MuseumPress> _card;
        private readonly IReadOnlyList<Block> _blocks;
        private readonly IReadOnlyList<TextPage> _pages;
        private readonly PushButton _done;

        /// <summary>The arrows and the count between them; nothing for a text that fits one page.</summary>
        private readonly (PageArrow Back, PageArrow On, TextMeshPro Count)? _pager;
        private int _page;

        private CreditsCard(Transform parent, ArtCatalogue art, int raisedBy)
        {
            // The text is set first, beside the card, to know how high its pages are and how many it takes.
            _blocks = WriteTheText(parent, art, raisedBy);
            IReadOnlyList<float> heights = _blocks.Select(block => block.Height).ToArray();
            _pages = TextPages.Of(heights, BlockGap, MostTextHeight);
            float textHeight = _pages.Max(page => TextPages.HeightOf(page, heights, BlockGap));
            bool isPaged = _pages.Count > 1;
            float bandEnd = TextTop + textHeight + (isPaged ? PagerBand : 0f);
            _card = new HangingCard<MuseumPress>(
                "Credits card", parent, art, new Vector2(SettingsLayout.BodyWidth, bandEnd + ButtonUnderTheBand + BodyUnderTheButton),
                new CardNameplate(CreditWords.Title, NameplateWidth, TitleSize), CardHanging.FromPinsInTheWall, raisedBy: raisedBy);
            _card.AddCloseButton(new MuseumPress.CloseTheCard());
            foreach (Block block in _blocks)
            {
                block.MoveOnto(_card.Content);
            }

            if (isPaged)
            {
                float pagerHeight = _card.At(TextTop + textHeight + PagerUnderText).y;
                TextMeshPro count = Lettering.Write("Page count", _card.Content, art, LetteringStyle.Soft, PageCountSize, Lettering.SoftInk, DrawOrder.CardLettering + raisedBy);
                count.transform.localPosition = new Vector3(0f, pagerHeight, 0f);
                _pager = (
                    new PageArrow("Page back", _card.Content, art, new Vector2(-ArrowsApart / 2f, pagerHeight), pointsBack: true, raisedBy),
                    new PageArrow("Page on", _card.Content, art, new Vector2(ArrowsApart / 2f, pagerHeight), pointsBack: false, raisedBy),
                    count);
            }

            _done = new PushButton("Done", _card.Content, art, ButtonLook.Green, MuseumWords.Done, ButtonWidth, ButtonLabelSize, DrawOrder.CardArt + raisedBy, DrawOrder.CardLettering + raisedBy);
            _done.Place(_card.At(bandEnd + ButtonUnderTheBand));
            ShowThePage(0);
        }

        public HangingCard<MuseumPress> Card => _card;

        /// <summary>Which page of the text is shown, counted from 1, and of how many.</summary>
        public (int Page, int Pages) PageShown => (_page + 1, _pages.Count);

        /// <summary>The middle of "Tamam", in the layout's points: where a finger presses it.</summary>
        public Vector2 DoneButtonMiddle => _card.InTheLayout(_done.Middle);

        /// <summary>The middles of the arrows to the next page and to the one before, in the layout's points; nothing where there is no such page.</summary>
        public Vector2? NextPageMiddle => _pager is (_, PageArrow on, _) && _page < _pages.Count - 1 ? _card.InTheLayout(on.Middle) : (Vector2?)null;

        public Vector2? PreviousPageMiddle => _pager is (PageArrow back, _, _) && _page > 0 ? _card.InTheLayout(back.Middle) : (Vector2?)null;

        /// <summary>Builds the card out of sight above the screen. Place it before the first picture.</summary>
        /// <param name="raisedBy">Added to the card's places in the drawing order: <see cref="DrawOrder.OverACard"/> in front of the settings card.</param>
        public static CreditsCard Create(Transform parent, ArtCatalogue art, int raisedBy) => new CreditsCard(parent, art, raisedBy);

        /// <summary>An arrow was pressed: the text turns to the next page, or to the one before; past its first or last page it stays.</summary>
        public void TurnThePage(bool isForward)
        {
            int turnedTo = Mathf.Clamp(_page + (isForward ? 1 : -1), 0, _pages.Count - 1);
            if (turnedTo != _page)
            {
                ShowThePage(turnedTo);
            }
        }

        /// <summary>The card tells of nothing of the wallet.</summary>
        public void Tell(WalletView wallet)
        {
        }

        public void Dispose() => _card.Dispose();

        /// <summary>The blocks of a page, laid one under the other from the text's top; the others are not shown. The count between the arrows follows, and the controls in sight are listened to.</summary>
        private void ShowThePage(int page)
        {
            _page = page;
            TextPage shown = _pages[page];
            float down = TextTop;
            for (int index = 0; index < _blocks.Count; index++)
            {
                bool isOnThePage = shown.Shows(index);
                _blocks[index].Show(isOnThePage);
                if (isOnThePage)
                {
                    down += _blocks[index].LayFrom(_card.At(down).y) + BlockGap;
                }
            }

            _card.ForgetTheControls();
            _card.Listen(_done, new MuseumPress.CloseTheCard());
            if (_pager is (PageArrow back, PageArrow on, TextMeshPro count))
            {
                Lettering.Say(count, PrivacyWords.PageOf(page + 1, _pages.Count));
                back.Dim(page == 0);
                on.Dim(page == _pages.Count - 1);
                if (page > 0)
                {
                    _card.Listen(back, new MuseumPress.TurnTheCreditsPage(isForward: false));
                }

                if (page < _pages.Count - 1)
                {
                    _card.Listen(on, new MuseumPress.TurnTheCreditsPage(isForward: true));
                }
            }
        }

        /// <summary>
        /// The text as blocks, set beside the card: the small first line; then each part's first line with its
        /// heading over it, and each of its other lines alone, so a heading never ends a page.
        /// </summary>
        private static IReadOnlyList<Block> WriteTheText(Transform beside, ArtCatalogue art, int raisedBy)
        {
            var blocks = new List<Block>
            {
                new Block(beside, art, raisedBy, heading: null, CreditWords.InEnglish, FirstLineSize, Lettering.SoftInk),
            };
            foreach (CreditSection section in CreditWords.Sections)
            {
                for (int line = 0; line < section.Lines.Count; line++)
                {
                    blocks.Add(new Block(beside, art, raisedBy, line == 0 ? section.Heading : null, section.Lines[line], LineSize, Lettering.Ink));
                }
            }

            return blocks;
        }

        /// <summary>One block of the text: a line broken to the text's width, with the heading of its part over it where it is the part's first.</summary>
        private sealed class Block
        {
            /// <summary>The heading over the line and its words; nothing for a line that is not its part's first.</summary>
            private readonly (TextMeshPro Text, string Words)? _heading;
            private readonly TextMeshPro _line;
            private readonly string _lineWords;

            public Block(Transform beside, ArtCatalogue art, int raisedBy, string? heading, string line, float lineSize, Color lineInk)
            {
                if (heading != null)
                {
                    _heading = (Lettering.Write("Heading", beside, art, LetteringStyle.Label, HeadingSize, Lettering.Ink, DrawOrder.CardLettering + raisedBy), heading);
                }

                _line = Lettering.Write("Line", beside, art, LetteringStyle.Soft, lineSize, lineInk, DrawOrder.CardLettering + raisedBy);
                _lineWords = line;
                Height = LayFrom(0f);
            }

            /// <summary>How high the block is set: its heading, the gap under it, and its line.</summary>
            public float Height { get; }

            /// <summary>Moves the block's words onto the card they are written on.</summary>
            public void MoveOnto(Transform card)
            {
                _heading?.Text.transform.SetParent(card, worldPositionStays: false);
                _line.transform.SetParent(card, worldPositionStays: false);
            }

            public void Show(bool isShown)
            {
                _heading?.Text.gameObject.SetActive(isShown);
                _line.gameObject.SetActive(isShown);
            }

            /// <summary>Lays the block out with its top edge at a height of the card; returns how high it is.</summary>
            public float LayFrom(float top)
            {
                float heading = _heading is (TextMeshPro lettering, string words)
                    ? Lettering.SayInLines(lettering, words, TextWidth, SettingsLayout.RowsLeft, top) + HeadingGap
                    : 0f;
                return heading + Lettering.SayInLines(_line, _lineWords, TextWidth, SettingsLayout.RowsLeft, top - heading);
            }
        }
    }
}
