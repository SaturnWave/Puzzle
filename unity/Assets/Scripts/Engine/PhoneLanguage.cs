#nullable enable
using Puzzle.Client.Words;
using UnityEngine;

namespace Puzzle.Client.Engine
{
    /// <summary>
    /// The language the phone is set to, as the game reads it: on Android its own locale's tag ("pt-BR",
    /// "zh-Hant-TW"), which tells Taiwan's and Hong Kong's Chinese from the mainland's; elsewhere (the Editor, a
    /// desktop) the language Unity reports. <see cref="Languages.FromPhone"/> says which of the game's languages that is.
    /// </summary>
    public static class PhoneLanguage
    {
        /// <summary>The phone's language among the game's; English for one the game is not made to speak.</summary>
        public static Language OfThisDevice() => Languages.FromPhone(Tag());

        /// <summary>The phone's language as a BCP 47 tag.</summary>
        public static string Tag()
        {
            if (Application.platform == RuntimePlatform.Android)
            {
                try
                {
                    using var locales = new AndroidJavaClass("java.util.Locale");
                    using AndroidJavaObject locale = locales.CallStatic<AndroidJavaObject>("getDefault");
                    return locale.Call<string>("toLanguageTag");
                }
                catch (AndroidJavaException failure)
                {
                    Debug.LogWarning($"The phone's locale cannot be read, so the game goes by the language Unity reports: {failure.Message}");
                }
            }

            return TagOf(Application.systemLanguage);
        }

        /// <summary>Unity's name of a language as a tag; one the game is not made to speak becomes an empty tag, which reads as English.</summary>
        private static string TagOf(SystemLanguage language) => language switch
        {
            SystemLanguage.Turkish => "tr",
            SystemLanguage.English => "en",
            SystemLanguage.Korean => "ko",
            SystemLanguage.Japanese => "ja",
            SystemLanguage.German => "de",
            SystemLanguage.French => "fr",
            SystemLanguage.Spanish => "es",
            SystemLanguage.Portuguese => "pt",
            SystemLanguage.Italian => "it",
            SystemLanguage.Russian => "ru",
            SystemLanguage.Indonesian => "id",
            SystemLanguage.ChineseTraditional => "zh-Hant",
            SystemLanguage.ChineseSimplified => "zh-Hans",
            SystemLanguage.Chinese => "zh",
            _ => string.Empty,
        };
    }
}
