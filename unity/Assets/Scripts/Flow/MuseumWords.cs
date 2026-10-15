#nullable enable
using System;
using System.Globalization;
using Puzzle.Museum;
using Puzzle.Museum.Catalogue;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// What the museum's screens say, in the player's language. The words are the design canvas' (HomeTR, MapTR,
    /// WinTR) where it has them. No Unity dependency.
    /// </summary>
    public static class MuseumWords
    {
        /// <summary>The names of the navigation bar's destinations.</summary>
        public const string ShopTab = "Mağaza";
        public const string HomeTab = "Müze";
        public const string MapTab = "Harita";
        public const string CollectionTab = "Koleksiyon";

        public const string CollectionTitle = "Koleksiyon";

        /// <summary>The jigsaw's screen: the counter's label, the label of a painting that is whole, and the hints on the tray.</summary>
        public const string PieceLabel = "PARÇA";
        public const string RestoredLabel = "TAMAMLANDI";
        public const string DragThePiece = "Parçayı yerine sürükle";
        public const string DragThePieces = "Parçaları yerine sürükle";
        public const string NoPieceWaits = "Yeni parça için bölüm kazan";
        public const string PaintingRestored = "Tablo tamamlandı!";

        /// <summary>The jigsaw's buttons: the one that places every waiting piece, and the one that leaves when nothing waits.</summary>
        public const string PlaceThemAll = "Otomatik tamamla";
        public const string Done = "Tamam";

        /// <summary>The home's button that leads to the jigsaw of the painting in the frame.</summary>
        public const string ToTheJigsaw = "Tablo";

        /// <summary>The line under a painting that is looked at by itself.</summary>
        public const string TouchToClose = "Kapatmak için dokun";

        public const string MapTitle = "Müze Haritası";
        public const string Entrance = "GİRİŞ";

        /// <summary>The button of a result's card that leads back to the museum's hall.</summary>
        public const string BackToTheMuseum = "Müze";

        /// <summary>The second line of the plate under a frame whose painting is not chosen yet.</summary>
        public const string PaintingToCome = "HAZIRLANIYOR";

        /// <summary>On the map, the pill of a room no work is chosen for yet; and the name such a room goes by while its movement is only pencilled in.</summary>
        public const string RoomToCome = "Hazırlanıyor";
        public const string RoomWithoutAName = "Yeni oda";

        /// <summary>The hall's note when nothing is left to restore: more paintings are on their way, or the museum is whole.</summary>
        public const string MorePaintingsToCome = "Yeni tablolar hazırlanıyor";
        public const string MuseumRestored = "Müzenin tamamı onarıldı!";

        /// <summary>The play button once every level is won: the last one again.</summary>
        public const string PlayAgain = "Tekrar oyna";

        /// <summary>The banner over a room whose fifth painting has just become whole, and the button that leads on from it.</summary>
        public const string RoomRestored = "Oda onarıldı!";
        public const string OnFromTheRoom = "Devam";

        /// <summary>The map's plaque: what the museum holds in all.</summary>
        public static string MapSummary =>
            $"{Number(MuseumPlan.RoomCount)} ODA · {Number(MuseumPlan.PaintingCount)} TABLO";

        /// <summary>A room among the museum's rooms: "ODA 1 / 20".</summary>
        public static string RoomOfRooms(RoomNumber room) => $"ODA {Number(room.Value)} / {Number(MuseumPlan.RoomCount)}";

        /// <summary>How many of a room's paintings hang whole again, on the room's plaque: "4 / 5 ONARILDI".</summary>
        public static string Restored(int paintingsWhole) => $"{Number(paintingsWhole)} / {Number(MuseumPlan.PaintingsPerRoom)} ONARILDI";

        /// <summary>The same count on the map: "4 / 5 TABLO".</summary>
        public static string PaintingsWhole(int paintingsWhole) => $"{Number(paintingsWhole)} / {Number(MuseumPlan.PaintingsPerRoom)} TABLO";

        /// <summary>A room's name as the player reads it: its movement once that is decided, and until then only that it is a new room.</summary>
        public static string NameOf(MuseumRoom room) => room.IsNameProvisional ? RoomWithoutAName : room.Movement;

        /// <summary>Under the banner of a restored room: what comes next. The next room by its name, or that it is being prepared, or that the museum is whole.</summary>
        public static string AfterTheRoom(RoomNumber restored, MuseumCatalogue catalogue)
        {
            if (restored.Value == MuseumPlan.RoomCount)
            {
                return MuseumRestored;
            }

            MuseumRoom next = catalogue.Room(new RoomNumber(restored.Value + 1));
            return next.Artworks.Count == 0 ? MorePaintingsToCome : $"Sıradaki oda: {NameOf(next)}";
        }

        /// <summary>The line under the banner of a restored room: the room and its paintings, "Empresyonizm · 5 / 5 tablo".</summary>
        public static string RoomAndItsPaintings(string roomName) =>
            $"{roomName} · {Number(MuseumPlan.PaintingsPerRoom)} / {Number(MuseumPlan.PaintingsPerRoom)} tablo";

        /// <summary>The play button: the level it opens.</summary>
        public static string Play(int levelNumber) => $"Bölüm {Number(levelNumber)}";

        /// <summary>The label under the map's pin.</summary>
        public static string YouAreHere(int levelNumber) => $"Buradasın · Bölüm {Number(levelNumber)}";

        /// <summary>When a locked room opens: "Bölüm 51'de açılır", the level being the first one of the room.</summary>
        public static string OpensAt(RoomNumber room) => $"Bölüm {TurkishNumbers.In(room.FirstLevel.Value)} açılır";

        /// <summary>Several rooms as one stretch of the map: "7–20".</summary>
        public static string Rooms(RoomNumber first, RoomNumber last) =>
            first == last ? Number(first.Value) : $"{Number(first.Value)}–{Number(last.Value)}";

        /// <summary>The first line of the plate under a painting's frame: its title, or its place while no work is chosen for it.</summary>
        public static string TitleOf(PaintingNumber painting, Artwork? artwork) =>
            artwork?.Caption.Title ?? $"{Number(painting.Value)}. tablo";

        /// <summary>
        /// The second line of the plate: the painter and the year in capitals. A painter's name is not a Turkish
        /// word, so its i stays an undotted capital I, as the painter wrote it.
        /// </summary>
        public static string ByLineOf(Artwork? artwork) => artwork == null
            ? PaintingToCome
            : $"{artwork.Caption.Artist.ToUpperInvariant()} · {artwork.Caption.Year}";

        /// <summary>What a won level gave, on its result's card: "Yeni parça: 7 / 10", or the pieces when it gave several.</summary>
        public static string Earned(PieceReward reward) => $"{EarnedLead(reward)} {EarnedCount(reward)}";

        /// <summary>The words of <see cref="Earned"/> before its figures, which the card sets in another typeface: "Yeni parça:".</summary>
        public static string EarnedLead(PieceReward reward) => reward.Pieces.Count == 1 ? "Yeni parça:" : $"{Number(reward.Pieces.Count)} yeni parça:";

        /// <summary>The figures of <see cref="Earned"/>: how many of the painting's pieces the player has with these, of all, "7 / 10".</summary>
        public static string EarnedCount(PieceReward reward) => $"{Number(reward.WonWithThem)} / {Number(reward.PieceCount)}";

        /// <summary>The collection's plaque: how many of the museum's paintings hang whole, "4 / 100 TABLO".</summary>
        public static string CollectionCount(int paintingsWhole) => $"{Number(paintingsWhole)} / {Number(MuseumPlan.PaintingCount)} TABLO";

        /// <summary>The piece counter: the pieces in place of the pieces a painting has, "7 / 10".</summary>
        public static string PiecesOf(int pieces, int pieceCount) => $"{Number(pieces)} / {Number(pieceCount)}";

        /// <summary>The pill on a painting of the collection that is being restored: "6 / 10 parça".</summary>
        public static string PiecesInPlace(int inPlace, int pieceCount) => $"{PiecesOf(inPlace, pieceCount)} parça";

        /// <summary>The second line of the plate under a covered frame: the levels that restore the painting, "BÖLÜM 21–30".</summary>
        public static string LevelsOf(PaintingNumber painting) => $"BÖLÜM {Number(painting.FirstLevel.Value)}–{Number(painting.LastLevel.Value)}";

        /// <summary>A result card's button that leads to the jigsaw, on two lines: one piece or several.</summary>
        public static string PlaceWhatWasWon(int pieces) => pieces == 1 ? "Parçayı\nyerleştir" : "Parçaları\nyerleştir";

        /// <summary>The hint on the tray while pieces wait: one piece or several.</summary>
        public static string DragHint(int piecesWaiting) => piecesWaiting == 1 ? DragThePiece : DragThePieces;

        /// <summary>How many pieces wait behind the ones the tray shows: "+6".</summary>
        public static string MoreBehind(int pieces) => $"+{Number(pieces)}";

        /// <summary>A count on a badge.</summary>
        public static string Count(int count) => Number(count);

        private static string Number(int number) => number.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Numbers inside a Turkish sentence. A suffix follows the sound of the number as it is read aloud, so "in level
    /// 51" is "51'de" (elli bir) while "in level 6" is "6'da" (altı) and "in level 3" is "3'te" (üç).
    /// </summary>
    public static class TurkishNumbers
    {
        /// <summary>The locative of each last digit as it is read: bir, iki, üç, dört, beş, altı, yedi, sekiz, dokuz.</summary>
        private static readonly string[] AfterAOne = { "de", "de", "te", "te", "te", "da", "de", "de", "da" };

        /// <summary>...and of each ten: on, yirmi, otuz, kırk, elli, altmış, yetmiş, seksen, doksan.</summary>
        private static readonly string[] AfterATen = { "da", "de", "ta", "ta", "de", "ta", "te", "de", "da" };

        /// <summary>A number with the suffix that says "in" or "at" it: "51'de", "40'ta", "100'de".</summary>
        /// <exception cref="ArgumentOutOfRangeException">The number is less than 1 or a million or more: the game counts nothing that far.</exception>
        public static string In(int number)
        {
            if (number < 1 || number >= 1_000_000)
            {
                throw new ArgumentOutOfRangeException(nameof(number), number, "Only the numbers 1 to 999 999 have their suffix here.");
            }

            return $"{number.ToString(CultureInfo.InvariantCulture)}'{LocativeOf(number)}";
        }

        /// <summary>The suffix follows the last word of the number: its last digit, or its tens, or "yüz" or "bin" when it ends in noughts.</summary>
        private static string LocativeOf(int number)
        {
            int ones = number % 10;
            if (ones != 0)
            {
                return AfterAOne[ones - 1];
            }

            int tens = number / 10 % 10;
            if (tens != 0)
            {
                return AfterATen[tens - 1];
            }

            return "de"; // the number ends in "yüz" or "bin": both take the same suffix
        }
    }
}
