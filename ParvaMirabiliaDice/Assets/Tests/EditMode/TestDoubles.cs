using ParvaMirabilia.Core;

namespace ParvaMirabilia.Tests
{
    /// <summary>Wraps a real generator and counts how many values were drawn.</summary>
    public sealed class CountingRandom : IRandomSource
    {
        private readonly IRandomSource _inner;

        public int Calls { get; private set; }

        public CountingRandom(int seed)
        {
            _inner = new SeededRandom(seed);
        }

        public int Range(int minInclusive, int maxExclusive)
        {
            Calls++;
            return _inner.Range(minInclusive, maxExclusive);
        }

        public double NextDouble()
        {
            Calls++;
            return _inner.NextDouble();
        }
    }

    /// <summary>
    /// Returns a queued die value per draw and a fixed fallback once the queue runs dry.
    /// Lets a test pin down both sides' dice, which is the only way to reach a stand-off
    /// (both strikes blocked) on purpose.
    /// </summary>
    public sealed class ScriptedRandom : IRandomSource
    {
        private readonly int[] _values;
        private readonly int _fallback;
        private int _index;

        public int Calls { get; private set; }

        public ScriptedRandom(int[] values, int fallback = 1)
        {
            _values = values ?? new int[0];
            _fallback = fallback;
        }

        public int Range(int minInclusive, int maxExclusive)
        {
            Calls++;
            int value = _index < _values.Length ? _values[_index] : _fallback;
            _index++;
            if (value < minInclusive) return minInclusive;
            if (value > maxExclusive) return maxExclusive;
            return value;
        }

        public double NextDouble()
        {
            Calls++;
            return 0.5;
        }
    }
}
