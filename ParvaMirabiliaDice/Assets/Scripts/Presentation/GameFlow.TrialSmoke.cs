using System;
using System.Collections;
using System.Collections.Generic;
using ParvaMirabilia.Core;
using UnityEngine;

namespace ParvaMirabilia.Presentation
{
    public sealed partial class GameFlow
    {
        // These helpers only run from the opt-in executable check or a PlayMode test.
        // The policy reads the same public forecast as a player; it never reads hidden NPC dice.
        private static YakuKind SmokeDecision(MatchEngine game, out bool boost, out double utility)
        {
            YakuKind best = YakuKind.Chance;
            boost = false;
            utility = double.NegativeInfinity;
            foreach (var offer in game.PlayerOffers())
            for (int powered = 0; powered <= 1; powered++)
            {
                if (powered == 1 && (game.PlayerMana == 0 || offer.Kind == YakuKind.Guard)) continue;
                var forecast = game.Forecast(offer.Kind, powered == 1);
                double value = Math.Min(forecast.DamageToNpc, game.NpcHp) - forecast.DamageToPlayer * 1.2 +
                    forecast.ManaAfter * .7 - (offer.Kind == YakuKind.Chance ? 0 : .3);
                if (forecast.DamageToPlayer >= game.PlayerHp) value -= 100;
                if (forecast.DamageToNpc >= game.NpcHp && forecast.DamageToPlayer < game.PlayerHp) value += 200;
                if (value <= utility) continue;
                utility = value;
                best = offer.Kind;
                boost = powered == 1;
            }
            return best;
        }

        private static int SmokeWinningTrialSeed()
        {
            for (int seed = 0; seed < 300; seed++)
            {
                var run = new DuelRun(new SeededRandom(seed));
                while (run.Phase == DuelRunPhase.ChoosingBlessing)
                {
                    run.ChooseBlessing(seed % 3);
                    var game = run.BeginFight(new SeededRandom(seed * 97 + run.Fight));
                    while (game.Outcome == null)
                    {
                        game.Roll();
                        while (game.PlayerRerollsLeft > 0)
                        {
                            SmokeDecision(game, out _, out double value);
                            if (value >= 3) break;
                            bool[] keep = NpcBrain.PlanReroll(game.PlayerDice, game.Config,
                                NpcArchetype.Balanced, game.PlayerHp < game.NpcHp);
                            if (keep == null) break;
                            game.SetKept(keep);
                            game.Reroll();
                        }
                        var kind = SmokeDecision(game, out bool boost, out _);
                        game.CommitYaku(kind, boost);
                        if (game.Outcome == null) game.NextRound();
                    }
                    run.CompleteFight(game);
                }
                if (run.Phase == DuelRunPhase.Cleared) return seed;
            }
            throw new InvalidOperationException("No deterministic winning trial found for the UI check");
        }

        private IEnumerator SmokeTrial()
        {
            int seed = SmokeWinningTrialSeed();
            var proofRun = new DuelRun(new SeededRandom(seed));
            var sequence = new Queue<int>(new[] { seed, seed * 97 + 1, seed * 97 + 2, seed * 97 + 3, seed + 1 });
            SeedOverride = sequence.Dequeue;
            try
            {
                Click(_titleRoot, "Trials");
                for (int fight = 1; fight <= 3; fight++)
                {
                    SmokeCheck(_run.Phase == DuelRunPhase.ChoosingBlessing && _run.Fight == fight &&
                        _runRoot.gameObject.activeInHierarchy, "trial " + fight + " offers its next blessing");
                    int carriedLife = _run.Life;
                    proofRun.ChooseBlessing(seed % 3);
                    var proof = proofRun.BeginFight(new SeededRandom(seed * 97 + fight));
                    proof.Roll();
                    Click(_runRoot, "Blessing" + seed % 3);
                    SmokeCheck(_run.Blessings.Count == fight && _engine.PlayerHp == _run.Life &&
                        _engine.PlayerHp >= carriedLife && _engine.NpcHp == 28 + (fight - 1) * 6,
                        "trial " + fight + " carries life and applies its blessing");
                    yield return new WaitForSecondsRealtime(.7f);
                    if (fight == 2) SmokeCapture("trial-second-duel");
                    while (_engine.Outcome == null)
                    {
                        SmokeCheck(string.Join(",", _engine.PlayerDice) == string.Join(",", proof.PlayerDice) &&
                            _engine.PlayerHp == proof.PlayerHp && _engine.NpcHp == proof.NpcHp,
                            "trial " + fight + " round " + _engine.Round + " matches deterministic core replay");
                        while (_engine.PlayerRerollsLeft > 0)
                        {
                            SmokeDecision(_engine, out _, out double value);
                            if (value >= 3) break;
                            bool[] keep = NpcBrain.PlanReroll(_engine.PlayerDice, _engine.Config,
                                NpcArchetype.Balanced, _engine.PlayerHp < _engine.NpcHp);
                            if (keep == null) break;
                            proof.SetKept(keep);
                            proof.Reroll();
                            for (int i = 0; i < 5; i++) if (!keep[i]) Click(_battleRoot, "PlayerDie" + i);
                            Click(_battleRoot, "Reroll");
                            yield return new WaitForSecondsRealtime(.7f);
                        }
                        var kind = SmokeDecision(_engine, out bool boost, out _);
                        Click(_battleRoot, "Yaku" + (int)kind);
                        if (_empower != boost) Click(_battleRoot, "Empower");
                        SmokeCheck(_yakuOffer.Kind == kind && _empower == boost,
                            "trial choice reflects selected " + kind + " and empowerment");
                        Click(_battleRoot, "Commit");
                        proof.CommitYaku(kind, boost);
                        SmokeCheck(_engine.PlayerHp == proof.PlayerHp && _engine.NpcHp == proof.NpcHp,
                            "trial commit matches core damage and life");
                        yield return new WaitForSecondsRealtime(.7f);
                        if (_engine.Outcome != null) break;
                        Click(_battleRoot, "Advance");
                        proof.NextRound();
                        proof.Roll();
                        yield return new WaitForSecondsRealtime(.1f);
                    }
                    int remaining = _engine.PlayerHp;
                    proofRun.CompleteFight(proof);
                    Click(_battleRoot, "Advance");
                    if (fight < 3)
                    {
                        SmokeCheck(_run.Life == Math.Min(_run.MaxLife, Math.Max(0, remaining) + 16),
                            "trial " + fight + " heals exactly sixteen after victory: seed=" + seed +
                            " result=" + _engine.Outcome.Result + " remaining=" + remaining + " life=" + _run.Life);
                        SmokeCapture("trial-after-" + fight);
                    }
                }
                SmokeCheck(_run.Phase == DuelRunPhase.Cleared && _resultRoot.gameObject.activeInHierarchy,
                    "trial clears all three duels and displays the final result");
                SmokeCapture("trial-cleared");
                Click(_resultRoot, "Rematch");
                SmokeCheck(_run.Fight == 1 && _run.Life == 32 && _run.Blessings.Count == 0,
                    "trial rematch resets life, progression and blessings");
                Click(_runRoot, "CancelTrial");
                SmokeCheck(_run == null && _engine == null && _titleRoot.gameObject.activeInHierarchy,
                    "cancelled fresh trial returns cleanly to title");
            }
            finally { SeedOverride = null; }
        }
    }
}
