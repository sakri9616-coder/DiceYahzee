using UnityEngine;
using UnityEngine.UI;

namespace ParvaMirabilia.Presentation
{
    public sealed partial class GameFlow
    {
        private struct ScorePulse
        {
            public int LastScore;
            public float Age;
            public bool Visible;
        }

        private ScorePulse _sdOwnScorePulse, _sdEnemyScorePulse;
        private ScorePulse _onlineOwnScorePulse, _onlineEnemyScorePulse;

        private static int SDScorePulseTier(int score) => score < 50 ? 0 : score < 100 ? 1 : score < 130 ? 2 : 3;

        private static void SDObserveScorePulse(Text label, ref ScorePulse pulse, int score)
        {
            if (label == null) return;
            if (score < 0)
            {
                pulse.Visible = false;
                pulse.Age = -1;
                label.rectTransform.localScale = Vector3.one;
                return;
            }
            if (!pulse.Visible || pulse.LastScore != score)
            {
                pulse.LastScore = score;
                pulse.Visible = true;
                pulse.Age = 0;
            }
        }

        private void SDTickScorePulse(Text label, ref ScorePulse pulse, float dt)
        {
            if (label == null || !pulse.Visible || pulse.Age < 0) return;
            int tier = SDScorePulseTier(pulse.LastScore);
            float duration = .28f + tier * .085f;
            pulse.Age += Mathf.Max(0, dt);
            if (_sdReducedMotion || pulse.Age >= duration)
            {
                label.rectTransform.localScale = Vector3.one;
                pulse.Age = -1;
                return;
            }
            float progress = Mathf.Clamp01(pulse.Age / duration);
            // The highest tier lands twice: a large first beat and a smaller echo.
            float envelope = tier == 3
                ? progress < .56f ? Mathf.Sin(progress / .56f * Mathf.PI)
                    : .38f * Mathf.Sin((progress - .56f) / .44f * Mathf.PI)
                : Mathf.Sin(progress * Mathf.PI);
            float amplitude = .09f + tier * .075f;
            label.rectTransform.localScale = Vector3.one * (1 + amplitude * envelope);
        }

        private void SDTickSingleScorePulses(float dt)
        {
            SDTickScorePulse(_sdScoreNumber, ref _sdOwnScorePulse, dt);
            SDTickScorePulse(_sdNpcScoreNumber, ref _sdEnemyScorePulse, dt);
        }

        private void SDTickOnlineScorePulses(float dt)
        {
            SDTickScorePulse(_onlineScore, ref _onlineOwnScorePulse, dt);
            SDTickScorePulse(_onlineEnemyScore, ref _onlineEnemyScorePulse, dt);
        }

        private void SDResetOnlineScorePulses()
        {
            SDObserveScorePulse(_onlineScore, ref _onlineOwnScorePulse, -1);
            SDObserveScorePulse(_onlineEnemyScore, ref _onlineEnemyScorePulse, -1);
        }

        private void SDResetSingleScorePulses()
        {
            SDObserveScorePulse(_sdScoreNumber, ref _sdOwnScorePulse, -1);
            SDObserveScorePulse(_sdNpcScoreNumber, ref _sdEnemyScorePulse, -1);
        }
    }
}
