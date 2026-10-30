#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Puzzle.Client.Art;
using Puzzle.Client.Canvases;
using Puzzle.Client.Cloud;
using Puzzle.Client.Engine;
using Puzzle.Client.Flow;
using Puzzle.Client.Layout;
using Puzzle.Client.Purchases;
using Puzzle.Client.Screens;
using Puzzle.Core.Levels;
using Puzzle.Meta;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// The pictures of Stage 5 of the v1 server (docs/research/server-v1.md §5.7, §6.5) on the screens the game is made
    /// for, upright and on their side: the phone's cover display (1080x2424), the folded-open display (2076x2152 and
    /// 2152x2076) and the large iPad (2732x2048 and 2048x2732). On the game's own session with its cloud on Unity's
    /// services and the Cloud Code module in memory, by touches alone: the settings card with its account row and the
    /// privacy row's line about the cloud save; the account card of an account of this phone (and one linked to Google
    /// Play Games), with the switch off, its question before "Verilerimi sil", its question about this phone's progress,
    /// and a deletion the module could not make; every page of the privacy card; and the purchase cards of the server's
    /// verification (the walks of GameWalk.Verification.cs); and the support card with its "Künye", and the credits it opens. Into out/unity/shots/account, with a sheet per screen.
    /// usage: Unity.exe -batchmode -quit -projectPath unity -executeMethod Puzzle.Client.EditorTools.AccountShots.Run [-language en]
    /// The command ends with exit code 1 when a press did not lead where it should; the log's last line says what came of it.
    /// </summary>
    public static class AccountShots
    {
        private static readonly ScreenShape[] Screens =
        {
            new ScreenShape(1080, 2424, 0, 0), new ScreenShape(2076, 2152, 0, 0), new ScreenShape(2152, 2076, 0, 0),
            new ScreenShape(2732, 2048, 0, 0), new ScreenShape(2048, 2732, 0, 0),
        };

        public static void Run()
        {
            try
            {
                WordsPack.Speak(ToolLanguage.FromCommandLine);
                ArtCatalogue art = AssetDatabase.LoadAssetAtPath<ArtCatalogue>(ArtCatalogueBuilder.CataloguePath)
                    ?? throw new InvalidOperationException($"{ArtCatalogueBuilder.CataloguePath} is missing; run ProjectSetup.Run first.");
                IReadOnlyList<LevelDefinition> levels = LevelPack.LoadPacked();
                var studio = new Studio(art, levels, PaintingGallery.OfThePackedMuseum(), string.Empty);
                int pictures = 0;
                foreach (ScreenShape screen in Screens)
                {
                    var sheet = new ContactSheet(shrinkBy: 4, mostInARow: 5);
                    pictures += ShootTheAccount(studio, screen, sheet, linked: false);
                    pictures += ShootTheAccount(studio, screen, sheet, linked: true);
                    StoryStudio.WriteSheet(sheet, $"account/account-sheet-{screen}{ToolLanguage.FileNameEnding}.png");
                    pictures += GameWalk.RunTheVerificationWalks(screen, studio);
                }

                Debug.Log($"Account shots: done, {pictures} pictures on {Screens.Length} screens.");
            }
            catch (Exception failure) when (failure is InvalidOperationException || failure is ArgumentException || failure is LevelFormatException)
            {
                Debug.LogError($"Account shots: failed: {failure.Message}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>The settings card, the account card in its states, and the privacy card's pages, on one screen.</summary>
        private static int ShootTheAccount(Studio studio, ScreenShape screen, ContactSheet sheet, bool linked)
        {
            var services = new FakeUnityServices();
            var server = new FakePurchaseServer(services, new UnityDocumentJson());
            IPlatformSignIn platform = linked
                ? new FakePlatformSignIn(SignInPlatform.PlayGames, "alice", isSignedIn: true)
                : (IPlatformSignIn)NoPlatformSignIn.Instance;
            (GameSession session, CloudSaving cloud) = SessionWithTheCloud(studio, screen, services, server, platform);
            using (session)
            {
                string Named(string what) => $"account/{(linked ? "linked-" : string.Empty)}{what}-{screen}{ToolLanguage.FileNameEnding}.png";
                int pictures = 0;
                void Shoot(string what)
                {
                    StoryStudio.AdvanceUntilSettled(session);
                    StoryStudio.Shoot(session, screen, sheet, Named(what));
                    pictures++;
                }

                Press(session, MuseumOf(session).GearPixel, "the wallet row's gear");
                Require(MuseumOf(session).PrivacyLineShown == SettingsWords.PrivacyLine(DeviceSettings.Default, cloudSaveIsOn: true), "the privacy row does not say the cloud save leaves the phone");
                Shoot("settings");
                Press(session, MuseumOf(session).AccountRowPixel, "the settings card's account row");
                Require(MuseumOf(session).SettingsCardInFront == SettingsCardKind.Account, "the account row did not bring the account card");
                Shoot("account");
                if (linked)
                {
                    Require(cloud.Account is Account.Linked, $"the linked phone's account is {cloud.Account}");
                    return pictures;
                }

                Press(session, MuseumOf(session).AccountSwitchPixel, "the account card's switch");
                Require(!cloud.IsSwitchedOn, "the switch did not turn the cloud save off");
                Shoot("account-off");
                Press(session, MuseumOf(session).AccountSwitchPixel, "the account card's switch, again");
                Press(session, MuseumOf(session).AccountLeftAnswerPixel, "the account card's Verilerimi sil");
                Shoot("account-delete-question");
                server.GoesOut(FakeServerOutage.NotDeployed);
                Press(session, MuseumOf(session).AccountRightAnswerPixel, "the question's Sil");
                Require(MuseumOf(session).AccountShown?.Asks == AccountCardAsks.Tells, "a deletion the module could not make did not say so");
                Shoot("account-not-deleted");
                Press(session, MuseumOf(session).AccountDonePixel, "the card's Tamam");
                server.ComesBack();
                Press(session, MuseumOf(session).AccountLeftAnswerPixel, "the account card's Verilerimi sil, again");
                Press(session, MuseumOf(session).AccountRightAnswerPixel, "the question's Sil, again");
                Require(MuseumOf(session).AccountShown?.Asks == AccountCardAsks.AboutThePhone, "the deletion did not ask about this phone's progress");
                Shoot("account-phone-question");
                Press(session, MuseumOf(session).AccountLeftAnswerPixel, "the second question's Kalsın");
                Shoot("account-deleted");
                Press(session, MuseumOf(session).AccountDonePixel, "the card's Tamam");
                Press(session, MuseumOf(session).CardClosePixel, "the account card's round button");
                Press(session, MuseumOf(session).PrivacyRowPixel, "the settings card's privacy row");
                int pages = MuseumOf(session).PrivacyPageShown?.Pages ?? 1;
                Shoot("privacy-1");
                for (int page = 2; page <= pages; page++)
                {
                    Press(session, MuseumOf(session).PrivacyNextPagePixel, $"the privacy card's arrow to page {page}");
                    Shoot($"privacy-{page}");
                }

                Press(session, MuseumOf(session).CardClosePixel, "the privacy card's round button");
                Press(session, MuseumOf(session).SupportRowPixel, "the settings card's support row");
                Shoot("support");
                Press(session, MuseumOf(session).CreditsButtonPixel, "the support card's Künye");
                Require(MuseumOf(session).SettingsCardInFront == SettingsCardKind.Credits, "the support card's Künye did not bring the credits card");
                Shoot("credits");
                return pictures;
            }
        }

        /// <summary>The game's session in the hall on the purchase walks' level, over a scripted store, its cloud on the fakes, started and at rest.</summary>
        private static (GameSession Session, CloudSaving Cloud) SessionWithTheCloud(
            Studio studio, ScreenShape screen, FakeUnityServices services, FakePurchaseServer server, IPlatformSignIn platform)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EconomyRules rules = StoryStudio.Economy;
            PlayerProgress standing = StoryStudio.StandingOn(LevelPack.Numbered(studio.Levels, 25), studio.Levels);
            var session = new GameSession(
                studio.Levels, studio.Art, studio.Paintings, rules, StoryStudio.Clock, new ProgressInMemory(standing), StoryStudio.AttemptsKeptInMemory(),
                new SettingsInMemory(DeviceSettings.Default), DeviceAbilities.OfThisBuild, CuePlayer.Silent,
                ScriptedStore.AtUsPrices(rules.Shop.Store, Application.identifier, offersRestore: false), screen, SessionOpening.InTheMuseum);
            FakePhoneAccounts accounts = services.Phone();
            var sync = new CloudSync(
                accounts, new CloudStateInMemory(CloudState.New(RandomIds.New())), session.ProgressForTheCloud, rules, new UnityDocumentJson(), StoryStudio.Clock, "1.1.0", RandomIds.New);
            var cloud = new CloudSaving(
                sync, accounts, platform, NoRemoteSettings.Instance, server.Phone(accounts), session.ProgressForTheCloud, hasACloudProject: true, Debug.Log,
                failure => throw new InvalidOperationException($"the cloud's background work failed: {failure.Message}", failure));
            session.OpenToTheCloud(cloud);
            Task start = cloud.StartAsync(CancellationToken.None);
            Require(start.IsCompleted && !start.IsFaulted, "the cloud's start on the fakes did not end at once");
            session.Advance(StoryStudio.FrameSeconds);
            StoryStudio.AdvanceUntilSettled(session);
            return (session, cloud);
        }

        private static void Press(GameSession session, Vector2? pixel, string what)
        {
            ThroughTheCard.Press(session, pixel, what);
            session.Advance(StoryStudio.FrameSeconds); // a step at least, as the game takes one every frame
            StoryStudio.AdvanceUntilSettled(session);
        }

        private static MuseumStage MuseumOf(GameSession session) => session.Museum ?? throw new InvalidOperationException("The session is not in the museum.");

        private static void Require(bool holds, string otherwise)
        {
            if (!holds)
            {
                throw new InvalidOperationException(otherwise);
            }
        }
    }
}
