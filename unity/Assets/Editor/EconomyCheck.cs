#nullable enable
using System;
using System.IO;
using Puzzle.Client.Engine;
using Puzzle.Meta;
using Puzzle.Meta.Settings;
using UnityEditor;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// Checks from the command line that the economy settings reach the game whole, with Unity's own JSON reader.
    /// The pack the project carries (Assets/Generated/Resources/Economy.json) must come back from the reader as it
    /// is written, character for character: the reader takes a field it does not know for nothing and a field the
    /// document lacks for 0, and says neither. The pack must be the one the repository's tests hold against the
    /// settings file and the simulator's reading of it (tests/fixtures/economy-pack.json). And the game must find
    /// it as a resource and make rules of it.
    /// usage: Unity.exe -batchmode -quit -projectPath unity -executeMethod Puzzle.Client.EditorTools.EconomyCheck.Run
    /// </summary>
    public static class EconomyCheck
    {
        private const string PackPath = "Assets/Generated/Resources/Economy.json";
        private const string PinnedPackPath = "../tests/fixtures/economy-pack.json";
        private const string ReportFolder = "../out/unity";
        private const string ReportPath = ReportFolder + "/economy-pack-unity.json";

        public static void Run()
        {
            try
            {
                string written = File.ReadAllText(PackPath);
                string held = AsUnityHoldsIt(written);
                Directory.CreateDirectory(ReportFolder);
                File.WriteAllText(ReportPath, held);
                ExpectHeldAsWritten(written, held);
                ExpectThePinnedPack(held);

                EconomyRules rules = EconomyPack.LoadPacked();
                Debug.Log($"Economy check passed: Unity's reader holds the pack as it is written ({written.Length} characters), it is the pinned pack, "
                    + $"and the game reads its rules from it: {Summary(rules)}.");
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException || failure is EconomyFormatException || failure is InvalidOperationException)
            {
                Debug.LogError($"Economy check failed: {failure.Message}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>A pack read by Unity's reader and written again by Unity's writer: everything the game can see of it, and nothing else.</summary>
        private static string AsUnityHoldsIt(string json) => JsonUtility.ToJson(EconomyPack.ReadFile(json));

        /// <exception cref="InvalidOperationException">Unity's reader dropped, added or changed something of the pack.</exception>
        private static void ExpectHeldAsWritten(string written, string held)
        {
            if (held != written)
            {
                throw new InvalidOperationException(
                    $"Unity's reader does not hold the economy pack as it is written: compare {ReportPath} with {PackPath}. "
                    + "A field of the pack that the file model lacks is lost, and one the pack lacks reads as 0.");
            }
        }

        /// <exception cref="InvalidOperationException">The project's pack is not the one the tests checked.</exception>
        private static void ExpectThePinnedPack(string held)
        {
            if (AsUnityHoldsIt(File.ReadAllText(PinnedPackPath)) != held)
            {
                throw new InvalidOperationException(
                    $"The economy pack in the project is not the pinned one ({PinnedPackPath}): after a change of economy/default.json run "
                    + "node tools/unity/economy-pack.js --pin and node tools/unity/sync-engine.js from the repository root.");
            }
        }

        private static string Summary(EconomyRules rules) =>
            $"{rules.Lives}; {rules.Win.PerMoveLeft} coins a move left; extra moves for {rules.ExtraMoves.Price} coins or one of {rules.ExtraMoves.FreeAdsPerDay} ads a day; "
            + $"{rules.StartBoosters.PerAttempt} start boosters an attempt; a new player holds {rules.StartingWallet}; "
            + $"{rules.Shop.BoosterSets.Count} sets and {rules.Shop.CoinPacks.Count} coin packs in the shop, {rules.Shop.AdForCoins.Coins} coins for one of {rules.Shop.AdForCoins.PerDay} ads a day; "
            + $"ads are {rules.Ads}";
    }
}
