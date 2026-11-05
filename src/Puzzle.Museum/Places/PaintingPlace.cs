using System;
using Puzzle.Museum.Catalogue;

namespace Puzzle.Museum.Places
{
    /// <summary>How closely a painting's pin stands where the painting was made or what it shows. A closed set.</summary>
    public enum PlacePrecision
    {
        /// <summary>The very spot: a bridge, a beach, a house.</summary>
        Exact,

        /// <summary>The town or the city the painting was made in or shows.</summary>
        City,

        /// <summary>Only a region or a country is known.</summary>
        Region,
    }

    /// <summary>What a label of the map names, each kind in a list of its own in a language's file. A closed set.</summary>
    public enum LabelKind
    {
        /// <summary>The place a painting's pin stands on: "Giverny, Fransa".</summary>
        Place,

        /// <summary>A note on the painting's card that says more of its place: "Saint-Rémy'de yapıldı; Arles'daki odayı gösterir".</summary>
        Note,

        /// <summary>The museum that holds the painting today.</summary>
        Museum,
    }

    /// <summary>
    /// The key of a label in museum/places/&lt;code&gt;.json, with what it names: a place key cannot be looked up
    /// as a museum's.
    /// </summary>
    public readonly struct LabelKey : IEquatable<LabelKey>
    {
        private readonly string? _value;

        /// <exception cref="ArgumentException">The key is empty or holds a space.</exception>
        public LabelKey(LabelKind kind, string value)
        {
            if (string.IsNullOrEmpty(value) || value.Contains(' '))
            {
                throw new ArgumentException($"A {kind} label's key is a word without spaces, not \"{value}\".", nameof(value));
            }

            Kind = kind;
            _value = value;
        }

        public LabelKind Kind { get; }

        /// <summary>Empty only for the default value, which names nothing.</summary>
        public string Value => _value ?? string.Empty;

        public bool Equals(LabelKey other) => Kind == other.Kind && string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object? obj) => obj is LabelKey other && Equals(other);

        public override int GetHashCode() => ((int)Kind * 397) ^ StringComparer.Ordinal.GetHashCode(Value);

        public override string ToString() => $"{Kind} {Value}";

        public static bool operator ==(LabelKey left, LabelKey right) => left.Equals(right);

        public static bool operator !=(LabelKey left, LabelKey right) => !left.Equals(right);
    }

    /// <summary>
    /// Where a painting's pin stands on the world map: where it was made, or the place it shows (#4,
    /// docs/GAME_DESIGN.md section 19), and the museum that holds it today, which its card names.
    /// </summary>
    public sealed class PaintingPlace
    {
        public PaintingPlace(ArtworkId artwork, GeoPoint point, LabelKey place, PlacePrecision precision, bool isVerified, LabelKey museum, LabelKey? note)
        {
            if (place.Kind != LabelKind.Place || museum.Kind != LabelKind.Museum || (note is LabelKey given && given.Kind != LabelKind.Note))
            {
                throw new ArgumentException($"The labels of {artwork}'s place are of the wrong kinds: {place}, {museum}, {note}.");
            }

            Artwork = artwork;
            Point = point;
            Place = place;
            Precision = precision;
            IsVerified = isVerified;
            Museum = museum;
            Note = note;
        }

        public ArtworkId Artwork { get; }

        public GeoPoint Point { get; }

        public LabelKey Place { get; }

        public PlacePrecision Precision { get; }

        /// <summary>True once the place was checked against Wikidata or the holding museum's own page.</summary>
        public bool IsVerified { get; }

        public LabelKey Museum { get; }

        /// <summary>A note that says more of the place; nothing for most paintings, whose card names the museum instead.</summary>
        public LabelKey? Note { get; }

        /// <summary>Where the pin stands on the map.</summary>
        public MapPoint OnTheMap => NaturalEarthProjection.Project(Point);
    }
}
