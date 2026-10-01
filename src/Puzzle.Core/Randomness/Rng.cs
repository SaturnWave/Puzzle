using System;

namespace Puzzle.Core.Randomness
{
    /// <summary>
    /// Deterministic PCG32 (XSH-RR) generator. The simulation never touches <see cref="System.Random"/>:
    /// the bot, the level generator and the Unity client must draw identical sequences from the same seed
    /// on every platform and runtime.
    /// </summary>
    public sealed class Rng
    {
        private const ulong Multiplier = 6364136223846793005UL;
        private const ulong DefaultStream = 0xDA3E39CB94B95BDBUL;

        private ulong _state;
        private readonly ulong _increment;

        public Rng(ulong seed)
            : this(seed, DefaultStream)
        {
        }

        public Rng(ulong seed, ulong stream)
        {
            _increment = (stream << 1) | 1UL;
            _state = 0UL;
            NextUInt();
            _state = unchecked(_state + seed);
            NextUInt();
        }

        public uint NextUInt()
        {
            ulong old = _state;
            _state = unchecked(old * Multiplier + _increment);
            uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
            int rotation = (int)(old >> 59);
            return (xorShifted >> rotation) | (xorShifted << ((-rotation) & 31));
        }

        public ulong NextULong() => ((ulong)NextUInt() << 32) | NextUInt();

        /// <summary>Uniform integer in [0, <paramref name="maxExclusive"/>) without modulo bias (Lemire's method).</summary>
        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), maxExclusive, "Upper bound must be positive.");
            }

            uint range = (uint)maxExclusive;
            ulong product = (ulong)NextUInt() * range;
            uint low = (uint)product;
            if (low < range)
            {
                uint threshold = unchecked(0u - range) % range;
                while (low < threshold)
                {
                    product = (ulong)NextUInt() * range;
                    low = (uint)product;
                }
            }

            return (int)(product >> 32);
        }

        public bool RollPercent(int percent) => NextInt(100) < percent;

        /// <summary>Uniform double in [0, 1). Only for bot heuristics; the simulation itself stays integer-only.</summary>
        public double NextDouble() => (NextUInt() >> 8) * (1.0 / (1 << 24));

        public Rng Clone() => (Rng)MemberwiseClone();

        /// <summary>Derives an independent seed from a parent seed and a salt (SplitMix64 finalizer).</summary>
        public static ulong Derive(ulong seed, ulong salt)
        {
            unchecked
            {
                ulong z = seed + 0x9E3779B97F4A7C15UL * (salt + 1UL);
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }
    }
}
