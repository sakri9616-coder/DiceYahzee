using System;

namespace ParvaMirabilia.Core
{
    /// <summary>Source of pseudo-random numbers. Deterministic for a given seed.</summary>
    public interface IRandomSource
    {
        /// <summary>Integer in [minInclusive, maxExclusive).</summary>
        int Range(int minInclusive, int maxExclusive);

        /// <summary>Double in [0, 1).</summary>
        double NextDouble();
    }

    /// <summary>
    /// Deterministic generator (SplitMix64). Tests, replays and NPC simulations depend only
    /// on the seed, never on System.Random's implementation details.
    /// </summary>
    public sealed class SeededRandom : IRandomSource
    {
        private ulong _state;

        public SeededRandom(int seed)
        {
            _state = unchecked((ulong)seed) + 0x9E3779B97F4A7C15UL;
            NextUInt64(); // spread small seeds
        }

        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive),
                    "maxExclusive must be greater than minInclusive");

            uint bound = (uint)(maxExclusive - minInclusive);
            uint limit = (uint)(0x100000000UL % bound);
            uint sample;
            do
            {
                sample = NextUInt32();
            } while (sample < limit);

            return minInclusive + (int)(sample % bound);
        }

        public double NextDouble()
        {
            return NextUInt32() / 4294967296.0;
        }

        private uint NextUInt32()
        {
            return (uint)(NextUInt64() >> 32);
        }

        private ulong NextUInt64()
        {
            _state += 0x9E3779B97F4A7C15UL;
            var z = _state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
