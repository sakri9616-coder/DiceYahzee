using System.Reflection;
using NUnit.Framework;
using ParvaMirabilia.Presentation;
using UnityEngine;

namespace ParvaMirabilia.Quality.Tests
{
    public sealed class OnlineProtocolTests
    {
        [Test]
        public void FinishRerollsTravelsThroughHostCommandValidationAndCannotReplay()
        {
            var owner = new GameObject("OnlineProtocolUnderTest");
            try
            {
                var session = owner.AddComponent<DuelNetworkSession>();
                int accepted = 0;
                session.CommandReceived += (player, command) =>
                {
                    Assert.That(player, Is.EqualTo(1));
                    Assert.That(command.Action, Is.EqualTo(DuelNetworkAction.FinishRerolls));
                    accepted++;
                };
                var accept = typeof(DuelNetworkSession).GetMethod("AcceptCommand", BindingFlags.NonPublic | BindingFlags.Instance);
                var finish = new DuelNetworkCommand { Round = 1, Sequence = 1, Action = DuelNetworkAction.FinishRerolls };
                accept.Invoke(session, new object[] { 1, finish });
                accept.Invoke(session, new object[] { 1, finish });
                accept.Invoke(session, new object[] { 1, new DuelNetworkCommand { Round = 1, Sequence = 2, Action = (DuelNetworkAction)99 } });
                Assert.That(accepted, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(owner); }
        }

        [Test]
        public void RematchCommandIsAcceptedAndSequenceStaysMonotonicAcrossMatches()
        {
            var owner = new GameObject("OnlineProtocolRematchUnderTest");
            try
            {
                var session = owner.AddComponent<DuelNetworkSession>();
                int accepted = 0;
                var assertActions = new System.Collections.Generic.List<DuelNetworkAction>();
                session.CommandReceived += (player, command) =>
                {
                    Assert.That(player, Is.EqualTo(1));
                    assertActions.Add(command.Action);
                    accepted++;
                };
                var accept = typeof(DuelNetworkSession).GetMethod("AcceptCommand", BindingFlags.NonPublic | BindingFlags.Instance);
                // Match 0 finishes, guest consents to rematch with a low sequence, host restarts match 1.
                accept.Invoke(session, new object[] { 1, new DuelNetworkCommand { Round = 1, Sequence = 10, Action = DuelNetworkAction.Continue, MatchGeneration = 0 } });
                accept.Invoke(session, new object[] { 1, new DuelNetworkCommand { Round = 1, Sequence = 11, Action = DuelNetworkAction.Rematch, MatchGeneration = 0 } });
                // After restart, the new match uses higher sequences and generation 1. The old 11
                // consent cannot replay, and the stale round from match 0 invalidates 12.
                accept.Invoke(session, new object[] { 1, new DuelNetworkCommand { Round = 1, Sequence = 11, Action = DuelNetworkAction.Rematch, MatchGeneration = 1 } });
                accept.Invoke(session, new object[] { 1, new DuelNetworkCommand { Round = 1, Sequence = 12, Action = DuelNetworkAction.Submit, MatchGeneration = 1 } });
                Assert.That(accepted, Is.EqualTo(3));
                Assert.That(assertActions[2], Is.EqualTo(DuelNetworkAction.Submit));
            }
            finally { Object.DestroyImmediate(owner); }
        }

    }
}
