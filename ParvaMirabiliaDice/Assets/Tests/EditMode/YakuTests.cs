using System;
using System.Collections.Generic;
using NUnit.Framework;
using ParvaMirabilia.Core;

namespace ParvaMirabilia.Tests
{
    /// <summary>
    /// The declared yaku: what a hand is allowed to say, and what each declaration is worth.
    ///
    /// The yaku set replaces two things at once - the hand-built strike/ward split, and the flat
    /// formation bonus - so these tests pin the properties both replacements were chosen for:
    ///
    /// - every offer partitions the dice exactly, so a declaration can never leave a die homeless
    /// - no offer is free: each one buys strike by giving up ward
    /// - the rare yaku pay a printed number while the common ones pay pips, so a hand's worth
    ///   does not collapse into one pip total
    /// </summary>
    public class YakuTests
    {
        private static GameConfig Shipped()
        {
            return new GameConfig().ApplyShippedPatternLayer();
        }

        private static IEnumerable<int[]> EveryHand()
        {
            for (int a = 1; a <= 6; a++)
                for (int b = 1; b <= 6; b++)
                    for (int c = 1; c <= 6; c++)
                        for (int d = 1; d <= 6; d++)
                            for (int e = 1; e <= 6; e++)
                                yield return new[] { a, b, c, d, e };
        }

        // ------------------------------------------------------------ what may be declared

        [Test]
        public void Evaluate_AlwaysOffersAWardAndSomethingBesidesIt()
        {
            // The worst roll in the game still has to be committable, or the round can deadlock.
            var config = Shipped();
            foreach (var hand in new[]
                     {
                         new[] { 1, 1, 2, 3, 4 },
                         new[] { 1, 2, 3, 5, 6 },
                         new[] { 1, 1, 1, 1, 1 },
                         new[] { 2, 3, 4, 5, 6 },
                         new[] { 6, 6, 6, 6, 6 }
                     })
            {
                var offers = Yaku.Evaluate(hand, config);

                Assert.That(Has(offers, YakuKind.Guard), Is.True,
                    "守護 must always be declarable");
                Assert.That(offers.Length, Is.GreaterThan(1),
                    "a hand must offer a real choice, not just the pure ward - " + Hand(hand));
            }
        }

        [Test]
        public void Evaluate_ShapelessHandStillOffersAPipScoredYaku()
        {
            // The escape valve exists for hands with no shape at all. It can be shadowed by a
            // triple that nominates the same three dice, but never by a shapeless hand, because
            // a shapeless hand has no triple to shadow it.
            var config = Shipped();

            foreach (var hand in EveryHand())
            {
                if (HasFaceAtLeast(hand, 2) || HasRun(hand, 4))
                    continue;

                Assert.That(Has(Yaku.Evaluate(hand, config), YakuKind.Chance), Is.True,
                    "a hand with no shape at all must still have a pip-scored option: " + Hand(hand));
            }
        }

        [Test]
        public void Evaluate_OnlyOffersWhatTheHandQualifiesFor()
        {
            var config = Shipped();

            foreach (var hand in EveryHand())
            {
                var kinds = new HashSet<YakuKind>(KindsOf(Yaku.Evaluate(hand, config)));

                Assert.That(kinds.Contains(YakuKind.Pair), Is.EqualTo(HasFaceAtLeast(hand, 2)),
                    "ペア on " + Hand(hand));
                Assert.That(kinds.Contains(YakuKind.Triple), Is.EqualTo(HasFaceAtLeast(hand, 3)),
                    "スリーカインド on " + Hand(hand));
                Assert.That(kinds.Contains(YakuKind.Five), Is.EqualTo(HasFaceAtLeast(hand, 5)),
                    "ヤッツィー on " + Hand(hand));
                Assert.That(kinds.Contains(YakuKind.SmallRun), Is.EqualTo(HasRun(hand, 4)),
                    "小ストレート on " + Hand(hand));
                Assert.That(kinds.Contains(YakuKind.LargeRun), Is.EqualTo(HasRun(hand, 5)),
                    "大ストレート on " + Hand(hand));
            }
        }

        [Test]
        public void Evaluate_NeverOffersAHandTheDiceDoNotContain()
        {
            var config = Shipped();

            foreach (var hand in EveryHand())
            {
                foreach (var offer in Yaku.Evaluate(hand, config))
                {
                    // 役が「成立条件を満たさないのに出る」こと、および前へ出す個数が
                    // 段（0/2/3/4/5）から外れることを止める。
                    int[] forward = FacesOf(hand, offer.AttackMask);
                    switch (offer.Kind)
                    {
                        case YakuKind.Guard:
                            Assert.That(forward.Length, Is.EqualTo(0));
                            break;
                        case YakuKind.Pair:
                            Assert.That(forward.Length, Is.EqualTo(2));
                            Assert.That(forward[0], Is.EqualTo(forward[1]),
                                "ペア must send two dice of the same face on " + Hand(hand));
                            break;
                        case YakuKind.Triple:
                            Assert.That(forward.Length, Is.EqualTo(3));
                            Assert.That(forward[0], Is.EqualTo(forward[1]));
                            Assert.That(forward[1], Is.EqualTo(forward[2]));
                            break;
                        case YakuKind.Chance:
                            Assert.That(forward.Length, Is.EqualTo(3));
                            break;
                        case YakuKind.SmallRun:
                            Assert.That(forward.Length, Is.EqualTo(4));
                            AssertConsecutive(forward, hand);
                            break;
                        case YakuKind.LargeRun:
                            Assert.That(forward.Length, Is.EqualTo(5));
                            AssertConsecutive(forward, hand);
                            break;
                        case YakuKind.Five:
                            Assert.That(forward.Length, Is.EqualTo(5));
                            Assert.That(new HashSet<int>(forward).Count, Is.EqualTo(1),
                                "ヤッツィー must send five dice of one face");
                            break;
                        default:
                            Assert.Fail("unexpected yaku " + offer.Kind);
                            break;
                    }
                }
            }
        }

        [Test]
        public void FiveOfAKind_IsNotAStraight()
        {
            // Five of a kind is five of one face, never five consecutive faces. Both can never be
            // declarable from the same hand, so the two top rungs cannot shadow each other.
            var offers = Yaku.Evaluate(new[] { 4, 4, 4, 4, 4 }, Shipped());
            var kinds = KindsOf(offers);

            Assert.That(Has(kinds, YakuKind.Five), Is.True);
            Assert.That(Has(kinds, YakuKind.SmallRun), Is.False);
            Assert.That(Has(kinds, YakuKind.LargeRun), Is.False);
        }

        // ------------------------------------------------------------ what a declaration is worth

        [Test]
        public void EveryOffer_PartitionsTheDiceExactly()
        {
            var config = Shipped();

            foreach (var hand in EveryHand())
            {
                int all = config.AllMask;
                foreach (var offer in Yaku.Evaluate(hand, config))
                {
                    Assert.That(offer.AttackMask | offer.GuardMask, Is.EqualTo(all),
                        offer.Kind + " on " + Hand(hand) + " leaves a die unassigned");
                    Assert.That(offer.AttackMask & offer.GuardMask, Is.EqualTo(0),
                        offer.Kind + " on " + Hand(hand) + " assigns a die twice");
                    Assert.That(offer.ToSplit().AttackMask, Is.EqualTo(offer.AttackMask));
                    Assert.That(offer.ToSplit().GuardMask, Is.EqualTo(offer.GuardMask));
                }
            }
        }

        [Test]
        public void EveryOffer_BuysStrikeByGivingUpWard()
        {
            // The ladder's whole claim: moving up costs ward. If two offers had the same ward, or
            // one had more of both, "which yaku" would collapse into "the bigger number".
            var config = Shipped();

            foreach (var hand in EveryHand())
            {
                var offers = Yaku.Evaluate(hand, config);
                int total = Scoring.Sum(hand);

                foreach (var offer in offers)
                {
                    if (offer.Kind == YakuKind.Guard)
                    {
                        Assert.That(offer.WardValue(hand), Is.EqualTo(total),
                            "守護 must take the whole hand as the ward");
                        continue;
                    }

                    Assert.That(offer.ForwardCount, Is.GreaterThan(0));
                    Assert.That(offer.WardValue(hand), Is.LessThan(total),
                        offer.Kind + " on " + Hand(hand) + " forwards dice but keeps the full ward");
                    Assert.That(offer.StrikeValue(hand), Is.GreaterThan(0),
                        offer.Kind + " on " + Hand(hand) + " deals no strike at all");
                }
            }
        }

        [Test]
        public void RareYakuPayAPrintedNumber_CommonYakuPayThePips()
        {
            var config = Shipped();

            // Yahtzee pays its straights and five of a kind a fixed amount and scores three of a
            // kind and Chance as the total. That split is what stops a hand's worth from being
            // one pip total: ones and fives pay the same for a small straight.
            Assert.That(Yaku.Resolve(new[] { 1, 2, 3, 4, 6 }, config, YakuKind.SmallRun).StrikeValue(new[] { 1, 2, 3, 4, 6 }),
                Is.EqualTo(config.YakuSmallRunAttack));
            Assert.That(Yaku.Resolve(new[] { 3, 4, 5, 6, 6 }, config, YakuKind.SmallRun).StrikeValue(new[] { 3, 4, 5, 6, 6 }),
                Is.EqualTo(config.YakuSmallRunAttack), "a small straight pays the same whatever it is built from");

            var triple = new[] { 6, 6, 6, 1, 2 };
            Assert.That(Yaku.Resolve(triple, config, YakuKind.Triple).StrikeValue(triple),
                Is.EqualTo(18), "スリーカインド is pip-scored, so it pays the three dice");

            var lowTriple = new[] { 1, 1, 1, 5, 6 };
            Assert.That(Yaku.Resolve(lowTriple, config, YakuKind.Triple).StrikeValue(lowTriple),
                Is.EqualTo(3), "and therefore a low triple pays less than a high one");

            var chance = new[] { 6, 5, 4, 3, 2 };
            Assert.That(Yaku.Resolve(chance, config, YakuKind.Chance).StrikeValue(chance),
                Is.EqualTo(15));
        }

        [Test]
        public void EveryOffer_HasADistinctResult()
        {
            // On 6,6,6,5,4 the triple and the chance nominate the same three dice for the same
            // number. Two identical buttons are noise, so one of them is dropped.
            var hand = new[] { 6, 6, 6, 5, 4 };
            var offers = Yaku.Evaluate(hand, Shipped());
            var seen = new HashSet<string>();

            foreach (var offer in offers)
            {
                string key = offer.AttackMask + ":" + offer.FixedAttack;
                Assert.That(seen.Add(key), Is.True,
                    offer.Kind + " duplicates another offer on " + Hand(hand));
            }

            var kinds = KindsOf(offers);
            Assert.That(Has(kinds, YakuKind.Triple) || Has(kinds, YakuKind.Chance),
                Is.True, "one of the two identical rung-3 offers must survive");
        }

        [Test]
        public void StraightHand_TakesTheHighestRunAndLeavesTheOddDieBehind()
        {
            var hand = new[] { 1, 2, 3, 4, 5 };
            var offer = Yaku.Resolve(hand, Shipped(), YakuKind.SmallRun);

            // Two four-runs exist in 1-2-3-4-5 (1-4 and 2-5); the higher one is chosen so the
            // NPC, the UI and a replay all read the same hand the same way.
            Assert.That(FacesOf(hand, offer.AttackMask), Is.EqualTo(new[] { 2, 3, 4, 5 }));
            Assert.That(offer.WardValue(hand), Is.EqualTo(1));
        }

        [Test]
        public void TripleAndChance_AreDifferentTradesOnTheSameHand()
        {
            // The one case that decides whether "which yaku" is a real question at all: on the
            // most ordinary hand in the game, the two rung-3 options must not be the same bet.
            var hand = new[] { 3, 3, 3, 5, 6 };
            var config = Shipped();

            var triple = Yaku.Resolve(hand, config, YakuKind.Triple);
            var chance = Yaku.Resolve(hand, config, YakuKind.Chance);

            Assert.That(triple.StrikeValue(hand), Is.EqualTo(9), "スリーカインド sends 3,3,3 forward");
            Assert.That(triple.WardValue(hand), Is.EqualTo(11), "and leaves 5+6 behind");
            Assert.That(chance.StrikeValue(hand), Is.EqualTo(14), "チャンス sends 6,5,3 forward");
            Assert.That(chance.WardValue(hand), Is.EqualTo(6), "and leaves 3+3 behind");

            Assert.That(triple.WardValue(hand), Is.Not.EqualTo(chance.WardValue(hand)),
                "the same rung must still be two different trades");
        }

        // ------------------------------------------------------------ defaults and fallbacks

        [Test]
        public void Default_IsAlwaysOneOfTheOffers()
        {
            var config = Shipped();

            foreach (var hand in EveryHand())
            {
                YakuOffer fallback = Yaku.Default(hand, config);
                bool found = false;
                foreach (var offer in Yaku.Evaluate(hand, config))
                {
                    if (offer.Kind == fallback.Kind)
                    {
                        found = true;
                        break;
                    }
                }

                Assert.That(found, Is.True, "default on " + Hand(hand) + " is not declarable");
            }
        }

        [Test]
        public void Resolve_FallsBackToTheDefaultWhenTheHandDoesNotQualify()
        {
            var config = Shipped();
            var hand = new[] { 1, 2, 3, 5, 6 }; // no pair, no straight

            Assert.That(Has(Yaku.Evaluate(hand, config), YakuKind.Five), Is.False);

            YakuOffer resolved = Yaku.Resolve(hand, config, YakuKind.Five);
            Assert.That(resolved.Kind, Is.Not.EqualTo(YakuKind.Five));
            Assert.That(resolved.Kind, Is.EqualTo(Yaku.Default(hand, config).Kind),
                "an unavailable declaration must land on the default, not on nothing");
        }

        [Test]
        public void Evaluate_IsDeterministic()
        {
            var hand = new[] { 4, 4, 5, 5, 6 };
            var config = Shipped();

            var first = Yaku.Evaluate(hand, config);
            var second = Yaku.Evaluate(hand, config);

            Assert.That(first.Length, Is.EqualTo(second.Length));
            for (int i = 0; i < first.Length; i++)
            {
                Assert.That(first[i].Kind, Is.EqualTo(second[i].Kind));
                Assert.That(first[i].AttackMask, Is.EqualTo(second[i].AttackMask));
                Assert.That(first[i].FixedAttack, Is.EqualTo(second[i].FixedAttack));
            }
        }

        // ------------------------------------------------------------ through the engine

        [Test]
        public void CommitYaku_RecordsBothDeclarationsAndResolvesTheStatedValue()
        {
            var config = Shipped();

            for (int seed = 0; seed < 60; seed++)
            {
                var engine = new MatchEngine(config, new SeededRandom(seed), NpcArchetype.Balanced);
                engine.Roll();
                int[] dice = (int[])engine.PlayerDice.Clone();

                var outcome = engine.CommitYaku(YakuKind.Guard);

                Assert.That(outcome.PlayerYaku, Is.EqualTo(YakuKind.Guard));
                Assert.That(outcome.NpcYaku, Is.Not.EqualTo(YakuKind.None),
                    "the NPC must have declared something");
                Assert.That(outcome.PlayerAttackSum, Is.EqualTo(0), "守護 strikes with nothing");
                Assert.That(outcome.PlayerGuardSum, Is.EqualTo(Scoring.Sum(dice)),
                    "守護 wards with everything");

                // Whatever the NPC declared, the number it put forward must be the number its own
                // yaku prints - a declaration resolving to something else would mean the rule and
                // the display had drifted apart.
                YakuOffer npcOffer = Yaku.Resolve(outcome.NpcDice, config, outcome.NpcYaku);
                Assert.That(outcome.NpcAttackSum, Is.EqualTo(npcOffer.StrikeValue(outcome.NpcDice)),
                    "NPC declared " + outcome.NpcYaku + " on " + Hand(outcome.NpcDice));
                Assert.That(outcome.NpcGuardSum, Is.EqualTo(npcOffer.WardValue(outcome.NpcDice)));
            }
        }

        [Test]
        public void CommitYaku_PaysThePrintedStakeThroughTheEngine()
        {
            // The printed yaku are the ones the whole split-vs-sum design rests on, so the chain
            // has to be proven end to end: a declared small straight must reach the round outcome
            // as the configured number, not as the pips underneath it.
            var config = Shipped();
            bool verified = false;

            for (int seed = 0; seed < 2000 && !verified; seed++)
            {
                var engine = new MatchEngine(config, new SeededRandom(seed), NpcArchetype.Balanced);
                engine.Roll();

                YakuOffer offer = Yaku.Resolve(engine.PlayerDice, config, YakuKind.SmallRun);
                if (offer.Kind != YakuKind.SmallRun)
                    continue;

                int expectedWard = offer.WardValue(engine.PlayerDice);
                int pips = Scoring.SumMask(engine.PlayerDice, offer.AttackMask);
                if (pips == config.YakuSmallRunAttack)
                    continue; // would prove nothing; find a hand where the printed number differs

                var outcome = engine.CommitYaku(YakuKind.SmallRun);

                Assert.That(outcome.PlayerYaku, Is.EqualTo(YakuKind.SmallRun));
                Assert.That(outcome.PlayerAttackSum, Is.EqualTo(config.YakuSmallRunAttack),
                    "a declared small straight pays the printed stake, not its pips (" + pips + ")");
                Assert.That(outcome.PlayerGuardSum, Is.EqualTo(expectedWard));
                verified = true;
            }

            Assert.That(verified, Is.True,
                "no seed in 2000 produced a small straight whose pips differ from its stake");
        }

        [Test]
        public void CommitYaku_LeavesTheHandDrivenPathUntouched()
        {
            // The yaku path must be additive: with it switched off the engine still behaves the
            // way every pre-existing test says it does.
            var legacy = new GameConfig();
            Assert.That(legacy.YakuEnabled, Is.False);

            var engine = new MatchEngine(legacy, new SeededRandom(7), NpcArchetype.Balanced);
            engine.Roll();
            var outcome = engine.CommitSplit(new[] { 0, 1 }, new[] { 2, 3, 4 });

            Assert.That(outcome.PlayerYaku, Is.EqualTo(YakuKind.None));
            Assert.That(outcome.NpcYaku, Is.EqualTo(YakuKind.None));
            Assert.That(outcome.PlayerAttackSum, Is.EqualTo(Scoring.SumMask(engine.PlayerDice, 0b00011)));
        }

        // ------------------------------------------------------------ helpers

        private static bool Has(YakuOffer[] offers, YakuKind kind)
        {
            for (int i = 0; i < offers.Length; i++)
            {
                if (offers[i].Kind == kind)
                    return true;
            }
            return false;
        }

        private static bool Has(YakuKind[] kinds, YakuKind kind)
        {
            return Array.IndexOf(kinds, kind) >= 0;
        }

        private static YakuKind[] KindsOf(YakuOffer[] offers)
        {
            var kinds = new YakuKind[offers.Length];
            for (int i = 0; i < offers.Length; i++)
                kinds[i] = offers[i].Kind;
            return kinds;
        }

        private static int[] FacesOf(int[] dice, int mask)
        {
            var found = new List<int>();
            for (int i = 0; i < dice.Length; i++)
            {
                if ((mask & (1 << i)) != 0)
                    found.Add(dice[i]);
            }
            return found.ToArray();
        }

        private static bool HasFaceAtLeast(int[] dice, int howMany)
        {
            var count = new int[7];
            for (int i = 0; i < dice.Length; i++)
                count[dice[i]]++;

            for (int face = 1; face <= 6; face++)
            {
                if (count[face] >= howMany)
                    return true;
            }
            return false;
        }

        private static bool HasRun(int[] dice, int length)
        {
            var count = new int[7];
            for (int i = 0; i < dice.Length; i++)
                count[dice[i]]++;

            for (int low = 1; low + length - 1 <= 6; low++)
            {
                bool complete = true;
                for (int k = 0; k < length; k++)
                {
                    if (count[low + k] == 0)
                    {
                        complete = false;
                        break;
                    }
                }
                if (complete)
                    return true;
            }
            return false;
        }

        private static void AssertConsecutive(int[] forward, int[] hand)
        {
            var sorted = (int[])forward.Clone();
            Array.Sort(sorted);
            for (int i = 1; i < sorted.Length; i++)
            {
                Assert.That(sorted[i] - sorted[i - 1], Is.EqualTo(1),
                    "expected consecutive faces in [" + string.Join(",", forward) + "]");
            }
        }

        private static string Hand(int[] dice)
        {
            return "[" + string.Join(",", dice) + "]";
        }
    }
}
