using System;
using System.Collections.Generic;

namespace ParvaMirabilia.Core
{
    /// <summary>
    /// The hand a side declares at the end of a round.
    ///
    /// This is the answer to "the player lost Yahtzee's yaku". In Yahtzee a yaku is not something
    /// that happens to you, it is something you <b>choose</b>: the same roll can be scored in
    /// several boxes, and which box you spend it in is the whole game. The old rules had no
    /// equivalent - a formation either fired or it did not, and there was never a second thing to
    /// consider putting the hand towards.
    ///
    /// Declaring a yaku also replaces the strike/ward split. Every yaku says how many dice go
    /// forward, and whatever stays back becomes the ward, so "how thin do I leave my defence" is
    /// answered by the choice of yaku instead of by walking each die through a three-state cycle.
    /// </summary>
    public enum YakuKind
    {
        /// <summary>No yaku declared - the hand-driven split path, or an unavailable offer.</summary>
        None = -1,
        /// <summary>Nothing forward; the whole hand is the ward.</summary>
        Guard = 0,
        /// <summary>Two matching dice forward.</summary>
        Pair = 1,
        /// <summary>Three matching dice forward.</summary>
        Triple = 2,
        /// <summary>The three strongest dice forward.</summary>
        Chance = 3,
        /// <summary>Four consecutive faces forward.</summary>
        SmallRun = 4,
        /// <summary>Five consecutive faces forward.</summary>
        LargeRun = 5,
        /// <summary>Five of a kind forward.</summary>
        Five = 6
    }

    /// <summary>
    /// One yaku this hand is entitled to declare: where the dice go, and what the strike is worth.
    /// </summary>
    public readonly struct YakuOffer
    {
        public readonly YakuKind Kind;

        /// <summary>Dice sent forward. Bit i is die i.</summary>
        public readonly int AttackMask;

        /// <summary>Dice left behind as the ward.</summary>
        public readonly int GuardMask;

        /// <summary>Printed strike, or -1 when the strike is simply the sum of the forward dice.</summary>
        public readonly int FixedAttack;
        public readonly int AttackBonus;
        public readonly int WardBonus;

        public YakuOffer(YakuKind kind, int attackMask, int guardMask, int fixedAttack,
            int attackBonus = 0, int wardBonus = 0)
        {
            Kind = kind;
            AttackMask = attackMask;
            GuardMask = guardMask;
            FixedAttack = fixedAttack;
            AttackBonus = attackBonus;
            WardBonus = wardBonus;
        }

        /// <summary>How many dice this yaku pushes forward - the whole defense trade-off.</summary>
        public int ForwardCount
        {
            get { return SplitDecision.CountBits(AttackMask); }
        }

        /// <summary>The printed strike, or the forward dice's sum for the pip-scored yaku.</summary>
        public int StrikeValue(int[] dice)
        {
            if (FixedAttack >= 0)
                return FixedAttack + AttackBonus;
            return Scoring.SumMask(dice, AttackMask) + AttackBonus;
        }

        /// <summary>The dice left behind. Zero when the yaku sends the whole hand forward.</summary>
        public int WardValue(int[] dice)
        {
            return Scoring.SumMask(dice, GuardMask) + WardBonus;
        }

        public SplitDecision ToSplit()
        {
            return new SplitDecision(AttackMask, GuardMask);
        }

        public string DisplayName()
        {
            return Yaku.DisplayName(Kind);
        }
    }

    /// <summary>
    /// A printed yaku that one re-roll would complete - the concrete form of "one more throw".
    ///
    /// The re-roll is the whole appetite of this game: at most two of them a round, and a die
    /// committed to the re-roll is a die not defending this round. Naming the prize and the dice
    /// that could win it turns that appetite into something the screen can act on.
    /// </summary>
    public readonly struct YakuNearMiss
    {
        public readonly YakuKind Kind;

        /// <summary>Dice whose re-roll could complete this yaku. Bit i is die i.</summary>
        public readonly int DieMask;

        /// <summary>One such die, named so the hint can say what to throw.</summary>
        public readonly int ExampleDie;

        /// <summary>The face that die has to land on.</summary>
        public readonly int ExampleFace;

        public YakuNearMiss(YakuKind kind, int dieMask, int exampleDie, int exampleFace)
        {
            Kind = kind;
            DieMask = dieMask;
            ExampleDie = exampleDie;
            ExampleFace = exampleFace;
        }

        public string DisplayName()
        {
            return Yaku.DisplayName(Kind);
        }

        /// <summary>The hint as it is shown on screen, e.g. "あと1回で 大ストレート（6を1つ入れる）".</summary>
        public string Hint()
        {
            return "あと1回で " + DisplayName() + "（" + ExampleFace + "を1つ入れる）";
        }
    }

    /// <summary>
    /// Reads one hand's available yaku.
    ///
    /// The set forms a ladder by how many dice go forward - 0, 2, 3, 4, 5 - and each rung trades
    /// strike for ward:
    ///
    /// <code>
    ///   守護         0 forward   strike   0   ward = all five
    ///   ペア         2 forward   strike sum   ward = the other three
    ///   スリーカインド 3 forward   strike sum   ward = the other two
    ///   チャンス      3 forward   strike sum   ward = the other two
    ///   小ストレート   4 forward   printed      ward = the odd die out
    ///   大ストレート   5 forward   printed      ward = 0
    ///   ヤッツィー     5 forward   printed      ward = 0
    /// </code>
    ///
    /// Two properties are load-bearing and worth stating plainly:
    ///
    /// <b>No yaku dominates another.</b> Moving up the ladder buys strike and pays for it in ward,
    /// so "which yaku" is the attack/defense decision, asked once and answered with one click.
    /// Even two yaku at the same rung are not interchangeable: on 3,3,3,5,6 the triple sends
    /// 3,3,3 forward for 9 and leaves 11 behind, while the chance sends 6,5,3 forward for 14 and
    /// leaves 6 behind. Same rung, different hands, different trade.
    ///
    /// <b>The rare yaku pay a printed number, the common ones pay a sum.</b> That is Yahtzee's
    /// structure: straights and five of a kind are worth a fixed amount regardless of pips, while
    /// three of a kind and Chance are the total of the dice. It means a hand's worth does not
    /// collapse into one pip total - a five of a kind of ones is worth exactly as much as fives.
    ///
    /// The yaku are read in a fixed order so the NPC, the UI and the tests all see the same list.
    /// </summary>
    public static class Yaku
    {
        /// <summary>Consecutive faces for the straights, as in Yahtzee's small and large straight.</summary>
        public const int SmallRunLength = 4;
        public const int LargeRunLength = 5;

        /// <summary>
        /// The name on the button. Every yaku that exists in Yahtzee is called what Yahtzee calls
        /// it: a player who already knows ヤッツィー, 小ストレート or スリーカインド arrives knowing
        /// this list, and inventing shorter words for them ("五つ", "小階段", "対") would buy a
        /// shorter label at the price of that whole head start - the one yaku with no Yahtzee
        /// counterpart, 守護, is the only name this game had to make up.
        /// </summary>
        public static string DisplayName(YakuKind kind)
        {
            switch (kind)
            {
                case YakuKind.Guard: return "守護";
                case YakuKind.Pair: return "ペア";
                case YakuKind.Triple: return "スリーカインド";
                case YakuKind.Chance: return "チャンス";
                case YakuKind.SmallRun: return "小ストレート";
                case YakuKind.LargeRun: return "大ストレート";
                case YakuKind.Five: return "ヤッツィー";
                default: return "なし";
            }
        }

        /// <summary>
        /// Every yaku this hand may declare, in a fixed order. Always at least two: 守護 and
        /// チャンス are unconditional, so a legal declaration always exists even on the worst roll.
        /// </summary>
        public static YakuOffer[] Evaluate(int[] dice, GameConfig config)
        {
            var offers = new List<YakuOffer>(7);
            CollectOffers(dice, config, offers);

            // Recovery makes identical assignments different resources: Chance stays available
            // while a Triple rests next round. Keep both choices in a tactical duel.
            if (config.TacticalEnabled)
                return offers.ToArray();

            // Two yaku can name the exact same play: on 6,6,6,5,4 the triple and the chance send
            // the same three dice forward for the same number. One button is enough - two
            // identical ones only make the row harder to read.
            var distinct = new List<YakuOffer>(offers.Count);
            for (int i = 0; i < offers.Count; i++)
            {
                bool duplicate = false;
                for (int j = 0; j < distinct.Count; j++)
                {
                    if (distinct[j].AttackMask == offers[i].AttackMask &&
                        distinct[j].FixedAttack == offers[i].FixedAttack)
                    {
                        duplicate = true;
                        break;
                    }
                }

                if (!duplicate)
                    distinct.Add(offers[i]);
            }

            return distinct.ToArray();
        }

        /// <summary>
        /// Whether this hand holds <paramref name="kind"/> at all, asked before the duplicate
        /// pass. Two callers want different answers from the same facts: the button row hides a
        /// yaku that names a play already offered, while "can I still make 大ストレート" does not care
        /// whether some other yaku happens to describe the same dice.
        /// </summary>
        public static bool Qualifies(int[] dice, GameConfig config, YakuKind kind)
        {
            var offers = new List<YakuOffer>(7);
            CollectOffers(dice, config, offers);
            return IsOffer(offers, kind);
        }

        /// <summary>Raw qualification, before identical offers are folded together.</summary>
        private static void CollectOffers(int[] dice, GameConfig config, List<YakuOffer> offers)
        {
            if (dice == null)
                throw new ArgumentNullException(nameof(dice));
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            int all = config.AllMask;

            void Add(YakuKind kind, int attackMask, int fixedAttack)
            {
                offers.Add(new YakuOffer(kind, attackMask, all & ~attackMask, fixedAttack,
                    config.TacticalEnabled && kind == YakuKind.Triple ? config.TacticalTripleAttack : 0,
                    config.TacticalEnabled && kind == YakuKind.Pair ? config.TacticalPairWard : 0));
            }

            // Unconditional floor: nothing forward, the whole hand is the ward.
            Add(YakuKind.Guard, 0, -1);

            int[] count = new int[7];
            for (int i = 0; i < dice.Length; i++)
            {
                int face = dice[i];
                if (face >= 1 && face <= 6)
                    count[face]++;
            }

            // Rung 2: the highest face that repeats, exactly two of them. A triple also qualifies,
            // but only two of its dice go forward, so it is a different trade rather than a
            // strictly worse triple.
            int pairFace = HighestFaceWith(count, 2);
            if (pairFace > 0)
                Add(YakuKind.Pair, MaskOfFaces(dice, pairFace, 2), -1);

            // Rung 3 (pip-scored): the highest face that repeats three times.
            int tripleFace = HighestFaceWith(count, 3);
            if (tripleFace > 0)
                Add(YakuKind.Triple, MaskOfFaces(dice, tripleFace, 3), -1);

            // Rung 3 (the escape valve): the three strongest dice. Unconditional, so a hand with
            // no shape at all still has something to declare besides the pure ward.
            Add(YakuKind.Chance, TopMask(dice, 3), -1);

            // Rung 4 and up: the printed yaku. Rare enough that they are events rather than taxes.
            int smallRun = RunMask(dice, count, SmallRunLength);
            if (smallRun != 0)
                Add(YakuKind.SmallRun, smallRun, config.YakuSmallRunAttack);

            int largeRun = RunMask(dice, count, LargeRunLength);
            if (largeRun != 0)
                Add(YakuKind.LargeRun, largeRun, config.YakuLargeRunAttack);

            int fiveFace = HighestFaceWith(count, 5);
            if (fiveFace > 0)
                Add(YakuKind.Five, MaskOfFaces(dice, fiveFace, dice.Length), config.YakuFiveAttack);
        }

        private static bool IsOffer(List<YakuOffer> offers, YakuKind kind)
        {
            for (int i = 0; i < offers.Count; i++)
            {
                if (offers[i].Kind == kind)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// The offer of a given kind, or the default when this hand cannot declare it.
        ///
        /// The default is the strongest strike available, ties broken towards fewer dice forward
        /// (more ward). It exists so that a round with no decisions at all still costs exactly one
        /// input - the same promise the old default split made.
        /// </summary>
        public static YakuOffer Resolve(int[] dice, GameConfig config, YakuKind kind)
        {
            var offers = Evaluate(dice, config);
            for (int i = 0; i < offers.Length; i++)
            {
                if (offers[i].Kind == kind)
                    return offers[i];
            }

            return Default(dice, config);
        }

        /// <summary>The offer a player lands on without touching anything.</summary>
        public static YakuOffer Default(int[] dice, GameConfig config)
        {
            var offers = Evaluate(dice, config);
            YakuOffer best = offers[0];
            int bestStrike = best.StrikeValue(dice);

            for (int i = 1; i < offers.Length; i++)
            {
                int strike = offers[i].StrikeValue(dice);
                if (strike > bestStrike ||
                    (strike == bestStrike && offers[i].ForwardCount < best.ForwardCount))
                {
                    best = offers[i];
                    bestStrike = strike;
                }
            }

            return best;
        }

        /// <summary>
        /// A printed yaku that one re-roll would complete: which dice could be thrown, and one
        /// die plus the face it has to land on.
        ///
        /// Only the printed yaku appear here. A pair is one re-roll away from almost any hand, so
        /// listing it would put a hint on screen during nearly every decision and the line would
        /// stop meaning anything. 大ストレート, ヤッツィー and 小ストレート are rare enough that "you are one roll
        /// short of one" is news.
        ///
        /// This is an exact enumeration, not a heuristic: every die is thrown to every other face
        /// and the qualification is re-run on the result, so the answer is whatever the rules
        /// would actually say. Cost is 30 hand evaluations, paid once per screen refresh rather
        /// than per frame.
        /// </summary>
        public static YakuNearMiss[] NearMisses(int[] dice, GameConfig config)
        {
            if (dice == null)
                throw new ArgumentNullException(nameof(dice));
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            var held = new List<YakuOffer>(7);
            CollectOffers(dice, config, held);

            var probe = (int[])dice.Clone();
            var buffer = new List<YakuOffer>(7);
            var found = new List<YakuNearMiss>(3);

            // Highest rung first: when two are one roll away, the bigger prize is the one worth
            // naming.
            for (int k = PrintedKinds.Length - 1; k >= 0; k--)
            {
                YakuKind kind = PrintedKinds[k];
                if (IsOffer(held, kind))
                    continue;

                int dieMask = 0;
                int exampleDie = -1;
                int exampleFace = 0;

                for (int i = 0; i < probe.Length; i++)
                {
                    int original = probe[i];
                    for (int face = 6; face >= 1; face--)
                    {
                        if (face == original)
                            continue;

                        probe[i] = face;
                        buffer.Clear();
                        CollectOffers(probe, config, buffer);
                        if (!IsOffer(buffer, kind))
                            continue;

                        dieMask |= 1 << i;
                        if (exampleDie < 0)
                        {
                            exampleDie = i;
                            exampleFace = face;
                        }
                        break;                       // one qualifying face per die is enough
                    }
                    probe[i] = original;
                }

                if (dieMask != 0)
                    found.Add(new YakuNearMiss(kind, dieMask, exampleDie, exampleFace));
            }

            return found.ToArray();
        }

        /// <summary>The yaku worth chasing: the ones that pay a printed stake rather than a sum.</summary>
        private static readonly YakuKind[] PrintedKinds =
        {
            YakuKind.SmallRun, YakuKind.LargeRun, YakuKind.Five
        };

        private static int HighestFaceWith(int[] count, int howMany)
        {
            for (int face = 6; face >= 1; face--)
            {
                if (count[face] >= howMany)
                    return face;
            }
            return 0;
        }

        /// <summary>Mask of the first <paramref name="howMany"/> dice showing <paramref name="face"/>.</summary>
        private static int MaskOfFaces(int[] dice, int face, int howMany)
        {
            int mask = 0;
            int taken = 0;
            for (int i = 0; i < dice.Length && taken < howMany; i++)
            {
                if (dice[i] != face)
                    continue;
                mask |= 1 << i;
                taken++;
            }
            return mask;
        }

        /// <summary>
        /// Mask of the strongest run of the given length: one die per face, highest run first, so
        /// 1,2,3,4,5 declares the 2-5 run rather than 1-4. Ties inside a face fall to the lowest
        /// index, which keeps the NPC and the tests on the same answer.
        /// </summary>
        private static int RunMask(int[] dice, int[] count, int length)
        {
            for (int low = 7 - length; low >= 1; low--)
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
                if (!complete)
                    continue;

                int mask = 0;
                for (int k = 0; k < length; k++)
                    mask |= 1 << FirstIndexOf(dice, low + k);
                return mask;
            }

            return 0;
        }

        /// <summary>Mask of the <paramref name="howMany"/> strongest dice, highest face first.</summary>
        private static int TopMask(int[] dice, int howMany)
        {
            var order = new List<int>();
            for (int i = 0; i < dice.Length; i++)
                order.Add(i);

            order.Sort((a, b) =>
            {
                int byValue = dice[b].CompareTo(dice[a]);
                return byValue != 0 ? byValue : a.CompareTo(b);
            });

            int mask = 0;
            for (int i = 0; i < howMany && i < order.Count; i++)
                mask |= 1 << order[i];
            return mask;
        }

        private static int FirstIndexOf(int[] dice, int face)
        {
            for (int i = 0; i < dice.Length; i++)
            {
                if (dice[i] == face)
                    return i;
            }
            return 0;
        }
    }
}
