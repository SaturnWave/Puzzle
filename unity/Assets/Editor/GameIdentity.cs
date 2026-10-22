namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// What the stores know the game by, the same for the Android package (AndroidBuild) and the iOS project
    /// (IosBuild). Placeholders: the game's name is decided last, and the application id follows it. No Unity dependency.
    /// </summary>
    internal static class GameIdentity
    {
        public const string CompanyName = "SaturnWave";

        /// <summary>The name under the icon on either phone.</summary>
        public const string ProductName = "Puzzle";

        /// <summary>Android's package name and iOS's bundle identifier.</summary>
        public const string ApplicationId = "com.saturnwave.puzzle";
    }
}
