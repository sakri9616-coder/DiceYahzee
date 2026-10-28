using UnityEngine;
using UnityEngine.UI;

namespace ParvaMirabilia.Presentation
{
    /// <summary>
    /// Green motes that rise off a health bar while its kept-reroll heal is filling in.
    ///
    /// The heal beat of <see cref="HealthPresentation"/> only moved the bar, so a restored forty
    /// read the same as a bar sliding back after a miss. A few sparks lifting off the growing edge
    /// say "this came back" without a word. The field lives on the bar's track, is never a raycast
    /// target, and draws nothing at all under the reduced-motion switch.
    /// </summary>
    internal sealed class HealMotes : MonoBehaviour
    {
        private const float Rate = 44f;
        private const int Capacity = 72;
        private UiParticles _field;
        private float _debt;
        private int _lastFrame = -1;

        /// <summary>Called with every sampled health frame; emits only while the bar is rising.</summary>
        public static void Feed(Image fill, HealthPresentation.Frame frame, int maximum)
        {
            if (fill == null || Juice.ReducedMotion || !Application.isPlaying) return;
            Transform track = fill.transform.parent;
            if (track == null) return;
            bool rising = frame.Heal > 0 && frame.Damage == 0 && frame.Hp > frame.LossFrom + .01f;
            if (!rising) return;
            var motes = track.GetComponent<HealMotes>();
            if (motes == null) motes = track.gameObject.AddComponent<HealMotes>();
            motes.Emit(fill, frame, Mathf.Max(1, maximum));
        }

        private void Emit(Image fill, HealthPresentation.Frame frame, int maximum)
        {
            if (_lastFrame == Time.frameCount) return;
            _lastFrame = Time.frameCount;
            if (_field == null)
            {
                _field = UiParticles.Create("HealMotes", transform, Capacity, true);
                // Above the fill, below the label that follows it.
                _field.transform.SetSiblingIndex(fill.transform.GetSiblingIndex() + 1);
            }

            Rect area = _field.rectTransform.rect;
            float barWidth = fill.rectTransform.sizeDelta.x;
            float fromWidth = area.width * Mathf.Clamp01(frame.LossFrom / maximum);
            float thickness = Mathf.Max(8f, area.height);
            _debt += Rate * Mathf.Min(Time.unscaledDeltaTime, .05f);
            while (_debt >= 1f)
            {
                _debt -= 1f;
                // Most motes leave the growing edge; the rest twinkle along the restored span.
                float x = Random.value < .6f
                    ? barWidth - Random.Range(0f, 6f)
                    : Mathf.Lerp(fromWidth, barWidth, Random.value);
                bool white = Random.value < .3f;
                float size = Random.Range(4f, 8.5f);
                _field.Spawn(new UiParticles.Particle
                {
                    Position = new Vector2(area.xMin + x, area.center.y + Random.Range(-.4f, .4f) * thickness),
                    Velocity = new Vector2(Random.Range(-18f, 34f), Random.Range(46f, 104f)),
                    Size = new Vector2(size, size),
                    Life = Random.Range(.5f, .85f),
                    Gravity = -40f,
                    Drag = 1.6f,
                    Flutter = Random.Range(8f, 22f),
                    Phase = Random.Range(0f, 6.28f),
                    Hold = .35f,
                    Color = white ? new Color32(246, 255, 236, 235) : new Color32(150, 238, 172, 225)
                });
            }
        }

        private void Update()
        {
            if (_field == null || _field.Count == 0) return;
            if (Juice.ReducedMotion) { _field.Clear(); return; }
            _field.Step(Time.unscaledDeltaTime);
        }

        private void OnDisable()
        {
            _debt = 0f;
            if (_field != null) _field.Clear();
        }
    }
}
