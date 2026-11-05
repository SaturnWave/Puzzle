#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Puzzle.Client.Art;
using Puzzle.Client.Flow;
using Puzzle.Client.Gallery;
using Puzzle.Client.Stage;
using Puzzle.Meta;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Screens
{
    /// <summary>What a booster's disc shows, whether the booster is taken before a level or used during play.</summary>
    public static class BoosterPictures
    {
        public static BoosterPicture Of(BoosterKind booster) => booster.IsStart() ? StartBoosterPictures.Of(booster) : PlayBoosterPictures.Of(booster);
    }

    /// <summary>One thing in a row of goods, with the words of the pill under it. A closed set (private constructor).</summary>
    public abstract class GoodsItem
    {
        private GoodsItem(string count)
        {
            Count = count ?? throw new ArgumentNullException(nameof(count));
        }

        /// <summary>What the pill under it says: "×3", "+80".</summary>
        public string Count { get; }

        /// <summary>The goods of a reward: its coins, where it has any, and each kind of booster with how many of it.</summary>
        public static IReadOnlyList<GoodsItem> Of(Reward reward)
        {
            var items = new List<GoodsItem>();
            if (!reward.Coins.IsNone)
            {
                items.Add(new Coins(ShopWords.More(reward.Coins.Amount)));
            }

            foreach (IGrouping<BoosterKind, BoosterKind> kind in reward.Boosters.GroupBy(booster => booster))
            {
                items.Add(new Booster(kind.Key, ShopWords.More(kind.Count())));
            }

            return items;
        }

        /// <summary>A booster on its bare disc.</summary>
        public sealed class Booster : GoodsItem
        {
            public Booster(BoosterKind kind, string count)
                : base(count)
            {
                Kind = kind;
            }

            public BoosterKind Kind { get; }
        }

        /// <summary>Coins, as the pile of the shop's free strip.</summary>
        public sealed class Coins : GoodsItem
        {
            public Coins(string count)
                : base(count)
            {
            }
        }
    }

    /// <summary>
    /// A row of what a set holds or a reward gives, as the design canvas draws a set's boosters (ShopTR): each
    /// booster on its bare cream disc, coins as a pile of them, and under each a cream pill in a gilt line that says
    /// how many ("×1", "+80"), the pill's top on the disc's foot. The things stand at equal steps, centred on the
    /// row's place. Sizes are in points around the row's middle, at the level of the discs' middles.
    /// </summary>
    public sealed class GoodsRow
    {
        /// <summary>A pill is 24 high with its words at 16 at full size, 7 from its ends; the shop draws it at three quarters (18 high, its words at 12).</summary>
        private const float PillHeight = 24f;
        private const float CountSize = 16f;
        private const float PillPadding = 7f;

        /// <summary>A pill's picture is the pill and the room around it: 16 points wider, 40 high for a pill of 24.</summary>
        private const float PillPictureMargin = 16f;
        private const float PillPictureHeight = 40f;

        /// <summary>The pill's top lies half a point over the disc's foot (ShopTR).</summary>
        private const float PillOverTheFoot = 0.5f;

        /// <summary>The pile of coins is drawn a little larger than a disc: its drawing leaves room around the coins.</summary>
        private const float CoinsOverADisc = 1.15f;
        private const float MarkBox = 100f;

        private readonly Transform _root;

        /// <param name="diameter">How far across a disc is.</param>
        /// <param name="step">How far apart the middles of two things stand.</param>
        /// <param name="pillScale">How large the pills are drawn: 1, or 0.75 as in the shop's sets.</param>
        /// <param name="discOrder">The discs' place in the drawing order; their parts take the places above (<see cref="DiscPart"/>).</param>
        public GoodsRow(
            string name, Transform parent, ArtCatalogue art, IReadOnlyList<GoodsItem> items, float diameter, float step, float pillScale,
            DrawOrder discOrder, DrawOrder pillOrder, DrawOrder countOrder)
        {
            if (items.Count == 0)
            {
                throw new ArgumentException("A row of goods shows at least one thing.", nameof(items));
            }

            _root = new GameObject(name).transform;
            _root.SetParent(parent, worldPositionStays: false);
            Width = items.Count * step;
            float pillMiddle = -(diameter / 2f - PillOverTheFoot + PillHeight * pillScale / 2f);
            for (int place = 0; place < items.Count; place++)
            {
                float across = (place - (items.Count - 1) / 2f) * step;
                AddThePicture(items[place], art, diameter, discOrder).localPosition = new Vector3(across, 0f, 0f);
                AddThePill(items[place].Count, art, pillScale, pillOrder, countOrder).localPosition = new Vector3(across, pillMiddle, 0f);
            }
        }

        public Transform Transform => _root;

        /// <summary>How wide the row is: a step for every thing in it.</summary>
        public float Width { get; }

        /// <summary>Puts the middle of the row at a place of what it stands on, drawn at a share of its full size.</summary>
        public void Place(Vector2 middle, float scale = 1f)
        {
            _root.localPosition = new Vector3(middle.x, middle.y, 0f);
            _root.localScale = new Vector3(scale, scale, 1f);
        }

        private Transform AddThePicture(GoodsItem item, ArtCatalogue art, float diameter, DrawOrder discOrder)
        {
            switch (item)
            {
                case GoodsItem.Booster booster:
                    return new BoosterDisc(booster.Kind.Name(), _root, art, BoosterPictures.Of(booster.Kind), diameter, discOrder, BoosterFace.Goods).Transform;
                case GoodsItem.Coins _:
                    Transform pile = SceneObjects.AddSprite("Coins", _root, art.Of(GalleryArt.CoinPile), art.SpriteMaterial, discOrder + (int)DiscPart.Picture).transform;
                    float scale = CoinsOverADisc * diameter / MarkBox;
                    pile.localScale = new Vector3(scale, scale, 1f);
                    return pile;
                default:
                    throw new ArgumentOutOfRangeException(nameof(item), item, "Goods a row cannot show.");
            }
        }

        private Transform AddThePill(string count, ArtCatalogue art, float pillScale, DrawOrder pillOrder, DrawOrder countOrder)
        {
            SpriteRenderer pill = SceneObjects.AddSprite("Count", _root, art.Of(GalleryArt.Pill), art.SpriteMaterial, pillOrder);
            pill.drawMode = SpriteDrawMode.Tiled; // the picture's middle repeats, its round ends keep their shape
            pill.transform.localScale = new Vector3(pillScale, pillScale, 1f);
            TextMeshPro words = Lettering.Write("Words", pill.transform, art, LetteringStyle.Figure, CountSize, Lettering.Ink, countOrder);
            Lettering.Say(words, count);
            words.transform.localPosition = new Vector3(0f, -Lettering.FigureDrop(LetteringStyle.Figure) * CountSize, 0f);
            pill.size = new Vector2(Lettering.WidthOf(words, count) + 2f * PillPadding + PillPictureMargin, PillPictureHeight);
            return pill.transform;
        }
    }
}
