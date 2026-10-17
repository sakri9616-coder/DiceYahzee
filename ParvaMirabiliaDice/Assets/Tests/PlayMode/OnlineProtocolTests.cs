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
    }
}
