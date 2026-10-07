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
        private const float RoomNameSize = 13.5f;

        /// <summary>The room's lines keep this far from the buttons at their sides.</summary>
        private const float RoomLinesClearOfButtons = 5f;

        private readonly PagePlaque _plaque;
        private readonly TextMeshPro _whichRoom;
        private readonly TextMeshPro _roomName;

        private CollectionPlaque(Transform parent, ArtCatalogue art, int paintingsWhole)
        {
            _plaque = new PagePlaque("Collection plaque", parent, art);
            Transform content = _plaque.Content;
            float left = -PagePlaque.DesignWidth / 2f + PagePlaque.SidePadding;

            TextMeshPro count = PagePlaque.WriteLabel("Paintings whole", content, art, DrawOrder.PlaqueLettering);
            Lettering.SayFromLeft(count, MuseumWords.CollectionCount(paintingsWhole), left, LabelUp);
            TextMeshPro title = Lettering.Write("Title", content, art, LetteringStyle.Label, PagePlaque.TitleSize, Lettering.Ink, DrawOrder.PlaqueLettering);
            Lettering.SayFromLeft(title, MuseumWords.CollectionTitle, left, -NameDown);

            float roomMiddle = (CollectionLayout.PreviousRoomAt + CollectionLayout.NextRoomAt) / 2f;
            _whichRoom = PagePlaque.WriteLabel("Which room", content, art, DrawOrder.PlaqueLettering);
            _whichRoom.transform.localPosition = new Vector3(roomMiddle, LabelUp, 0f);
            _roomName = Lettering.Write("Room name", content, art, LetteringStyle.Label, RoomNameSize, Lettering.Ink, DrawOrder.PlaqueLettering);
            _roomName.transform.localPosition = new Vector3(roomMiddle, -NameDown, 0f);
        }

        /// <param name="paintingsWhole">How many of the museum's paintings hang whole.</param>
        public static CollectionPlaque Create(Transform parent, ArtCatalogue art, int paintingsWhole) => new CollectionPlaque(parent, art, paintingsWhole);

        public void Place(PageLayout page) => _plaque.Place(page.Plaque);

        /// <summary>Names the room whose wall is shown.</summary>
        /// <param name="isOpen">True when the player's levels have opened the room.</param>
        public void Show(RoomWall room, bool isOpen)
        {
            float between = CollectionLayout.NextRoomAt - CollectionLayout.PreviousRoomAt - CollectionLayout.RoomButtonSize - 2f * RoomLinesClearOfButtons;
            Lettering.Say(_whichRoom, MuseumWords.RoomOfRooms(room.Number));
            string name = room.IsToCome ? MuseumWords.RoomToCome : isOpen ? room.Name : MuseumWords.OpensAt(room.Number);
            Lettering.SayWithin(_roomName, name, RoomNameSize, between);
        }

        public void Dispose() => SceneObjects.DestroyObjectOf(_plaque.Root);
    }
}
