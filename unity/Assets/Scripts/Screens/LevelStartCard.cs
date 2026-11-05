#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Puzzle.Client.Art;
using Puzzle.Client.Flow;
using Puzzle.Client.Gallery;
using Puzzle.Client.Layout;
using Puzzle.Meta;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Screens
{
    /// <summary>
    /// The card before a level, as the design canvas draws it (LevelStartTR), over the museum's hall: the brass
    /// nameplate with the level and the jigsaw piece winning it gives, the goals under their header, the moves on
    /// their medallion, the three start boosters on a wooden shelf under the header that asks to choose, each with
    /// its name, the green "Oyna" over the card's lower edge, and the round button that closes the card. A disc shows
    /// how many of its booster the player holds, or the plus where they hold none. A press on a disc is the stage's to
    /// follow (<see cref="Press"/>): a booster that is held is chosen (a green ring, a tick, and on the medallion the
    /// moves the booster of extra moves adds) or put back; one that is not held is offered by the card the stage
    /// hangs over this one. Nothing is chosen for the player, and nothing is spent here: the attempt's first move
    /// spends what was chosen (<see cref="AttemptLedger"/>).
    /// </summary>
    /// <remarks>
    /// Sizes and places are the canvas' points (<see cref="LevelStartLayout"/>); the card is drawn at the share of
    /// them the screen's card place gives it (<see cref="PageLayout.PlaceOfACard"/>).
    /// </remarks>
    public sealed class LevelStartCard : IMuseumCard
    {
        /// <summary>The medallion's label and figure, as the board's plaque row sets them (BoardTR): 10 and 40, 2 apart, 3 to spare under the figure.</summary>
        private const float MovesLabelSize = 10f;
        private const float MovesLabelSpacing = 9f;
        private const float GapUnderMovesLabel = 2f;
        private const float MovesSize = 40f;
        private const float RoomUnderMoves = 3f;

        /// <summary>The place a start booster stands in (<see cref="PlaceOf"/>) keeps this far from its neighbour's, and reaches this far past its disc and its name.</summary>
        private const float PlaceGap = 6f;
        private const float PlaceRoom = 8f;

        /// <summary>The plank's picture is its body and the room around it for its rim and shadow: 28 points wider, 68 higher.</summary>
        private static readonly Vector2 PlankPictureMargin = new Vector2(28f, 68f);

        private readonly HangingCard<MuseumPress> _card;
        private readonly LevelBrief _brief;
        private readonly TextMeshPro _moves;
        private readonly PushButton _play;
        private readonly Dictionary<BoosterKind, BoosterDisc> _discs = new Dictionary<BoosterKind, BoosterDisc>();
        private readonly List<SectionHeader> _headers = new List<SectionHeader>();

        /// <summary>The boosters the player holds, as the card was last told.</summary>
        private BoosterShelf _held;

        private LevelStartCard(Transform parent, ArtCatalogue art, LevelBrief brief, WalletView wallet)
        {
            _brief = brief;
            _held = wallet.Shelf;
            string? rewardLine = brief.Reward is PieceReward reward ? LevelStartWords.Reward(reward) : null;
            var nameplate = new CardNameplate(LevelStartWords.Title(brief.LevelNumber), LevelStartLayout.NameplateWidth, LevelStartLayout.TitleSize, rewardLine);
            _card = new HangingCard<MuseumPress>(
                "Level start card", parent, art, new Vector2(LevelStartLayout.BodyWidth, LevelStartLayout.BodyHeight), nameplate, CardHanging.FromPinsInTheWall,
                ridesBelow: LevelStartLayout.RidesBelow);
            _card.AddCloseButton(new MuseumPress.CloseTheCard());

            AddHeader("Goals header", LevelStartWords.Goals, LevelStartLayout.GoalsRight, LevelStartLayout.GoalsHeaderDown, art);
            AddTheGoals(art);
            _moves = AddTheMedallion(art);
            AddHeader("Boosters header", LevelStartWords.ChooseBoosters, 0f, LevelStartLayout.BoostersHeaderDown, art);
            AddTheShelf(art);

            _play = new PushButton(
                "Play", _card.Content, art, ButtonLook.Green, LevelStartWords.Play, LevelStartLayout.PlayWidth, LevelStartLayout.PlayLabelSize,
                DrawOrder.CardArt, DrawOrder.CardLettering);
            _play.Place(At(0f, LevelStartLayout.PlayDown));
            _card.Listen(_play, new MuseumPress.StartTheLevel());
            Show();
        }

        /// <summary>The card itself: where it hangs, how far it has come down, and what a finger on it presses.</summary>
        public HangingCard<MuseumPress> Card => _card;

        /// <summary>The start boosters chosen on the card so far; none when it comes down.</summary>
        public StartChoice Choice { get; private set; } = StartChoice.None;

        /// <summary>The middle of the green "Oyna", in the layout's points: where a finger presses it.</summary>
        public Vector2 PlayButtonMiddle => _card.InTheLayout(_play.Middle);

        /// <summary>Builds the card out of sight above the screen for the level a brief tells of. Place it before the first picture.</summary>
        public static LevelStartCard Create(Transform parent, ArtCatalogue art, LevelBrief brief, WalletView wallet) => new LevelStartCard(parent, art, brief, wallet);

        /// <summary>The middle of a start booster's disc, in the layout's points: where a finger presses it.</summary>
        /// <exception cref="ArgumentException">The booster is used during play: the card has no disc for it.</exception>
        public Vector2 DiscMiddle(BoosterKind booster) => _discs.TryGetValue(booster, out BoosterDisc disc)
            ? _card.InTheLayout(disc.Middle)
            : throw new ArgumentException($"The card before a level has no disc for {booster.Name()}.", nameof(booster));

        /// <summary>
        /// Where a start booster stands on the card, in the layout's points: its disc and its name under it, with a
        /// little room around them, the place the tutorial lights when it shows the start boosters (#7).
        /// </summary>
        /// <exception cref="ArgumentException">The booster is used during play: the card has no disc for it.</exception>
        public Box PlaceOf(BoosterKind booster)
        {
            if (!_discs.ContainsKey(booster))
            {
                throw new ArgumentException($"The card before a level has no disc for {booster.Name()}.", nameof(booster));
            }

            float right = LevelStartLayout.DiscRight(LevelBrief.StartBoosters.ToList().IndexOf(booster));
            float halfWidth = (LevelStartLayout.DiscsApart - PlaceGap) / 2f;
            Vector2 topLeft = _card.InTheLayout(At(right - halfWidth, LevelStartLayout.DiscDown - LevelStartLayout.DiscSize / 2f - PlaceRoom));
            Vector2 bottomRight = _card.InTheLayout(At(right + halfWidth, LevelStartLayout.NameDown + LevelStartLayout.NameLinesApart + LevelStartLayout.NameSize / 2f + PlaceRoom));
            return new Box(topLeft.x, bottomRight.y, bottomRight.x - topLeft.x, topLeft.y - bottomRight.y);
        }

        /// <summary>
        /// A press on a start booster's disc, as the rules answer it: a held one is chosen or put back, and the card
        /// shows the choice after it; one held none of, or one more than an attempt takes, leaves the choice as it is.
        /// </summary>
        public StartToggle Press(BoosterKind booster)
        {
            StartToggle toggle = _brief.Press(Choice, booster, _held);
            if (toggle is StartToggle.Changed changed)
            {
                Choice = changed.Choice;
                Show();
            }

            return toggle;
        }

        /// <summary>Shows how many of each booster the wallet holds now: a booster just bought loses its plus.</summary>
        public void Tell(WalletView wallet)
        {
            if (wallet.Shelf.Equals(_held))
            {
                return;
            }

            _held = wallet.Shelf;
            Choice = _brief.HeldOnly(Choice, _held);
            Show();
        }

        public void Dispose()
        {
            foreach (SectionHeader header in _headers)
            {
                header.Dispose();
            }

            _card.Dispose();
        }

        /// <summary>A place on the card: so far right of its middle, so far under its top edge.</summary>
        private Vector3 At(float right, float down) => _card.At(down) + new Vector3(right, 0f, 0f);

        private void AddHeader(string name, string words, float right, float down, ArtCatalogue art)
        {
            var header = new SectionHeader(name, _card.Content, art, words, DrawOrder.CardArt, DrawOrder.CardLettering);
            header.Transform.localPosition = At(right, down);
            _headers.Add(header);
        }

        /// <summary>Each goal's icon and what is to be done of it, as the layout places them for the widths of the numbers.</summary>
        private void AddTheGoals(ArtCatalogue art)
        {
            var numbers = new List<TextMeshPro>();
            var widths = new List<float>();
            foreach (GoalFigure goal in _brief.Goals)
            {
                string words = goal.Remaining.ToString(CultureInfo.InvariantCulture);
                TextMeshPro number = Lettering.Write($"Goal {numbers.Count + 1}", _card.Content, art, LetteringStyle.Figure, LevelStartLayout.GoalNumberSize, Lettering.Ink, DrawOrder.CardLettering);
                Lettering.Say(number, words);
                numbers.Add(number);
                widths.Add(Lettering.WidthOf(number, words));
            }

            IReadOnlyList<StartGoalPlace> places = LevelStartLayout.Goals(widths);
            for (int goal = 0; goal < places.Count; goal++)
            {
                StartGoalPlace place = places[goal];
                SpriteRenderer icon = SceneObjects.AddSprite($"Goal {goal + 1} icon", _card.Content, art.Of(_brief.Goals[goal].Icon), art.SpriteMaterial, DrawOrder.CardExhibit);
                icon.transform.localPosition = At(place.IconRight, place.IconDown);
                icon.transform.localScale = new Vector3(place.IconSize, place.IconSize, 1f);

                TextMeshPro number = numbers[goal];
                number.fontSize = place.NumberSize;
                number.ForceMeshUpdate();
                number.transform.localPosition = At(place.NumberRight, place.NumberDown + Lettering.FigureDrop(LetteringStyle.Figure) * place.NumberSize);
            }
        }

        /// <summary>The moves' medallion with its label over the figure, centred together on it as the board's plaque row has them.</summary>
        /// <returns>The figure, which the choice changes.</returns>
        private TextMeshPro AddTheMedallion(ArtCatalogue art)
        {
            Vector3 middle = At(LevelStartLayout.MedallionRight, LevelStartLayout.MedallionDown);
            SpriteRenderer medallion = SceneObjects.AddSprite("Moves medallion", _card.Content, art.Of(GalleryArt.Medallion), art.SpriteMaterial, DrawOrder.CardArt);
            medallion.transform.localPosition = middle;

            float top = (MovesLabelSize + GapUnderMovesLabel + MovesSize + RoomUnderMoves) / 2f;
            TextMeshPro label = Lettering.Write("Moves label", _card.Content, art, LetteringStyle.Label, MovesLabelSize, Lettering.LabelInk, DrawOrder.CardLettering);
            label.characterSpacing = MovesLabelSpacing;
            Lettering.Say(label, LevelStartWords.Moves);
            label.transform.localPosition = middle + new Vector3(0f, top - MovesLabelSize / 2f, 0f);

            TextMeshPro moves = Lettering.Write("Moves", _card.Content, art, LetteringStyle.Figure, MovesSize, Lettering.Ink, DrawOrder.CardLettering);
            float figureMiddle = top - MovesLabelSize - GapUnderMovesLabel - MovesSize / 2f;
            moves.transform.localPosition = middle + new Vector3(0f, figureMiddle - Lettering.FigureDrop(LetteringStyle.Figure) * MovesSize, 0f);
            return moves;
        }

        /// <summary>The wooden shelf with a disc for each start booster, and the booster's name under it.</summary>
        private void AddTheShelf(ArtCatalogue art)
        {
            SpriteRenderer plank = SceneObjects.AddSprite("Shelf", _card.Content, art.Of(GalleryArt.Plank), art.SpriteMaterial, DrawOrder.CardArt);
            plank.drawMode = SpriteDrawMode.Sliced; // the picture's middle stretches both ways; its round corners keep their shape
            plank.size = new Vector2(LevelStartLayout.ShelfWidth, LevelStartLayout.ShelfHeight) + PlankPictureMargin;
            plank.transform.localPosition = At(0f, LevelStartLayout.ShelfDown);

            for (int column = 0; column < LevelBrief.StartBoosters.Count; column++)
            {
                BoosterKind booster = LevelBrief.StartBoosters[column];
                float right = LevelStartLayout.DiscRight(column);
                var disc = new BoosterDisc(
                    booster.Name(), _card.Content, art, StartBoosterPictures.Of(booster), LevelStartLayout.DiscSize, DrawOrder.CardDisc,
                    _brief.FaceOf(booster, _held, StartChoice.None));
                disc.Place(At(right, LevelStartLayout.DiscDown));
                _card.Listen(disc, new MuseumPress.ChooseStartBooster(booster));
                _discs.Add(booster, disc);

                string[] lines = LevelStartWords.OnTheShelf(booster, _brief.ExtraMoves).Split('\n');
                for (int line = 0; line < lines.Length; line++)
                {
                    TextMeshPro name = Lettering.Write($"{booster.Name()} name", _card.Content, art, LetteringStyle.Label, LevelStartLayout.NameSize, Lettering.Ink, DrawOrder.CardLettering);
                    Lettering.Say(name, lines[line]);
                    name.transform.localPosition = At(right, LevelStartLayout.NameDown + line * LevelStartLayout.NameLinesApart);
                }
            }
        }

        /// <summary>Every disc as the stock and the choice have it, and the moves the level opens with.</summary>
        private void Show()
        {
            foreach (KeyValuePair<BoosterKind, BoosterDisc> disc in _discs)
            {
                disc.Value.Show(_brief.FaceOf(disc.Key, _held, Choice));
            }

            Lettering.Say(_moves, _brief.MovesWith(Choice).ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>What a start booster's disc shows, as the design canvas draws the three (LevelStartTR).</summary>
    public static class StartBoosterPictures
    {
        /// <exception cref="ArgumentException">The booster is used during play.</exception>
        public static BoosterPicture Of(BoosterKind booster) => booster switch
        {
            BoosterKind.StartRocketAndBomb => BoosterPicture.BrushAndSplat,
            BoosterKind.StartColorBomb => BoosterPicture.Of(PieceArt.ColorBomb),
            BoosterKind.StartExtraMoves => BoosterPicture.Of(GalleryArt.IconThreeMoves),
            _ => throw new ArgumentException($"{booster.Name()} is used during play; no start booster's disc shows it.", nameof(booster)),
        };
    }
}
