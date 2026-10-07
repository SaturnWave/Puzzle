using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Puzzle.Client.Art;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// Writes the art catalogue (Assets/Art/ArtCatalogue.asset) from the drawings in Assets/Art/Pieces and
    /// Assets/Art/Gallery and from the game's typeface. Each list of drawings and its folder must agree exactly: a
    /// drawing without a file, or a file no drawing asks for, stops the build with its name, so the game cannot meet
    /// a missing sprite later.
    /// </summary>
    public static class ArtCatalogueBuilder
    {
        public const string CataloguePath = "Assets/Art/ArtCatalogue.asset";

        private const string PiecesExporter = "tools/unity/export-sprites.js";
        private const string GalleryExporter = "tools/unity/export-gallery-art.js";
        private const string PaintShaderName = "Puzzle/Paint";
        private const string PaintMaterialPath = "Assets/Art/Shaders/Paint.mat";

        /// <summary>The pipeline's own unlit sprite material; the catalogue refers to it so that builds carry its shader.</summary>
        private const string SpriteMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

        /// <summary>The file of each drawing, named as the web test page's sprite names its symbols.</summary>
        private static readonly IReadOnlyDictionary<PieceArt, string> FileNames = new Dictionary<PieceArt, string>
        {
            [PieceArt.GemRed] = "gem-red",
            [PieceArt.GemGreen] = "gem-green",
            [PieceArt.GemBlue] = "gem-blue",
            [PieceArt.GemYellow] = "gem-yellow",
            [PieceArt.GemPurple] = "gem-purple",
            [PieceArt.GemOrange] = "gem-orange",
            [PieceArt.GemCyan] = "gem-cyan",
            [PieceArt.GemPink] = "gem-pink",
            [PieceArt.RocketHorizontal] = "sp-rocket-h",
            [PieceArt.RocketVertical] = "sp-rocket-v",
            [PieceArt.Bomb] = "sp-bomb",
            [PieceArt.BigBomb] = "sp-bigbomb",
            [PieceArt.Seeker] = "sp-seeker",
            [PieceArt.ColorBomb] = "sp-colorbomb",
            [PieceArt.CrateOneHit] = "sp-box-1",
            [PieceArt.CrateTwoHits] = "sp-box-2",
            [PieceArt.CrateThreeHits] = "sp-box-3",
            [PieceArt.Item] = "item",
            [PieceArt.TapeOneHit] = "ov-chain-1",
            [PieceArt.TapeTwoHits] = "ov-chain-2",
            [PieceArt.ThinSlabA] = "slab-1-0",
            [PieceArt.ThinSlabB] = "slab-1-1",
            [PieceArt.ThinSlabC] = "slab-1-2",
            [PieceArt.ThickSlabA] = "slab-2-0",
            [PieceArt.ThickSlabB] = "slab-2-1",
            [PieceArt.ThickSlabC] = "slab-2-2",
            [PieceArt.Exit] = "exit",
            [PieceArt.GoalRed] = "goal-red",
            [PieceArt.GoalGreen] = "goal-green",
            [PieceArt.GoalBlue] = "goal-blue",
            [PieceArt.GoalYellow] = "goal-yellow",
            [PieceArt.GoalPurple] = "goal-purple",
            [PieceArt.GoalOrange] = "goal-orange",
            [PieceArt.GoalCyan] = "goal-cyan",
            [PieceArt.GoalPink] = "goal-pink",
            [PieceArt.GoalCrates] = "goal-boxes",
            [PieceArt.GoalSlabs] = "goal-jelly",
            [PieceArt.GoalTape] = "goal-chains",
            [PieceArt.GoalItems] = "goal-items",
            [PieceArt.Star] = "mark-star",
            [PieceArt.Unknown] = "unknown",
        };

        /// <summary>The file of each drawing of the gallery, named as its source in art/unity is.</summary>
        private static readonly IReadOnlyDictionary<GalleryArt, string> GalleryFileNames = new Dictionary<GalleryArt, string>
        {
            [GalleryArt.Plaque] = "plaque",
            [GalleryArt.Medallion] = "medallion",
            [GalleryArt.Shelf] = "shelf",
            [GalleryArt.Sconce] = "sconce",
            [GalleryArt.GoalDone] = "goal-done",
            [GalleryArt.Card] = "card",
            [GalleryArt.Nameplate] = "nameplate",
            [GalleryArt.Button] = "button",
            [GalleryArt.Alcove] = "alcove",
            [GalleryArt.Sparkle] = "sparkle",
            [GalleryArt.CountBadge] = "count-badge",
        };

        /// <exception cref="InvalidOperationException">The drawings and the files do not agree, or a material is missing.</exception>
        public static void Build()
        {
            RefuseUnlistedFiles(PieceSpriteImport.PiecesFolder, FileNames.Values);
            RefuseUnlistedFiles(GalleryArtImport.GalleryFolder, GalleryFileNames.Values.Append(Path.GetFileNameWithoutExtension(GalleryArtImport.DamaskPath)));
            Dictionary<PieceArt, Sprite> pieces = SpritesOf(FileNames, PieceSpriteImport.PiecesFolder, PiecesExporter);
            Dictionary<GalleryArt, Sprite> gallery = SpritesOf(GalleryFileNames, GalleryArtImport.GalleryFolder, GalleryExporter);
            (TMP_FontAsset typeface, IReadOnlyDictionary<LetteringStyle, Material> letteringMaterials) = LetteringBuilder.Ensure();

            ArtCatalogue catalogue = AssetDatabase.LoadAssetAtPath<ArtCatalogue>(CataloguePath);
            if (catalogue == null)
            {
                catalogue = ScriptableObject.CreateInstance<ArtCatalogue>();
                AssetDatabase.CreateAsset(catalogue, CataloguePath);
            }

            catalogue.FillDrawings(pieces, gallery, Damask(), SpriteMaterial(), PaintMaterial());
            catalogue.FillLettering(typeface, letteringMaterials);
            EditorUtility.SetDirty(catalogue);
        }

        private static void RefuseUnlistedFiles(string folder, IEnumerable<string> listedNames)
        {
            var listed = new HashSet<string>(listedNames);
            string[] unlisted = Directory.GetFiles(folder, "*.png")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(name => !listed.Contains(name))
                .ToArray();
            if (unlisted.Length > 0)
            {
                throw new InvalidOperationException($"{folder} holds drawings the game does not list: {string.Join(", ", unlisted)}. Add them to the list of drawings and to the catalogue builder.");
            }
        }

        /// <summary>The sprite of every member of a list of drawings, from the folder its exporter writes.</summary>
        private static Dictionary<TArt, Sprite> SpritesOf<TArt>(IReadOnlyDictionary<TArt, string> fileNames, string folder, string exporter)
            where TArt : Enum
        {
            var sprites = new Dictionary<TArt, Sprite>();
            foreach (TArt art in (TArt[])Enum.GetValues(typeof(TArt)))
            {
                if (!fileNames.TryGetValue(art, out string fileName))
                {
                    throw new InvalidOperationException($"The catalogue builder names no file for {art}.");
                }

                string path = $"{folder}/{fileName}.png";
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null)
                {
                    throw new InvalidOperationException($"{path} is missing or was not imported as a sprite; run node {exporter} from the repository root.");
                }

                sprites[art] = sprite;
            }

            return sprites;
        }

        private static Texture2D Damask()
        {
            Texture2D damask = AssetDatabase.LoadAssetAtPath<Texture2D>(GalleryArtImport.DamaskPath);
            if (damask == null)
            {
                throw new InvalidOperationException($"{GalleryArtImport.DamaskPath} is missing; run node {GalleryExporter} from the repository root.");
            }

            return damask;
        }

        private static Material SpriteMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(SpriteMaterialPath);
            if (material == null)
            {
                throw new InvalidOperationException($"{SpriteMaterialPath} is missing: the Universal Render Pipeline package is not installed.");
            }

            return material;
        }

        private static Material PaintMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(PaintMaterialPath);
            if (material != null)
            {
                return material;
            }

            Shader shader = Shader.Find(PaintShaderName);
            if (shader == null)
            {
                throw new InvalidOperationException($"The shader {PaintShaderName} is missing or does not compile.");
            }

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, PaintMaterialPath);
            return material;
        }
    }
}
