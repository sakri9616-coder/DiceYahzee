using NUnit.Framework;
using ParvaMirabilia.Presentation;
using UnityEngine;

namespace ParvaMirabilia.Tests.PlayMode
{
    // These suites preserve the v3/v4 controls as regression evidence. New score-duel UI tests
    // live outside this namespace and exercise the actual production default.
    [SetUpFixture]
    public sealed class HistoricalPresentationScope
    {
        [OneTimeSetUp]
        public void EnterHistoricalPresentation()
        {
            GameFlow.LegacyPresentationForTests = true;
            foreach (var flow in Object.FindObjectsByType<GameFlow>(FindObjectsSortMode.None))
                Object.DestroyImmediate(flow.gameObject);
        }

        [OneTimeTearDown]
        public void RestoreProductionPresentation()
        {
            GameFlow.LegacyPresentationForTests = false;
            GameFlow.ConfigOverride = null;
        }
    }
}
