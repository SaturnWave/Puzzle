#nullable enable
using Puzzle.Client.Art;
using Puzzle.Client.Flow;
using UnityEngine;

namespace Puzzle.Client.Screens
{
    /// <summary>
    /// The card the device's way back brings down over the bare hall: it asks whether to close the game. The question
    /// stands on its nameplate, as the board's question whether to leave a level stands on that card's, and under it
    /// the two answers stand side by side, as the privacy card's question has them: the blue "Vazgeç" takes the card
    /// up, and the green "Çık" asks for the game to close. The way back, pressed again, takes the card up too; a touch
    /// beside it presses nothing, as beside every card. It has no round button: its question has its two answers.
    /// </summary>
    /// <remarks>
    /// The design canvas draws no such card. It is built in the language of the cards it does draw (LevelStartTR,
    /// SettingsTR), with the answers of the privacy card's question, and is the owner's to look at and to redraw. Its
    /// sizes are the canvas' points around the middle of its body, and it is drawn at the share of them the screen's
    /// card place gives it.
    /// </remarks>
    public sealed class QuitCard : IMuseumCard
    {
        /// <summary>
        /// The body: as wide as the settings card and the privacy card (SettingsTR), so the pins in the wall stand just
        /// beside the nameplate as on the privacy card; as high as one row of answers under the nameplate.
        /// </summary>
        private static readonly Vector2 Body = new Vector2(362f, 112f);

        /// <summary>The nameplate, as wide as the privacy card's; a question too long for it is set smaller than the other cards' titles.</summary>
        private const float NameplateWidth = 236f;
        private const float TitleSize = 22f;

        /// <summary>The answers' middles this far under the body's top edge and this far apart, each as wide as the privacy question's and labelled as large.</summary>
        private const float AnswersDown = 62f;
        private const float AnswersApart = 152f;
        private const float AnswerWidth = 136f;
        private const float AnswerLabelSize = 16f;

        private readonly HangingCard<MuseumPress> _card;
        private readonly SmallButton _stay;
        private readonly SmallButton _quit;

        private QuitCard(Transform parent, ArtCatalogue art)
        {
            Question = MuseumWords.QuitQuestion;
            _card = new HangingCard<MuseumPress>(
                "Quit card", parent, art, Body, new CardNameplate(Question, NameplateWidth, TitleSize), CardHanging.FromPinsInTheWall);

            Vector3 answers = _card.At(AnswersDown);
            _stay = new SmallButton(
                "Stay", _card.Content, art, SmallButtonColour.Blue, MuseumWords.StayInTheGame, AnswerWidth, AnswerLabelSize, DrawOrder.CardArt, DrawOrder.CardLettering);
            _stay.Place(answers + new Vector3(-AnswersApart / 2f, 0f, 0f));
            _quit = new SmallButton(
                "Quit", _card.Content, art, SmallButtonColour.Green, MuseumWords.QuitTheGame, AnswerWidth, AnswerLabelSize, DrawOrder.CardArt, DrawOrder.CardLettering);
            _quit.Place(answers + new Vector3(AnswersApart / 2f, 0f, 0f));

            _card.Listen(_stay, new MuseumPress.CloseTheCard());
            _card.Listen(_quit, new MuseumPress.QuitTheGame());
        }

        public HangingCard<MuseumPress> Card => _card;

        /// <summary>What its nameplate asks.</summary>
        public string Question { get; }

        /// <summary>The middles of "Vazgeç" and "Çık", in the layout's points: where a finger presses them.</summary>
        public Vector2 StayButtonMiddle => _card.InTheLayout(_stay.Middle);

        public Vector2 QuitButtonMiddle => _card.InTheLayout(_quit.Middle);

        /// <summary>Builds the card out of sight above the screen, over the hall with nothing in front of it. Place it before the first picture.</summary>
        public static QuitCard Create(Transform parent, ArtCatalogue art) => new QuitCard(parent, art);

        /// <summary>The card tells of nothing of the wallet.</summary>
        public void Tell(WalletView wallet)
        {
        }

        public void Dispose() => _card.Dispose();
    }
}
