using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// Brings everything the Editor derives up to date, in the order the parts need each other: the render pipeline,
    /// the atlas of the sprites, the art catalogue from the drawings, the typeface and the sounds (it lists every sound
    /// cue with its file, length and peak level), the scene that refers to the catalogue; it keeps Unity's own
    /// player statistics off, TextMesh Pro breaking Korean lines between words, and the game's icon in the player
    /// settings (<see cref="AppIcons"/>). Safe to run again at any time; run it after the drawings, the lists of drawings, the
    /// lettering styles, the sounds or the icon change (after node tools/unity/export-sprites.js, export-gallery-art.js
    /// or fetch-sounds.js, for instance).
    /// usage: Unity.exe -batchmode -quit -projectPath unity -executeMethod Puzzle.Client.EditorTools.ProjectSetup.Run
    /// </summary>
    public static class ProjectSetup
    {
        /// <summary>The player settings' field of Unity's own statistics.</summary>
        private const string StatisticsField = "submitAnalytics";

        /// <summary>TextMesh Pro's settings' field that breaks Korean lines between words, as Korean is written, rather than between any two syllables.</summary>
        private const string HangulLineBreakingField = "m_UseModernHangulLineBreakingRules";

        public static void Run()
        {
            try
            {
                BringUpToDate();
                Debug.Log("Project setup: the render pipeline, the art catalogue and the main scene are up to date.");
            }
            catch (Exception failure) when (failure is InvalidOperationException || failure is IOException)
            {
                Debug.LogError($"Project setup failed: {failure.Message}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// What <see cref="Run"/> does, for a tool that must not work with derived files older than their sources:
        /// the package build, which would otherwise carry a catalogue that lacks the drawings added since.
        /// </summary>
        /// <exception cref="InvalidOperationException">A drawing, a font file or a list of them does not fit the others, or a picture of the icon is missing; the message names it.</exception>
        /// <exception cref="IOException">A derived file could not be written.</exception>
        public static void BringUpToDate()
        {
            RenderPipelineSetup.Ensure();
            PieceAtlasBuilder.Ensure();
            ArtCatalogueBuilder.Build();
            PaintingSizesBuilder.Build();
            MainSceneBuilder.Build();
            SwitchOffUnitysStatistics();
            BreakKoreanLinesBetweenWords();
            AppIcons.Apply();
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Korean is written with spaces between its words and breaks its lines there; TextMesh Pro breaks it by
        /// default between any two syllables, as it breaks Chinese and Japanese, which splits a word over two lines
        /// (the privacy card's "무\n작위"). Its settings asset has a flag for the modern rule; the Editor sets it by
        /// the asset's field, as the settings window does.
        /// </summary>
        /// <exception cref="InvalidOperationException">TextMesh Pro's settings are missing or have no such field: check how this version breaks Korean lines.</exception>
        private static void BreakKoreanLinesBetweenWords()
        {
            TMP_Settings settings = Resources.Load<TMP_Settings>("TMP Settings")
                ?? throw new InvalidOperationException("TextMesh Pro's settings asset is missing; its own resources come with node tools/unity/import-text-resources.js.");
            var serialized = new SerializedObject(settings);
            SerializedProperty modernHangul = serialized.FindProperty(HangulLineBreakingField)
                ?? throw new InvalidOperationException($"TextMesh Pro's settings have no field {HangulLineBreakingField}; check how this version breaks Korean lines.");
            if (!modernHangul.boolValue)
            {
                modernHangul.boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(settings);
                Debug.Log("Project setup: TextMesh Pro breaks Korean lines between words.");
            }
        }

        /// <summary>
        /// Unity's own statistics of a built game (the hardware statistics the player would send to Unity) are
        /// switched off, so that the privacy card's sentence holds that this build sends nothing anywhere
        /// (unity/Assets/Scripts/Flow/PrivacyWords.cs). The package build runs this setup first, so no package
        /// is made with them on. The Editor has no public property for the flag; it is the player settings'
        /// field submitAnalytics, as ProjectSettings/ProjectSettings.asset writes it.
        /// </summary>
        /// <exception cref="InvalidOperationException">The player settings have no such field: Unity has renamed it, and the privacy text must be checked anew.</exception>
        private static void SwitchOffUnitysStatistics()
        {
            var playerSettings = new SerializedObject(Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings"));
            SerializedProperty statistics = playerSettings.FindProperty(StatisticsField)
                ?? throw new InvalidOperationException($"The player settings have no field {StatisticsField}; check how this Unity sends its statistics before the privacy card says it sends nothing.");
            if (statistics.boolValue)
            {
                statistics.boolValue = false;
                playerSettings.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("Project setup: Unity's player statistics are switched off.");
            }
        }
    }
}
