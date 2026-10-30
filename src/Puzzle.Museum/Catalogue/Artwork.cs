namespace Puzzle.Museum.Catalogue
{
    /// <summary>
    /// A work of art the museum shows: what the player reads under its frame, and where its picture comes from.
    /// A <see cref="PaintingNumber"/> is a place in the museum; this is the work that hangs there once it is chosen.
    /// </summary>
    public sealed class Artwork
    {
        public Artwork(ArtworkId id, ArtworkCaption caption, PaintingOrientation orientation, string imageFile, ArtworkProvenance provenance)
        {
            Id = id;
            Caption = caption;
            Orientation = orientation;
            ImageFile = imageFile;
            Provenance = provenance;
        }

        public ArtworkId Id { get; }

        public ArtworkCaption Caption { get; }

        /// <summary>Which way the painting hangs; its picture file must lie the same way.</summary>
        public PaintingOrientation Orientation { get; }

        /// <summary>The picture, as a path below the paintings folder with forward slashes: "room-01/water-lilies.jpg".</summary>
        public string ImageFile { get; }

        public ArtworkProvenance Provenance { get; }

        public override string ToString() => $"{Caption.Title}, {Caption.Artist}, {Caption.Year}";
    }

    /// <summary>What the nameplate under a painting says.</summary>
    public sealed class ArtworkCaption
    {
        public ArtworkCaption(string title, string originalTitle, string artist, string year)
        {
            Title = title;
            OriginalTitle = originalTitle;
            Artist = artist;
            Year = year;
        }

        /// <summary>The title the player reads, in Turkish.</summary>
        public string Title { get; }

        /// <summary>The title the holding museum lists the work under: what a licence check searches for.</summary>
        public string OriginalTitle { get; }

        public string Artist { get; }

        /// <summary>A text, not a number: a work can span years ("1890–91").</summary>
        public string Year { get; }
    }

    /// <summary>
    /// Where a painting's picture comes from and why the game may show it. Every picture in the game has both
    /// recorded and is checked one by one (docs/GAME_DESIGN.md section 18).
    /// </summary>
    public sealed class ArtworkProvenance
    {
        public ArtworkProvenance(string source, string licence)
        {
            Source = source;
            Licence = licence;
        }

        /// <summary>The holding museum and the work's page there.</summary>
        public string Source { get; }

        public string Licence { get; }
    }
}
