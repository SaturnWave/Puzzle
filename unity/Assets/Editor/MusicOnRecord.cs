#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Puzzle.Client.Art;
using UnityEditor;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// The game's music for the art catalogue: the two clips tools/music/cut-loop.py writes into Assets/Art/Music, as the
    /// music's record (art/sounds/music.json) names them, with the licence on that record. The folder and the record
    /// must agree exactly, the licence must say who made the music and that it may be used, and each clip must come
    /// out of the import with exactly as many samples as its file holds (the loop is scheduled to begin on the intro's
    /// last sample); otherwise the catalogue is not built, and the reason is told by name.
    /// </summary>
    internal static class MusicOnRecord
    {
        /// <summary>Seen from the Unity project's folder.</summary>
        public const string RecordPath = "../art/sounds/music.json";

        private const string Cutter = "py tools/music/cut-loop.py";

        /// <exception cref="InvalidOperationException">The folder, the record and the clips do not agree, or the licence is not on record.</exception>
        /// <exception cref="IOException">The record or a clip's file cannot be read.</exception>
        public static MusicClips Clips()
        {
            Record record = Read();
            if (record.cut.introFile != MusicImport.IntroFile)
            {
                throw new InvalidOperationException($"{RecordPath} names the intro {record.cut.introFile}, but MusicImport keeps {MusicImport.IntroFile} in memory and streams every other clip; name them alike.");
            }

            string[] unlisted = Directory.GetFiles(MusicImport.MusicFolder)
                .Select(Path.GetFileName)
                .Where(name => !name.EndsWith(".meta") && name != record.cut.introFile && name != record.cut.loopFile)
                .ToArray();
            if (unlisted.Length > 0)
            {
                throw new InvalidOperationException($"{MusicImport.MusicFolder} holds files the music's record does not name: {string.Join(", ", unlisted)}.");
            }

            if (string.IsNullOrWhiteSpace(record.licence.account) || string.IsNullOrWhiteSpace(record.licence.generated) || string.IsNullOrWhiteSpace(record.licence.commercialUse))
            {
                throw new InvalidOperationException($"{RecordPath} does not say whose account made the music, when, and that it may be used commercially.");
            }

            AudioClip intro = ClipAt(record.cut.introFile);
            AudioClip loop = ClipAt(record.cut.loopFile);
            Debug.Log(
                $"Music {record.piece}, loop {record.cut.loop}: {Described(intro, record.cut.introFile)}; {Described(loop, record.cut.loopFile)}; "
                + $"made with {record.licence.madeWith} on {record.licence.account} on {record.licence.generated}; commercial use {record.licence.commercialUse}.");
            return new MusicClips(intro, loop);
        }

        /// <exception cref="IOException">The record cannot be read.</exception>
        private static Record Read()
        {
            if (!File.Exists(RecordPath))
            {
                throw new FileNotFoundException($"{RecordPath}, the music's record, is missing.", RecordPath);
            }

            return JsonUtility.FromJson<Record>(File.ReadAllText(RecordPath)) ?? throw new IOException($"{RecordPath} holds no record.");
        }

        /// <exception cref="InvalidOperationException">The clip is missing, was not imported as a sound, or lost or gained samples on import.</exception>
        /// <exception cref="IOException">The clip's file cannot be read.</exception>
        private static AudioClip ClipAt(string fileName)
        {
            string path = $"{MusicImport.MusicFolder}/{fileName}";
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
            {
                throw new InvalidOperationException($"{path} is missing or was not imported as a sound; run {Cutter} from the repository root.");
            }

            long framesInTheFile = WaveFrames(path);
            if (clip.samples != framesInTheFile)
            {
                throw new InvalidOperationException($"{path} holds {framesInTheFile} frames, and its import {clip.samples}: the loop would not begin on the intro's last sample.");
            }

            return clip;
        }

        private static string Described(AudioClip clip, string fileName)
        {
            var importer = (AudioImporter)AssetImporter.GetAtPath($"{MusicImport.MusicFolder}/{fileName}");
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            return $"{fileName} {clip.samples} samples ({clip.length:0.00} s), {clip.channels} channel(s) at {clip.frequency} Hz, "
                + $"{settings.loadType} {settings.compressionFormat} at {settings.quality:0.00}, {ImportedSize(importer)} for {EditorUserBuildSettings.activeBuildTarget}";
        }

        /// <summary>
        /// The size the clip takes in the package, as the import inspector shows it: AudioImporter keeps it in a
        /// property it does not make public, so it is read by its name and said to be unknown should a later Unity rename it.
        /// </summary>
        private static string ImportedSize(AudioImporter importer)
        {
            PropertyInfo? size = typeof(AudioImporter).GetProperty("compSize", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return size?.GetValue(importer) is int bytes ? $"{bytes / 1024.0 / 1024.0:0.00} MB imported" : "imported size not reported by this Unity";
        }

        /// <summary>How many frames a PCM WAV file holds: its data chunk's length over its frame's.</summary>
        /// <exception cref="IOException">The file is not a PCM WAV file.</exception>
        private static long WaveFrames(string path)
        {
            using var reader = new BinaryReader(File.OpenRead(path));
            if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "RIFF")
            {
                throw new IOException($"{path} is not a WAV file.");
            }

            reader.ReadUInt32();
            if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "WAVE")
            {
                throw new IOException($"{path} is not a WAV file.");
            }

            int frameBytes = 0;
            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                string chunk = Encoding.ASCII.GetString(reader.ReadBytes(4));
                uint length = reader.ReadUInt32();
                if (chunk == "fmt ")
                {
                    reader.ReadUInt16(); // the format
                    int channels = reader.ReadUInt16();
                    reader.ReadUInt32(); // the rate
                    reader.ReadUInt32(); // bytes a second
                    frameBytes = reader.ReadUInt16();
                    reader.BaseStream.Seek(length - 2 - 2 - 4 - 4 - 2, SeekOrigin.Current);
                    if (frameBytes <= 0 || channels <= 0)
                    {
                        throw new IOException($"{path} has no frames to count.");
                    }
                }
                else if (chunk == "data" && frameBytes > 0)
                {
                    return length / frameBytes;
                }
                else
                {
                    reader.BaseStream.Seek(length + (length & 1), SeekOrigin.Current);
                }
            }

            throw new IOException($"{path} has no sound data after its format.");
        }

#pragma warning disable IDE1006, CA1051 // public camelCase fields are the record's layout: Unity's JSON reader maps fields
        /// <summary>The music's record, as far as the catalogue reads it.</summary>
        [Serializable]
        private sealed class Record
        {
            public string piece = string.Empty;
            public Licence licence = new Licence();
            public Cut cut = new Cut();
        }

        [Serializable]
        private sealed class Licence
        {
            public string madeWith = string.Empty;
            public string account = string.Empty;
            public string generated = string.Empty;
            public string commercialUse = string.Empty;
        }

        [Serializable]
        private sealed class Cut
        {
            public string loop = string.Empty;
            public string introFile = string.Empty;
            public string loopFile = string.Empty;
        }
#pragma warning restore IDE1006, CA1051
    }
}
