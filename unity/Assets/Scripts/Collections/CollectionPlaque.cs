#nullable enable
using System;
using Puzzle.Client.Art;
using Puzzle.Client.Flow;
using Puzzle.Client.Gallery;
using Puzzle.Client.Hall;
using Puzzle.Client.Layout;
using Puzzle.Museum;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Collections
{
    /// <summary>
    /// The plaque of the collection's page, as the design canvas draws it (CollectionTR): at the left how many of
    /// the museum's paintings hang whole and the page's name; at the right the room whose wall is shown, between
    /// the two round buttons that turn to the room before and the room after. A room the player's levels have not
    /// opened says when it opens instead of its name, and a room no work is chosen for yet says that it is being
    /// prepared.
    /// </summary>
    public sealed class CollectionPlaque : IDisposable
    {
        /// <summary>The two lines at each side stand this far over and under the plaque's middle, in the plaque's design points.</summary>
        private const float LabelUp = 9.5f;
        private const float NameDown = 6f;

        /// <summary>
        /// The room's name between its two buttons. The design names the rooms on a strip of pills there
        /// (CollectionTR, "Oda 1 · Empresyonizm": 11.5 points); between the game's two buttons one name stands, in
        /// the serif of the page's name and a size under it, or smaller where the name needs it.
        /// </summary>
        private const float LargestRoomNameSize = 13.5f;

        /// <summary>
        /// A name that would be set smaller than this on one line is set on two. The rooms named for periods
        /// ("Rönesans Avrupası: Holbein'den Arcimboldo'ya") would come down to about 7 points on one line.
        /// </summary>
        private const float SmallestOneLineNameSize = 11f;

        /// <summary>
        /// The size of a name on two lines, or smaller where its longer line needs it, and how far apart the two
        /// lines stand, as a part of the type size: two lines of this size fit between the room's label and the
        /// plaque's rim.
        /// </summary>
        private const float LargestTwoLineNameSize = 10f;
        private const float TwoLinesApart = 1.25f;

        /// <summary>Two lines are centred this far under the plaque's middle: a little lower than one, away from the label.</summary>
        private const float TwoLinesDown = 8f;

        /// <summary>The room's lines keep this far from the buttons at their sides: clear of the buttons' rims.</summary>
        private const float RoomLinesClearOfButtons = 8f;

        /// <summary>The room the room's lines have between the two buttons.</summary>
        private const float RoomBetweenButtons =
            CollectionLayout.NextRoomAt - CollectionLayout.PreviousRoomAt - CollectionLayout.RoomButtonSize - 2f * RoomLinesClearOfButtons;

        private readonly PagePlaque _plaque;
        private readonly TextMeshPro _whichRoom;

        /// <summary>The room's name, or the first of its two lines.</summary>
        private readonly TextMeshPro _roomName;

        /// <summary>The second line of a long name; it says nothing under a name on one line.</summary>
        private readonly TextMeshPro _roomNameGoesOn;

        private CollectionPlaque(Transform parent, ArtCatalogue art, int paintingsWhole)
        {
            _plaque = new PagePlaque("Collection plaque", parent, art);
            Transform content = _plaque.Content;
            float left = -PagePlaque.DesignWidth / 2f + PagePlaque.SidePadding;

            TextMeshPro count = PagePlaque.WriteLabel("Paintings whole", content, art, DrawOrder.PlaqueLettering);
            Lettering.SayFromLeft(count, MuseumWords.CollectionCount(paintingsWhole), left, LabelUp);
            TextMeshPro title = Lettering.Write("Title", content, art, LetteringStyle.Title, PagePlaque.TitleSize, Lettering.Ink, DrawOrder.PlaqueLettering);
            Lettering.SayFromLeft(title, MuseumWords.CollectionTitle, left, -NameDown);

            float roomMiddle = (CollectionLayout.PreviousRoomAt + CollectionLayout.NextRoomAt) / 2f;
            _whichRoom = PagePlaque.WriteLabel("Which room", content, art, DrawOrder.PlaqueLettering);
            _whichRoom.transform.localPosition = new Vector3(roomMiddle, LabelUp, 0f);
            _roomName = Lettering.Write("Room name", content, art, LetteringStyle.Title, LargestRoomNameSize, Lettering.Ink, DrawOrder.PlaqueLettering);
            _roomNameGoesOn = Lettering.Write("Room name goes on", content, art, LetteringStyle.Title, LargestTwoLineNameSize, Lettering.Ink, DrawOrder.PlaqueLettering);
            _roomName.transform.localPosition = new Vector3(roomMiddle, -NameDown, 0f);
            _roomNameGoesOn.transform.localPosition = new Vector3(roomMiddle, -NameDown, 0f);
        }

        /// <param name="paintingsWhole">How many of the museum's paintings hang whole.</param>
        public static CollectionPlaque Create(Transform parent, ArtCatalogue art, int paintingsWhole) => new CollectionPlaque(parent, art, paintingsWhole);

        public void Place(PageLayout page) => _plaque.Place(page.Plaque);

        /// <summary>Names the room whose wall is shown.</summary>
        /// <param name="isOpen">True when the player's levels have opened the room.</param>
        public void Show(RoomWall room, bool isOpen)
        {
            Lettering.Say(_whichRoom, MuseumWords.RoomOfRooms(room.Number));
            string name = room.IsToCome ? MuseumWords.RoomToCome : isOpen ? room.Name : MuseumWords.OpensAt(room.Number);

            Lettering.SayWithin(_roomName, name, LargestRoomNameSize, RoomBetweenButtons);
            if (_roomName.fontSize >= SmallestOneLineNameSize)
            {
                SayOnOneLine();
                return;
            }

            (string first, string second) = BalancedLines.SplitInTwo(name, words => Lettering.WidthOf(_roomNameGoesOn, words));
            float size = Mathf.Min(SizeToFit(first), SizeToFit(second));
            float apart = TwoLinesApart * size;
            Lettering.SayWithin(_roomName, first, size, RoomBetweenButtons);
            Lettering.SayWithin(_roomNameGoesOn, second, size, RoomBetweenButtons);
            MoveDownTo(_roomName, -TwoLinesDown + apart / 2f);
            MoveDownTo(_roomNameGoesOn, -TwoLinesDown - apart / 2f);
        }

        private void SayOnOneLine()
        {
            Lettering.Say(_roomNameGoesOn, string.Empty);
            MoveDownTo(_roomName, -NameDown);
        }

        /// <summary>The size at which these words fill the room between the buttons, no larger than a two-line name's.</summary>
        private float SizeToFit(string words)
        {
            _roomNameGoesOn.fontSize = LargestTwoLineNameSize;
            return LargestTwoLineNameSize * Mathf.Min(1f, RoomBetweenButtons / Lettering.WidthOf(_roomNameGoesOn, words));
        }

        private static void MoveDownTo(TextMeshPro line, float height)
        {
            Vector3 place = line.transform.localPosition;
            line.transform.localPosition = new Vector3(place.x, height, place.z);
        }

        public void Dispose() => SceneObjects.DestroyObjectOf(_plaque.Root);
    }
}
