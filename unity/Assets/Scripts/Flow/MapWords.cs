#nullable enable
using System.Globalization;
using Puzzle.Client.Words;
using Puzzle.Museum;
using Puzzle.Museum.Catalogue;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// What the world map says (MapWorldTR, #4), in the language the game speaks (<see cref="Speech"/>): its plaque, its
    /// legend, the oceans and the compass, a painting's card and pin, the strip of eras, and what its buttons without
    /// words say to a screen reader. The eras' names and short names are the catalogue's in Turkish and the museum's
    /// table's in another language; a place, a museum and a note come from the map's own labels. No Unity dependency.
    /// </summary>
    public static class MapWords
    {
        public static string WorldTitle => Speech.Say(WordId.MapWorldTitle);

        /// <summary>The plaque's label in the view of the whole world: "20 DÖNEM · 100 TABLO".</summary>
        public static string WorldSummary =>
            Speech.Say(WordId.MapWorldSummary, ("eras", Number(MuseumPlan.RoomCount)), ("paintings", Number(MuseumPlan.PaintingCount)));

        /// <summary>The plaque's label in an era's view: "ODA 6 / 20 · 1847–1891", or without its years for an era with no dated painting.</summary>
        public static string EraLabel(MuseumRoom room) => room.Years is { } years
            ? Speech.Say(WordId.MapEraLabel, ("room", Number(room.Number.Value)), ("rooms", Number(MuseumPlan.RoomCount)), ("years", Years(years)))
            : MuseumWords.RoomOfRooms(room.Number);

        /// <summary>The years an era's paintings span, "1867–1906", or one year where they share it.</summary>
        public static string Years((int First, int Last) years) => years.First == years.Last
            ? Number(years.First)
            : $"{Number(years.First)}–{Number(years.Last)}";

        /// <summary>The plaque's label at the right of an era still ahead: "BÖLÜM 301'DE AÇILIR".</summary>
        public static string EraOpensAt(RoomNumber room)
        {
            int level = room.FirstLevel.Value;
            return Speech.Say(WordId.MapEraOpensAt, ("level", Number(level)), ("levelIn", TurkishNumbers.In(level).ToUpperInvariant()));
        }

        /// <summary>An era's name as the player reads it: its movement, in the language spoken.</summary>
        public static string EraName(MuseumRoom room) => MuseumWords.NameOf(room);

        /// <summary>An era's short name on the strip, "Edo ve Meiji".</summary>
        public static string EraShortName(MuseumRoom room) => Speech.Captions.ShortNameOf(room);

        public static string LegendRestored => Speech.Say(WordId.MapLegendRestored);

        public static string LegendHere => Speech.Say(WordId.MapLegendHere);

        public static string LegendAhead => Speech.Say(WordId.MapLegendAhead);

        public static string AtlanticOcean => Speech.Say(WordId.MapAtlanticOcean);

        public static string IndianOcean => Speech.Say(WordId.MapIndianOcean);

        public static string CompassNorth => Speech.Say(WordId.MapCompassNorth);

        /// <summary>The marker over the pin of the painting being restored: "Buradasın · Bölüm 287".</summary>
        public static string YouAreHere(int level) => MuseumWords.YouAreHere(level);

        /// <summary>A pin's badge and a card's place among its era's paintings: "7/10", "3/5".</summary>
        public static string OfAll(int some, int all) => $"{Number(some)}/{Number(all)}";

        /// <summary>The small capitals over a card's title: "ODA 6 · TABLO 4".</summary>
        public static string CardEyebrow(PaintingNumber painting) =>
            Speech.Say(WordId.MapCardEyebrow, ("room", Number(painting.Room.Value)), ("painting", Number(painting.PlaceInRoom)));

        /// <summary>A card's line of the painter and the year: "Claude Monet · 1867".</summary>
        public static string ByLine(Artwork artwork) => $"{Speech.Captions.ArtistOf(artwork)} · {Speech.Captions.YearOf(artwork)}";

        public static string Title(Artwork artwork) => Speech.Captions.TitleOf(artwork);

        /// <summary>A card's last line where the place has no note: the museum that holds the painting today.</summary>
        public static string Today(string museum) => Speech.Say(WordId.MapCardToday, ("museum", museum));

        public static string Veiled => Speech.Say(WordId.MapCardVeiled);

        public static string StatusRestored => Speech.Say(WordId.MapStatusRestored);

        /// <summary>The red pill of the painting being restored: "Parça 7 / 10 · Bölüm 287".</summary>
        public static string StatusPieces(int held, int all, int level) =>
            Speech.Say(WordId.MapStatusPieces, ("held", Number(held)), ("all", Number(all)), ("level", Number(level)));

        /// <summary>The cream pill of a painting still ahead: "Bölüm 261–270'te açılır".</summary>
        public static string StatusOpensAt(PaintingNumber painting)
        {
            int first = painting.FirstLevel.Value;
            int last = painting.LastLevel.Value;
            return Speech.Say(WordId.MapStatusOpensAt, ("first", Number(first)), ("last", Number(last)), ("lastIn", TurkishNumbers.In(last)));
        }

        /// <summary>The green button of the painting being restored: the card before its level.</summary>
        public static string Play => Speech.Say(WordId.Play);

        public static string SeeInTheCollection => Speech.Say(WordId.MapSeeInTheCollection);

        public static string ShowTheWorld => Speech.Say(WordId.MapShowTheWorld);

        public static string GoToYourPlace => Speech.Say(WordId.MapGoToYourPlace);

        public static string PreviousPainting => Speech.Say(WordId.MapPreviousPainting);

        public static string NextPainting => Speech.Say(WordId.MapNextPainting);

        public static string CloseTheCard => Speech.Say(WordId.MapCloseTheCard);

        public static string ErasList => Speech.Say(WordId.MapErasList);

        public static string BeingRestored => Speech.Say(WordId.MapBeingRestored);

        /// <summary>What a pin says to a screen reader: "Sainte-Adresse Plajı, Sainte-Adresse, Fransa. Onarıldı".</summary>
        public static string PinLabel(string title, string place, string status) =>
            Speech.Say(WordId.MapPinLabel, ("title", title), ("place", place), ("status", status));

        /// <summary>What an era's medallion says to a screen reader: "Oda 6: Japonya: Edo'nun Son Yılları ve Meiji, 1847–1891. 3 / 5 TABLO".</summary>
        public static string EraButtonLabel(MuseumRoom room, string status) => Speech.Say(
            WordId.MapEraButtonLabel,
            ("room", Number(room.Number.Value)),
            ("name", EraName(room)),
            ("years", room.Years is { } years ? Years(years) : string.Empty),
            ("status", status));

        private static string Number(int number) => number.ToString(CultureInfo.InvariantCulture);
    }
}
