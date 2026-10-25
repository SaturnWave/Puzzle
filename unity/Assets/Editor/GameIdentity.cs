namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// What the stores know the game by, the same for the Android package (AndroidBuild) and the iOS project
    /// (IosBuild). No Unity dependency.
    /// </summary>
    internal static class GameIdentity
    {
        public const string CompanyName = "SaturnWave";

        /// <summary>The name under the icon on either phone. A placeholder: the game's name is decided last.</summary>
        public const string ProductName = "Puzzle";

        /// <summary>
        /// Android's package name and iOS's bundle identifier, permanent (decided 2026-10-25): the stores know the game and
        /// its products by it (com.saturnwave.museumpuzzle.coins.pouch), and Xcode signs it with an explicit App ID that
        /// carries In-App Purchase. It stays when the name is decided.
        /// </summary>
        public const string ApplicationId = "com.saturnwave.museumpuzzle";
    }
}
