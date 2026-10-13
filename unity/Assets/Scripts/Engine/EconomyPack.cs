#nullable enable
using System;
using Puzzle.Meta;
using Puzzle.Meta.Settings;
using UnityEngine;

namespace Puzzle.Client.Engine
{
    /// <summary>
    /// The economy's rules inside a built game: the game's part of economy/default.json as
    /// tools/unity/sync-engine.js packs it, read into the rules of lives, coins, boosters, rewards and the shop.
    /// The pack's file model is the meta library's own, which is laid out for Unity's JSON reader. The game holds
    /// no number of the economy besides this pack.
    /// </summary>
    public static class EconomyPack
    {
        /// <summary>Resources/Economy.json, named without its extension.</summary>
        private const string PackResource = "Economy";

        /// <summary>The rules the game carries with it.</summary>
        /// <exception cref="EconomyFormatException">The pack is missing or holds no settings the game can play by.</exception>
        public static EconomyRules LoadPacked()
        {
            TextAsset? packed = Resources.Load<TextAsset>(PackResource);
            if (packed == null)
            {
                throw new EconomyFormatException("The game carries no economy settings; run node tools/unity/sync-engine.js from the repository root.");
            }

            return Read(packed.text);
        }

        /// <exception cref="EconomyFormatException">The text holds no settings the game can play by.</exception>
        public static EconomyRules Read(string json) => EconomyFileMapper.ToRules(ReadFile(json));

        /// <summary>The pack as Unity's reader takes it in, unchecked: what <see cref="Read"/> makes the rules from.</summary>
        /// <exception cref="EconomyFormatException">The text is not JSON, or is empty.</exception>
        public static EconomyFile ReadFile(string json)
        {
            EconomyFile? file;
            try
            {
                file = JsonUtility.FromJson<EconomyFile>(json);
            }
            catch (ArgumentException notJson)
            {
                throw new EconomyFormatException($"The economy settings are not JSON: {notJson.Message}", notJson);
            }

            if (file == null)
            {
                throw new EconomyFormatException("The economy settings are empty.");
            }

            return file;
        }
    }
}
