namespace Puzzle.Museum
{
    /// <summary>Which way a painting hangs. It decides which way its jigsaw layout lies.</summary>
    public enum PaintingOrientation
    {
        /// <summary>At least as wide as it is tall; the layout's bands are rows.</summary>
        Landscape,

        /// <summary>Taller than wide; the layout is turned by a quarter and its bands are columns.</summary>
        Portrait,
    }
}
