#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Client.Words;
using Puzzle.Museum.Catalogue;
using Puzzle.Museum.Places;
using UnityEngine;

namespace Puzzle.Client.Engine
{
    /// <summary>
    /// The world map's data a built game carries (#4): where each painting's pin stands, museum/places.json, and each
    /// language's labels for the places, the museums and the notes, museum/places/&lt;code&gt;.json, as
    /// tools/unity/sync-engine.js packs them into Resources/Places.json and Resources/PlaceLabels; and the map's
    /// coastlines, Resources/WorldMap.bytes, which the CLI's <c>worldmap build</c> writes. A painting without a place
    /// stops the map from being built; a label is said in the language spoken, else in English, else in the source,
    /// Turkish (<see cref="PlaceLabels"/>). What is read is kept for the next map: the places and the coastlines once,
    /// the labels once per language spoken.
    /// </summary>
    public static class PlacesPack
    {
        private const string PlacesResource = "Places";
        private const string LabelsFolder = "PlaceLabels";
        private const string WorldMapResource = "WorldMap";
        private const string HowToPack = "run node tools/unity/sync-engine.js from the repository root";

        /// <summary>The language the source labels are written in; the places name their keys there.</summary>
        private const Language SourceLanguage = Language.Turkish;

        /// <summary>The language asked after the one spoken, as the game speaks English where it has not the phone's.</summary>
        private const Language FallbackLanguage = Language.English;

        private static (MuseumCatalogue Catalogue, MuseumPlaces Places)? _places;
        private static (Language Language, PlaceLabels Labels)? _labels;
        private static WorldMapShape? _worldMap;

        /// <summary>Where each painting of a catalogue stands on the map.</summary>
        /// <exception cref="PlacesFormatException">The game carries no places, or a painting of the catalogue has none.</exception>
        public static MuseumPlaces PlacesOf(MuseumCatalogue catalogue)
        {
            if (_places is { } read && ReferenceEquals(read.Catalogue, catalogue))
            {
                return read.Places;
            }

            PlacesFile file = ReadJson<PlacesFile>(PlacesResource) ?? throw new PlacesFormatException($"The game carries no places for the world map; {HowToPack}.");
            MuseumPlaces places = PlacesFileMapper.ToPlaces(file, catalogue);
            _places = (catalogue, places);
            return places;
        }

        /// <summary>The map's labels in a language: its own where the game carries them, else English's, else the source's.</summary>
        /// <exception cref="PlacesFormatException">The game carries no source labels, or they lack a key the places name, or a table is not one.</exception>
        public static PlaceLabels LabelsIn(Language language, MuseumPlaces places)
        {
            if (_labels is { } read && read.Language == language)
            {
                return read.Labels;
            }

            PlaceLabelTable source = TableOf(SourceLanguage) ?? throw new PlacesFormatException($"The game carries no {Languages.CodeOf(SourceLanguage)} labels for the world map; {HowToPack}.");
            var askedFirst = new List<PlaceLabelTable>();
            foreach (string code in PlaceLabels.AskedBeforeTheSource(Languages.CodeOf(language), Languages.CodeOf(FallbackLanguage), Languages.CodeOf(SourceLanguage)))
            {
                if (Languages.FromCode(code) is Language asked && TableOf(asked) is PlaceLabelTable table)
                {
                    askedFirst.Add(table);
                }
            }

            PlaceLabels labels = PlaceLabels.Of(places, source, askedFirst);
            _labels = (language, labels);
            return labels;
        }

        /// <summary>The coastlines and lakes the map draws.</summary>
        /// <exception cref="WorldMapFormatException">The game carries no map data, or not data of this version.</exception>
        public static WorldMapShape WorldMap()
        {
            if (_worldMap != null)
            {
                return _worldMap;
            }

            TextAsset? packed = Resources.Load<TextAsset>(WorldMapResource);
            if (packed == null)
            {
                throw new WorldMapFormatException("The game carries no world map; run `dotnet run --project src/Puzzle.Cli -- worldmap build` from the repository root.");
            }

            _worldMap = WorldMapFormat.Read(packed.bytes);
            Resources.UnloadAsset(packed);
            return _worldMap;
        }

        /// <summary>Every label of a language's table for the map, as the game carries it; none where it carries no table: what the typefaces must be able to write.</summary>
        /// <exception cref="PlacesFormatException">The table the game carries is not one.</exception>
        public static IEnumerable<string> EveryLabelIn(Language language) => TableOf(language)?.AllWords ?? Array.Empty<string>();

        private static PlaceLabelTable? TableOf(Language language) =>
            ReadJson<PlaceLabelsFile>($"{LabelsFolder}/{Languages.CodeOf(language)}") is PlaceLabelsFile file ? PlacesFileMapper.ToTable(file) : null;

        /// <exception cref="PlacesFormatException">The resource is there but is not JSON.</exception>
        private static T? ReadJson<T>(string resource)
            where T : class
        {
            TextAsset? packed = Resources.Load<TextAsset>(resource);
            if (packed == null)
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<T>(packed.text);
            }
            catch (ArgumentException notJson)
            {
                throw new PlacesFormatException($"Resources/{resource}.json is not JSON: {notJson.Message}", notJson);
            }
        }
    }
}
