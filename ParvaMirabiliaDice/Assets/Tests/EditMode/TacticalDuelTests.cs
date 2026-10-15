using System;
using System.IO;
using NUnit.Framework;
using ParvaMirabilia.Core;

namespace ParvaMirabilia.Tests
{
    public sealed class TacticalDuelTests
    {
        private static MatchEngine Duel(int seed = 71)
        {
            var config = new GameConfig().ApplyTacticalDuelLayer();
            config.StartingHp = 10000;
            return new MatchEngine(config, new SeededRandom(seed), NpcArchetype.Balanced);
        }

        [Test]
        public void IntentLocksBeforeFirstThrowAndMatchesTheCommittedDamage()
        {
            var game = Duel();
            var intent = game.Intent;
            Assert.That(intent, Is.Not.Null);
            Assert.That(game.NpcDice, Is.Null);
            game.Roll();
            game.Reroll();
            Assert.That(game.Intent, Is.SameAs(intent));
            var forecast = game.Forecast(YakuKind.Chance, true);
            var result = game.CommitYaku(YakuKind.Chance, true);
            Assert.That(result.NpcAttackSum, Is.EqualTo(intent.Attack));
            Assert.That(result.NpcGuardSum, Is.EqualTo(intent.Ward));
            Assert.That(result.NpcYaku, Is.EqualTo(intent.Kind));
            Assert.That(result.PlayerAttackSum, Is.EqualTo(forecast.Attack));
            Assert.That(result.PlayerManaAfter, Is.EqualTo(forecast.ManaAfter));
            if (!result.Pushback)
            {
                Assert.That(result.DamageToNpc, Is.EqualTo(forecast.DamageToNpc));
                Assert.That(result.DamageToPlayer, Is.EqualTo(forecast.DamageToPlayer));
            }
        }

        [Test]
        public void SpellRestsForOneRoundButChanceIsAlwaysAvailable()
        {
            var game = Duel();
            game.Roll();
            game.CommitYaku(YakuKind.Guard);
            game.NextRound();
            game.Roll();
            Assert.That(game.IsYakuReady(YakuKind.Guard), Is.False);
            Assert.Throws<InvalidOperationException>(() => game.CommitYaku(YakuKind.Guard));
            Assert.That(game.IsYakuReady(YakuKind.Chance), Is.True);
            game.CommitYaku(YakuKind.Chance);
            game.NextRound();
            game.Roll();
            Assert.That(game.IsYakuReady(YakuKind.Guard), Is.True);
        }

        [Test]
        public void BankedRerollsFundFutureEmpowermentAndRematchResetsEverything()
        {
            var game = Duel();
            game.Roll();
            var result = game.CommitYaku(YakuKind.Chance, true);
            Assert.That(result.PlayerManaGained, Is.EqualTo(2));
            Assert.That(game.PlayerMana, Is.EqualTo(2));
            game.NextRound();
            game.Roll();
            game.Reroll();
            game.Reroll();
            game.CommitYaku(YakuKind.Chance, true);
            Assert.That(game.PlayerMana, Is.EqualTo(1));
            game.BeginMatch();
            Assert.That(game.PlayerMana, Is.EqualTo(1));
            Assert.That(game.RecoveryLeft(YakuKind.Guard), Is.Zero);
            Assert.That(game.Round, Is.EqualTo(1));
        }

        [Test]
        public void IllegalEmpowermentAndUnformedSpellCannotMutateRound()
        {
            var game = Duel();
            game.Roll();
            for (int i = 0; i < 5; i++) game.PlayerDice[i] = i + 1;
            Assert.Throws<InvalidOperationException>(() => game.CommitYaku(YakuKind.Five, true));
            Assert.Throws<InvalidOperationException>(() => game.CommitYaku(YakuKind.Guard, true));
            Assert.That(game.PlayerMana, Is.EqualTo(1));
            Assert.That(game.History.Count, Is.Zero);
            Assert.That(game.NpcDice, Is.Null);
        }

        [Test]
        public void EqualAssignmentsRemainSeparateSpellResources()
        {
            var game = Duel();
            game.Roll();
            int[] hand = { 6, 6, 6, 5, 4 };
            Array.Copy(hand, game.PlayerDice, 5);
            Assert.That(Array.Exists(game.PlayerOffers(), x => x.Kind == YakuKind.Triple), Is.True);
            Assert.That(Array.Exists(game.PlayerOffers(), x => x.Kind == YakuKind.Chance), Is.True);
            game.CommitYaku(YakuKind.Triple);
            game.NextRound();
            game.Roll();
            Array.Copy(hand, game.PlayerDice, 5);
            Assert.That(Array.Exists(game.PlayerOffers(), x => x.Kind == YakuKind.Triple), Is.False);
            Assert.That(Array.Exists(game.PlayerOffers(), x => x.Kind == YakuKind.Chance), Is.True);
        }

        [Test]
        public void ForecastDoesNotLeakHiddenPushbackWinner()
        {
            var game = Duel();
            game.Roll();
            for (int i = 0; i < 5; i++) game.PlayerDice[i] = 6;
            var forecast = game.Forecast(YakuKind.Guard, false);
            Assert.That(forecast.PushbackUncertain, Is.True);
            Assert.That(forecast.DamageToPlayer, Is.Zero);
            Assert.That(game.NpcDice, Is.Null);
        }

        [Test]
        public void EveryOpponentHonoursRecoveryAcrossManyRounds()
        {
            foreach (NpcArchetype npc in Enum.GetValues(typeof(NpcArchetype)))
            for (int seed = 0; seed < 40; seed++)
            {
                var config = new GameConfig().ApplyTacticalDuelLayer();
                config.StartingHp = 10000;
                var game = new MatchEngine(config, new SeededRandom(seed), npc);
                YakuKind previous = YakuKind.None;
                while (game.Outcome == null)
                {
                    game.Roll();
                    Assert.That(game.PlayerOffers().Length, Is.GreaterThan(0));
                    if (previous != YakuKind.Chance)
                        Assert.That(game.Intent.Kind, Is.Not.EqualTo(previous));
                    var result = game.CommitYaku(YakuKind.Chance);
                    Assert.That(game.PlayerMana, Is.InRange(0, config.MaxMana));
                    Assert.That(game.NpcMana, Is.InRange(0, config.MaxMana));
                    previous = result.NpcYaku;
                    if (game.Outcome == null) game.NextRound();
                }
            }
        }
    }
}
