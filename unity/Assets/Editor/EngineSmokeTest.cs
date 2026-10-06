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
    /// reads, and the sample levels play exactly as tests/fixtures/engine-trace.txt says.
    /// usage: Unity.exe -batchmode -quit -projectPath unity -executeMethod Puzzle.Client.EditorTools.EngineSmokeTest.Run
    /// </summary>
    public static class EngineSmokeTest
    {
        private const string LevelPackPath = "Assets/Generated/Resources/Levels.json";
        private const string SampleLevelsPath = "../levels/samples";
        private const string PinnedTracePath = "../tests/fixtures/engine-trace.txt";
        private const string ReportPath = "../out/unity/engine-trace-unity.txt";

        public static void Run()
        {
            try
            {
                IReadOnlyList<LevelDefinition> levels = LevelPack.Read(File.ReadAllText(LevelPackPath));
                string[] played = EngineTrace.OfSamples(Directory.GetFiles(SampleLevelsPath, "*.json")
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .Select(path => LevelPack.ReadLevel(File.ReadAllText(path))));
                Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
                File.WriteAllLines(ReportPath, played);

                string[] pinned = File.ReadAllLines(PinnedTracePath);
                if (!played.SequenceEqual(pinned))
                {
                    throw new InvalidOperationException($"The engine played differently inside Unity; compare {ReportPath} with {PinnedTracePath}.");
                }

                Debug.Log($"Engine smoke test passed: {levels.Count} levels read, {played.Length} pinned games identical.");
            }
            catch (Exception failure) when (failure is IOException || failure is LevelFormatException || failure is InvalidOperationException)
            {
                Debug.LogError($"Engine smoke test failed: {failure.Message}");
                EditorApplication.Exit(1);
            }
        }
    }
}
