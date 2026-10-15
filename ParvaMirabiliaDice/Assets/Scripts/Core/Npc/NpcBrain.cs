using System;

namespace ParvaMirabilia.Core
{
    public enum NpcArchetype
    {
        /// <summary>Pushes as much pips as possible into strike.</summary>
        Aggressive = 0,
        /// <summary>Prefers a thick ward over a heavy strike.</summary>
        Guardian = 1,
        /// <summary>Adapts the split to the current HP difference.</summary>
        Balanced = 2
    }

    public static class NpcArchetypes
    {
        public static string DisplayName(NpcArchetype archetype)
        {
            switch (archetype)
            {
                case NpcArchetype.Aggressive: return "攻撃型";
                case NpcArchetype.Guardian: return "守護型";
                default: return "均衡型";
            }
        }

        public static string Description(NpcArchetype archetype)
        {
            switch (archetype)
            {
                case NpcArchetype.Aggressive:
                    return "攻撃にダイスを寄せて一気に削る。守護は薄い。";
                case NpcArchetype.Guardian:
                    return "守護を厚くして被害を抑え、確実に削る。";
                default:
                    return "HP差を見ながら攻守を調整する器用な戦い方。";
            }
        }

        /// <summary>Attack dice count the archetype leans towards when utilities tie.</summary>
        public static int PreferredAttackCount(NpcArchetype archetype)
        {
            switch (archetype)
            {
                case NpcArchetype.Aggressive: return 4;
                case NpcArchetype.Guardian: return 2;
                default: return 3;
            }
        }

        /// <summary>
        /// Re-roll every die below this value. 5d6 has an expected value of 3.5, so a die showing
        /// less than 3.5 is worth throwing away regardless of personality. Keeping the threshold
        /// uniform across archetypes matters: an archetype specific threshold quietly gave one
        /// line a higher total (and therefore a free win rate) before any split decision happened.
        /// </summary>
        public const int IdealRerollThreshold = 4;

        /// <summary>Kept for call sites that pass the archetype; the answer is intentionally uniform.</summary>
        public static int RerollThreshold(NpcArchetype archetype, bool behind)
        {
            return IdealRerollThreshold;
        }
    }

    /// <summary>What the NPC did during one round, for display and for tests.</summary>
    public sealed class NpcTurnResult
    {
        public int[] InitialDice = new int[0];
        public int[] Dice = new int[0];
        public SplitDecision Split;

        /// <summary>
        /// The yaku the NPC declared. Kind is <see cref="YakuKind.None"/> on the hand-driven
        /// split path, where there is nothing to declare.
        /// </summary>
        public YakuOffer Yaku;

        public int RerollsUsed;
        public string Summary = "";
    }

    /// <summary>
    /// NPC decision making. It only ever sees its own dice, its own HP and the player's HP,
    /// never the player's dice or split, so the reveal stays simultaneous.
    ///
    /// Re-roll policy: throw away every die below the EV-optimal threshold (4), so every line
    /// maximises its roll total equally. Personality only enters on the split, never on the roll.
    ///
    /// Split policy: evaluate all valid strike/ward splits with an expected value model.
    /// With strike S and ward T the net HP swing is max(0, S - T_opp) - max(0, S - T_me),
    /// where S = my strike + their strike. That model says the split decision is really
    /// "is my roll total worth pushing?" - a high total is pushed into strike, a low total is
    /// tucked into ward. Archetype weights shift the threshold for pushing, the HP gap shifts
    /// how much risk each line is willing to carry.
    /// </summary>
    public sealed class NpcBrain
    {
        /// <summary>
        /// Expected strike total of an opponent that also re-rolls optimally.
        /// 5d6 after threshold-4 re-rolls averages about 21; the mean strike share of that
        /// total across the three lines sits near 11, leaving about 10 behind the ward - the
        /// same 10 the ward actually measures, which is the point of keeping these together.
        ///
        /// Public so that <c>TypicalOpponentTests</c> can hold these two against what this brain
        /// actually rolls. They used to be a pair of hand-written guesses that disagreed with
        /// each other - the attack and the ward they implied summed to a hand worth 23, while
        /// this brain's own re-roll policy produced hands worth 21 - and because declaring beats
        /// guarding exactly when your hand is better than theirs, the disagreement marked almost
        /// every hand as below average and the lines guarded out of 78% of all rounds.
        /// </summary>
        public const double TypicalOpponentAttack = 11.0;

        /// <summary>
        /// Expected roll total of an optimally re-rolled hand (5d6, threshold 4).
        /// Verified against <see cref="TakeTurn"/> rather than assumed.
        /// </summary>
        public const double TypicalOpponentTotal = 21.0;

        private readonly NpcArchetype _archetype;

        public NpcArchetype Archetype
        {
            get { return _archetype; }
        }

        public NpcBrain(NpcArchetype archetype)
        {
            _archetype = archetype;
        }

        public NpcTurnResult TakeTurn(GameConfig config, IRandomSource rng, int myHp, int foeHp)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            var dice = new int[config.DiceCount];
            for (int i = 0; i < dice.Length; i++)
                dice[i] = rng.Range(config.DieMin, config.DieMax + 1);

            var initial = (int[])dice.Clone();
            bool behind = myHp < foeHp;

            int rerollsUsed = 0;
            while (rerollsUsed < config.MaxRerolls)
            {
                // The same policy the other seat runs. Sharing it is not a convenience: the
                // simulation once drove one seat through this brain and the other through a
                // plain threshold helper, and the two only happened to agree while patterns were
                // switched off - with patterns on, the seats drifted and one of them won 72%
                // of mirror matches before anyone noticed.
                bool[] keep = PlanReroll(dice, config, _archetype, behind);
                if (keep == null)
                    break;

                for (int i = 0; i < dice.Length; i++)
                {
                    if (!keep[i])
                        dice[i] = rng.Range(config.DieMin, config.DieMax + 1);
                }
                rerollsUsed++;
            }

            YakuOffer yaku;
            SplitDecision split;
            string declared;

            if (config.YakuEnabled)
            {
                // Declares instead of splitting: the same ladder the player climbs, evaluated with
                // the same utility model, so neither seat is playing a different game.
                yaku = ChooseYaku(dice, config, myHp, foeHp);
                split = yaku.ToSplit();
                declared = yaku.DisplayName();
            }
            else
            {
                yaku = new YakuOffer(YakuKind.None, 0, config.AllMask, -1);
                split = ChooseSplit(dice, config, myHp, foeHp);
                declared = split.ToString();
            }

            return new NpcTurnResult
            {
                InitialDice = initial,
                Dice = dice,
                Split = split,
                Yaku = yaku,
                RerollsUsed = rerollsUsed,
                Summary = NpcArchetypes.DisplayName(_archetype) + " / 振り直し" + rerollsUsed + "回 / " + declared
            };
        }

        /// <summary>
        /// One re-roll attempt under an archetype's policy: the dice to keep, or
        /// <c>null</c> when this hand should not be rolled again.
        ///
        /// This is the single definition of "what the AI considers worth holding", shared by the
        /// brain and by the headless match runner. The pattern layer only changes which dice are
        /// worth holding - dice carrying the current formation stay even when their face is
        /// average, and a strong formation in hand is reason enough to stop - while everything
        /// else is the threshold policy the game shipped with.
        /// </summary>
        public static bool[] PlanReroll(int[] dice, GameConfig config, NpcArchetype archetype, bool behind)
        {
            if (dice == null) throw new ArgumentNullException(nameof(dice));
            if (config == null) throw new ArgumentNullException(nameof(config));

            int threshold = NpcArchetypes.RerollThreshold(archetype, behind);
            if (AllAtOrAbove(dice, threshold))
                return null;

            Formation formation = Scoring.FindFormation(dice, config);
            if (formation.Exists && formation.Bonus >= Math.Max(config.TripleBonus, config.RunBonus))
                return null;

            var keep = new bool[dice.Length];
            bool anyToReroll = false;
            for (int i = 0; i < dice.Length; i++)
            {
                keep[i] = dice[i] >= threshold || formation.IsMember(i);
                if (!keep[i])
                    anyToReroll = true;
            }

            return anyToReroll ? keep : null;
        }

        private static bool AllAtOrAbove(int[] dice, int threshold)
        {
            for (int i = 0; i < dice.Length; i++)
            {
                if (dice[i] < threshold)
                    return false;
            }
            return true;
        }

        /// <summary>Picks the strike/ward split with the highest weighted utility.</summary>
        public SplitDecision ChooseSplit(int[] dice, GameConfig config, int myHp, int foeHp)
        {
            double weightDeal;
            double weightTake;
            ResolveWeights(myHp, foeHp, out weightDeal, out weightTake);

            int all = config.AllMask;
            int preferredCount = NpcArchetypes.PreferredAttackCount(_archetype);
            int myTotal = Scoring.Sum(dice);

            // The pattern this hand holds. Only splits that put every die of it on the strike side
            // cash it, so the NPC now weighs *which* dice go forward and not just how many points.
            Formation formation = Scoring.FindFormation(dice, config);

            int bestMask = -1;
            double bestUtility = double.NegativeInfinity;
            int bestCountDistance = int.MaxValue;
            int bestAttackSum = -1;
            bool bestCashed = false;

            for (int mask = 1; mask < all; mask++)
            {
                int attackCount = SplitDecision.CountBits(mask);
                if (attackCount == 0 || attackCount >= config.DiceCount)
                    continue; // both sides need at least one die

                int attackSum = Scoring.SumMask(dice, mask);

                // S = my strike + their expected strike.
                // gain: damage I expect to deal (their ward = their total - their strike).
                // loss: damage I expect to take (my ward = my total - my strike).
                double strikePower = attackSum + TypicalOpponentAttack;
                double gain = Math.Max(0.0, strikePower - TypicalOpponentTotal);
                double loss = Math.Max(0.0, strikePower - myTotal);

                // A cashed formation is guaranteed damage on top of the strike, so the NPC weighs
                // it the same way the player is being asked to: not just how many points go
                // forward, but which dice carry the pattern.
                bool cashed = formation.IsCashedBy(mask);
                double bonus = cashed ? formation.Bonus : 0.0;
                double utility = weightDeal * (gain + bonus) - weightTake * loss;

                // Tie-break chain: personality first, then a cashed formation, then the harder
                // strike so equal utility does not degrade into an arbitrary subset of dice.
                int distance = Math.Abs(attackCount - preferredCount);
                bool better = utility > bestUtility + 1e-9;
                bool tied = Math.Abs(utility - bestUtility) <= 1e-9;
                if (better ||
                    (tied && cashed && !bestCashed) ||
                    (tied && cashed == bestCashed && distance < bestCountDistance) ||
                    (tied && cashed == bestCashed && distance == bestCountDistance && attackSum > bestAttackSum))
                {
                    bestUtility = utility;
                    bestMask = mask;
                    bestCountDistance = distance;
                    bestAttackSum = attackSum;
                    bestCashed = cashed;
                }
            }

            if (bestMask < 0)
                throw new InvalidOperationException("分割を決定できませんでした");

            return new SplitDecision(bestMask, all & ~bestMask);
        }

        /// <summary>
        /// Picks the yaku with the highest weighted utility - the declared-yaku counterpart of
        /// <see cref="ChooseSplit"/>.
        ///
        /// The model is the same shape, but written against what a yaku actually costs:
        ///
        /// <code>
        ///   gain = max(0, myStrike − theirWard)     expected damage I deal
        ///   loss = max(0, theirStrike − myWard)     expected damage I take
        /// </code>
        ///
        /// Both terms are the honest pair this time. In the split model <c>myStrike + myWard</c>
        /// always equalled the roll total, so one number carried both sides of the trade; a
        /// printed yaku breaks that identity, and a yaku that sends every die forward (大ストレート,
        /// ヤッツィー) has to be seen for what it is - full strike, no ward at all.
        ///
        /// <see cref="TypicalOpponentAttack"/> and <see cref="TypicalOpponentTotal"/> are inherited
        /// from the split era, where they were fitted to a threshold-4 reroll policy. Under the
        /// yaku ladder an opponent's ward is a choice rather than a remainder, so these two are
        /// estimates and are meant to be corrected against measured match data, not trusted.
        /// </summary>
        public YakuOffer ChooseYaku(int[] dice, GameConfig config, int myHp, int foeHp,
            Predicate<YakuKind> canDeclare = null, int attackBoost = 0)
        {
            double weightDeal;
            double weightTake;
            ResolveWeights(myHp, foeHp, out weightDeal, out weightTake);

            int preferredCount = NpcArchetypes.PreferredAttackCount(_archetype);
            double theirWard = TypicalOpponentTotal - TypicalOpponentAttack;

            var offers = Yaku.Evaluate(dice, config);

            YakuOffer best = offers[0];
            double bestUtility = double.NegativeInfinity;
            int bestDistance = int.MaxValue;
            int bestStrike = -1;

            for (int i = 0; i < offers.Length; i++)
            {
                YakuOffer offer = offers[i];
                if (canDeclare != null && !canDeclare(offer.Kind)) continue;
                int strike = offer.StrikeValue(dice) + (offer.Kind == YakuKind.Guard ? 0 : attackBoost);

                double gain = Math.Max(0.0, strike - theirWard);
                double loss = Math.Max(0.0, TypicalOpponentAttack - offer.WardValue(dice));
                double utility = weightDeal * gain - weightTake * loss;

                // Tie-break chain mirrors ChooseSplit: personality first (how many dice a line
                // likes to send), then the heavier strike, so equal utility never degrades into
                // an arbitrary pick from the ladder.
                int distance = Math.Abs(offer.ForwardCount - preferredCount);
                bool better = utility > bestUtility + 1e-9;
                bool tied = Math.Abs(utility - bestUtility) <= 1e-9;
                if (better ||
                    (tied && distance < bestDistance) ||
                    (tied && distance == bestDistance && strike > bestStrike))
                {
                    bestUtility = utility;
                    best = offer;
                    bestDistance = distance;
                    bestStrike = strike;
                }
            }

            return best;
        }

        private void ResolveWeights(int myHp, int foeHp, out double weightDeal, out double weightTake)
        {
            switch (_archetype)
            {
                case NpcArchetype.Aggressive:
                    weightDeal = 1.4;
                    weightTake = 1.0;
                    break;
                case NpcArchetype.Guardian:
                    weightDeal = 1.0;
                    weightTake = 1.45;
                    break;
                default:
                    weightDeal = 1.0;
                    weightTake = 1.0;
                    break;
            }

            // Situation overrides personality: a line that is behind trades safety for damage,
            // a line that is ahead does the opposite. Kept symmetric for all three archetypes so
            // no line gets a hidden advantage from its HP adjustment.
            int gap = foeHp - myHp;
            if (gap >= 4) weightDeal += 0.5;
            else if (gap <= -4) weightTake += 0.5;
        }
    }
}
