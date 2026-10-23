#nullable enable
using System;
using System.Collections.Generic;

namespace Puzzle.Client.Canvases
{
    /// <summary>
    /// The pictures the game has loaded, each once however many screens show it, and a bound on the ones nothing
    /// shows any more. Whoever shows a picture holds it (<see cref="Hold"/>) and lets go of the hold when it takes
    /// the picture down. A picture nothing holds stays loaded for a while, so a room the player turns back to hangs
    /// at once: the shelf keeps the pictures let go of last, at most <see cref="KeptUnheld"/> of them, and unloads
    /// the one let go of longest ago as soon as one more is let go. A picture that is held is never unloaded.
    /// </summary>
    /// <remarks>
    /// Without the bound, every picture a player had seen stayed loaded until the screen changed: turning through
    /// the collection's twenty rooms loaded the museum's hundred pictures, about 250 MB on a phone (A42). No Unity
    /// dependency, so the repository's tests compile it too; the game's pictures are textures, its tests' are not.
    /// </remarks>
    /// <typeparam name="TKey">What names a picture: its file, for the game's own.</typeparam>
    /// <typeparam name="TPicture">A loaded picture.</typeparam>
    public sealed class PictureShelf<TKey, TPicture>
        where TKey : notnull
        where TPicture : class
    {
        private readonly Func<TKey, TPicture?> _load;
        private readonly Action<TPicture> _unload;
        private readonly Dictionary<TKey, Shelved> _loaded = new Dictionary<TKey, Shelved>();

        /// <summary>The pictures that are loaded and that nothing holds, the one let go of longest ago first.</summary>
        private readonly LinkedList<TKey> _unheld = new LinkedList<TKey>();

        /// <param name="keptUnheld">How many pictures that nothing holds stay loaded; nothing unloads each one as soon as it is let go.</param>
        /// <param name="load">Loads a picture; nothing when there is none by that name.</param>
        /// <param name="unload">Frees a picture that was loaded; it is not asked for again before it is loaded anew.</param>
        /// <exception cref="ArgumentOutOfRangeException">The bound is less than nothing.</exception>
        public PictureShelf(int keptUnheld, Func<TKey, TPicture?> load, Action<TPicture> unload)
        {
            if (keptUnheld < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(keptUnheld), keptUnheld, "A shelf cannot keep fewer than no pictures.");
            }

            KeptUnheld = keptUnheld;
            _load = load;
            _unload = unload;
        }

        /// <summary>How many pictures that nothing holds stay loaded.</summary>
        public int KeptUnheld { get; }

        /// <summary>How many pictures are loaded now: the ones held, and the ones kept although nothing holds them.</summary>
        public int Loaded => _loaded.Count;

        /// <summary>How many of the loaded pictures something holds.</summary>
        public int Held => _loaded.Count - _unheld.Count;

        /// <summary>
        /// Holds a picture, loading it if it is not loaded yet; it stays loaded at least until the hold is let go of.
        /// A name that has no picture gives a hold of nothing, and is tried again the next time it is asked for.
        /// </summary>
        public PictureHold Hold(TKey key)
        {
            if (_loaded.TryGetValue(key, out Shelved? shelved))
            {
                if (shelved.Unheld is LinkedListNode<TKey> unheld)
                {
                    _unheld.Remove(unheld);
                    shelved.Unheld = null;
                }
            }
            else
            {
                TPicture? picture = _load(key);
                if (picture == null)
                {
                    return new PictureHold(this, key, picture: null);
                }

                shelved = new Shelved(picture);
                _loaded.Add(key, shelved);
            }

            shelved.Holds++;
            return new PictureHold(this, key, shelved.Picture);
        }

        /// <summary>Unloads every picture that nothing holds: what a change of screen does, where nothing of the last screen comes back at once.</summary>
        public void LetGoOfUnheld()
        {
            while (_unheld.First is LinkedListNode<TKey> oldest)
            {
                Unload(oldest.Value);
            }
        }

        private void LetGo(TKey key)
        {
            Shelved shelved = _loaded[key];
            shelved.Holds--;
            if (shelved.Holds > 0)
            {
                return;
            }

            shelved.Unheld = _unheld.AddLast(key);
            while (_unheld.Count > KeptUnheld && _unheld.First is LinkedListNode<TKey> oldest)
            {
                Unload(oldest.Value);
            }
        }

        private void Unload(TKey key)
        {
            Shelved shelved = _loaded[key];
            _unheld.Remove(shelved.Unheld ?? throw new InvalidOperationException($"Picture {key} is held; a held picture is never unloaded."));
            _loaded.Remove(key);
            _unload(shelved.Picture);
        }

        /// <summary>
        /// One holder's claim on a picture: the picture stays loaded until the claim is let go of. Letting go a second
        /// time does nothing, so a holder may let go wherever it is taken down.
        /// </summary>
        public sealed class PictureHold : IDisposable
        {
            private PictureShelf<TKey, TPicture>? _shelf;
            private readonly TKey _key;

            internal PictureHold(PictureShelf<TKey, TPicture> shelf, TKey key, TPicture? picture)
            {
                _shelf = picture == null ? null : shelf;
                _key = key;
                Picture = picture;
            }

            /// <summary>The picture held; nothing for a name that has none. Once the hold is let go of, the picture may be unloaded.</summary>
            public TPicture? Picture { get; }

            /// <summary>Lets go of the picture.</summary>
            public void Dispose()
            {
                PictureShelf<TKey, TPicture>? shelf = _shelf;
                _shelf = null;
                shelf?.LetGo(_key);
            }
        }

        /// <summary>A loaded picture, how many holds it has, and its place among the unheld ones while it has none.</summary>
        private sealed class Shelved
        {
            public Shelved(TPicture picture)
            {
                Picture = picture;
            }

            public TPicture Picture { get; }

            public int Holds { get; set; }

            public LinkedListNode<TKey>? Unheld { get; set; }
        }
    }
}
