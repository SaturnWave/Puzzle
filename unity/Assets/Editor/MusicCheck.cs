#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Puzzle.Client.Art;
using Puzzle.Client.Flow;
using Puzzle.Client.Screens;
using Puzzle.Client.Sound;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// Starts the game's scene as a built game starts it and follows its music (#9) by touches alone, as a player's
    /// finger would press, from the entry page on, which a touch leaves once it is ready: the music begins once, on
    /// the entry page; it plays on, never begun again, into the hall and while the pages of the museum turn
    /// (the map, the collection, the shop, the hall) and while a level is opened and left without a move; the
    /// settings card's music switch turns it off at once in the device file, the music fades out in about
    /// <see cref="MusicFade.Seconds"/> and is held where it was; turned on, the file says so at once, and the music
    /// fades back in from where it was held, not from its beginning. Put away and brought back, it is held at once and
    /// fades in from there; and at the intro's end it runs into the loop, still the music begun at the start. Through
    /// all of that the music never gives way: its own fades and holds are not taken for another app's music. Then
    /// another app's music plays on the Editor's stand-in (<see cref="OtherAudioStandIn"/>): the music gives way at the
    /// next question (<see cref="MusicYield"/>), fades out and is held with its switch still on, while the effects go
    /// on; a gap between two songs does not bring it back; once that music stops, it comes back after two quiet
    /// answers and fades in from where it was held. Brought back to the front while the other music plays, it stays
    /// silent; given the touches back once that music has stopped, it fades in at once.
    /// usage: Unity.exe -batchmode -projectPath unity -executeMethod Puzzle.Client.EditorTools.MusicCheck.Run
    /// Without -quit: the command ends the Editor itself, with exit code 1 when a step failed.
    /// </summary>
    /// <remarks>
    /// The game runs on the Editor's own device file (Application.persistentDataPath), as PlayModeCheck's does; the
    /// check leaves the music's switch as it found it. Where a level cannot be opened from the hall as it stands (no
    /// life left, a restored room shown), that part is told and left out; the page turns prove the same thing. A
    /// batch-mode Editor without a sound output has an audio clock that does not move: then the music's place cannot
    /// be measured, which the check says, and only that part of it is left out.
    /// </remarks>
    [InitializeOnLoad]
    public static class MusicCheck
    {
        /// <summary>Entering play mode loads the scripts anew and forgets every static field; the Editor's session state survives it.</summary>
        private const string IsRunningKey = "Puzzle.Client.MusicCheck.IsRunning";

        /// <summary>Set when the Editor's device file had the music off before the check, which turns it on to hear it and off again at its end.</summary>
        private const string WasOffKey = "Puzzle.Client.MusicCheck.WasOff";

        /// <summary>A page or a card is given this long to come to rest after a press, and the music this long to play on meanwhile.</summary>
        private const float SettleSeconds = 1f;

        /// <summary>A step that has not come about in this long has failed.</summary>
        private const float LongestWaitSeconds = 12f;

        /// <summary>A game that has not opened its session in this long after play mode began has failed.</summary>
        private const float LongestStartSeconds = 60f;

        /// <summary>A fade, timed on the game's clock between frames, may end this much before or after <see cref="MusicFade.Seconds"/>.</summary>
        private const float FadeSlackSeconds = 0.12f;

        /// <summary>
        /// A gap in the other app's music between two songs: shorter than an asking interval, so that at most one
        /// question falls in it, and long enough that one may.
        /// </summary>
        private const float GapBetweenSongsSeconds = MusicYield.AskEverySeconds - 0.25f;

        /// <summary>
        /// A question asked on the game's clock, which a slow frame holds back (GameRoot's longest step), may come this
        /// much later on the Editor's clock than its interval says.
        /// </summary>
        private const float QuestionSlackSeconds = 0.3f;

        private static readonly List<Step> Steps = new List<Step>();
        private static int _current;
        private static float _stepBegan;
        private static bool _stepIsBegun;
        private static bool _failed;
        private static readonly List<string> Findings = new List<string>();

        private static GameRoot? _root;
        private static float? _playBegan;
        private static bool _isTheEntryTouched;
        private static float _litPressed = float.NegativeInfinity;
        private static double _audioClockAtTheStart;
        private static MusicPlace _lastPlace;
        private static float _switchedAt;
        private static MusicPlace _heldAt;
        private static float _otherAudioTurnedAt;

        static MusicCheck()
        {
            if (SessionState.GetBool(IsRunningKey, false))
            {
                Application.logMessageReceived += NoteTheGamesErrors;
                EditorApplication.update += Follow;
            }
        }

        public static void Run()
        {
            DeviceFile device = DeviceFile.LoadOrMake(Application.persistentDataPath);
            bool wasOff = !device.Settings.Music;
            if (wasOff)
            {
                device.Keep(device.Settings.With(DeviceSetting.Music, isOn: true));
            }

            SessionState.SetBool(WasOffKey, wasOff);
            EditorSceneManager.OpenScene(MainSceneBuilder.ScenePath);
            SessionState.SetBool(IsRunningKey, true);
            EditorApplication.EnterPlaymode();
        }

        private static MusicPlayer Music => _root?.Music ?? throw new InvalidOperationException("the running game has no music player");

        private static GameRoot Root => _root ?? throw new InvalidOperationException("the running game has no root");

        private static OtherAudioStandIn OtherApp =>
            _root?.OtherAudioOfTheDevice as OtherAudioStandIn
            ?? throw new InvalidOperationException("the Editor's game does not ask a stand-in whether another app's music plays");

        private static GameSession Session => _root?.Session ?? throw new InvalidOperationException("the running game has no session");

        private static MuseumStage Museum => Session.Museum ?? throw new InvalidOperationException("the running game is not in the museum");

        private static string DeviceFileText => File.ReadAllText(Path.Combine(Application.persistentDataPath, DeviceFile.FileName));

        private static void NoteTheGamesErrors(string message, string stackTrace, LogType type)
        {
            bool isError = type == LogType.Error || type == LogType.Exception || type == LogType.Assert;
            if (isError && !stackTrace.Contains("UnityEditor."))
            {
                _failed = true;
            }
        }

        /// <summary>One tick of the Editor while the game plays: the step in hand is begun once, then watched until it has come about.</summary>
        private static void Follow()
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            _playBegan ??= Time.realtimeSinceStartup;
            _root ??= UnityEngine.Object.FindAnyObjectByType<GameRoot>();
            if (_root?.Session == null)
            {
                TouchTheReadyEntryPage();
                if (Time.realtimeSinceStartup - _playBegan > LongestStartSeconds)
                {
                    Fail($"the game opened no session in {LongestStartSeconds} s of play");
                    Finish();
                }

                return;
            }

            if (Steps.Count == 0)
            {
                PlanTheSteps();
            }

            try
            {
                if (_current < Steps.Count)
                {
                    TakeTheStep(Steps[_current]);
                    return;
                }
            }
            catch (InvalidOperationException failure)
            {
                Fail(failure.Message);
            }

            Finish();
        }

        /// <summary>The entry page, once its bar is full and the line to touch shows, is touched in its middle, once: the game then opens its hall.</summary>
        private static void TouchTheReadyEntryPage()
        {
            if (_isTheEntryTouched || !(_root?.Entry is EntryStage { IsReady: true } entry))
            {
                return;
            }

            entry.PointerDown(entry.MiddlePixel);
            entry.PointerUp();
            _isTheEntryTouched = true;
            Findings.Add($"the entry page was ready and touched {Time.realtimeSinceStartup - _playBegan:0.0} s into play");
        }

        private static void TakeTheStep(Step step)
        {
            float now = Time.realtimeSinceStartup;
            if (!_stepIsBegun)
            {
                _stepIsBegun = true;
                _stepBegan = now;
                step.Begin();
                return;
            }

            if (step.HasComeAbout(now - _stepBegan))
            {
                step.End();
                Debug.Log($"Music check: {step.What}.");
                _current++;
                _stepIsBegun = false;
            }
            else if (now - _stepBegan > step.LongestWaitSeconds)
            {
                throw new InvalidOperationException($"{step.What}: it did not come about in {step.LongestWaitSeconds} s");
            }
        }

        private static void PlanTheSteps()
        {
            Steps.Add(new Step(
                "the game started and its music began once",
                begin: () => { },
                hasComeAbout: waited => waited >= SettleSeconds && Music.IsPlaying,
                end: () =>
                {
                    Require(Music.Beginnings == 1, $"the music began {Music.Beginnings} times as the game started");
                    Require(!Music.IsGivingWay && !OtherApp.IsPlaying, "as the game started the music gave way, with no other app playing");
                    _audioClockAtTheStart = AudioSettings.dspTime;
                    _lastPlace = Music.Place;
                    Findings.Add($"at the start the music is at {_lastPlace}, at {Music.Volume:0.00} of full volume");
                    Findings.Add(
                        $"in memory (the Editor's measure of the {EditorUserBuildSettings.activeBuildTarget} import): the intro "
                        + $"{Profiler.GetRuntimeMemorySizeLong(Music.Clips.Intro) / 1048576.0:0.00} MB, the streamed loop {Profiler.GetRuntimeMemorySizeLong(Music.Clips.Loop) / 1048576.0:0.00} MB");
                }));

            // A page has turned once the museum is settled on it: until then it takes no touch, and the press of the next
            // step would be lost. The turn is timed on the game's clock, which slow frames hold back (GameRoot's longest
            // step): in an Editor that decodes the paintings of an iOS import, the collection's turn outlasts the second.
            foreach (Destination page in new[] { Destination.Map, Destination.Collection, Destination.Shop, Destination.Home })
            {
                Steps.Add(new Step(
                    $"the page turned to {page} and the music played on",
                    begin: () => Press(Museum.PixelOf(page), $"the bar's place for {page}"),
                    hasComeAbout: waited => waited >= SettleSeconds && Session.Museum is MuseumStage museum && museum.Showing == page && museum.IsSettled,
                    end: () => RequireTheMusicPlaysOn($"the page turned to {page}")));
            }

            PlanTheLevel();
            PlanTheSwitch();
            PlanTheBackgroundAndTheLoop();
            PlanTheOtherAppsMusic();
        }

        /// <summary>Into the level the player stands on, through the hall's play button and the level's card, and out again by the leaving card before any move.</summary>
        private static void PlanTheLevel()
        {
            bool isLeftOut = false;
            Steps.Add(new Step(
                "the hall's play button brought the level's card down, or the level is left out",
                begin: () =>
                {
                    if (Museum.PlayButtonPixel is Vector2 play)
                    {
                        Press(play, "the hall's play button");
                    }
                    else
                    {
                        isLeftOut = true;
                    }
                },
                hasComeAbout: waited =>
                {
                    if (isLeftOut || Museum.ShowsTheLivesCard)
                    {
                        isLeftOut = true;
                        return true;
                    }

                    if (Museum.StartBoosterLit != null && Museum.StartCardPlayPixel is Vector2 lit)
                    {
                        // The tutorial lights the start boosters one by one: a touch for each, as a player's, once the card has turned to the next.
                        if (Time.realtimeSinceStartup - _litPressed >= SettleSeconds)
                        {
                            Press(lit, "the card the start boosters are lit on");
                            _litPressed = Time.realtimeSinceStartup;
                        }

                        return false;
                    }

                    return waited >= SettleSeconds && Museum.StartCardPlayPixel != null;
                },
                end: () =>
                {
                    if (isLeftOut)
                    {
                        Findings.Add("the level was left out: the hall offered no level to open as it stands (no life, or a restored room)");
                        if (Museum.LivesCardClosePixel is Vector2 close)
                        {
                            Press(close, "the lives card's round button");
                        }
                    }
                }));
            Steps.Add(new Step(
                "the level's Oyna opened the board and the music played on",
                begin: () =>
                {
                    if (!isLeftOut)
                    {
                        Press(Museum.StartCardPlayPixel, "the level card's Oyna");
                    }
                },
                hasComeAbout: waited => isLeftOut || (waited >= SettleSeconds && Session.Board?.PauseButtonPixel != null),
                end: () =>
                {
                    if (!isLeftOut)
                    {
                        RequireTheMusicPlaysOn("the board opened");
                    }
                }));
            float pausePressed = float.NegativeInfinity;
            Steps.Add(new Step(
                "the pause disc asked whether to leave",
                begin: () => { },
                hasComeAbout: waited =>
                {
                    if (isLeftOut || (Session.Board?.IsAskingWhetherToLeave == true && Session.Stage.MuseumButtonPixel != null))
                    {
                        return true;
                    }

                    // A lesson of the tutorial on the board takes the first touch away and does nothing else with it: the disc is pressed again.
                    if (Time.realtimeSinceStartup - pausePressed >= SettleSeconds && Session.Board?.PauseButtonPixel is Vector2 pause)
                    {
                        Press(pause, "the shelf's pause disc");
                        pausePressed = Time.realtimeSinceStartup;
                    }

                    return false;
                },
                end: () => { }));
            Steps.Add(new Step(
                "the leaving card led back to the hall before any move and the music played on",
                begin: () =>
                {
                    if (!isLeftOut)
                    {
                        Press(Session.Stage.MuseumButtonPixel, "the leaving card's museum button");
                    }
                },
                hasComeAbout: waited => isLeftOut || (waited >= SettleSeconds && Session.Museum != null && Session.Museum.PlayButtonPixel != null),
                end: () =>
                {
                    if (!isLeftOut)
                    {
                        RequireTheMusicPlaysOn("the level was left for the hall");
                    }
                }));
        }

        /// <summary>The gear, then the music's switch off and on again, each kept in the device file at once and heard in a fade.</summary>
        private static void PlanTheSwitch()
        {
            Steps.Add(new Step(
                "the gear brought the settings card down with the music's switch on",
                begin: () => Press(Museum.GearPixel, "the wallet row's gear"),
                hasComeAbout: waited => waited >= SettleSeconds && Museum.SwitchPixel(DeviceSetting.Music) != null,
                end: () => Require(Museum.SwitchesShown.Contains((DeviceSetting.Music, true)), $"the card shows the switches {string.Join(", ", Museum.SwitchesShown)}")));
            Steps.Add(new Step(
                $"the music's switch turned the music off at once in the device file, and it faded out in about {MusicFade.Seconds} s and was held",
                begin: () =>
                {
                    RequireTheMusicPlaysOn("the settings card came down");
                    Press(Museum.SwitchPixel(DeviceSetting.Music), "the music's switch");
                    _switchedAt = Time.time;
                    Require(DeviceFileText.Contains("\"music\":false"), $"the switch did not keep the music off at once: {DeviceFileText}");
                },
                hasComeAbout: waited => !Music.IsPlaying,
                end: () =>
                {
                    RequireAFade("out", Time.time - _switchedAt);
                    Require(Music.Volume == 0f, $"the music was held at {Music.Volume} of full volume");
                    _heldAt = Music.Place;
                    Findings.Add($"held at {_heldAt}");
                }));
            Steps.Add(new Step(
                "the music stayed held while it was off",
                begin: () => { },
                hasComeAbout: waited => waited >= SettleSeconds,
                end: () => Require(!Music.IsPlaying && Music.Place.Equals(_heldAt), $"switched off, the music is {(Music.IsPlaying ? "playing" : "held")} at {Music.Place}, not held at {_heldAt}")));
            Steps.Add(new Step(
                $"the music's switch turned the music on at once in the device file, and it faded in from where it was held",
                begin: () =>
                {
                    Press(Museum.SwitchPixel(DeviceSetting.Music), "the music's switch");
                    _switchedAt = Time.time;
                    Require(DeviceFileText.Contains("\"music\":true"), $"the switch did not keep the music on at once: {DeviceFileText}");
                },
                hasComeAbout: waited => Music.IsPlaying && Music.Volume >= SoundLevels.Music,
                end: () =>
                {
                    RequireAFade("in", Time.time - _switchedAt);
                    Require(Music.Beginnings == 1, $"turned on again, the music began from its first note ({Music.Beginnings} beginnings)");
                    MusicPlace goneOnFrom = Music.Place;
                    Findings.Add($"went on from {goneOnFrom}");
                    RequireNotBefore(goneOnFrom, _heldAt, "turned on again");
                    _lastPlace = goneOnFrom;
                }));
            Steps.Add(new Step(
                "the settings card went up and the music played on",
                begin: () => Press(Museum.CardClosePixel, "the settings card's round button"),
                hasComeAbout: waited => waited >= SettleSeconds && !Museum.ShowsTheSettings,
                end: () => RequireTheMusicPlaysOn("the settings card went up")));
        }

        /// <summary>
        /// The game put away and brought back, as the phone tells the game's root: the music is held at once and fades
        /// in from there. (In the Editor the game's frames go on while it is "away", so it comes back at the next frame.)
        /// Then the intro runs into the loop, on the audio clock, and the music is still the one begun at the start.
        /// </summary>
        private static void PlanTheBackgroundAndTheLoop()
        {
            Steps.Add(new Step(
                "put away, the music was held at once; brought back, it faded in from where it was held",
                begin: () =>
                {
                    GameRoot root = _root ?? throw new InvalidOperationException("the running game has no root");
                    root.SendMessage("OnApplicationPause", true);
                    Require(!Music.IsPlaying && Music.Volume == 0f, $"put away, the music is {(Music.IsPlaying ? "playing" : "held")} at {Music.Volume} of full volume");
                    _heldAt = Music.Place;
                    Findings.Add($"put away at {_heldAt}");
                    root.SendMessage("OnApplicationPause", false);
                },
                hasComeAbout: waited => Music.IsPlaying && Music.Volume >= SoundLevels.Music,
                end: () =>
                {
                    Require(Music.Beginnings == 1, $"brought back, the music began from its first note ({Music.Beginnings} beginnings)");
                    RequireNotBefore(Music.Place, _heldAt, "brought back");
                    _lastPlace = Music.Place;
                }));
            Steps.Add(new Step(
                "the intro ran into the loop on the audio clock, and the music was still the one begun at the start",
                begin: () => { },
                hasComeAbout: waited => Music.Place.Part == MusicPart.Loop,
                end: () =>
                {
                    MusicPlace inTheLoop = Music.Place;
                    Findings.Add($"in the loop at {inTheLoop} ({inTheLoop.Sample / (double)Music.Clips.Loop.frequency:0.00} s into it)");
                    Require(Music.IsPlaying && Music.Beginnings == 1, $"in the loop the music is {(Music.IsPlaying ? "playing" : "held")} and began {Music.Beginnings} times");
                    Require(inTheLoop.Sample < Music.Clips.Loop.frequency, $"the loop was found {inTheLoop.Sample} samples in, more than a second after it began");
                    _lastPlace = inTheLoop;
                },
                longestWaitSeconds: 2f * (float)Music.Clips.IntroSecondsFrom(0)));
            Steps.Add(new Step(
                "the loop played on",
                begin: () => { },
                hasComeAbout: waited => waited >= SettleSeconds,
                end: () => RequireTheMusicPlaysOn("the loop had begun a moment before")));
        }

        /// <summary>
        /// Another app's music, played and stopped on the Editor's stand-in as the player would on the phone: the music
        /// gives way to it and comes back, without coming in and out between two songs, and the effects go on meanwhile.
        /// Then the same across the game put away and given the touches back, where the answer is followed at once.
        /// </summary>
        private static void PlanTheOtherAppsMusic()
        {
            Steps.Add(new Step(
                $"another app's music began: at the next question (every {MusicYield.AskEverySeconds} s) the music gave way, faded out and was held, its switch still on",
                begin: () => TurnTheOtherApp(isPlaying: true),
                hasComeAbout: waited => !Music.IsPlaying,
                end: () =>
                {
                    float took = Time.time - _otherAudioTurnedAt;
                    Findings.Add($"gave way {took:0.000} s after the other app began");
                    Require(Music.IsGivingWay && Music.Volume == 0f, $"held at {Music.Volume} of full volume, {(Music.IsGivingWay ? "giving way" : "not giving way")}");
                    Require(took <= MusicYield.AskEverySeconds + MusicFade.Seconds + QuestionSlackSeconds, $"the music gave way {took:0.000} s after the other app began");
                    Require(DeviceFileText.Contains("\"music\":true"), $"giving way turned the music's switch off: {DeviceFileText}");
                    _heldAt = Music.Place;
                }));
            Steps.Add(new Step(
                "the effects went on while the music gave way",
                begin: () => (Root.Cues ?? throw new InvalidOperationException("the running game has no cue player")).Sound(SoundCue.Press),
                hasComeAbout: waited => true,
                end: RequireThePressSounded));
            Steps.Add(new Step(
                "the music stayed held over two more questions while the other app played on",
                begin: () => { },
                hasComeAbout: waited =>
                {
                    RequireHeldWhileTheOtherAppPlays("the other app played on");
                    return waited >= 2f * MusicYield.AskEverySeconds + SettleSeconds;
                },
                end: () => { }));
            Steps.Add(new Step(
                $"a gap of {GapBetweenSongsSeconds} s between two songs of the other app did not bring the music in and out",
                begin: () => TurnTheOtherApp(isPlaying: false),
                hasComeAbout: waited =>
                {
                    RequireHeldWhileTheOtherAppPlays("the other app was between two songs");
                    return waited >= GapBetweenSongsSeconds;
                },
                end: () => TurnTheOtherApp(isPlaying: true)));
            Steps.Add(new Step(
                "the next song of the other app kept the music held",
                begin: () => { },
                hasComeAbout: waited =>
                {
                    RequireHeldWhileTheOtherAppPlays("the next song played");
                    return waited >= MusicYield.AskEverySeconds + SettleSeconds;
                },
                end: () => { }));
            Steps.Add(new Step(
                "the other app's music stopped: after two quiet answers the music came back, fading in from where it was held",
                begin: () => TurnTheOtherApp(isPlaying: false),
                hasComeAbout: waited => Music.IsPlaying && Music.Volume >= SoundLevels.Music,
                end: () =>
                {
                    float took = Time.time - _otherAudioTurnedAt;
                    Findings.Add($"came back {took:0.000} s after the other app stopped");
                    Require(
                        took >= MusicYield.AskEverySeconds && took <= 2f * MusicYield.AskEverySeconds + MusicFade.Seconds + QuestionSlackSeconds,
                        $"the music came back {took:0.000} s after the other app stopped, not after two quiet answers");
                    Require(Music.Beginnings == 1, $"come back, the music began from its first note ({Music.Beginnings} beginnings)");
                    RequireNotBefore(Music.Place, _heldAt, "come back");
                    _lastPlace = Music.Place;
                }));
            Steps.Add(new Step(
                "put away and brought back while another app's music played, the music stayed silent",
                begin: () =>
                {
                    TurnTheOtherApp(isPlaying: true);
                    Root.SendMessage("OnApplicationPause", true);
                    _heldAt = Music.Place;
                    Root.SendMessage("OnApplicationPause", false);
                    Require(Music.IsGivingWay, "brought back, the music did not ask at once whether another app played");
                },
                hasComeAbout: waited =>
                {
                    RequireHeldWhileTheOtherAppPlays("the game was brought back");
                    return waited >= SettleSeconds;
                },
                end: () => { }));
            Steps.Add(new Step(
                $"given the touches back once the other app had stopped, the music faded in at once, in about {MusicFade.Seconds} s",
                begin: () =>
                {
                    TurnTheOtherApp(isPlaying: false);
                    Root.SendMessage("OnApplicationFocus", true);
                    Require(!Music.IsGivingWay, "given the touches back, the music did not ask at once whether another app played");
                },
                hasComeAbout: waited => Music.IsPlaying && Music.Volume >= SoundLevels.Music,
                end: () =>
                {
                    RequireAFade("in", Time.time - _otherAudioTurnedAt);
                    Require(Music.Beginnings == 1, $"given the touches back, the music began from its first note ({Music.Beginnings} beginnings)");
                    RequireNotBefore(Music.Place, _heldAt, "given the touches back");
                    _lastPlace = Music.Place;
                }));
            Steps.Add(new Step(
                "with no other app playing the music played on",
                begin: () => { },
                hasComeAbout: waited => waited >= SettleSeconds,
                end: () => RequireTheMusicPlaysOn("the other app had stopped a moment before")));
        }

        private static void TurnTheOtherApp(bool isPlaying)
        {
            OtherApp.IsPlaying = isPlaying;
            _otherAudioTurnedAt = Time.time;
        }

        private static void RequireHeldWhileTheOtherAppPlays(string when) =>
            Require(
                Music.IsGivingWay && !Music.IsPlaying && Music.Volume == 0f && Music.Place.Equals(_heldAt),
                $"when {when} the music is {(Music.IsPlaying ? "playing" : "held")} at {Music.Place} and {Music.Volume} of full volume, not held at {_heldAt}");

        /// <summary>
        /// A press was handed to one of the effects' voices, at their level, with the listener on, while the music stayed
        /// held. Whether the voice is still playing is not asked: a press lasts less than the Editor may take between two
        /// ticks, and a batch-mode Editor may have no sound device (as PlayModeCheck says of every cue).
        /// </summary>
        private static void RequireThePressSounded()
        {
            ArtCatalogue art = AssetDatabase.LoadAssetAtPath<ArtCatalogue>(ArtCatalogueBuilder.CataloguePath);
            AudioClip press = art.SoundOf(SoundCue.Press) ?? throw new InvalidOperationException("the catalogue has no sound for a press");
            AudioSource? voice = Root.GetComponents<AudioSource>().FirstOrDefault(source => source.clip == press);
            Require(voice != null && Mathf.Approximately(voice.volume, SoundLevels.Effects), $"no effect's voice was given the press at the effects' level {SoundLevels.Effects}");
            Require(!AudioListener.pause && AudioListener.volume > 0f, $"the listener is {(AudioListener.pause ? "paused" : "on")} at volume {AudioListener.volume}");
            RequireHeldWhileTheOtherAppPlays("the press sounded");
        }

        /// <summary>
        /// The music plays, was begun once in all, has not given way to its own sound, and has gone on from where it was
        /// last seen (where the audio clock moves).
        /// </summary>
        private static void RequireTheMusicPlaysOn(string when)
        {
            MusicPlayer music = Music;
            Require(music.IsPlaying && music.Beginnings == 1, $"when {when} the music is {(music.IsPlaying ? "playing" : "held")} and began {music.Beginnings} times");
            Require(!music.IsGivingWay, $"when {when} the music gave way with no other app playing");
            MusicPlace now = music.Place;
            if (AudioSettings.dspTime > _audioClockAtTheStart)
            {
                Require(IsAfter(now, _lastPlace), $"when {when} the music is at {now}, not after {_lastPlace}: it did not play on");
            }
            else
            {
                Findings.Add($"when {when} the audio clock had not moved: this Editor has no sound output, so the music's place was not measured");
            }

            _lastPlace = now;
        }

        private static void RequireAFade(string way, float tookSeconds)
        {
            Findings.Add($"faded {way} in {tookSeconds:0.000} s of the game's clock");
            Require(
                Math.Abs(tookSeconds - MusicFade.Seconds) <= FadeSlackSeconds,
                $"the music faded {way} in {tookSeconds:0.000} s, not about {MusicFade.Seconds} s");
        }

        private static void RequireNotBefore(MusicPlace later, MusicPlace earlier, string when) =>
            Require(later.Equals(earlier) || IsAfter(later, earlier), $"{when}, the music is at {later}, before {earlier} where it was held: it began again");

        private static bool IsAfter(MusicPlace later, MusicPlace earlier) =>
            later.Part > earlier.Part || (later.Part == earlier.Part && later.Sample > earlier.Sample);

        private static void Press(Vector2? pixel, string what)
        {
            GameSession session = Session;
            session.PointerDown(pixel ?? throw new InvalidOperationException($"there is no {what} to press"));
            session.PointerUp();
        }

        private static void Require(bool holds, string otherwise)
        {
            if (!holds)
            {
                throw new InvalidOperationException(otherwise);
            }
        }

        private static void Fail(string why)
        {
            Debug.LogError($"Music check failed: {why}.");
            _failed = true;
            _current = Steps.Count;
        }

        private static void Finish()
        {
            EditorApplication.update -= Follow;
            SessionState.EraseBool(IsRunningKey);

            // As the check found it, also when it failed between turning the music off and on again.
            bool wasOn = !SessionState.GetBool(WasOffKey, false);
            DeviceFile device = DeviceFile.LoadOrMake(Application.persistentDataPath);
            if (device.Settings.Music != wasOn)
            {
                device.Keep(device.Settings.With(DeviceSetting.Music, isOn: wasOn));
            }

            SessionState.EraseBool(WasOffKey);
            Debug.Log($"Music check: {(_failed ? "FAILED" : "passed")}; {string.Join("; ", Findings)}.");
            EditorApplication.Exit(_failed ? 1 : 0);
        }

        /// <summary>One step of the check: what is done, what must then come about (given how long it has been waited for), and what is checked when it has.</summary>
        private sealed class Step
        {
            private readonly Action _begin;
            private readonly Func<float, bool> _hasComeAbout;
            private readonly Action _end;

            public Step(string what, Action begin, Func<float, bool> hasComeAbout, Action end, float longestWaitSeconds = MusicCheck.LongestWaitSeconds)
            {
                What = what;
                _begin = begin;
                _hasComeAbout = hasComeAbout;
                _end = end;
                LongestWaitSeconds = longestWaitSeconds;
            }

            public string What { get; }

            /// <summary>How long the step may take to come about before it has failed.</summary>
            public float LongestWaitSeconds { get; }

            public void Begin() => _begin();

            public bool HasComeAbout(float waitedSeconds) => _hasComeAbout(waitedSeconds);

            public void End() => _end();
        }
    }
}
