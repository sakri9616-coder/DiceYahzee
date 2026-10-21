using UnityEngine;

namespace ParvaMirabilia.Presentation
{
    /// <summary>
    /// The single health-bar timeline shared by the solo and online duels.
    ///
    /// A settled round has two beats, in rules order: the kept-reroll heal lands first, then
    /// the strike's damage. The bars and their one-line readouts follow those beats instead of
    /// jumping straight to the net result, so a green +N is never printed beside a bar that is
    /// already falling and a red -N is never printed beside a bar that is still rising.
    /// </summary>
    internal static class HealthPresentation
    {
        /// <summary>Healing begins after the shared hit-stop, before the strike reaches contact.</summary>
        public const float HealStart = SharedAttackPresenter.HitStopEnd;
        /// <summary>The strike lands at the same instant the attack presenter reaches contact.</summary>
        public const float DamageStart = SharedAttackPresenter.ContactTime;
        /// <summary>Long enough to read the drop after the strike, while the attack overlay is gone.</summary>
        public const float DamageDuration = 0.60f;
        /// <summary>Both phases end here; callers keep the impact gate closed until this age.</summary>
        public const float End = DamageStart + DamageDuration;

        /// <summary>One sample of a bar: the visible health and the figure its line should name.</summary>
        public readonly struct Frame
        {
            public readonly float Hp;
            /// <summary>The heal to print in green, or 0 outside the healing beat.</summary>
            public readonly int Heal;
            /// <summary>The damage to print in red, or 0 outside the damage beat.</summary>
            public readonly int Damage;
            /// <summary>The bottom of the loss band, before the damage phase.</summary>
            public readonly float LossFrom;
            /// <summary>The bottom of the loss band after the damage phase.</summary>
            public readonly int LossTo;
            /// <summary>1 at the start of the damage beat, falling to 0 at <see cref="End"/>.</summary>
            public readonly float LossAlpha;

            public Frame(float hp, int heal, int damage, float lossFrom, int lossTo, float lossAlpha)
            {
                Hp = hp; Heal = heal; Damage = damage;
                LossFrom = lossFrom; LossTo = lossTo; LossAlpha = lossAlpha;
            }
        }

        /// <summary>The final health after the capped heal and the damage, never below zero.</summary>
        public static int After(int before, int heal, int damage, int maxHp)
        {
            int appliedHeal = Mathf.Clamp(heal, 0, Mathf.Max(0, maxHp - before));
            return Mathf.Max(0, before + appliedHeal - Mathf.Max(0, damage));
        }

        /// <summary>
        /// Samples the shared timeline at an absolute age measured from the start of the impact.
        /// Before <see cref="HealStart"/> the bar is still at the pre-round health, but the coming
        /// heal is already named in green; during the heal beat the bar rises to the capped value
        /// and keeps naming only that heal; during the damage beat it falls to the final value and
        /// names only the red drop. At and after <see cref="End"/> the bar rests at the final value
        /// with no figure.
        /// </summary>
        public static Frame At(float age, int before, int heal, int damage, int maxHp)
        {
            maxHp = Mathf.Max(1, maxHp);
            before = Mathf.Clamp(before, 0, maxHp);
            int appliedHeal = Mathf.Clamp(heal, 0, maxHp - before);
            int healed = before + appliedHeal;
            int appliedDamage = Mathf.Max(0, damage);
            int after = Mathf.Max(0, healed - appliedDamage);

            if (age <= HealStart)
                // Name the coming heal from the first frame of the impact. The bar has not moved
                // yet, but keeping the green figure avoids a one-frame flicker between the
                // pre-commit forecast and the heal beat.
                return new Frame(before, appliedHeal, 0, before, before, 0f);
            if (age < DamageStart)
            {
                float t = Smooth((age - HealStart) / Mathf.Max(0.0001f, DamageStart - HealStart));
                return new Frame(Mathf.Lerp(before, healed, t), appliedHeal, 0, before, before, 0f);
            }
            if (age < End)
            {
                float t = Smooth((age - DamageStart) / Mathf.Max(0.0001f, End - DamageStart));
                return new Frame(Mathf.Lerp(healed, after, t), 0, appliedDamage, healed, after, 1f - t);
            }
            return new Frame(after, 0, 0, after, after, 0f);
        }

        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
