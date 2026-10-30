using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Puzzle.Client.Engine;
using Puzzle.Core.Levels;
using UnityEditor;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// Checks from the command line that the engine runs inside Unity as it does in the tools: every packed level
    /// reads, the sample levels play exactly as tests/fixtures/engine-trace.txt says, and the game's hint (by the bot
    /// the economy settings name) shows the moves tests/fixtures/hint-trace.txt says on the same games.
    /// usage: Unity.exe -batchmode -quit -projectPath unity -executeMethod Puzzle.Client.EditorTools.EngineSmokeTest.Run
    /// </summary>
    public static class EngineSmokeTest
    {
        private const string LevelPackPath = "Assets/Generated/Resources/Levels.json";
        private const string SampleLevelsPath = "../levels/samples";
        private const string PinnedTracePath = "../tests/fixtures/engine-trace.txt";
        private const string ReportPath = "../out/unity/engine-trace-unity.txt";
        private const string PinnedHintsPath = "../tests/fixtures/hint-trace.txt";
        private const string HintsReportPath = "../out/unity/hint-trace-unity.txt";

        /// <summary>The bot the pinned hints are thought out by: the hint bot the shipped settings start with.</summary>
        private const string PinnedHintBot = "casual";

        public static void Run()
        {
            try
            {
                IReadOnlyList<LevelDefinition> levels = LevelPack.Read(File.ReadAllText(LevelPackPath));
                LevelDefinition[] samples = Directory.GetFiles(SampleLevelsPath, "*.json")
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .Select(path => LevelPack.ReadLevel(File.ReadAllText(path)))
                    .ToArray();
                string[] played = EngineTrace.OfSamples(samples);
                Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
                File.WriteAllLines(ReportPath, played);

                string[] pinned = File.ReadAllLines(PinnedTracePath);
                if (!played.SequenceEqual(pinned))
                {
                    throw new InvalidOperationException($"The engine played differently inside Unity; compare {ReportPath} with {PinnedTracePath}.");
                }

                string[] hinted = EngineTrace.HintsOfSamples(samples, PinnedHintBot);
                File.WriteAllLines(HintsReportPath, hinted);
                if (!hinted.SequenceEqual(File.ReadAllLines(PinnedHintsPath)))
                {
                    throw new InvalidOperationException($"The game's hints were thought out differently inside Unity; compare {HintsReportPath} with {PinnedHintsPath}.");
                }

                Debug.Log($"Engine smoke test passed: {levels.Count} levels read, {played.Length} pinned games identical, and their {hinted.Length} pinned runs of hints by {PinnedHintBot}.");
            }
            catch (Exception failure) when (failure is IOException || failure is LevelFormatException || failure is InvalidOperationException)
            {
                Debug.LogError($"Engine smoke test failed: {failure.Message}");
                EditorApplication.Exit(1);
            }
        }
    }
}
