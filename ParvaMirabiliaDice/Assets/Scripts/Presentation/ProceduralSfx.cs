using System;
using System.Collections.Generic;
using UnityEngine;

namespace ParvaMirabilia.Presentation
{
    /// <summary>
    /// Small sounds synthesised at boot for the moments the recorded set has no clip for: the
    /// pointer brushing a button, the tick of a counting figure, the chime of a heal, the slam of
    /// the K.O. stamp and the two result stings. They are built once with AudioClip.Create, so no
    /// asset or import setting is involved, and each is short enough to cost nothing in memory.
    /// </summary>
    internal static class ProceduralSfx
    {
        private const int Rate = 44100;

        /// <summary>A synthesised clip and the volume scale it should play at.</summary>
        public readonly struct Entry
        {
            public readonly string Name;
            public readonly AudioClip Clip;
            public readonly float Gain;

            public Entry(string name, AudioClip clip, float gain)
            {
                Name = name; Clip = clip; Gain = gain;
            }
        }

        public static List<Entry> Build()
        {
            var list = new List<Entry>(6);
            var random = new System.Random(7331);
            list.Add(new Entry("ui_hover", Make("ui_hover", Hover(random)), .22f));
            list.Add(new Entry("tick", Make("tick", Tick(random)), .32f));
            list.Add(new Entry("heal", Make("heal", Heal()), .45f));
            list.Add(new Entry("stamp", Make("stamp", Stamp(random)), .9f));
            list.Add(new Entry("fanfare", Make("fanfare", Fanfare()), .42f));
            list.Add(new Entry("toll", Make("toll", Toll()), .5f));
            return list;
        }

        private static AudioClip Make(string name, float[] data)
        {
            // Peak-normalise so every gain above is a plain, comparable loudness choice.
            float peak = 0f;
            for (int i = 0; i < data.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            if (peak > 0.0001f)
            {
                float scale = .8f / peak;
                for (int i = 0; i < data.Length; i++) data[i] *= scale;
            }
            // A 4 ms fade at both edges so no clip starts or stops on a step.
            int edge = Mathf.Min(Rate / 250, data.Length / 2);
            for (int i = 0; i < edge; i++)
            {
                float w = i / (float)edge;
                data[i] *= w;
                data[data.Length - 1 - i] *= w;
            }
            var clip = AudioClip.Create("proc_" + name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float[] Buffer(float seconds) => new float[Mathf.Max(1, Mathf.RoundToInt(seconds * Rate))];

        private static float Noise(System.Random random) => (float)(random.NextDouble() * 2.0 - 1.0);

        /// <summary>A soft, airy tap: a high sine blip with a breath of noise.</summary>
        private static float[] Hover(System.Random random)
        {
            float[] d = Buffer(.05f);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Exp(-t * 110f);
                d[i] = env * (.7f * Mathf.Sin(2f * Mathf.PI * 1850f * t) + .12f * Noise(random));
            }
            return d;
        }

        /// <summary>A dry wooden tick for counting figures.</summary>
        private static float[] Tick(System.Random random)
        {
            float[] d = Buffer(.035f);
            float lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Exp(-t * 190f);
                lp += .35f * (Noise(random) - lp);
                d[i] = env * (.8f * Mathf.Sin(2f * Mathf.PI * 2900f * t) + .5f * lp);
            }
            return d;
        }

        /// <summary>A rising three-note bell, the sound of health coming back.</summary>
        private static float[] Heal()
        {
            float[] d = Buffer(.9f);
            float[] notes = { 1046.5f, 1318.5f, 1568f, 2093f };
            for (int n = 0; n < notes.Length; n++)
            {
                int start = Mathf.RoundToInt(n * .065f * Rate);
                float amp = n == notes.Length - 1 ? .55f : .8f;
                for (int i = start; i < d.Length; i++)
                {
                    float t = (i - start) / (float)Rate;
                    float env = Mathf.Min(1f, t * 400f) * Mathf.Exp(-t * 5.5f);
                    float f = notes[n];
                    float shimmer = 1f + .004f * Mathf.Sin(2f * Mathf.PI * 6f * t);
                    d[i] += amp * env * (Mathf.Sin(2f * Mathf.PI * f * shimmer * t) + .25f * Mathf.Sin(2f * Mathf.PI * f * 2.01f * t));
                }
            }
            return d;
        }

        /// <summary>The K.O. stamp: a pitched-down body thump under a short crack.</summary>
        private static float[] Stamp(System.Random random)
        {
            float[] d = Buffer(.75f);
            double phase = 0.0;
            float lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float freq = 38f + 95f * Mathf.Exp(-t * 18f);
                phase += 2.0 * Math.PI * freq / Rate;
                float body = (float)Math.Sin(phase) * Mathf.Exp(-t * 5.2f);
                lp += .5f * (Noise(random) - lp);
                float crack = lp * Mathf.Exp(-t * 60f);
                float sub = .35f * Mathf.Sin(2f * Mathf.PI * 52f * t) * Mathf.Exp(-t * 3.2f);
                // A gentle tanh saturation gives the thump weight on small speakers.
                d[i] = (float)Math.Tanh(1.8f * (body + sub) + .9f * crack);
            }
            return d;
        }

        /// <summary>A bright four-note rising sting that sits on top of the recorded win clip.</summary>
        private static float[] Fanfare()
        {
            float[] d = Buffer(1.5f);
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
            for (int n = 0; n < notes.Length; n++)
            {
                bool last = n == notes.Length - 1;
                int start = Mathf.RoundToInt(n * .09f * Rate);
                float decay = last ? 2.4f : 9f;
                for (int i = start; i < d.Length; i++)
                {
                    float t = (i - start) / (float)Rate;
                    float env = Mathf.Min(1f, t * 250f) * Mathf.Exp(-t * decay);
                    if (env < .0005f && t > .05f) break;
                    float f = notes[n];
                    float vib = last ? 1f + .006f * Mathf.Sin(2f * Mathf.PI * 5.5f * t) * Mathf.Min(1f, t * 3f) : 1f;
                    float w = 2f * Mathf.PI * f * vib * t;
                    // Odd harmonics give it a brass-like edge without going harsh.
                    d[i] += env * (Mathf.Sin(w) + .33f * Mathf.Sin(3f * w) + .12f * Mathf.Sin(5f * w));
                }
            }
            return d;
        }

        /// <summary>A low, inharmonic bell for a lost match: one toll, slowly fading.</summary>
        private static float[] Toll()
        {
            float[] d = Buffer(2.2f);
            float[] ratios = { 1f, 2.0f, 2.76f, 5.4f };
            float[] amps = { 1f, .5f, .35f, .12f };
            float[] decays = { 1.6f, 2.4f, 3.4f, 6f };
            const float fundamental = 146.8f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float attack = Mathf.Min(1f, t * 120f);
                float sum = 0f;
                for (int p = 0; p < ratios.Length; p++)
                    sum += amps[p] * Mathf.Exp(-t * decays[p]) * Mathf.Sin(2f * Mathf.PI * fundamental * ratios[p] * t);
                d[i] = attack * sum;
            }
            return d;
        }
    }
}
