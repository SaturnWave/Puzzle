#nullable enable
using System.Collections.Generic;
using Puzzle.Client.Art;
using Puzzle.Client.Flow;
using Puzzle.Client.Gallery;
using Puzzle.Client.Layout;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Screens
{
    /// <summary>
    /// The question about the play statistics, which comes down once over the hall after the entry page of the first
    /// start (<see cref="ConsentWords"/>): the nameplate "Oyun istatistikleri", the question, what is counted and by
    /// whom, what never goes, that no is a full answer; under them "İzin verme" and "İzin ver" side by side, as large
    /// as each other and in the same blue, so that neither answer is the one the card leans to. It has no round button:
    /// its question has its two answers, and the device's way back answers "İzin verme". Nothing is sent before an
    /// answer (Statistics/PlayStatistics.cs).
    /// </summary>
    /// <remarks>
    /// The design canvas draws no such card. It is built in the language of the cards it does draw (SettingsTR), as
    /// wide as the settings card, its text set as the privacy card's, its answers the small buttons of the quit card's
    /// question; it is the owner's to look at and to redraw. The card is as high as its text needs; a screen too
    /// small for it draws all of it smaller (<see cref="PageLayout.PlaceOfACard"/>).
    /// </remarks>
    public sealed class ConsentCard : IMuseumCard
    {
        private const float NameplateWidth = 236f;
        private const float TitleSize = 22f;

        /// <summary>The question begins this far under the body's top edge, in the label's type; the paragraphs follow it in the soft type, this far apart.</summary>
        private const float TextTop = 40f;
        private const float QuestionSize = 16f;
        private const float QuestionGap = 10f;
        private const float ParagraphSize = 13f;
        private const float ParagraphGap = 7f;

        /// <summary>Under the text: the answers' middles this far under its end, and the card's lower edge this far.</summary>
        private const float AnswersUnderText = 38f;
        private const float BodyUnderText = 74f;

        /// <summary>The two answers, each as wide as the privacy question's and labelled as large, their middles this far apart.</summary>
        private const float AnswerWidth = 136f;
        private const float AnswersApart = 152f;
        private const float AnswerLabelSize = 16f;

        private readonly HangingCard<MuseumPress> _card;
        private readonly SmallButton _decline;
        private readonly SmallButton _allow;

        private ConsentCard(Transform parent, ArtCatalogue art)
        {
            // The text is set first, beside the card, to know how high the card must be to hold it.
            IReadOnlyList<(TextMeshPro Line, string Words)> text = WriteTheText(parent, art);
            float textEnd = TextTop + Lay(text, down => -down);
            _card = new HangingCard<MuseumPress>(
                "Consent card", parent, art, new Vector2(SettingsLayout.BodyWidth, textEnd + BodyUnderText),
                new CardNameplate(ConsentWords.Title, NameplateWidth, TitleSize), CardHanging.FromPinsInTheWall);
            foreach ((TextMeshPro line, string _) in text)
            {
                line.transform.SetParent(_card.Content, worldPositionStays: false);
            }

            Lay(text, down => _card.At(down).y);

            Vector3 answers = _card.At(textEnd + AnswersUnderText);
            _decline = new SmallButton(
                "Decline", _card.Content, art, SmallButtonColour.Blue, ConsentWords.Decline, AnswerWidth, AnswerLabelSize, DrawOrder.CardArt, DrawOrder.CardLettering);
            _decline.Place(answers + new Vector3(-AnswersApart / 2f, 0f, 0f));
            _allow = new SmallButton(
                "Allow", _card.Content, art, SmallButtonColour.Blue, ConsentWords.Allow, AnswerWidth, AnswerLabelSize, DrawOrder.CardArt, DrawOrder.CardLettering);
            _allow.Place(answers + new Vector3(AnswersApart / 2f, 0f, 0f));

            _card.Listen(_decline, new MuseumPress.DeclineTheStatistics());
            _card.Listen(_allow, new MuseumPress.AllowTheStatistics());
        }

        public HangingCard<MuseumPress> Card => _card;

        /// <summary>The middles of "İzin verme" and "İzin ver", in the layout's points: where a finger presses them.</summary>
        public Vector2 DeclineButtonMiddle => _card.InTheLayout(_decline.Middle);

        public Vector2 AllowButtonMiddle => _card.InTheLayout(_allow.Middle);

        /// <summary>Builds the card out of sight above the screen, over the hall with nothing in front of it. Place it before the first picture.</summary>
        public static ConsentCard Create(Transform parent, ArtCatalogue art) => new ConsentCard(parent, art);

        /// <summary>The card tells of nothing of the wallet.</summary>
        public void Tell(WalletView wallet)
        {
        }

        public void Dispose() => _card.Dispose();

        /// <summary>The question in the label's type, and under it each paragraph in the soft type, beside the card.</summary>
        private static IReadOnlyList<(TextMeshPro Line, string Words)> WriteTheText(Transform beside, ArtCatalogue art)
        {
            var text = new List<(TextMeshPro, string)>
            {
                (Lettering.Write("Question", beside, art, LetteringStyle.Label, QuestionSize, Lettering.Ink, DrawOrder.CardLettering), ConsentWords.Question),
            };
            for (int index = 0; index < ConsentWords.Text.Count; index++)
            {
                text.Add((Lettering.Write($"Paragraph {index + 1}", beside, art, LetteringStyle.Soft, ParagraphSize, Lettering.Ink, DrawOrder.CardLettering), ConsentWords.Text[index]));
            }

            return text;
        }

        /// <summary>Lays the question and the paragraphs one under the other from the text's top, broken to the width of the rows' room.</summary>
        /// <param name="heightOf">Where a point that far under the body's top edge stands on what the lines stand on.</param>
        /// <returns>How high the text is.</returns>
        private static float Lay(IReadOnlyList<(TextMeshPro Line, string Words)> text, System.Func<float, float> heightOf)
        {
            float width = SettingsLayout.RowsRight - SettingsLayout.RowsLeft;
            float down = TextTop;
            for (int index = 0; index < text.Count; index++)
            {
                (TextMeshPro line, string words) = text[index];
                down += Lettering.SayInLines(line, words, width, SettingsLayout.RowsLeft, heightOf(down));
                down += index == 0 ? QuestionGap : ParagraphGap;
            }

            return down - ParagraphGap - TextTop;
        }
    }
}
