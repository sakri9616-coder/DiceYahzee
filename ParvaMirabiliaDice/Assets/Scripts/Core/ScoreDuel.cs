using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ParvaMirabilia.Core
{
    public enum ScoreHandKind
    {
        Pair = 0, TwoPair = 1, Three = 2, FullHouse = 3, SmallStraight = 4,
        Four = 5, LargeStraight = 6, Five = 7, Chance = 8
    }

    public enum NpcPersonality { Balanced = 0, Aggressive = 1, Careful = 2, Expert = 3 }

    public readonly struct ScoreOffer
    {
        public readonly ScoreHandKind Kind;
        public readonly int Score;
        public readonly int Mask;
        public readonly bool IsFormed;
        public readonly bool IsAvailable;
        public readonly int BaseScore;
        public readonly int BonusScore;

        public ScoreOffer(ScoreHandKind kind, int score, int mask, bool formed, bool available = true, int baseScore = 0)
        {
            Kind = kind; Score = score; Mask = mask; IsFormed = formed; IsAvailable = available;
            BaseScore = baseScore; BonusScore = score - baseScore;
        }
    }

    public sealed class ScoreIntent
    {
        public ScoreHandKind Kind { get; private set; }
        public int Score { get; private set; }
        public int BaseScore { get; private set; }
        public int BonusScore { get; private set; }
        internal ScoreIntent(ScoreOffer offer)
        {
            Kind = offer.Kind; Score = offer.Score; BaseScore = offer.BaseScore; BonusScore = offer.BonusScore;
        }
    }

    public readonly struct ScoreForecast
    {
        public readonly int Score;
        public readonly int NpcScore;
        public readonly int DamageToPlayer;
        public readonly int DamageToNpc;
        public ScoreForecast(int score, int npcScore, bool saved = false)
        {
            Score = score; NpcScore = npcScore;
            if (saved)
            {
                DamageToPlayer = npcScore; DamageToNpc = score;
            }
            else
            {
                int commonAbsorption = Math.Min(score, npcScore) / 2;
                DamageToPlayer = npcScore - commonAbsorption;
                DamageToNpc = score - commonAbsorption;
            }
        }
    }

    /// <summary>Immutable real roll; HeldMask retains dice from the preceding roll.</summary>
    public sealed class NpcRollSnapshot
    {
        public IReadOnlyList<int> Dice { get; private set; }
        public int HeldMask { get; private set; }
        internal NpcRollSnapshot(int[] dice, int heldMask)
        {
            Dice = Array.AsReadOnly((int[])dice.Clone()); HeldMask = heldMask;
        }
    }

    public sealed class ScoreRound
    {
        public int Round { get; internal set; }
        public int[] PlayerDice { get; internal set; }
        public int[] NpcDice { get; internal set; }
        public bool[] PlayerHeld { get; internal set; }
        public ScoreHandKind PlayerKind { get; internal set; }
        public ScoreHandKind NpcKind { get; internal set; }
        public int PlayerScore { get; internal set; }
        public int NpcScore { get; internal set; }
        public int PlayerBaseScore { get; internal set; }
        public int PlayerBonusScore { get; internal set; }
        public int NpcBaseScore { get; internal set; }
        public int NpcBonusScore { get; internal set; }
        public bool IsSaved { get; internal set; }
        public int DamageToPlayer { get; internal set; }
        public int DamageToNpc { get; internal set; }
        public int PlayerRawHpAfter { get; internal set; }
        public int NpcRawHpAfter { get; internal set; }
        public int PlayerHpAfter { get; internal set; }
        public int NpcHpAfter { get; internal set; }
        public int PlayerRerollsUsed { get; internal set; }
        public int NpcRerollsUsed { get; internal set; }
        public IReadOnlyList<NpcRollSnapshot> NpcRolls { get; internal set; }
    }

    /// <summary>A fixed base and every face contribute before the formed hand multiplier.</summary>
    public static class ScoreRules
    {
        public const int HandCount = 9;
        public const int BasePoints = 20;
        public static readonly string[] Names =
        {
            "ペア", "ツーペア", "スリーカインド", "フルハウス", "小ストレート",
            "フォーカインド", "大ストレート", "ヤッツィー", "チャンス"
        };
        private static readonly int[] Quarters = { 5, 6, 7, 8, 8, 10, 10, 13, 4 };
        // Ordered-hand lookup preserves the exact matching dice masks as well as scores.
        // It also bounds NPC planning: no recursive search or allocation per simulated hand.
        private static readonly ScoreOffer[] Table = BuildTable();

        public static string Name(ScoreHandKind kind) { return Names[CheckKind(kind)]; }
        public static int MultiplierQuarters(ScoreHandKind kind) { return Quarters[CheckKind(kind)]; }
        // Compatibility for ordering: the value now represents multiplier quarters.
        public static int PrintedScore(ScoreHandKind kind) { return MultiplierQuarters(kind); }

        public static ScoreOffer Evaluate(ScoreHandKind kind, int[] dice)
        {
            return Table[Encode(dice) * HandCount + CheckKind(kind)];
        }

        /// <param name="available">True means the category has not been used. Null means all are available.</param>
        public static ScoreOffer Best(int[] dice, bool[] available = null)
        {
            if (available != null && available.Length != HandCount)
                throw new ArgumentException("役の使用状態は9個必要です", nameof(available));
            int offset = Encode(dice) * HandCount;
            bool found = false;
            ScoreOffer best = default(ScoreOffer);
            for (int i = 0; i < HandCount; i++)
            {
                if (available != null && !available[i]) continue;
                var offer = Table[offset + i];
                if (!found || offer.Score > best.Score ||
                    (offer.Score == best.Score && Quarters[i] < Quarters[(int)best.Kind]))
                {
                    best = offer;
                    found = true;
                }
            }
            if (!found) throw new InvalidOperationException("未使用の役がありません");
            return best;
        }

        /// <summary>
        /// An exact one- or two-reroll pattern suggestion. All outcomes are enumerated;
        /// the seed parameter is retained for callers but does not change the result.
        /// Planning never advances real dice streams. True means hold that physical die.
        /// </summary>
        public static bool[] SuggestHeld(int[] dice, bool[] available, int rerollsLeft, int seed = 0,
            int round = 1, NpcPersonality personality = NpcPersonality.Balanced)
        {
            Encode(dice);
            if (available == null || available.Length != HandCount)
                throw new ArgumentException("未使用の役を9個指定してください", nameof(available));
            if (rerollsLeft < 0 || rerollsLeft > 2) throw new ArgumentOutOfRangeException(nameof(rerollsLeft));
            int mask = rerollsLeft == 0 ? 31 : ScoreHoldPolicyExact.Plan(dice, available, rerollsLeft, round, personality);
            var held = new bool[5];
            for (int i = 0; i < 5; i++) held[i] = (mask & (1 << i)) != 0;
            return held;
        }

        internal static int Encode(int[] dice)
        {
            if (dice == null) throw new ArgumentNullException(nameof(dice));
            if (dice.Length != 5) throw new ArgumentException("ダイスは5個必要です", nameof(dice));
            int code = 0;
            for (int i = 0; i < 5; i++)
            {
                if (dice[i] < 1 || dice[i] > 6)
                    throw new ArgumentOutOfRangeException(nameof(dice), "出目は1から6です");
                code = code * 6 + dice[i] - 1;
            }
            return code;
        }

        internal static ScoreOffer At(int code, int kind) { return Table[code * HandCount + kind]; }
        internal static int CheckKind(ScoreHandKind kind)
        {
            int index = (int)kind;
            if (index < 0 || index >= HandCount) throw new ArgumentOutOfRangeException(nameof(kind));
            return index;
        }

        private static ScoreOffer[] BuildTable()
        {
            var table = new ScoreOffer[7776 * HandCount];
            var dice = new int[5];
            var counts = new int[7];
            var masks = new int[7];
            for (int code = 0; code < 7776; code++)
            {
                Array.Clear(counts, 0, counts.Length);
                Array.Clear(masks, 0, masks.Length);
                int n = code, total = 0;
                for (int i = 4; i >= 0; i--)
                {
                    int face = n % 6 + 1;
                    n /= 6; dice[i] = face; counts[face]++; masks[face] |= 1 << i; total += face;
                }
                int pair = 0, secondPair = 0, three = 0, four = 0, five = 0;
                for (int face = 1; face <= 6; face++)
                {
                    if (counts[face] >= 2)
                    {
                        if (pair == 0) pair = face;
                        else if (secondPair == 0) secondPair = face;
                    }
                    if (counts[face] >= 3 && three == 0) three = face;
                    if (counts[face] >= 4) four = face;
                    if (counts[face] == 5) five = face;
                }
                int smallMask = StraightMask(counts, masks, 4);
                int largeMask = StraightMask(counts, masks, 5);
                int fullMask = three != 0 && pair != 0 && secondPair != 0 ? 31 : 0;
                int twoPairMask = secondPair != 0 ? FirstBits(masks[pair], 2) | FirstBits(masks[secondPair], 2) : 0;
                int offset = code * HandCount;
                Add(table, offset, ScoreHandKind.Pair, pair == 0 ? 0 : FirstBits(masks[pair], 2), total);
                Add(table, offset, ScoreHandKind.TwoPair, twoPairMask, total);
                Add(table, offset, ScoreHandKind.Three, three == 0 ? 0 : FirstBits(masks[three], 3), total);
                Add(table, offset, ScoreHandKind.FullHouse, fullMask, total);
                Add(table, offset, ScoreHandKind.SmallStraight, smallMask, total);
                Add(table, offset, ScoreHandKind.Four, four == 0 ? 0 : FirstBits(masks[four], 4), total);
                Add(table, offset, ScoreHandKind.LargeStraight, largeMask, total);
                Add(table, offset, ScoreHandKind.Five, five == 0 ? 0 : 31, total);
                table[offset + (int)ScoreHandKind.Chance] = new ScoreOffer(ScoreHandKind.Chance, BasePoints + total, 31, true, true, BasePoints + total);
            }
            return table;
        }

        private static void Add(ScoreOffer[] table, int offset, ScoreHandKind kind, int mask, int total)
        {
            bool formed = mask != 0;
            table[offset + (int)kind] = new ScoreOffer(kind, (BasePoints + total) * (formed ? Quarters[(int)kind] : 4) / 4, mask, formed, true, BasePoints + total);
        }

        private static int StraightMask(int[] counts, int[] masks, int length)
        {
            for (int start = 1; start <= 7 - length; start++)
            {
                int mask = 0;
                for (int face = start; face < start + length; face++)
                {
                    if (counts[face] == 0) { mask = 0; break; }
                    mask |= FirstBits(masks[face], 1);
                }
                if (mask != 0) return mask;
            }
            return 0;
        }

        private static int FirstBits(int mask, int count)
        {
            int result = 0;
            for (int i = 0; i < 5 && count > 0; i++)
                if ((mask & (1 << i)) != 0) { result |= 1 << i; count--; }
            return result;
        }
    }

    internal static class ScoreHoldPolicy
    {
        internal static ScoreOffer Select(int code, bool[] available, int round,
            NpcPersonality personality = NpcPersonality.Balanced)
        {
            var best = default(ScoreOffer);
            float bestValue = float.NegativeInfinity;
            bool found = false;
            for (int kind = 0; kind < ScoreRules.HandCount; kind++)
            {
                if (!available[kind]) continue;
                var offer = ScoreRules.At(code, kind);
                float value = Utility(offer, round, personality);
                if (!found || value > bestValue || (value == bestValue &&
                    ScoreRules.PrintedScore(offer.Kind) < ScoreRules.PrintedScore(best.Kind)))
                {
                    best = offer; bestValue = value; found = true;
                }
            }
            if (!found) throw new InvalidOperationException("未使用の役がありません");
            return best;
        }

        internal static float Utility(ScoreOffer offer, int round, NpcPersonality personality)
        {
            float value = offer.Score;
            if (offer.Kind == ScoreHandKind.Chance && round < 5) value -= 4f;
            if (personality == NpcPersonality.Aggressive) value += offer.Score * offer.Score * 0.0025f;
            if (personality == NpcPersonality.Careful && !offer.IsFormed) value -= 8f;
            return value;
        }
    }

    /// <summary>
    /// A scorecard duel ending in a knockout. Each side refreshes its nine categories
    /// only after using them all. NPC dice are locked before the human rolls and public.
    /// </summary>
    public sealed class ScoreDuel
    {
        private readonly int _seed;
        private readonly bool[] _playerUsed = new bool[ScoreRules.HandCount];
        private readonly bool[] _npcUsed = new bool[ScoreRules.HandCount];
        private readonly List<ScoreRound> _history = new List<ScoreRound>();
        private readonly ReadOnlyCollection<bool> _playerUsedView;
        private readonly ReadOnlyCollection<bool> _npcUsedView;
        private readonly ReadOnlyCollection<ScoreRound> _historyView;
        private SeededRandom _playerRandom, _npcRandom;
        private int[] _preparedNpc;
        private ReadOnlyCollection<int> _preparedNpcView;
        private int[] _savedDice;
        private bool[] _savedHeld;
        private int _npcRerollsUsed;
        private IReadOnlyList<NpcRollSnapshot> _preparedNpcRolls;

        public const int StartingHp = 240;
        public const int MinimumNormalDamage = 13;
        // This bound guards simulations, not gameplay. No turn limit or HP judgement is applied.
        public const int SafetyRoundBound = (StartingHp + MinimumNormalDamage - 1) / MinimumNormalDamage;
        public int MaxRounds { get { return SafetyRoundBound; } }
        public NpcPersonality Personality { get; private set; }
        public MatchPhase Phase { get; private set; }
        public int Round { get; private set; }
        public int PlayerHp { get; private set; }
        public int NpcHp { get; private set; }
        public int RerollsLeft { get; private set; }
        public int[] PlayerDice { get; private set; }
        public int[] NpcDice { get; private set; }
        public IReadOnlyList<int> PlannedNpcDice { get { return _preparedNpcView; } }
        public IReadOnlyList<NpcRollSnapshot> PlannedNpcRolls { get { return _preparedNpcRolls; } }
        public bool[] Held { get; private set; }
        public bool SavedHandUsed { get; private set; }
        public bool HasSavedHand { get { return _savedDice != null; } }
        public ScoreIntent Intent { get; private set; }
        public MatchOutcome Outcome { get; private set; }
        public ScoreRound LastRound { get; private set; }
        public IReadOnlyList<bool> PlayerUsed { get { return _playerUsedView; } }
        public IReadOnlyList<bool> NpcUsed { get { return _npcUsedView; } }
        public IReadOnlyList<ScoreRound> History { get { return _historyView; } }

        public IReadOnlyList<ScoreOffer> Offers
        {
            get
            {
                var offers = new ScoreOffer[ScoreRules.HandCount];
                bool rolled = PlayerDice[0] != 0;
                for (int i = 0; i < offers.Length; i++)
                {
                    var offer = rolled ? ScoreRules.Evaluate((ScoreHandKind)i, PlayerDice) : default(ScoreOffer);
                    offers[i] = new ScoreOffer((ScoreHandKind)i, offer.Score, offer.Mask, offer.IsFormed, !_playerUsed[i], offer.BaseScore);
                }
                return offers;
            }
        }

        public ScoreDuel(int seed, NpcPersonality personality = NpcPersonality.Balanced)
        {
            if (!Enum.IsDefined(typeof(NpcPersonality), personality)) throw new ArgumentOutOfRangeException(nameof(personality));
            _seed = seed; Personality = personality;
            _playerUsedView = Array.AsReadOnly(_playerUsed);
            _npcUsedView = Array.AsReadOnly(_npcUsed);
            _historyView = _history.AsReadOnly();
            Begin();
        }

        public void Begin()
        {
            // Independent streams make the NPC's future rolls invariant under human re-roll choices.
            _playerRandom = new SeededRandom(_seed);
            _npcRandom = new SeededRandom(unchecked(_seed ^ (int)0xA837CF13));
            Array.Clear(_playerUsed, 0, _playerUsed.Length);
            Array.Clear(_npcUsed, 0, _npcUsed.Length);
            _history.Clear(); Outcome = null; LastRound = null;
            SavedHandUsed = false; _savedDice = null; _savedHeld = null;
            PlayerHp = NpcHp = StartingHp; Round = 1;
            PrepareRound();
        }

        public void Roll()
        {
            Require(MatchPhase.AwaitingRoll);
            for (int i = 0; i < 5; i++) PlayerDice[i] = _playerRandom.Range(1, 7);
            Phase = MatchPhase.AwaitingReroll;
        }

        public void ToggleHeld(int index)
        {
            RequirePlanning();
            if (index < 0 || index >= 5) throw new ArgumentOutOfRangeException(nameof(index));
            Held[index] = !Held[index];
        }

        public void SetHeld(bool[] held)
        {
            RequirePlanning();
            if (held == null) throw new ArgumentNullException(nameof(held));
            if (held.Length != 5) throw new ArgumentException("保持するダイスは5個です", nameof(held));
            Array.Copy(held, Held, 5);
        }

        public int Reroll()
        {
            Require(MatchPhase.AwaitingReroll);
            int count = 0;
            for (int i = 0; i < 5; i++) if (!Held[i]) count++;
            if (count == 0) throw new InvalidOperationException("振り直すダイスを選んでください");
            for (int i = 0; i < 5; i++) if (!Held[i]) PlayerDice[i] = _playerRandom.Range(1, 7);
            RerollsLeft--;
            if (RerollsLeft == 0) Phase = MatchPhase.AwaitingSplit;
            return count;
        }

        public ScoreForecast Forecast(ScoreHandKind kind, bool saved = false)
        {
            RequirePlanning();
            int index = ScoreRules.CheckKind(kind);
            if (_playerUsed[index]) throw new InvalidOperationException("その役はこの対戦で使用済みです");
            if (saved && SavedHandUsed) throw new InvalidOperationException("出目の保存はこの対戦で1回だけです");
            return new ScoreForecast(ScoreRules.Evaluate(kind, PlayerDice).Score, Intent.Score, saved);
        }

        public ScoreRound Commit(ScoreHandKind kind)
        {
            var forecast = Forecast(kind);
            return Resolve(kind, forecast, false);
        }

        public ScoreRound SaveHand(ScoreHandKind kind)
        {
            var forecast = Forecast(kind, true);
            _savedDice = (int[])PlayerDice.Clone(); _savedHeld = (bool[])Held.Clone();
            SavedHandUsed = true;
            return Resolve(kind, forecast, true);
        }

        private ScoreRound Resolve(ScoreHandKind kind, ScoreForecast forecast, bool saved)
        {
            var offer = ScoreRules.Evaluate(kind, PlayerDice);
            _playerUsed[(int)kind] = true;
            _npcUsed[(int)Intent.Kind] = true;
            int playerRawHp = PlayerHp - forecast.DamageToPlayer;
            int npcRawHp = NpcHp - forecast.DamageToNpc;
            PlayerHp = Math.Max(0, playerRawHp);
            NpcHp = Math.Max(0, npcRawHp);
            NpcDice = (int[])_preparedNpc.Clone();
            var receipt = new ScoreRound
            {
                Round = Round, PlayerDice = (int[])PlayerDice.Clone(), NpcDice = (int[])NpcDice.Clone(),
                PlayerHeld = (bool[])Held.Clone(), PlayerKind = kind, NpcKind = Intent.Kind,
                PlayerScore = forecast.Score, NpcScore = forecast.NpcScore,
                PlayerBaseScore = offer.BaseScore, PlayerBonusScore = offer.BonusScore,
                NpcBaseScore = Intent.BaseScore, NpcBonusScore = Intent.BonusScore, IsSaved = saved,
                DamageToPlayer = forecast.DamageToPlayer, DamageToNpc = forecast.DamageToNpc,
                PlayerRawHpAfter = playerRawHp, NpcRawHpAfter = npcRawHp,
                PlayerHpAfter = PlayerHp, NpcHpAfter = NpcHp,
                PlayerRerollsUsed = 2 - RerollsLeft, NpcRerollsUsed = _npcRerollsUsed, NpcRolls = _preparedNpcRolls
            };
            _history.Add(receipt); LastRound = receipt;
            Phase = MatchPhase.Resolving;
            if (PlayerHp == 0 || NpcHp == 0)
            {
                bool simultaneous = PlayerHp == 0 && NpcHp == 0;
                MatchResult result = simultaneous
                    ? (playerRawHp == npcRawHp ? MatchResult.Draw : playerRawHp > npcRawHp ? MatchResult.PlayerWin : MatchResult.NpcWin)
                    : PlayerHp > 0 ? MatchResult.PlayerWin : MatchResult.NpcWin;
                Finish(result, simultaneous ? "同時KO" : PlayerHp == 0 ? "あなたの体力が0になりました" : "相手の体力が0になりました");
            }
            return receipt;
        }

        public void NextRound()
        {
            Require(MatchPhase.Resolving);
            Round++; PrepareRound();
        }

        private void PrepareRound()
        {
            RefreshInventoryIfEmpty(_playerUsed); RefreshInventoryIfEmpty(_npcUsed);
            bool restoring = _savedDice != null;
            PlayerDice = restoring ? _savedDice : new int[5];
            Held = restoring ? _savedHeld : new bool[5];
            _savedDice = null; _savedHeld = null;
            NpcDice = null; RerollsLeft = 2;
            Phase = restoring ? MatchPhase.AwaitingReroll : MatchPhase.AwaitingRoll;
            PrepareNpcTurn();
        }

        private static void RefreshInventoryIfEmpty(bool[] used)
        {
            for (int i = 0; i < used.Length; i++) if (!used[i]) return;
            Array.Clear(used, 0, used.Length);
        }

        private void PrepareNpcTurn()
        {
            _preparedNpc = new int[5];
            for (int i = 0; i < 5; i++) _preparedNpc[i] = _npcRandom.Range(1, 7);
            _npcRerollsUsed = 0;
            var rolls = new List<NpcRollSnapshot> { new NpcRollSnapshot(_preparedNpc, 0) };
            var available = new bool[ScoreRules.HandCount];
            for (int kind = 0; kind < available.Length; kind++) available[kind] = !_npcUsed[kind];
            int rerollLimit = Personality == NpcPersonality.Balanced ? 1 : 2;
            for (int reroll = 0; reroll < rerollLimit; reroll++)
            {
                int mask = Personality == NpcPersonality.Balanced
                    ? BasicHoldMask(_preparedNpc, available)
                    : ScoreHoldPolicyExact.Plan(_preparedNpc, available, 2 - reroll, Round, Personality);
                if (mask == 31) break;
                for (int i = 0; i < 5; i++) if ((mask & (1 << i)) == 0) _preparedNpc[i] = _npcRandom.Range(1, 7);
                _npcRerollsUsed++;
                rolls.Add(new NpcRollSnapshot(_preparedNpc, mask));
            }
            var chosen = ScoreHoldPolicy.Select(ScoreRules.Encode(_preparedNpc), available, Round, Personality);
            Intent = new ScoreIntent(chosen);
            _preparedNpcView = Array.AsReadOnly(_preparedNpc);
            _preparedNpcRolls = rolls.AsReadOnly();
        }

        // Only visible dice: retain a formed hand, then a matching group, then high faces.
        // The beginner policy does not enumerate possible future rolls.
        private static int BasicHoldMask(int[] dice, bool[] available)
        {
            var best = ScoreRules.Best(dice, available);
            if (best.IsFormed && best.Kind != ScoreHandKind.Chance) return best.Mask;
            bool groupingAvailable = available[0] || available[1] || available[2] ||
                available[3] || available[5] || available[7];
            int mask = 0, groupFace = 0, groupCount = 1;
            if (groupingAvailable)
                for (int face = 1; face <= 6; face++)
                {
                    int count = 0;
                    for (int i = 0; i < 5; i++) if (dice[i] == face) count++;
                    if (count >= groupCount && count >= 2) { groupFace = face; groupCount = count; }
                }
            for (int i = 0; i < 5; i++)
                if (groupFace != 0 ? dice[i] == groupFace : dice[i] >= 4) mask |= 1 << i;
            return mask;
        }

        private void Finish(MatchResult result, string reason)
        {
            _savedDice = null; _savedHeld = null;
            Outcome = MatchOutcome.Create(result, Round, reason); Phase = MatchPhase.Finished;
        }

        private void Require(MatchPhase phase)
        {
            if (Phase != phase) throw new InvalidOperationException("今はその操作を行えません");
        }

        private void RequirePlanning()
        {
            if (Phase != MatchPhase.AwaitingReroll && Phase != MatchPhase.AwaitingSplit)
                throw new InvalidOperationException("ダイスを振ってから役を選んでください");
        }
    }
}

