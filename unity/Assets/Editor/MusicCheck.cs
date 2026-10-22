#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// finger would press: the music begins once; it plays on, never begun again, while the pages of the museum turn
    /// (the map, the collection, the shop, the hall) and while a level is opened and left without a move; the
    /// settings card's music switch turns it off at once in the device file, the music fades out in about
    /// <see cref="MusicFade.Seconds"/> and is held where it was; turned on, the file says so at once, and the music
    /// fades back in from where it was held, not from its beginning. Put away and brought back, it is held at once and
    /// fades in from there; and at the intro's end it runs into the loop, still the music begun at the start.
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

        private static readonly List<Step> Steps = new List<Step>();
        private static int _current;
        private static float _stepBegan;
        private static bool _stepIsBegun;
        private static bool _failed;
        private static readonly List<string> Findings = new List<string>();

        private static GameRoot? _root;
        private static float? _playBegan;
        private static float _litPressed = float.NegativeInfinity;
        private static double _audioClockAtTheStart;
        private static MusicPlace _lastPlace;
        private static float _switchedAt;
        private static MusicPlace _heldAt;

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
                    _audioClockAtTheStart = AudioSettings.dspTime;
                    _lastPlace = Music.Place;
                    Findings.Add($"at the start the music is at {_lastPlace}, at {Music.Volume:0.00} of full volume");
                    Findings.Add(
                        $"in memory (the Editor's measure of the {EditorUserBuildSettings.activeBuildTarget} import): the intro "
                        + $"{Profiler.GetRuntimeMemorySizeLong(Music.Clips.Intro) / 1048576.0:0.00} MB, the streamed loop {Profiler.GetRuntimeMemorySizeLong(Music.Clips.Loop) / 1048576.0:0.00} MB");
                }));

            foreach (Destination page in new[] { Destination.Map, Destination.Collection, Destination.Shop, Destination.Home })
            {
                Steps.Add(new Step(
                    $"the page turned to {page} and the music played on",
                    begin: () => Press(Museum.PixelOf(page), $"the bar's place for {page}"),
                    hasComeAbout: waited => waited >= SettleSeconds && Session.Museum?.Showing == page,
                    end: () => RequireTheMusicPlaysOn($"the page turned to {page}")));
            }

            PlanTheLevel();
            PlanTheSwitch();
            PlanTheBackgroundAndTheLoop();
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

        /// <summary>The music plays, was begun once in all, and has gone on from where it was last seen (where the audio clock moves).</summary>
        private static void RequireTheMusicPlaysOn(string when)
        {
            MusicPlayer music = Music;
            Require(music.IsPlaying && music.Beginnings == 1, $"when {when} the music is {(music.IsPlaying ? "playing" : "held")} and began {music.Beginnings} times");
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
