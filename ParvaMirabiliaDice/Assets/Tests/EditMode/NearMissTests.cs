using System;
using System.Collections.Generic;
using NUnit.Framework;
using ParvaMirabilia.Core;

namespace ParvaMirabilia.Tests
{
    /// <summary>
    /// The "one more throw" hint: <see cref="Yaku.NearMisses"/>.
    ///
    /// A hint that names a hand the rules would not actually grant is worse than no hint, so the
    /// load-bearing assertion here is a property rather than an example - apply the near miss to
    /// the hand it came from and the yaku has to be there afterwards. The rest of the file pins
    /// down what must *not* be shown, because a hint that appears every round stops being a hint.
    /// </summary>
    public sealed class NearMissTests
    {
        private static readonly YakuKind[] PrintedKinds =
        {
            YakuKind.SmallRun, YakuKind.LargeRun, YakuKind.Five
        };

        private static GameConfig Shipped()
        {
            return new GameConfig().ApplyShippedPatternLayer();
        }

        private static string Hand(int[] dice)
        {
            return string.Join(",", dice);
        }

        private static bool Has(YakuKind[] kinds, YakuKind kind)
        {
            return Array.IndexOf(kinds, kind) >= 0;
        }

        private static YakuKind[] KindsOf(YakuNearMiss[] misses)
        {
            var kinds = new YakuKind[misses.Length];
            for (int i = 0; i < misses.Length; i++)
                kinds[i] = misses[i].Kind;
            return kinds;
        }

        // ------------------------------------------------------------ what it is allowed to say

        [Test]
        public void OnlyThePrintedYakuAreEverReported()
        {
            // 1,2,3,4,6 is one throw from a pair, a triple-free chance re-shuffle and a large
            // straight. Only the straight may be named: a pair is one throw away from almost any
            // hand, and a line that is always on screen is not a line anybody reads.
            var config = Shipped();
            var misses = Yaku.NearMisses(new[] { 1, 2, 3, 4, 6 }, config);

            Assert.That(misses.Length, Is.GreaterThan(0), "1,2,3,4,6 must name something");
            foreach (YakuKind kind in KindsOf(misses))
            {
                Assert.That(Has(PrintedKinds, kind), Is.True,
                    kind + " is not worth a hint line");
            }
        }

        [Test]
        public void AnAlreadyHeldYakuIsNotANearMiss()
        {
            // Holds both straights outright, and ヤッツィー is two throws away rather than one.
            var config = Shipped();
            var kinds = KindsOf(Yaku.NearMisses(new[] { 1, 2, 3, 4, 5 }, config));

            Assert.That(Has(kinds, YakuKind.SmallRun), Is.False, "小ストレート is already held");
            Assert.That(Has(kinds, YakuKind.LargeRun), Is.False, "大ストレート is already held");
        }

        [Test]
        public void FiveOfAKindOneThrowShortIsNamed()
        {
            // 3,3,3,3,1 - the 1 is the only thing standing between this hand and ヤッツィー.
            var config = Shipped();
            var misses = Yaku.NearMisses(new[] { 3, 3, 3, 3, 1 }, config);

            Assert.That(Has(KindsOf(misses), YakuKind.Five), Is.True,
                "one throw off five of a kind must be named");

            foreach (YakuNearMiss miss in misses)
            {
                if (miss.Kind != YakuKind.Five)
                    continue;

                Assert.That(miss.ExampleDie, Is.EqualTo(4), "the 1 is die index 4");
                Assert.That(miss.ExampleFace, Is.EqualTo(3), "it has to become a 3");
                Assert.That((miss.DieMask & (1 << 4)) != 0, Is.True);
            }
        }

        [Test]
        public void TheNamedThrowActuallyProducesTheYaku()
        {
            // The property the whole feature rests on, checked on a spread of hands.
            var config = Shipped();
            var hands = new[]
            {
                new[] { 1, 2, 3, 4, 6 },
                new[] { 2, 3, 4, 5, 1 },
                new[] { 3, 3, 3, 3, 1 },
                new[] { 6, 6, 6, 6, 2 },
                new[] { 1, 1, 2, 3, 4 },
                new[] { 5, 5, 6, 6, 6 },
                new[] { 2, 4, 4, 5, 6 },
                new[] { 1, 3, 5, 6, 6 },
                new[] { 4, 4, 4, 4, 4 },
                new[] { 1, 2, 3, 4, 5 }
            };

            foreach (int[] dice in hands)
            {
                foreach (YakuNearMiss miss in Yaku.NearMisses(dice, config))
                {
                    var probe = (int[])dice.Clone();
                    probe[miss.ExampleDie] = miss.ExampleFace;

                    Assert.That(Yaku.Qualifies(probe, config, miss.Kind), Is.True,
                        miss.Hint() + " is a lie about " + Hand(dice));
                    Assert.That(Yaku.Qualifies(dice, config, miss.Kind), Is.False,
                        miss.Kind + " is already in " + Hand(dice) + " and is not a near miss");
                }
            }
        }

        [Test]
        public void ExhaustiveEveryReportedThrowIsRealAndNothingElseIsReported()
        {
            var config = Shipped();
            int checkedMisses = 0;

            for (int a = 1; a <= 6; a++)
            for (int b = 1; b <= 6; b++)
            for (int c = 1; c <= 6; c++)
            for (int d = 1; d <= 6; d++)
            for (int e = 1; e <= 6; e++)
            {
                var dice = new[] { a, b, c, d, e };
                YakuNearMiss[] misses = Yaku.NearMisses(dice, config);

                foreach (YakuNearMiss miss in misses)
                {
                    Assert.That(Has(PrintedKinds, miss.Kind), Is.True,
                        miss.Kind + " must not be offered as a hint on " + Hand(dice));
                    Assert.That(Yaku.Qualifies(dice, config, miss.Kind), Is.False,
                        miss.Kind + " is already held on " + Hand(dice));

                    var probe = (int[])dice.Clone();
                    probe[miss.ExampleDie] = miss.ExampleFace;
                    Assert.That(Yaku.Qualifies(probe, config, miss.Kind), Is.True,
                        miss.Hint() + " does not hold for " + Hand(dice));
                    checkedMisses++;
                }

                // Anything not reported must genuinely be unreachable in a single throw.
                foreach (YakuKind kind in PrintedKinds)
                {
                    if (Has(KindsOf(misses), kind))
                        continue;

                    if (Yaku.Qualifies(dice, config, kind))
                        continue;

                    Assert.That(IsReachableInOneThrow(dice, config, kind), Is.False,
                        kind + " was reachable from " + Hand(dice) + " but was not reported");
                }
            }

            Assert.That(checkedMisses, Is.GreaterThan(0), "the sweep must actually have found some");
        }

        /// <summary>Independent brute force, so a bug in the reporting loop cannot hide itself.</summary>
        private static bool IsReachableInOneThrow(int[] dice, GameConfig config, YakuKind kind)
        {
            var probe = (int[])dice.Clone();
            for (int i = 0; i < probe.Length; i++)
            {
                int original = probe[i];
                for (int face = 1; face <= 6; face++)
                {
                    if (face == original)
                        continue;
                    probe[i] = face;
                    if (Yaku.Qualifies(probe, config, kind))
                    {
                        probe[i] = original;
                        return true;
                    }
                }
                probe[i] = original;
            }

            return false;
        }

        [Test]
        public void TheHintNamesARealDieAndFace()
        {
            var config = Shipped();
            var dice = new[] { 1, 2, 3, 4, 6 };

            foreach (YakuNearMiss miss in Yaku.NearMisses(dice, config))
            {
                Assert.That(miss.ExampleDie, Is.InRange(0, dice.Length - 1),
                    "the hint must point at a die that exists");
                Assert.That(miss.ExampleFace, Is.InRange(1, 6),
                    "the hint must name a face a die can actually show");
                Assert.That(miss.Hint(), Does.Contain(miss.DisplayName()));
                Assert.That(miss.Hint(), Does.Contain(miss.ExampleFace + "を1つ入れる"));
            }
        }
    }
}
