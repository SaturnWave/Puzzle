#nullable enable
using System;
using Puzzle.Client.Flow;

namespace Puzzle.Client.Statistics
{
    /// <summary>
    /// Where the notes of the play statistics are handed to be sent: the service's package in the game
    /// (AptabaseNotes), the device's log where nothing may be sent (<see cref="NotesToTheLog"/>), a recording in the
    /// Editor's checks.
    /// </summary>
    public interface IStatisticsSink
    {
        /// <summary>Hands a note over to be sent. Called only while the player allows the notes.</summary>
        void Send(PlayNote note);

        /// <summary>
        /// Turns the sending on or off. Off, nothing the sink holds leaves the phone, and nothing new is handed to it;
        /// on again, what it held goes with what comes, all of it noted while the player allowed it.
        /// </summary>
        void Turn(bool isOn);
    }

    /// <summary>
    /// The gate of the play statistics: a note goes to its sink only while the player's answer, as the device file
    /// keeps it, is "İzin ver" (<see cref="StatisticsChoice.Allowed"/>). Before the question is answered, after "İzin
    /// verme", and from the moment the settings card's switch is turned off, nothing is handed over and the sink is
    /// turned off. The gate is in the game's own code, not in the service's package: the package queues a note it is
    /// handed while it is turned off and sends it once turned on again (aptabase-unity 0.3.1, Aptabase.TrackEvent), so
    /// it is never handed one that the player has not allowed. Once a run, as soon as the notes are allowed, the
    /// game's start is noted first (<see cref="PlayNote.GameStarted"/>).
    /// </summary>
    /// <remarks>
    /// The game's own gate asks the question once, on the first start (<see cref="AsksOn"/>); the Editor's tools have
    /// one that never asks and drops every note (<see cref="Nowhere"/>), so their sessions look as they did before the
    /// statistics, and the statistics' check has one that asks and records (unity/Assets/Editor/ConsentCheck.cs).
    /// No Unity dependency.
    /// </remarks>
    public sealed class PlayStatistics
    {
        private readonly IStatisticsSink _sink;
        private readonly bool _asks;

        /// <summary>Whether the sink was last turned on; nothing before the gate first followed the player's answer.</summary>
        private bool? _isTurnedOn;

        private bool _hasNotedTheStart;

        private PlayStatistics(IStatisticsSink sink, bool asks)
        {
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _asks = asks;
        }

        /// <summary>The Editor's tools' gate: it never asks the question, and the notes it lets through go nowhere.</summary>
        public static PlayStatistics Nowhere => new PlayStatistics(new DroppedNotes(), asks: false);

        /// <summary>The game's gate: it asks the question while it is not answered, and hands the notes the player allows to the sink.</summary>
        public static PlayStatistics Through(IStatisticsSink sink) => new PlayStatistics(sink, asks: true);

        /// <summary>True when the question is to be asked: the player has not answered it, and this gate asks.</summary>
        public bool AsksOn(DeviceSettings settings) => _asks && settings.Statistics == StatisticsChoice.NotAsked;

        /// <summary>
        /// Follows the player's answer as it is now: the sink is turned on or off where it was not already, and once the
        /// notes are allowed, the run's start is noted, once. Call it when the session opens and after every change of the settings.
        /// </summary>
        public void Follow(DeviceSettings settings)
        {
            bool isAllowed = IsAllowedBy(settings);
            if (_isTurnedOn != isAllowed)
            {
                _sink.Turn(isAllowed);
                _isTurnedOn = isAllowed;
            }

            if (isAllowed && !_hasNotedTheStart)
            {
                _hasNotedTheStart = true;
                _sink.Send(new PlayNote.GameStarted());
            }
        }

        /// <summary>Hands the note to the sink if the player allows the notes now; otherwise it is forgotten here, and goes nowhere.</summary>
        public void Note(PlayNote note, DeviceSettings settings)
        {
            if (note == null)
            {
                throw new ArgumentNullException(nameof(note));
            }

            Follow(settings);
            if (IsAllowedBy(settings))
            {
                _sink.Send(note);
            }
        }

        private static bool IsAllowedBy(DeviceSettings settings) => settings.Statistics == StatisticsChoice.Allowed;

        /// <summary>The tools' sink: what it is handed is dropped.</summary>
        private sealed class DroppedNotes : IStatisticsSink
        {
            public void Send(PlayNote note)
            {
            }

            public void Turn(bool isOn)
            {
            }
        }
    }

    /// <summary>
    /// A sink that only writes each note to the device's log, with why it was not sent: the Editor's play mode, which
    /// sends nothing whatever the key, and a build whose settings file holds no key the game sends through. No Unity
    /// dependency: the log is the caller's.
    /// </summary>
    public sealed class NotesToTheLog : IStatisticsSink
    {
        private readonly Action<string> _log;
        private readonly string _whyNotSent;

        /// <param name="log">Where a line goes: <c>Debug.Log</c> in the game.</param>
        /// <param name="whyNotSent">Why nothing leaves: "the Editor sends nothing".</param>
        public NotesToTheLog(Action<string> log, string whyNotSent)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _whyNotSent = whyNotSent ?? throw new ArgumentNullException(nameof(whyNotSent));
        }

        public void Send(PlayNote note) => _log($"Statistics: {note}, not sent ({_whyNotSent}).");

        public void Turn(bool isOn) => _log($"Statistics: turned {(isOn ? "on" : "off")}; nothing is sent ({_whyNotSent}).");
    }
}
