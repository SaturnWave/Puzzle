#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Client.Flow;
using Puzzle.Client.Words;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Art
{
    /// <summary>
    /// The game's drawings as sprites, its typefaces, the materials everything is drawn with, its sounds and its music.
    /// One asset that the scene refers to, so a build carries exactly these sprites, fonts, shaders, sounds and music, and nothing is
    /// looked up by a name at run time. The asset (Assets/Art/ArtCatalogue.asset) is written by the Editor's ArtCatalogueBuilder,
    /// never by hand.
    /// </summary>
    /// <summary>
    /// What a part of the world map is drawn with, each its own shader (unity/Assets/Art/Shaders/Map*.shader). A closed
    /// set: <see cref="ArtCatalogue"/> stores a material per member in this order, so after adding one, build the
    /// catalogue again (ProjectSetup.Run).
    /// </summary>
    public enum MapInk
    {
        /// <summary>The sea and its waves on the whole sheet.</summary>
        Sea,

        /// <summary>The land's and the lakes' triangles in the colour of their corners.</summary>
        Fill,

        /// <summary>A line of a width in points at every zoom: the coasts, the shores, the graticule.</summary>
        Line,

        /// <summary>The soft light along the coasts on the sea's side.</summary>
        Glow,

        /// <summary>The dark at the sheet's edges.</summary>
        Vignette,
    }

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

        /// <summary>One material per <see cref="MapInk"/> member, in the enum's order: what the world map's sea, land and lines are drawn with.</summary>
        [SerializeField] private Material[] _mapMaterials = Array.Empty<Material>();

        /// <summary>The typeface of each <see cref="LetteringStyle"/> member, in the enum's order; styles of one typeface name the same one.</summary>
        [SerializeField] private TMP_FontAsset[] _typefaces = Array.Empty<TMP_FontAsset>();

        /// <summary>One material per <see cref="LetteringStyle"/> member, in the enum's order.</summary>
        [SerializeField] private Material[] _letteringMaterials = Array.Empty<Material>();

        /// <summary>The typeface of each <see cref="FallbackTypeface"/> member, in the enum's order: the letters of the other scripts, behind every typeface.</summary>
        [SerializeField] private TMP_FontAsset[] _fallbacks = Array.Empty<TMP_FontAsset>();

        /// <summary>The language whose own fallbacks the typefaces ask first now; nothing before a line was first written.</summary>
        private Language? _fallbacksOrderedFor;

        /// <summary>One sound per <see cref="SoundCue"/> member, in the enum's order; none for a cue that is only felt (SoundScore.IsHeard).</summary>
        [SerializeField] private AudioClip[] _sounds = Array.Empty<AudioClip>();

        /// <summary>The music's intro, played once as the game starts, and its loop, which follows and repeats (<see cref="MusicClips"/>).</summary>
        [SerializeField] private AudioClip? _musicIntro;

        [SerializeField] private AudioClip? _musicLoop;

        /// <summary>The unlit sprite material: the gallery is drawn as painted, with no light of the engine on it.</summary>
        /// <exception cref="InvalidOperationException">The catalogue was never built.</exception>
        public Material SpriteMaterial => Built(_spriteMaterial, "sprite material");

        /// <summary>The material of shapes built in code (the wall, the board's floor and frame): a texture times the colour of each corner.</summary>
        /// <exception cref="InvalidOperationException">The catalogue was never built.</exception>
        public Material PaintMaterial => Built(_paintMaterial, "paint material");

        /// <summary>The material a part of the world map is drawn with; its renderer gives it the map's camera (MapCommon.hlsl).</summary>
        /// <exception cref="InvalidOperationException">The catalogue is older than the list of the map's inks.</exception>
        public Material MaterialOf(MapInk ink) => Listed(_mapMaterials, (int)ink, ink);

        /// <summary>The wall's damask: white at low strength, see-through elsewhere, repeating in both directions.</summary>
        /// <exception cref="InvalidOperationException">The catalogue was never built.</exception>
        public Texture2D Damask => Built(_damask, "damask");

        /// <exception cref="InvalidOperationException">The catalogue is older than the list of drawings.</exception>
        public Sprite Of(PieceArt art) => Listed(_sprites, (int)art, art);

        /// <exception cref="InvalidOperationException">The catalogue is older than the list of drawings.</exception>
        public Sprite Of(GalleryArt art) => Listed(_gallerySprites, (int)art, art);

        /// <summary>
        /// The typeface a style is set in, ready to draw: every letter of the game as a picture, and behind it the
        /// fallbacks of the other scripts, the spoken language's own asked first (<see cref="FallbackTypefaces.Behind(Typeface, Language)"/>):
        /// a Chinese character takes the Japanese form in Japanese and the Chinese one in Chinese. The typefaces are
        /// shared by every line, so a language chosen anew puts its own first for every line laid out after that.
        /// </summary>
        /// <exception cref="InvalidOperationException">The catalogue is older than the list of styles, or than the list of fallbacks.</exception>
        public TMP_FontAsset TypefaceOf(LetteringStyle style)
        {
            OrderTheFallbacksFor(Speech.Language);
            return Listed(_typefaces, (int)style, style);
        }

        /// <summary>The material that draws a style's typeface as the style has it: what is drawn around its strokes and under them.</summary>
        /// <exception cref="InvalidOperationException">The catalogue is older than the list of styles.</exception>
        public Material MaterialOf(LetteringStyle style) => Listed(_letteringMaterials, (int)style, style);

        /// <summary>The sound a cue plays; nothing in a catalogue older than the list of cues, whose player then stays silent for it rather than stop the game.</summary>
        public AudioClip? SoundOf(SoundCue cue)
        {
            int index = (int)cue;
            return index < _sounds.Length && _sounds[index] != null ? _sounds[index] : null;
        }

        /// <summary>The game's music; nothing in a catalogue older than the music, whose game then plays none rather than stop.</summary>
        public MusicClips? Music => _musicIntro != null && _musicLoop != null ? new MusicClips(_musicIntro, _musicLoop) : null;

        /// <summary>
        /// Puts each typeface's fallbacks in the order a language asks them: its own first. Only the order changes,
        /// in memory; the typefaces' files keep the order the Editor made them with.
        /// </summary>
        private void OrderTheFallbacksFor(Language language)
        {
            if (_fallbacksOrderedFor == language)
            {
                return;
            }

            foreach (LetteringStyle style in (LetteringStyle[])Enum.GetValues(typeof(LetteringStyle)))
            {
                var behind = new List<TMP_FontAsset>();
                foreach (FallbackTypeface fallback in FallbackTypefaces.Behind(Typefaces.Of(style), language))
                {
                    behind.Add(Listed(_fallbacks, (int)fallback, fallback));
                }

                Listed(_typefaces, (int)style, style).fallbackFontAssetTable = behind;
            }

            _fallbacksOrderedFor = language;
        }

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

        /// <summary>Editor only: replaces the materials of the world map, one for every member of <see cref="MapInk"/>.</summary>
        public void FillMap(IReadOnlyDictionary<MapInk, Material> materials) => _mapMaterials = InEnumOrder(materials);

        /// <summary>Editor only: replaces the typeface and the material of each lettering style, and the typeface of each fallback. Every list must name every member of its enum.</summary>
        public void FillLettering(
            IReadOnlyDictionary<LetteringStyle, TMP_FontAsset> typefaces,
            IReadOnlyDictionary<LetteringStyle, Material> materials,
            IReadOnlyDictionary<FallbackTypeface, TMP_FontAsset> fallbacks)
        {
            _typefaces = InEnumOrder(typefaces);
            _letteringMaterials = InEnumOrder(materials);
            _fallbacks = InEnumOrder(fallbacks);
            _fallbacksOrderedFor = null;
        }

        /// <summary>Editor only: replaces the sound of every cue that is heard; a cue the list does not name has none.</summary>
        public void FillSounds(IReadOnlyDictionary<SoundCue, AudioClip> sounds)
        {
            var inOrder = new AudioClip[Enum.GetValues(typeof(SoundCue)).Length];
            foreach (KeyValuePair<SoundCue, AudioClip> sound in sounds)
            {
                inOrder[(int)sound.Key] = sound.Value;
            }

            _sounds = inOrder;
        }

        /// <summary>Editor only: replaces the music's two clips.</summary>
        public void FillMusic(MusicClips music)
        {
            _musicIntro = music.Intro;
            _musicLoop = music.Loop;
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
