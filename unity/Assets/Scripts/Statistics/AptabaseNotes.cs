#nullable enable
using System.Collections.Generic;
using AptabaseSDK;
using UnityEngine;

namespace Puzzle.Client.Statistics
{
    /// <summary>
    /// The sink of the play statistics in a built game whose settings file holds a key of the European region: Aptabase's
    /// own package (com.aptabase 0.3.1, MIT), which batches the notes and sends them once a minute to
    /// https://eu.aptabase.com. What it adds to each note is its own: the time, a session number of its own that lives
    /// in memory only, the game's version, the operating system and its version, the phone's locale and whether the
    /// build is a development one; the service works out the country from the connection and keeps no IP address
    /// (docs/GAME_DESIGN.md §20). Its crash reporting stays off in the settings file, and nothing else of it is called:
    /// the gate (<see cref="PlayStatistics"/>) decides what it is handed and when it is turned on.
    /// </summary>
    public sealed class AptabaseNotes : IStatisticsSink
    {
        /// <summary>The package's settings file, which it reads itself at the start: unity/Assets/Resources/AptabaseSettings.asset.</summary>
        public const string SettingsResource = "AptabaseSettings";

        /// <summary>
        /// The key in the package's settings file, read as the game reads it; no key where the build carries no such file.
        /// </summary>
        public static StatisticsKey KeyOfThisBuild()
        {
            Settings? settings = Resources.Load<Settings>(SettingsResource);
            return StatisticsKey.Read(settings != null ? settings.AppKey : null);
        }

        public void Send(PlayNote note)
        {
            // The package takes a dictionary it could change, and copies it into one of its own; the note's stay read-only.
            var properties = new Dictionary<string, object>();
            foreach (KeyValuePair<string, object> property in note.Properties)
            {
                properties.Add(property.Key, property.Value);
            }

            Aptabase.TrackEvent(note.Name, properties);
        }

        public void Turn(bool isOn) => Aptabase.SetEnabled(isOn);
    }
}
