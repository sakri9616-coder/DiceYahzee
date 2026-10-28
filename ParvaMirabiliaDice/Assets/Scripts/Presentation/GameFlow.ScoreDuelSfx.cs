using ParvaMirabilia.Core;

namespace ParvaMirabilia.Presentation
{
    public sealed partial class GameFlow
    {
        /// <summary>The impact age seen on the previous frame, used to catch the heal beat once.</summary>
        private float _sdHealCueAge = -1f;

        /// <summary>
        /// Rings the heal chime as the kept-reroll heal starts to rise on the bars. It listens to
        /// the shared health timeline instead of owning one, so it can never drift from the bars.
        /// </summary>
        private void SDCueHealSound()
        {
            float previous = _sdHealCueAge;
            _sdHealCueAge = _sdAttackAge;
            if (previous > _sdAttackAge) previous = -1f;          // a new impact has begun
            if (previous >= HealthPresentation.HealStart || _sdAttackAge < HealthPresentation.HealStart) return;
            if (_sdEngine == null) return;
            ScoreRound round = _sdEngine.LastRound;
            if (round == null || round.PlayerHeal + round.NpcHeal <= 0) return;
            GameAudio.Play("heal");
        }
    }
}
