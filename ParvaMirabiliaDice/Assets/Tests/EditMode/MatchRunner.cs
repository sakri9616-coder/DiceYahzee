using System;
using ParvaMirabilia.Core;

namespace ParvaMirabilia.Tests
{
    /// <summary>
    /// Drives a MatchEngine without any UI, using the same decision logic the NPC uses.
    /// This is what makes whole matches runnable inside NUnit and inside batch simulations.
    /// </summary>
    public static class MatchRunner
    {
        public static int[] IndicesOf(int mask)
        {
            var indices = new int[SplitDecision.CountBits(mask)];
            int next = 0;
            for (int i = 0; i < 31; i++)
            {
                if ((mask & (1 << i)) != 0)
                    indices[next++] = i;
            }
            return indices;
        }

        /// <summary>
        /// Applies the brain's re-roll policy to the human seat.
        ///
        /// Both seats have to run this exact logic. They did not: the NPC went through
        /// <see cref="NpcBrain.TakeTurn"/> while this helper held a plain threshold, and the two
        /// only happened to agree while patterns were switched off. With patterns on the seats
        /// were playing different games, and the first seat took 72% of mirror matches before
        /// anything looked wrong. The seat-bias assertion in the round-robin exists to catch this
        /// again.
        /// </summary>
        public static void ApplyRerollPolicy(MatchEngine engine, NpcArchetype archetype)
        {
            if (engine.Phase != MatchPhase.AwaitingReroll)
                return;

            bool behind = engine.PlayerHp < engine.NpcHp;

            int safety = 0;
            while (engine.PlayerRerollsLeft > 0 && safety++ < 10)
            {
                bool[] keep = NpcBrain.PlanReroll(engine.PlayerDice, engine.Config, archetype, behind);
                if (keep == null)
                    break;

                engine.SetKept(keep);
                engine.Reroll();
            }

            // Nothing held: leave the keep flags clear so the commit shows the real dice.
            engine.SetKept(new bool[engine.PlayerDice.Length]);
        }

        public static MatchOutcome Run(MatchEngine engine, NpcArchetype playerPolicy)
        {
            var brain = new NpcBrain(playerPolicy);
            int safety = 0;

            while (engine.Outcome == null)
            {
                if (safety++ > 500)
                    throw new InvalidOperationException("match never finished (phase " + engine.Phase + ")");

                switch (engine.Phase)
                {
                    case MatchPhase.AwaitingRoll:
                        engine.Roll();
                        break;

                    case MatchPhase.AwaitingReroll:
                        ApplyRerollPolicy(engine, playerPolicy);
                        Commit(engine, brain);
                        break;

                    case MatchPhase.AwaitingSplit:
                        Commit(engine, brain);
                        break;

                    case MatchPhase.Resolving:
                        engine.NextRound();
                        break;

                    case MatchPhase.Finished:
                        break;

                    default:
                        throw new InvalidOperationException("unexpected phase " + engine.Phase);
                }
            }

            return engine.Outcome;
        }

        private static void Commit(MatchEngine engine, NpcBrain brain)
        {
            // Both seats must declare the same way. They are driven by the same brain, but which
            // path the brain takes depends on the rules in force: taking the hand-built split here
            // while the engine's own NPC declares a yaku plays two different games. It did, and
            // the first seat took 77.6% of mirror matches before the seat-bias assertion caught it
            // (logs/editmode-results.xml).
            if (engine.Config.YakuEnabled)
            {
                YakuOffer offer = brain.ChooseYaku(engine.PlayerDice, engine.Config,
                    engine.PlayerHp, engine.NpcHp);
                engine.CommitYaku(offer.Kind);
                return;
            }

            var split = brain.ChooseSplit(engine.PlayerDice, engine.Config, engine.PlayerHp, engine.NpcHp);
            engine.CommitSplit(IndicesOf(split.AttackMask), IndicesOf(split.GuardMask));
        }
    }
}
