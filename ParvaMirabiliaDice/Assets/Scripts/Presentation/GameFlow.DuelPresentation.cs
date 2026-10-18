using UnityEngine;

namespace ParvaMirabilia.Presentation
{
    /// <summary>
    /// Shared, deterministic, allocation-free presentation math for the clash energy layer.
    /// No UnityEngine.Random state is consumed and every sample is a pure function of its
    /// arguments so online mode can reproduce the same offsets frame for frame.
    /// </summary>
    internal static class DuelPresentation
    {
        /// <summary>
        /// High-frequency (20-35Hz) subpixel-to-1.5px jitter used while dice charge.
        /// <paramref name="energy"/> in [0,1] scales the amplitude; per-die phase offsets
        /// keep neighbours from marching in lockstep.
        /// </summary>
        public static Vector2 SDChargeOffset(float time, int index, float energy)
        {
            if (energy <= 0f) return Vector2.zero;
            float phase = index * 1.37f;
            // ~27Hz primary jitter, subpixel floor 0.35px so reduced tiers still read.
            float amp = (.35f + 1.15f * energy) * Mathf.Clamp01(energy);
            float x = Mathf.Sin(time * 172f + phase) * amp;
            float y = Mathf.Sin(time * 149f * .93f + phase * 1.61f + .7f) * amp * .8f;
            return new Vector2(x, y);
        }

        /// <summary>Deterministic +-1 twist sign per die, used for the &lt;=0.7deg charge roll.</summary>
        public static float SDSign(float time, int index)
        {
            float v = Mathf.Sin(time * 63f + index * 2.1f);
            return v >= 0f ? 1f : -1f;
        }
    }
}
