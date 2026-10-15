using System;
using System.IO;
using NUnit.Framework;
using ParvaMirabilia.Core;
using UnityEngine;

namespace ParvaMirabilia.Tests
{
    /// <summary>A transparent benchmark policy, not a measurement of human enjoyment.</summary>
    public static class DuelBenchmark
    {
        public static YakuKind Choose(MatchEngine game, out bool empower, out double utility)
        {
            YakuKind best = YakuKind.Chance;
            empower = false;
            utility = double.NegativeInfinity;
            foreach (var offer in game.PlayerOffers())
            for (int boosted = 0; boosted <= 1; boosted++)
            {
                if (boosted == 1 && (game.PlayerMana == 0 || offer.Kind == YakuKind.Guard)) continue;
                var forecast = game.Forecast(offer.Kind, boosted == 1);
                double value = Math.Min(forecast.DamageToNpc, game.NpcHp) - forecast.DamageToPlayer * 1.2 +
                    forecast.ManaAfter * .7 - (offer.Kind == YakuKind.Chance ? 0 : .3);
                if (forecast.DamageToPlayer >= game.PlayerHp) value -= 100;
                if (forecast.DamageToNpc >= game.NpcHp && forecast.DamageToPlayer < game.PlayerHp) value += 200;
                if (value <= utility) continue;
                best = offer.Kind;
                empower = boosted == 1;
                utility = value;
            }
            return best;
        }

        public static void Play(MatchEngine game, bool considerIntent = true)
        {
            while (game.Outcome == null)
            {
                game.Roll();
                while (game.PlayerRerollsLeft > 0)
                {
                    Choose(game, out _, out double utility);
                    if (considerIntent && utility >= 3) break;
                    bool[] hold = NpcBrain.PlanReroll(game.PlayerDice, game.Config, NpcArchetype.Balanced,
                        game.PlayerHp < game.NpcHp);
                    if (hold == null) break;
                    game.SetKept(hold);
                    game.Reroll();
                }
                bool empowered;
                YakuKind kind = Choose(game, out empowered, out _);
                if (!considerIntent)
                {
                    kind = YakuKind.Chance;
                    empowered = game.PlayerMana > 0;
                }
                game.CommitYaku(kind, empowered);
                if (game.Outcome == null) game.NextRound();
            }
        }
    }

    public sealed class DuelRunTests
    {
        [Test]
        public void OffersAreDistinct_AndCannotBeChosenTwice()
        {
            var run = new DuelRun(new SeededRandom(32));
            Assert.That(run.Offers.Count, Is.EqualTo(3));
            Assert.That(run.Offers[0], Is.Not.EqualTo(run.Offers[1]));
            Assert.That(run.Offers[1], Is.Not.EqualTo(run.Offers[2]));
            Assert.Throws<InvalidOperationException>(() => run.FightConfig());
            var chosen = run.Offers[1];
            run.ChooseBlessing(1);
            Assert.That(run.Blessings[0], Is.EqualTo(chosen));
            Assert.That(run.Phase, Is.EqualTo(DuelRunPhase.Fighting));
            Assert.Throws<InvalidOperationException>(() => run.ChooseBlessing(1));
            var game = run.BeginFight(new SeededRandom(4));
            Assert.Throws<InvalidOperationException>(() => run.BeginFight(new SeededRandom(4)));
            Assert.Throws<InvalidOperationException>(() => run.CompleteFight(game));
        }

        [Test]
        public void ConfigCopiesCannotChangeTheChosenBuild()
        {
            var run = new DuelRun(new SeededRandom(17));
            run.ChooseBlessing(0);
            var config = run.FightConfig();
            int saved = config.PlayerSpellAttackBonus[1];
            config.PlayerSpellAttackBonus[1] = 1000;
            config.StartingHp = 1000;
            Assert.That(run.FightConfig().PlayerSpellAttackBonus[1], Is.EqualTo(saved));
            Assert.That(run.FightConfig().StartingHp, Is.LessThan(1000));
        }

        [Test]
        public void EveryBlessingChangesItsAdvertisedRule()
        {
            foreach (DuelBlessing blessing in Enum.GetValues(typeof(DuelBlessing)))
            {
                DuelRun run = null;
                int index = -1;
                for (int seed = 0; seed < 100 && index == -1; seed++)
                {
                    run = new DuelRun(new SeededRandom(seed));
                    for (int i = 0; i < 3; i++) if (run.Offers[i] == blessing) index = i;
                }
                Assert.That(index, Is.GreaterThanOrEqualTo(0));
                run.ChooseBlessing(index);
                var config = run.FightConfig();
                switch (blessing)
                {
                    case DuelBlessing.Ember: Assert.That(config.PlayerSpellAttackBonus[1], Is.EqualTo(4)); break;
                    case DuelBlessing.Hearth: Assert.That(config.PlayerSpellAttackBonus[2], Is.EqualTo(5)); break;
                    case DuelBlessing.Constellation: Assert.That(config.PlayerSpellAttackBonus[4], Is.EqualTo(5)); break;
                    case DuelBlessing.Reservoir: Assert.That(config.MaxMana, Is.EqualTo(4)); Assert.That(config.StartingMana, Is.EqualTo(2)); break;
                    case DuelBlessing.Spark: Assert.That(config.EmpowerAttack, Is.EqualTo(8)); break;
                    case DuelBlessing.Shelter: Assert.That(config.PlayerSpellWardBonus[3], Is.EqualTo(3)); break;
                    case DuelBlessing.Vitality: Assert.That(config.StartingHp, Is.EqualTo(42)); Assert.That(run.Life, Is.EqualTo(42)); break;
                    case DuelBlessing.Mirror: Assert.That(config.PlayerSpellWardBonus[5], Is.EqualTo(8)); break;
                    default: Assert.That(config.PlayerSpellWardBonus[1], Is.EqualTo(2)); Assert.That(config.PlayerSpellAttackBonus[1], Is.EqualTo(2)); break;
                }
            }
        }

        [Test]
        public void SuccessfulFightsCarryLife_HealSixteen_AndKeepBlessings()
        {
            int progressed = 0;
            for (int seed = 0; seed < 50; seed++)
            {
                var run = new DuelRun(new SeededRandom(seed));
                run.ChooseBlessing(0);
                var game = run.BeginFight(new SeededRandom(seed));
                Assert.That(game.NpcHp, Is.EqualTo(28));
                DuelBenchmark.Play(game);
                run.CompleteFight(game);
                Assert.Throws<InvalidOperationException>(() => run.CompleteFight(game));
                if (run.Phase == DuelRunPhase.Failed) continue;
                progressed++;
                Assert.That(run.Life, Is.EqualTo(Math.Min(run.MaxLife, game.PlayerHp + 16)));
                Assert.That(run.Fight, Is.EqualTo(2));
                Assert.That(run.Blessings.Count, Is.EqualTo(1));
                foreach (var next in run.Offers) Assert.That(next, Is.Not.EqualTo(run.Blessings[0]));
                run.ChooseBlessing(1);
                var nextGame = run.BeginFight(new SeededRandom(seed + 1));
                Assert.That(nextGame.PlayerHp, Is.EqualTo(run.Life));
                Assert.That(nextGame.NpcHp, Is.EqualTo(34));
            }
            Assert.That(progressed, Is.GreaterThan(0));
        }

        [TestCase(DuelBlessing.Reservoir)]
        [TestCase(DuelBlessing.Spark)]
        public void PlayerManaBlessingsDoNotBuffTheOpponent(DuelBlessing blessing)
        {
            DuelRun run = null;
            int chosen = -1;
            for (int seed = 0; seed < 100 && chosen == -1; seed++)
            {
                run = new DuelRun(new SeededRandom(seed));
                for (int i = 0; i < 3; i++) if (run.Offers[i] == blessing) chosen = i;
            }
            Assert.That(chosen, Is.GreaterThanOrEqualTo(0));
            run.ChooseBlessing(chosen);
            var config = run.FightConfig();
            config.StartingHp = config.PlayerOpeningHp = config.NpcStartingHp = 10000;
            var game = new MatchEngine(config, new SeededRandom(12), NpcArchetype.Aggressive);
            Assert.That(game.NpcMana, Is.EqualTo(1));
            Assert.That(game.PlayerMana, Is.EqualTo(blessing == DuelBlessing.Reservoir ? 2 : 1));
            int boosts = 0;
            while (game.Outcome == null)
            {
                game.Roll();
                var forecast = game.Forecast(YakuKind.Chance, true);
                var result = game.CommitYaku(YakuKind.Chance, true);
                Assert.That(result.PlayerAttackSum, Is.EqualTo(forecast.Attack));
                var npcRole = Yaku.Resolve(result.NpcDice, config, result.NpcYaku);
                Assert.That(result.NpcAttackSum - npcRole.StrikeValue(result.NpcDice),
                    Is.EqualTo(result.NpcEmpowered ? 5 : 0));
                Assert.That(game.NpcMana, Is.InRange(0, 3));
                if (result.NpcEmpowered) boosts++;
                if (game.Outcome == null) game.NextRound();
            }
            Assert.That(boosts, Is.GreaterThan(0));
        }

        [Serializable] private sealed class Metrics
        {
            public int matches, thoughtfulWins, repeatChanceWins, thoughtfulRounds, repeatChanceRounds,
                thoughtfulRerolls, repeatChanceRerolls, bankedMana, empoweredRounds, runs, clears;
            public int[] roles = new int[7];
        }

        [Test]
        public void BenchmarksActualChoicesAndCompleteRuns_WritesDesignEvidence()
        {
            var metrics = new Metrics();
            foreach (NpcArchetype npc in Enum.GetValues(typeof(NpcArchetype)))
            for (int seed = 0; seed < 200; seed++)
            {
                var game = new MatchEngine(new GameConfig().ApplyTacticalDuelLayer(), new SeededRandom(seed), npc);
                DuelBenchmark.Play(game);
                metrics.matches++;
                if (game.Outcome.Result == MatchResult.PlayerWin) metrics.thoughtfulWins++;
                metrics.thoughtfulRounds += game.Round;
                foreach (var round in game.History)
                {
                    metrics.roles[(int)round.PlayerYaku]++;
                    metrics.thoughtfulRerolls += round.PlayerRerollsUsed;
                    metrics.bankedMana += round.PlayerManaGained;
                    if (round.PlayerEmpowered) metrics.empoweredRounds++;
                }
                var repeat = new MatchEngine(new GameConfig().ApplyTacticalDuelLayer(), new SeededRandom(seed), npc);
                DuelBenchmark.Play(repeat, false);
                if (repeat.Outcome.Result == MatchResult.PlayerWin) metrics.repeatChanceWins++;
                metrics.repeatChanceRounds += repeat.Round;
                foreach (var round in repeat.History) metrics.repeatChanceRerolls += round.PlayerRerollsUsed;
            }
            for (int seed = 0; seed < 300; seed++)
            {
                var run = new DuelRun(new SeededRandom(seed));
                while (run.Phase != DuelRunPhase.Cleared && run.Phase != DuelRunPhase.Failed)
                {
                    run.ChooseBlessing(seed % 3);
                    var game = run.BeginFight(new SeededRandom(seed * 97 + run.Fight));
                    DuelBenchmark.Play(game);
                    run.CompleteFight(game);
                }
                metrics.runs++;
                if (run.Phase == DuelRunPhase.Cleared) metrics.clears++;
            }
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "logs", "tactical-design-benchmark.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(metrics, true));
            Assert.That(metrics.empoweredRounds, Is.GreaterThan(0));
            Assert.That(metrics.bankedMana, Is.GreaterThan(0));
            Assert.That(metrics.clears, Is.InRange(1, metrics.runs - 1));
            Debug.Log("Tactical design benchmark: " + JsonUtility.ToJson(metrics));
        }
    }
}
