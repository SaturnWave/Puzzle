#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
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
        /// the serif of the page's name and a size under it, or smaller where the museum's longest name needs it.
        /// </summary>
        private const float LargestRoomNameSize = 13.5f;

        /// <summary>The room's lines keep this far from the buttons at their sides: clear of the buttons' rims.</summary>
        private const float RoomLinesClearOfButtons = 8f;

        /// <summary>The room the room's lines have between the two buttons.</summary>
        private const float RoomBetweenButtons =
            CollectionLayout.NextRoomAt - CollectionLayout.PreviousRoomAt - CollectionLayout.RoomButtonSize - 2f * RoomLinesClearOfButtons;

        private readonly PagePlaque _plaque;
        private readonly TextMeshPro _whichRoom;
        private readonly TextMeshPro _roomName;

        /// <summary>
        /// The size every room's name is set at: the one at which the longest of them stands between the buttons,
        /// so that the name does not change its size as the player turns from room to room.
        /// </summary>
        private readonly float _roomNameSize;

        private CollectionPlaque(Transform parent, ArtCatalogue art, int paintingsWhole, IReadOnlyCollection<string> roomNames)
        {
            if (roomNames.Count == 0)
            {
                throw new ArgumentException("A museum without a room has no collection to show.", nameof(roomNames));
            }

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
            _roomName.transform.localPosition = new Vector3(roomMiddle, -NameDown, 0f);
            float widestName = roomNames.Max(name => Lettering.WidthOf(_roomName, name));
            _roomNameSize = LargestRoomNameSize * Mathf.Min(1f, RoomBetweenButtons / widestName);
        }

        /// <param name="paintingsWhole">How many of the museum's paintings hang whole.</param>
        /// <param name="roomNames">The name of every room of the museum, as the plaque may come to show it.</param>
        /// <exception cref="ArgumentException">No room is named.</exception>
        public static CollectionPlaque Create(Transform parent, ArtCatalogue art, int paintingsWhole, IReadOnlyCollection<string> roomNames) =>
            new CollectionPlaque(parent, art, paintingsWhole, roomNames);

        public void Place(PageLayout page) => _plaque.Place(page.Plaque);

        /// <summary>Names the room whose wall is shown.</summary>
        /// <param name="isOpen">True when the player's levels have opened the room.</param>
        public void Show(RoomWall room, bool isOpen)
        {
            Lettering.Say(_whichRoom, MuseumWords.RoomOfRooms(room.Number));
            string name = room.IsToCome ? MuseumWords.RoomToCome : isOpen ? room.Name : MuseumWords.OpensAt(room.Number);

            // What stands there instead of a name (when the room opens, that it is being prepared) is set no larger than the names.
            Lettering.SayWithin(_roomName, name, _roomNameSize, RoomBetweenButtons);
        }

        public void Dispose() => SceneObjects.DestroyObjectOf(_plaque.Root);
    }
}
