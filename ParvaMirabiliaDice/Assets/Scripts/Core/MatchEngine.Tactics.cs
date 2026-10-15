using System;
using System.Collections.Generic;

namespace ParvaMirabilia.Core
{
    /// <summary>Public promise made before the player's first throw. It cannot change at commit.</summary>
    public sealed class DuelIntent
    {
        public YakuKind Kind { get; }
        public int Attack { get; }
        public int Ward { get; }
        public bool Empowered { get; }

        public DuelIntent(YakuKind kind, int attack, int ward, bool empowered)
        { Kind = kind; Attack = attack; Ward = ward; Empowered = empowered; }
    }

    public readonly struct DuelForecast
    {
        public readonly int Attack, Ward, DamageToNpc, DamageToPlayer;
        public readonly bool PushbackUncertain;
        public readonly int ManaAfter;
        public DuelForecast(int attack, int ward, int dealt, int taken, bool uncertain, int manaAfter)
        { Attack = attack; Ward = ward; DamageToNpc = dealt; DamageToPlayer = taken;
            PushbackUncertain = uncertain; ManaAfter = manaAfter; }
    }

    public sealed partial class MatchEngine
    {
        private NpcTurnResult _preparedNpcTurn;
        private bool _npcEmpowered;
        private readonly int[] _playerRecovery = new int[7];
        private readonly int[] _npcRecovery = new int[7];
        public DuelIntent Intent { get; private set; }
        public int PlayerMana { get; private set; }
        public int NpcMana { get; private set; }
        private int NpcManaLimit => Config.NpcMaxMana >= 0 ? Config.NpcMaxMana : Config.MaxMana;
        private int NpcAttackBoost => Config.NpcEmpowerAttack >= 0 ? Config.NpcEmpowerAttack : Config.EmpowerAttack;

        private void ResetTactics()
        {
            Array.Clear(_playerRecovery, 0, _playerRecovery.Length);
            Array.Clear(_npcRecovery, 0, _npcRecovery.Length);
            PlayerMana = Config.TacticalEnabled ? Config.StartingMana : 0;
            NpcMana = Config.TacticalEnabled ?
                (Config.NpcStartingMana >= 0 ? Config.NpcStartingMana : Config.StartingMana) : 0;
            Intent = null;
            _preparedNpcTurn = null;
            _npcEmpowered = false;
        }

        private void PrepareTactics()
        {
            if (!Config.TacticalEnabled) return;
            for (int i = 0; i < 7; i++)
            {
                _playerRecovery[i] = Math.Max(0, _playerRecovery[i] - 1);
                _npcRecovery[i] = Math.Max(0, _npcRecovery[i] - 1);
            }
            // Draw and lock before the human rolls. No human hand, action, or future reroll
            // is supplied to this policy; the public promise comes from this exact turn.
            _preparedNpcTurn = _brain.TakeTurn(Config, _rng, NpcHp, PlayerHp);
            var turn = _preparedNpcTurn;
            turn.Yaku = _brain.ChooseYaku(turn.Dice, Config, NpcHp, PlayerHp, NpcReady);
            _npcEmpowered = NpcMana > 0 && turn.Yaku.Kind != YakuKind.Guard &&
                (turn.Yaku.StrikeValue(turn.Dice) >= 16 || NpcType == NpcArchetype.Aggressive ||
                 Round >= 3 || PlayerHp <= turn.Yaku.StrikeValue(turn.Dice) + NpcAttackBoost);
            if (_npcEmpowered)
                turn.Yaku = _brain.ChooseYaku(turn.Dice, Config, NpcHp, PlayerHp, NpcReady,
                    NpcAttackBoost);
            _npcEmpowered &= turn.Yaku.Kind != YakuKind.Guard;
            turn.Split = turn.Yaku.ToSplit();
            turn.Summary = NpcArchetypes.DisplayName(NpcType) + " / " + turn.Yaku.DisplayName() +
                (_npcEmpowered ? " / 魔力強化" : "");
            Intent = new DuelIntent(turn.Yaku.Kind,
                turn.Yaku.StrikeValue(turn.Dice) + (_npcEmpowered ? NpcAttackBoost : 0),
                turn.Yaku.WardValue(turn.Dice), _npcEmpowered);
        }

        private bool NpcReady(YakuKind kind)
        { return kind == YakuKind.Chance || _npcRecovery[(int)kind] == 0; }

        public int RecoveryLeft(YakuKind kind)
        {
            if ((int)kind < 0 || (int)kind >= 7) return 0;
            return _playerRecovery[(int)kind];
        }

        public bool IsYakuReady(YakuKind kind)
        { return !Config.TacticalEnabled || kind == YakuKind.Chance || RecoveryLeft(kind) == 0; }

        public YakuOffer[] PlayerOffers()
        {
            var offers = Yaku.Evaluate(PlayerDice, Config);
            if (!Config.TacticalEnabled) return offers;
            var ready = new List<YakuOffer>(offers.Length);
            foreach (var offer in offers)
                if (IsYakuReady(offer.Kind)) ready.Add(offer);
            return ready.ToArray();
        }

        public DuelForecast Forecast(YakuKind kind, bool empowered)
        {
            if (Intent == null || (Phase != MatchPhase.AwaitingReroll && Phase != MatchPhase.AwaitingSplit))
                throw new InvalidOperationException("予告を比較できる状態ではありません");
            ValidateTacticalCommit(kind, empowered);
            var offer = Yaku.Resolve(PlayerDice, Config, kind);
            int attack = offer.StrikeValue(PlayerDice) + Config.PlayerSpellAttackBonus[(int)kind] +
                (empowered ? Config.EmpowerAttack : 0);
            int ward = offer.WardValue(PlayerDice) + Config.PlayerSpellWardBonus[(int)kind];
            int dealt = Scoring.Damage(attack, Intent.Ward);
            int taken = Scoring.Damage(Intent.Attack, ward);
            return new DuelForecast(attack, ward, dealt, taken, dealt == 0 && taken == 0,
                Math.Min(Config.MaxMana, PlayerMana - (empowered ? 1 : 0) + PlayerRerollsLeft));
        }

        private void ValidateTacticalCommit(YakuKind kind, bool empowered)
        {
            if (Config.TacticalEnabled && !Yaku.Qualifies(PlayerDice, Config, kind))
                throw new InvalidOperationException("その役はまだ成立していません");
            if (!IsYakuReady(kind)) throw new InvalidOperationException("その役は回復中です。次のラウンドに戻ります");
            if (empowered && (!Config.TacticalEnabled || PlayerMana < 1 || kind == YakuKind.Guard))
                throw new InvalidOperationException("強化には魔力1と攻撃する役が必要です");
        }

        private void CompleteTactics(RoundOutcome outcome, bool empowered)
        {
            if (!Config.TacticalEnabled) return;
            int playerBefore = PlayerMana - (empowered ? 1 : 0);
            int npcBefore = NpcMana - (_npcEmpowered ? 1 : 0);
            PlayerMana = Math.Min(Config.MaxMana, playerBefore + PlayerRerollsLeft);
            NpcMana = Math.Min(NpcManaLimit, npcBefore + Config.MaxRerolls - _preparedNpcTurn.RerollsUsed);
            if (outcome.PlayerYaku != YakuKind.Chance) _playerRecovery[(int)outcome.PlayerYaku] = 2;
            if (outcome.NpcYaku != YakuKind.Chance) _npcRecovery[(int)outcome.NpcYaku] = 2;
            outcome.PlayerEmpowered = empowered;
            outcome.NpcEmpowered = _npcEmpowered;
            outcome.PlayerManaGained = PlayerMana - playerBefore;
            outcome.NpcManaGained = NpcMana - npcBefore;
            outcome.PlayerManaAfter = PlayerMana;
            outcome.NpcManaAfter = NpcMana;
        }
    }
}
