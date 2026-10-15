using System;

namespace ParvaMirabilia.Core
{
    /// <summary>Tunable rules and balance values for one match.</summary>
    public sealed class GameConfig
    {
        /// <summary>Dice rolled each round.</summary>
        public int DiceCount = 5;

        /// <summary>Lowest pip (inclusive).</summary>
        public int DieMin = 1;

        /// <summary>Highest pip (inclusive).</summary>
        public int DieMax = 6;

        /// <summary>How many times the player may re-roll per round (Yahtzee DNA: hold + re-roll).</summary>
        public int MaxRerolls = 2;

        /// <summary>Hit points both sides start with.</summary>
        public int StartingHp = 20;

        /// <summary>Hard round limit so a stalemate still ends. 0 disables the limit.</summary>
        public int MaxRounds = 10;

        /// <summary>
        /// Damage dealt by the side with the higher roll total when neither strike got through.
        /// Without it two ward-heavy lines could not hurt each other and the match would crawl
        /// into the round cap for no decision.
        /// </summary>
        public int PushbackDamage = 3;

        /// <summary>
        /// Master switch for the pattern layer.
        ///
        /// It defaults to <c>false</c> so that the pre-existing suite still measures the game as
        /// it was written, and the shipped build (see <c>GameFlow.StartMatch</c>) turns it on.
        /// Keeping the old rules constructible is what makes the redesign an A/B comparison
        /// instead of an assertion that the new rule is better.
        /// </summary>
        public bool FormationsEnabled = false;

        /// <summary>Bonus damage for a cashed pair / triple / run.</summary>
        public int PairBonus = 2;
        public int TripleBonus = 5;
        public int RunBonus = 4;

        /// <summary>
        /// How many consecutive faces make a run. Three is easy in five dice - it fires more
        /// often than not - so a run stopped being a hand worth building and became a flat tax
        /// on every round. Four is a small straight: rare enough to chase, common enough to see.
        /// </summary>
        public int RunLength = 3;

        /// <summary>
        /// Master switch for the yaku the player declares at the end of a round
        /// (<see cref="Yaku"/>). It replaces the hand-driven strike/ward split, not the damage
        /// formula: the declared yaku still resolves into a strike sum and a ward sum, and
        /// <c>max(0, strike − ward)</c> is unchanged.
        ///
        /// Defaults to <c>false</c> so the pre-existing suite still measures the rules it was
        /// written against, and <see cref="ApplyShippedPatternLayer"/> turns it on for the build
        /// that ships. When it is on, the old formation bonus is <b>not</b> paid on top: the
        /// printed yaku <em>is</em> the payoff, and stacking the two would pay for one hand twice.
        /// </summary>
        public bool YakuEnabled = false;

        /// <summary>Telegraphed duels with spell recovery and banked rerolls.</summary>
        public bool TacticalEnabled = false;
        public int MaxMana = 3;
        public int StartingMana = 1;
        public int EmpowerAttack = 5;
        // Negative values share the base rules. Runs pin these before applying human blessings.
        public int NpcMaxMana = -1;
        public int NpcStartingMana = -1;
        public int NpcEmpowerAttack = -1;
        public int TacticalPairWard = 2;
        public int TacticalTripleAttack = 3;
        /// <summary>Run-specific openings. Zero uses full StartingHp.</summary>
        public int PlayerOpeningHp = 0;
        public int NpcStartingHp = 0;
        public int[] PlayerSpellAttackBonus = new int[7];
        public int[] PlayerSpellWardBonus = new int[7];

        public GameConfig ApplyTacticalDuelLayer()
        {
            ApplyShippedPatternLayer();
            TacticalEnabled = true;
            return this;
        }

        /// <summary>
        /// Printed strike of the yaku that are worth a fixed number rather than a pip total,
        /// mirroring Yahtzee's Full House / straights / Yahtzee.
        ///
        /// These three are provisional. They are the balance-critical values of the whole yaku
        /// system and must be settled by a sweep over match data, the same way the formation
        /// bonuses were (logs/formation-sweep.json). What is <em>not</em> provisional is their
        /// role: the pip-scored yaku (ペア / スリーカインド / チャンス) stay pip-scored on purpose, so that
        /// "send the shape forward or bank the pips" remains a live comparison.
        /// </summary>
        public int YakuSmallRunAttack = 16;
        public int YakuLargeRunAttack = 22;
        public int YakuFiveAttack = 26;

        public int AllMask
        {
            get { return (1 << DiceCount) - 1; }
        }

        /// <summary>
        /// The pattern-layer settings the shipped build plays with, chosen from the sweep in
        /// logs/formation-sweep.json rather than by feel:
        ///
        /// - A pair is present in nine rolls out of ten, so it could never be a hand worth
        ///   building; it is switched off and the layer keeps the two hands that cost a
        ///   decision. Both also force dice onto the strike side to fire (a run needs four of
        ///   five), which ties the pattern directly to the strike/ward commitment.
        /// - The run needs four consecutive faces. At three it fired on 0.73 of sides and
        ///   carried 0.39 of all damage; at four the same figures are 0.51 and 0.31.
        /// - Hit points rise with the damage the bonuses add, so the match keeps roughly the
        ///   length the original rules had: 4.19 rounds against 4.65, with a lower draw rate
        ///   (0.0053 vs 0.0133) and fewer matches reaching the round cap (0.0137 vs 0.0707).
        /// </summary>
        public GameConfig ApplyShippedPatternLayer()
        {
            FormationsEnabled = true;
            PairBonus = 0;
            TripleBonus = 5;
            RunBonus = 5;
            RunLength = 4;
            StartingHp = 32;

            // The build that ships declares yaku instead of walking each die through a cycle.
            YakuEnabled = true;
            return this;
        }

        public GameConfig Clone()
        {
            var clone = (GameConfig)MemberwiseClone();
            clone.PlayerSpellAttackBonus = (int[])PlayerSpellAttackBonus.Clone();
            clone.PlayerSpellWardBonus = (int[])PlayerSpellWardBonus.Clone();
            return clone;
        }
    }
}
