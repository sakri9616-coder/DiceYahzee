using System;
using System.Collections.Generic;

namespace ParvaMirabilia.Core
{
    /// <summary>
    /// The whole match state machine. Pure C# (no UnityEngine), so the same code that runs
    /// in the game also runs inside NUnit tests and inside batch simulations.
    ///
    /// Round flow: Roll -> Re-roll (hold dice, up to the limit) -> Commit split ->
    /// NPC answers -> simultaneous damage -> next round or match end.
    /// </summary>
    public sealed partial class MatchEngine
    {
        private readonly IRandomSource _rng;
        private readonly NpcBrain _brain;
        private readonly List<RoundOutcome> _history = new List<RoundOutcome>();

        public GameConfig Config { get; private set; }
        public NpcArchetype NpcType { get; private set; }

        public MatchPhase Phase { get; private set; }
        public int Round { get; private set; }
        public int PlayerHp { get; private set; }
        public int NpcHp { get; private set; }
        public int PlayerRerollsLeft { get; private set; }
        public int[] PlayerDice { get; private set; }
        public bool[] PlayerKept { get; private set; }

        /// <summary>The NPC dice stay null until the round is committed (no information leak).</summary>
        public int[] NpcDice { get; private set; }

        public SplitDecision PlayerSplit { get; private set; }
        public SplitDecision NpcSplit { get; private set; }
        public RoundOutcome LastRound { get; private set; }
        public MatchOutcome Outcome { get; private set; }

        /// <summary>Cumulative damage, used as the first tie-break after a double knockout.</summary>
        public int PlayerDamageDealt { get; private set; }
        public int NpcDamageDealt { get; private set; }

        public IReadOnlyList<RoundOutcome> History
        {
            get { return _history; }
        }

        public int PlayerRerollsUsed
        {
            get { return Config.MaxRerolls - PlayerRerollsLeft; }
        }

        public MatchEngine(GameConfig config, IRandomSource rng, NpcArchetype npcType)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            Config = config;
            _rng = rng;
            NpcType = npcType;
            _brain = new NpcBrain(npcType);
            Phase = MatchPhase.NotStarted;
            BeginMatch();
        }

        /// <summary>Resets everything so a rematch starts from a clean slate.</summary>
        public void BeginMatch()
        {
            PlayerHp = Config.PlayerOpeningHp > 0 ? Math.Min(Config.StartingHp, Config.PlayerOpeningHp) : Config.StartingHp;
            NpcHp = Config.NpcStartingHp > 0 ? Config.NpcStartingHp : Config.StartingHp;
            Round = 1;
            PlayerDamageDealt = 0;
            NpcDamageDealt = 0;
            _history.Clear();
            LastRound = null;
            Outcome = null;
            NpcDice = null;
            ResetTactics();
            PrepareRound();
        }

        private void PrepareRound()
        {
            PlayerDice = new int[Config.DiceCount];
            PlayerKept = new bool[Config.DiceCount];
            NpcDice = null;
            PlayerRerollsLeft = Config.MaxRerolls;
            PlayerSplit = default(SplitDecision);
            NpcSplit = default(SplitDecision);
            Phase = MatchPhase.AwaitingRoll;
            PrepareTactics();
        }

        public void Roll()
        {
            Require(MatchPhase.AwaitingRoll, "ダイスを振る");
            for (int i = 0; i < PlayerDice.Length; i++)
                PlayerDice[i] = RollDie();
            Phase = MatchPhase.AwaitingReroll;
        }

        public void ToggleKeep(int index)
        {
            if (Phase != MatchPhase.AwaitingReroll && Phase != MatchPhase.AwaitingSplit)
                throw new InvalidOperationException("今はダイスを保持できません");
            if (index < 0 || index >= PlayerKept.Length)
                throw new ArgumentOutOfRangeException(nameof(index));

            PlayerKept[index] = !PlayerKept[index];
        }

        public void SetKept(bool[] kept)
        {
            if (kept == null) throw new ArgumentNullException(nameof(kept));
            if (Phase != MatchPhase.AwaitingReroll && Phase != MatchPhase.AwaitingSplit)
                throw new InvalidOperationException("今はダイスを保持できません");
            if (kept.Length != PlayerKept.Length)
                throw new ArgumentException("保持フラグの数がダイスの数と一致しません");

            Array.Copy(kept, PlayerKept, kept.Length);
        }

        /// <summary>Re-rolls every die the player is not holding. Costs one re-roll.</summary>
        public int Reroll()
        {
            Require(MatchPhase.AwaitingReroll, "振り直す");
            if (PlayerRerollsLeft <= 0)
                throw new InvalidOperationException("振り直し回数をすべて使い切りました");

            int rerolled = 0;
            for (int i = 0; i < PlayerDice.Length; i++)
            {
                if (!PlayerKept[i])
                {
                    PlayerDice[i] = RollDie();
                    rerolled++;
                }
            }

            if (rerolled == 0)
                throw new InvalidOperationException("すべてのダイスを保持しているので振り直せません");

            PlayerRerollsLeft--;
            if (PlayerRerollsLeft == 0)
                Phase = MatchPhase.AwaitingSplit;

            return rerolled;
        }

        /// <summary>
        /// Locks a hand-built split, makes the NPC answer with its own independent dice,
        /// resolves both damages simultaneously and decides whether the match is over.
        /// </summary>
        public RoundOutcome CommitSplit(int[] attackIndices, int[] guardIndices)
        {
            if (Config.TacticalEnabled) throw new InvalidOperationException("この決闘では役を宣告してください");
            if (Phase != MatchPhase.AwaitingReroll && Phase != MatchPhase.AwaitingSplit)
                throw new InvalidOperationException("今は分割を確定できません");

            var playerSplit = SplitDecision.Validate(attackIndices, guardIndices, Config.DiceCount);
            var npcTurn = _brain.TakeTurn(Config, _rng, NpcHp, PlayerHp);
            int playerAttackSum = Scoring.SumMask(PlayerDice, playerSplit.AttackMask);

            return ResolveRound(playerSplit, playerAttackSum, npcTurn, YakuKind.None, false);
        }

        /// <summary>
        /// The declared-yaku path: the player names one hand, that yaku decides which dice go
        /// forward, and whatever stays behind becomes the ward. No split is assembled by hand.
        ///
        /// The NPC answers with its own declaration, made only from its own dice and HP, so the
        /// reveal stays simultaneous. The strike here may be a printed number instead of a pip
        /// total, which is the point of the rare yaku: a five of a kind of ones is worth exactly
        /// what fives is worth.
        /// </summary>
        public RoundOutcome CommitYaku(YakuKind kind, bool empowered = false)
        {
            if (Phase != MatchPhase.AwaitingReroll && Phase != MatchPhase.AwaitingSplit)
                throw new InvalidOperationException("今は手を確定できません");

            YakuOffer offer = Yaku.Resolve(PlayerDice, Config, kind);
            ValidateTacticalCommit(kind, empowered);
            var npcTurn = Config.TacticalEnabled ? _preparedNpcTurn :
                _brain.TakeTurn(Config, _rng, NpcHp, PlayerHp);

            int boost = empowered ? Config.EmpowerAttack : 0;
            if (Config.TacticalEnabled) boost += Config.PlayerSpellAttackBonus[(int)kind];
            var outcome = ResolveRound(offer.ToSplit(), offer.StrikeValue(PlayerDice) + boost,
                npcTurn, offer.Kind, true);
            CompleteTactics(outcome, empowered);
            return outcome;
        }

        /// <summary>
        /// Everything both rules share: the NPC's dice are already drawn, so this only has to
        /// resolve the two damages and decide whether the match ended.
        /// </summary>
        private RoundOutcome ResolveRound(SplitDecision playerSplit, int playerAttackSum,
            NpcTurnResult npcTurn, YakuKind playerYaku, bool yakuMode)
        {
            NpcDice = npcTurn.Dice;
            NpcSplit = npcTurn.Split;

            int playerGuardSum = Scoring.SumMask(PlayerDice, playerSplit.GuardMask);
            if (Config.TacticalEnabled && yakuMode)
            {
                if (playerYaku == YakuKind.Pair) playerGuardSum += Config.TacticalPairWard;
                playerGuardSum += Config.PlayerSpellWardBonus[(int)playerYaku];
            }
            int npcAttackSum = yakuMode
                ? npcTurn.Yaku.StrikeValue(NpcDice)
                : Scoring.SumMask(NpcDice, NpcSplit.AttackMask);
            if (Config.TacticalEnabled && yakuMode && _npcEmpowered)
                npcAttackSum += NpcAttackBoost;
            int npcGuardSum = yakuMode ? npcTurn.Yaku.WardValue(NpcDice) : Scoring.SumMask(NpcDice, NpcSplit.GuardMask);

            // A formation is read from each side's own five dice and only pays out when every die
            // of it sits on the strike side. The bonus is added *outside* the punch-through test,
            // so a pattern is worth something even when the opposing ward held. That is what keeps
            // the split from collapsing into a single number: two splits with the same strike sum
            // no longer mean the same thing.
            //
            // On the yaku path this whole layer is switched off: the declared yaku already is the
            // payoff, and paying both would be paying for one hand twice.
            Formation playerFormation = Formation.None;
            Formation npcFormation = Formation.None;
            bool playerCashed = false;
            bool npcCashed = false;
            int playerBonus = 0;
            int npcBonus = 0;

            if (!yakuMode)
            {
                playerFormation = Scoring.FindFormation(PlayerDice, Config);
                npcFormation = Scoring.FindFormation(NpcDice, Config);
                playerCashed = playerFormation.IsCashedBy(playerSplit.AttackMask);
                npcCashed = npcFormation.IsCashedBy(NpcSplit.AttackMask);
                playerBonus = playerCashed ? playerFormation.Bonus : 0;
                npcBonus = npcCashed ? npcFormation.Bonus : 0;
            }

            int damageToNpc = Scoring.Damage(playerAttackSum, npcGuardSum) + playerBonus;
            int damageToPlayer = Scoring.Damage(npcAttackSum, playerGuardSum) + npcBonus;

            // Stand-off: both wards held. If neither strike can get through, the stronger roll
            // shoves the other side back instead of letting the round pass for nothing.
            //
            // Deliberately the roll total rather than strike + ward: on the yaku path those two
            // differ whenever a printed yaku is declared, and the stand-off should reward having
            // rolled better, not having declared a bigger number. For a hand-built split the two
            // are identical, because the split partitions the dice.
            Side? pushed = Scoring.ApplyPushback(Scoring.Sum(PlayerDice), Scoring.Sum(NpcDice),
                Config.PushbackDamage, ref damageToNpc, ref damageToPlayer);
            bool pushback = pushed.HasValue;
            Side pushbackWinner = pushed ?? Side.Player;

            PlayerSplit = playerSplit;
            PlayerHp -= damageToPlayer;
            NpcHp -= damageToNpc;
            PlayerDamageDealt += damageToNpc;
            NpcDamageDealt += damageToPlayer;

            var outcome = new RoundOutcome
            {
                Round = Round,
                PlayerDice = (int[])PlayerDice.Clone(),
                NpcDice = (int[])NpcDice.Clone(),
                PlayerKept = (bool[])PlayerKept.Clone(),
                PlayerRerollsUsed = PlayerRerollsUsed,
                NpcRerollsUsed = npcTurn.RerollsUsed,
                PlayerSplit = playerSplit,
                NpcSplit = NpcSplit,
                PlayerAttackSum = playerAttackSum,
                PlayerGuardSum = playerGuardSum,
                NpcAttackSum = npcAttackSum,
                NpcGuardSum = npcGuardSum,
                DamageToNpc = damageToNpc,
                DamageToPlayer = damageToPlayer,
                PlayerFormation = playerFormation,
                NpcFormation = npcFormation,
                PlayerFormationCashed = playerCashed,
                NpcFormationCashed = npcCashed,
                PlayerBonus = playerBonus,
                NpcBonus = npcBonus,
                PlayerYaku = playerYaku,
                NpcYaku = yakuMode ? npcTurn.Yaku.Kind : YakuKind.None,
                Pushback = pushback,
                PushbackWinner = pushbackWinner,
                PlayerHpAfter = PlayerHp,
                NpcHpAfter = NpcHp,
                NpcSummary = npcTurn.Summary
            };

            LastRound = outcome;
            _history.Add(outcome);

            EvaluateMatchEnd();

            if (Outcome != null)
                Phase = MatchPhase.Finished;
            else
                Phase = MatchPhase.Resolving;

            return outcome;
        }

        public void NextRound()
        {
            if (Phase != MatchPhase.Resolving)
                throw new InvalidOperationException("先にラウンドの結果を確認してください");

            Round++;
            PrepareRound();
        }

        private void EvaluateMatchEnd()
        {
            bool playerDead = PlayerHp <= 0;
            bool npcDead = NpcHp <= 0;

            if (playerDead || npcDead)
            {
                if (playerDead && npcDead)
                {
                    // Double knockout: settle it by total damage, then by this round, then draw.
                    if (PlayerDamageDealt != NpcDamageDealt)
                    {
                        Finish(PlayerDamageDealt > NpcDamageDealt ? MatchResult.PlayerWin : MatchResult.NpcWin,
                            "同時撃破 — 与ダメージ合計が多い側が勝利");
                    }
                    else if (LastRound.DamageToNpc != LastRound.DamageToPlayer)
                    {
                        Finish(LastRound.DamageToNpc > LastRound.DamageToPlayer ? MatchResult.PlayerWin : MatchResult.NpcWin,
                            "同時撃破 — 最終ラウンドの与ダメージで判定");
                    }
                    else
                    {
                        Finish(MatchResult.Draw, "同時撃破 — 完全に同点");
                    }
                }
                else
                {
                    Finish(playerDead ? MatchResult.NpcWin : MatchResult.PlayerWin,
                        playerDead ? "プレイヤーのHPが0になった" : "相手のHPが0になった");
                }
                return;
            }

            if (Config.MaxRounds > 0 && Round >= Config.MaxRounds)
            {
                if (PlayerHp != NpcHp)
                    Finish(PlayerHp > NpcHp ? MatchResult.PlayerWin : MatchResult.NpcWin,
                        "時間切れ — 残りHPの多い側が勝利");
                else if (PlayerDamageDealt != NpcDamageDealt)
                    Finish(PlayerDamageDealt > NpcDamageDealt ? MatchResult.PlayerWin : MatchResult.NpcWin,
                        "時間切れ — 与ダメージ合計の多い側が勝利");
                else
                    Finish(MatchResult.Draw, "時間切れ — 完全に同点");
            }
        }

        private void Finish(MatchResult result, string reason)
        {
            Outcome = MatchOutcome.Create(result, Round, reason);
        }

        private int RollDie()
        {
            return _rng.Range(Config.DieMin, Config.DieMax + 1);
        }

        private void Require(MatchPhase expected, string action)
        {
            if (Phase != expected)
                throw new InvalidOperationException(
                    action + "できない状態です（現在: " + Phase + " / 必要: " + expected + "）");
        }
    }
}
