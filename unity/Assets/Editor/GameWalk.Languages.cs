#nullable enable
using System;
using Puzzle.Client.Engine;
using Puzzle.Client.Flow;
using Puzzle.Client.Layout;
using Puzzle.Client.Screens;
using Puzzle.Client.Words;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// The walk of the language, once on each screen, on the game's own session and by touches alone: in the hall the
    /// gear brings the settings card down, its row "Dil" the language card in front of it, which ticks the language
    /// the tools speak. A press on another language (Japanese, whose Chinese characters the fallbacks draw in their
    /// own forms; Korean or English where the tools speak a language that names the hall as Japanese does) speaks it at
    /// once: the card ticks it and names itself in it, the settings card behind it and the hall behind both are built
    /// anew in it, and the device keeps the choice. With the cards up, the hall, the map, the collection and the card
    /// before the level all speak it; the board the level opens is built in it too, the next time one is built. Last,
    /// the language card is opened again and the tools' own language chosen back (<c>walk-languages-...</c>).
    /// </summary>
    internal static partial class GameWalk
    {
        public static void RunTheLanguageWalk(ScreenShape screen, Studio studio)
        {
            Language spokenByTheTools = Speech.Language;
            Language other = AnotherLanguageToChoose(spokenByTheTools);
            PlayerProgress standing = StoryStudio.StandingOn(LevelPack.Numbered(studio.Levels, RetriedLevel), studio.Levels);
            var settings = new SettingsInMemory(DeviceSettings.Default);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            using var session = new GameSession(
                studio.Levels, studio.Art, studio.Paintings, StoryStudio.Economy, StoryStudio.Clock, new ProgressInMemory(standing), StoryStudio.AttemptsKeptInMemory(),
                settings, DeviceAbilities.OfThisBuild, CuePlayer.Silent, screen, SessionOpening.InTheMuseum);
            var reel = new Reel(session, screen, $"walk-languages-{screen}{studio.FileNameEnding}", studio.Paintings.Catalogue);
            try
            {
                reel.Settle();
                string hallBefore = MuseumOf(session).BarSays;
                reel.Shoot("hall");
                OpenTheLanguageCard(reel, spokenByTheTools);
                reel.Shoot("languages");

                Choose(other, reel, "chosen");
                ThroughTheCard.Require(settings.Settings.Language == other, $"the device keeps {settings.Settings}, not the language chosen");
                ThroughTheCard.Require(
                    MuseumOf(session).BarSays == MuseumWords.HomeTab && MuseumOf(session).BarSays != hallBefore,
                    $"the hall behind the cards still says \"{MuseumOf(session).BarSays}\" on its bar after {Languages.CodeOf(other)} was chosen");
                Press(session, MuseumOf(session).LanguageDonePixel, "the language card's Tamam", reel, pressedShot: null);
                reel.Settle();
                ThroughTheCard.Require(MuseumOf(session).SettingsCardInFront == SettingsCardKind.Settings, "the language card's Tamam did not lead back to the settings card");
                reel.Shoot("settings");
                session.GoBack();
                reel.Settle();
                reel.Shoot("hall");

                foreach (Destination page in new[] { Destination.Map, Destination.Collection, Destination.Shop, Destination.Home })
                {
                    TurnThePage(reel, page, picturesTheTurn: false);
                    reel.Shoot(page.ToString().ToLowerInvariant());
                }

                Press(session, MuseumOf(session).PlayButtonPixel, "the hall's play button", reel, pressedShot: null);
                reel.Settle();
                ThroughTheCard.Require(MuseumOf(session).ShowsTheStartCard, "the play button did not bring the level's card");
                reel.Shoot("level-card");
                session.GoBack();
                reel.Settle();

                OpenTheLanguageCard(reel, other);
                Choose(spokenByTheTools, reel, "chosen-back");
                session.GoBack();
                reel.Settle();
                session.GoBack();
                reel.Settle();
                ThroughTheCard.Require(!MuseumOf(session).ShowsTheSettings && MuseumOf(session).BarSays == hallBefore, "the tools' own language did not come back on every card and page");
                reel.Shoot("hall-back");
                reel.WriteSheet();
                Debug.Log($"Screen shots: walk {reel.Name}: {Languages.CodeOf(spokenByTheTools)} to {Languages.CodeOf(other)} on the language card, every page and the level's card in it, and back.");
            }
            finally
            {
                // A walk that stopped half way leaves the tools speaking their own language for the walks after it.
                WordsPack.Speak(spokenByTheTools);
            }
        }

        /// <summary>
        /// The language the walk chooses: the first of Japanese, Korean and English that names the hall otherwise than
        /// the tools' language does, so the bar shows that the hall was built anew in it (German and Indonesian say
        /// "Museum" as English does, Traditional Chinese "美術館" as Japanese does).
        /// </summary>
        private static Language AnotherLanguageToChoose(Language spoken)
        {
            foreach (Language candidate in new[] { Language.Japanese, Language.Korean, Language.English })
            {
                if (candidate != spoken && WordsPack.Load(candidate) is Wording wording && wording.Words.WordsOf(WordId.NavMuseum, PluralForm.Other) != MuseumWords.HomeTab)
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException($"No language the game carries names the hall otherwise than {Languages.CodeOf(spoken)} does.");
        }

        /// <summary>The gear brings the settings card down, and its language row the language card, which ticks the language spoken.</summary>
        private static void OpenTheLanguageCard(Reel reel, Language spoken)
        {
            GameSession session = reel.Session;
            Press(session, MuseumOf(session).GearPixel, "the hall's gear", reel, pressedShot: null);
            reel.Settle();
            Press(session, MuseumOf(session).LanguageRowPixel, "the settings card's language row", reel, pressedShot: null);
            reel.Settle();
            ThroughTheCard.Require(MuseumOf(session).SettingsCardInFront == SettingsCardKind.Language, "the language row did not bring the language card");
            ThroughTheCard.Require(MuseumOf(session).LanguageTicked == spoken, $"the language card ticks {MuseumOf(session).LanguageTicked}, not {spoken}");
        }

        /// <summary>A language's row is pressed: the game speaks it at once, and the card in front still hangs, built anew in it.</summary>
        private static void Choose(Language language, Reel reel, string shot)
        {
            GameSession session = reel.Session;
            Press(session, MuseumOf(session).LanguagePixel(language), $"the language card's row of {Languages.CodeOf(language)}", reel, pressedShot: null);
            reel.Settle();
            ThroughTheCard.Require(Speech.Language == language, $"the game speaks {Languages.CodeOf(Speech.Language)} after {Languages.CodeOf(language)} was chosen");
            ThroughTheCard.Require(
                MuseumOf(session).SettingsCardInFront == SettingsCardKind.Language && MuseumOf(session).LanguageTicked == language,
                $"after {Languages.CodeOf(language)} was chosen the language card is not in front, or ticks {MuseumOf(session).LanguageTicked}");
            reel.Shoot($"{shot}-{Languages.CodeOf(language)}");
        }
    }
}
