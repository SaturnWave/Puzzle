#nullable enable
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Art
{
    /// <summary>
    /// The game's drawings as sprites, its typeface, and the materials everything is drawn with. One asset that the
    /// scene refers to, so a build carries exactly these sprites, fonts and shaders, and nothing is looked up by a
    /// name at run time. The asset (Assets/Art/ArtCatalogue.asset) is written by the Editor's ArtCatalogueBuilder,
    /// never by hand.
    /// </summary>
    public sealed class ArtCatalogue : ScriptableObject
    {
        private const string HowToBuild = "run ProjectSetup.Run in the Editor's batch mode";

        /// <summary>One sprite per <see cref="PieceArt"/> member, in the enum's order.</summary>
        [SerializeField] private Sprite[] _sprites = Array.Empty<Sprite>();

        /// <summary>One sprite per <see cref="GalleryArt"/> member, in the enum's order.</summary>
        [SerializeField] private Sprite[] _gallerySprites = Array.Empty<Sprite>();

        [SerializeField] private Texture2D? _damask;

        [SerializeField] private Material? _spriteMaterial;

        [SerializeField] private Material? _paintMaterial;

        [SerializeField] private TMP_FontAsset? _typeface;

        /// <summary>One material per <see cref="LetteringStyle"/> member, in the enum's order.</summary>
        [SerializeField] private Material[] _letteringMaterials = Array.Empty<Material>();

        /// <summary>The unlit sprite material: the gallery is drawn as painted, with no light of the engine on it.</summary>
        /// <exception cref="InvalidOperationException">The catalogue was never built.</exception>
        public Material SpriteMaterial => Built(_spriteMaterial, "sprite material");

        /// <summary>The material of shapes built in code (the wall, the board's floor and frame): a texture times the colour of each corner.</summary>
        /// <exception cref="InvalidOperationException">The catalogue was never built.</exception>
        public Material PaintMaterial => Built(_paintMaterial, "paint material");

        /// <summary>The wall's damask: white at low strength, see-through elsewhere, repeating in both directions.</summary>
        /// <exception cref="InvalidOperationException">The catalogue was never built.</exception>
        public Texture2D Damask => Built(_damask, "damask");

        /// <summary>The one typeface all of the game's lettering is set in.</summary>
        /// <exception cref="InvalidOperationException">The catalogue was never built.</exception>
        public TMP_FontAsset Typeface => Built(_typeface, "typeface");

        /// <exception cref="InvalidOperationException">The catalogue is older than the list of drawings.</exception>
        public Sprite Of(PieceArt art) => Listed(_sprites, (int)art, art);

        /// <exception cref="InvalidOperationException">The catalogue is older than the list of drawings.</exception>
        public Sprite Of(GalleryArt art) => Listed(_gallerySprites, (int)art, art);

        /// <summary>The material that draws the typeface in a style: how heavy its strokes are, whether it throws a shadow.</summary>
        /// <exception cref="InvalidOperationException">The catalogue is older than the list of styles.</exception>
        public Material MaterialOf(LetteringStyle style) => Listed(_letteringMaterials, (int)style, style);

        private static T Built<T>(T? held, string what)
            where T : UnityEngine.Object =>
            held != null ? held : throw new InvalidOperationException($"The art catalogue has no {what}; {HowToBuild}.");

        private static T Listed<T>(T[] list, int index, Enum member)
            where T : UnityEngine.Object =>
            index < list.Length && list[index] != null
                ? list[index]
                : throw new InvalidOperationException($"The art catalogue has nothing for {member}; {HowToBuild}.");

#if UNITY_EDITOR
        /// <summary>Editor only: replaces the drawings and their materials. Both lists must name every member of their enum.</summary>
        public void FillDrawings(
            IReadOnlyDictionary<PieceArt, Sprite> pieces,
            IReadOnlyDictionary<GalleryArt, Sprite> gallery,
            Texture2D damask,
            Material spriteMaterial,
            Material paintMaterial)
        {
            _sprites = InEnumOrder(pieces);
            _gallerySprites = InEnumOrder(gallery);
            _damask = damask;
            _spriteMaterial = spriteMaterial;
            _paintMaterial = paintMaterial;
        }

        /// <summary>Editor only: replaces the typeface and the material of each lettering style.</summary>
        public void FillLettering(TMP_FontAsset typeface, IReadOnlyDictionary<LetteringStyle, Material> materials)
        {
            _typeface = typeface;
            _letteringMaterials = InEnumOrder(materials);
        }

        private static TValue[] InEnumOrder<TEnum, TValue>(IReadOnlyDictionary<TEnum, TValue> byMember)
            where TEnum : Enum
        {
            var members = (TEnum[])Enum.GetValues(typeof(TEnum));
            var inOrder = new TValue[members.Length];
            foreach (TEnum member in members)
            {
                inOrder[Convert.ToInt32(member)] = byMember[member];
            }

            return inOrder;
        }
#endif
    }
}
