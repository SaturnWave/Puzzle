#nullable enable
using System;
using System.Globalization;
using Puzzle.Client.Words;
using Puzzle.Museum;
using Puzzle.Museum.Catalogue;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// What the museum's screens say, in the language the game speaks (<see cref="Speech"/>). The Turkish words are
    /// the design canvas' (HomeTR, MapTR, WinTR) where it has them; the rooms' names and the plates are the
    /// catalogue's in Turkish and the museum's table's in another language. No Unity dependency.
    /// </summary>
    public static class MuseumWords
    {
        /// <summary>The names of the navigation bar's destinations.</summary>
        public static string ShopTab => Speech.Say(WordId.NavShop);
        public static string HomeTab => Speech.Say(WordId.NavMuseum);
        public static string MapTab => Speech.Say(WordId.NavMap);
        public static string CollectionTab => Speech.Say(WordId.NavCollection);

        public static string CollectionTitle => Speech.Say(WordId.CollectionTitle);

        /// <summary>The jigsaw's screen: the counter's label, the label of a painting that is whole, and the hints on the tray.</summary>
        public static string PieceLabel => Speech.Say(WordId.JigsawPieceLabel);
        public static string RestoredLabel => Speech.Say(WordId.JigsawRestoredLabel);
        public static string DragThePiece => Speech.Say(WordId.JigsawDragThePiece);
        public static string DragThePieces => Speech.Say(WordId.JigsawDragThePieces);
        public static string NoPieceWaits => Speech.Say(WordId.JigsawNoPieceWaits);
        public static string PaintingRestored => Speech.Say(WordId.JigsawPaintingRestored);

        /// <summary>The jigsaw's buttons: the one that places every waiting piece, and the one that leaves when nothing waits.</summary>
        public static string PlaceThemAll => Speech.Say(WordId.JigsawPlaceThemAll);
        public static string Done => Speech.Say(WordId.Done);

        /// <summary>The home's button that leads to the jigsaw of the painting in the frame.</summary>
        public static string ToTheJigsaw => Speech.Say(WordId.HallToTheJigsaw);

        /// <summary>The line under a painting that is looked at by itself.</summary>
        public static string TouchToClose => Speech.Say(WordId.ViewerTouchToClose);

        /// <summary>The button of a result's card that leads back to the museum's hall.</summary>
        public static string BackToTheMuseum => Speech.Say(WordId.ResultBackToTheMuseum);

        /// <summary>The second line of the plate under a frame whose painting is not chosen yet.</summary>
        public static string PaintingToCome => Speech.Say(WordId.PlatePaintingToCome);

        /// <summary>On the map, the pill of a room no work is chosen for yet; and the name such a room goes by while its movement is only pencilled in.</summary>
        public static string RoomToCome => Speech.Say(WordId.MapRoomToCome);
        public static string RoomWithoutAName => Speech.Say(WordId.RoomWithoutAName);

        /// <summary>The hall's note when nothing is left to restore: more paintings are on their way, or the museum is whole.</summary>
        public static string MorePaintingsToCome => Speech.Say(WordId.HallMorePaintingsToCome);
        public static string MuseumRestored => Speech.Say(WordId.HallMuseumRestored);

        /// <summary>The play button once every level is won: the last one again.</summary>
        public static string PlayAgain => Speech.Say(WordId.PlayAgain);

        /// <summary>The card the way back brings in the hall: its question on the nameplate, and its two answers, which close the game or take the card up.</summary>
        public static string QuitQuestion => Speech.Say(WordId.HallQuitQuestion);
        public static string QuitTheGame => Speech.Say(WordId.HallQuitLeave);
        public static string StayInTheGame => Speech.Say(WordId.HallQuitStay);

        /// <summary>The banner over a room whose fifth painting has just become whole, and the button that leads on from it.</summary>
        public static string RoomRestored => Speech.Say(WordId.HallRoomRestored);
        public static string OnFromTheRoom => Speech.Say(WordId.GoOn);

        /// <summary>A room among the museum's rooms: "ODA 1 / 20".</summary>
        public static string RoomOfRooms(RoomNumber room) =>
            Speech.Say(WordId.RoomOfRooms, ("room", Number(room.Value)), ("rooms", Number(MuseumPlan.RoomCount)));

        /// <summary>How many of a room's paintings hang whole again, on the room's plaque: "4 / 5 ONARILDI".</summary>
        public static string Restored(int paintingsWhole) =>
            Speech.Say(WordId.RoomPaintingsRestored, ("whole", Number(paintingsWhole)), ("perRoom", Number(MuseumPlan.PaintingsPerRoom)));

        /// <summary>The same count on the world map's plaque in an era's view: "4 / 5 TABLO".</summary>
        public static string PaintingsWhole(int paintingsWhole) =>
            Speech.Say(WordId.MapRoomPaintingsWhole, ("whole", Number(paintingsWhole)), ("perRoom", Number(MuseumPlan.PaintingsPerRoom)));

        /// <summary>A room's name as the player reads it: its movement once that is decided, and until then only that it is a new room.</summary>
        public static string NameOf(MuseumRoom room) => room.IsNameProvisional ? RoomWithoutAName : Speech.Captions.NameOf(room);

        /// <summary>Under the banner of a restored room: what comes next. The next room by its name, or that it is being prepared, or that the museum is whole.</summary>
        public static string AfterTheRoom(RoomNumber restored, MuseumCatalogue catalogue)
        {
            if (restored.Value == MuseumPlan.RoomCount)
            {
                return MuseumRestored;
            }

            MuseumRoom next = catalogue.Room(new RoomNumber(restored.Value + 1));
            return next.Artworks.Count == 0 ? MorePaintingsToCome : Speech.Say(WordId.HallNextRoom, ("room", NameOf(next)));
        }

        /// <summary>The line under the banner of a restored room: the room and its paintings, "Empresyonizm · 5 / 5 tablo".</summary>
        public static string RoomAndItsPaintings(string roomName) => Speech.Say(
            WordId.HallRoomAndItsPaintings,
            ("room", roomName), ("whole", Number(MuseumPlan.PaintingsPerRoom)), ("perRoom", Number(MuseumPlan.PaintingsPerRoom)));

        /// <summary>The play button: the level it opens.</summary>
        public static string Play(int levelNumber) => Speech.Say(WordId.PlayLevel, ("level", Number(levelNumber)));

        /// <summary>The label under the map's pin.</summary>
        public static string YouAreHere(int levelNumber) => Speech.Say(WordId.MapYouAreHere, ("level", Number(levelNumber)));

        /// <summary>
        /// When a locked room opens: "Bölüm 51'de açılır", the level being the first one of the room. Turkish writes
        /// the number with the suffix that follows its sound (<see cref="TurkishNumbers"/>); the other languages
        /// write it plain.
        /// </summary>
        public static string OpensAt(RoomNumber room)
        {
            int level = room.FirstLevel.Value;
            return Speech.Say(WordId.RoomOpensAt, ("level", Number(level)), ("levelIn", TurkishNumbers.In(level)));
        }

        /// <summary>The first line of the plate under a painting's frame: its title, or its place while no work is chosen for it.</summary>
        public static string TitleOf(PaintingNumber painting, Artwork? artwork) => artwork != null
            ? Speech.Captions.TitleOf(artwork)
            : Speech.Say(WordId.PaintingByNumber, ("painting", Number(painting.Value)));

        /// <summary>
        /// The second line of the plate: the painter and the year in capitals. A painter's name is no word of the
        /// language, so its i stays an undotted capital I, as the painter wrote it.
        /// </summary>
        public static string ByLineOf(Artwork? artwork) => artwork == null
            ? PaintingToCome
            : $"{Speech.Captions.ArtistOf(artwork).ToUpperInvariant()} · {Speech.Captions.YearOf(artwork)}";

        /// <summary>What a won level gave, on its result's card: "Yeni parça: 7 / 10", or the pieces when it gave several.</summary>
        public static string Earned(PieceReward reward) => $"{EarnedLead(reward)} {EarnedCount(reward)}";

        /// <summary>The words of <see cref="Earned"/> before its figures, which the card sets in another typeface: "Yeni parça:".</summary>
        public static string EarnedLead(PieceReward reward) => reward.Pieces.Count == 1
            ? Speech.Say(WordId.ResultNewPiece)
            : Speech.SayCounted(WordId.ResultNewPieces, reward.Pieces.Count, ("count", Number(reward.Pieces.Count)));

        /// <summary>The figures of <see cref="Earned"/>: how many of the painting's pieces the player has with these, of all, "7 / 10".</summary>
        public static string EarnedCount(PieceReward reward) => $"{Number(reward.WonWithThem)} / {Number(reward.PieceCount)}";

        /// <summary>The collection's plaque: how many of the museum's paintings hang whole, "4 / 100 TABLO".</summary>
        public static string CollectionCount(int paintingsWhole) =>
            Speech.Say(WordId.CollectionPaintingsWhole, ("whole", Number(paintingsWhole)), ("all", Number(MuseumPlan.PaintingCount)));

        /// <summary>The piece counter: the pieces in place of the pieces a painting has, "7 / 10".</summary>
        public static string PiecesOf(int pieces, int pieceCount) => $"{Number(pieces)} / {Number(pieceCount)}";

        /// <summary>The pill on a painting of the collection that is being restored: "6 / 10 parça".</summary>
        public static string PiecesInPlace(int inPlace, int pieceCount) => Speech.SayCounted(
            WordId.CollectionPiecesInPlace, pieceCount, ("inPlace", Number(inPlace)), ("pieceCount", Number(pieceCount)));

        /// <summary>The second line of the plate under a covered frame: the levels that restore the painting, "BÖLÜM 21–30".</summary>
        public static string LevelsOf(PaintingNumber painting) =>
            Speech.Say(WordId.PlateLevels, ("first", Number(painting.FirstLevel.Value)), ("last", Number(painting.LastLevel.Value)));

        /// <summary>A result card's button that leads to the jigsaw, on two lines: one piece or several.</summary>
        public static string PlaceWhatWasWon(int pieces) => Speech.Say(pieces == 1 ? WordId.ResultPlaceThePiece : WordId.ResultPlaceThePieces);

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
