using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DiceAndScull.Tests
{
    public class GameFlowTests
    {
        [UnityTest]
        public IEnumerator ThreeRoundsConsumeTheDiceAndRestartTheMatch()
        {
            var game = Object.FindFirstObjectByType<GameView>();
            if (game == null) game = new GameObject("Test Game").AddComponent<GameView>();
            yield return null;
            yield return PlayRound(1, PlayerDie.Normal, "02 / 03");
            yield return PlayRound(2, PlayerDie.Low, "03 / 03");
            yield return PlayRound(7, PlayerDie.High, "◆");
            yield return new WaitForSeconds(4.2f);
            Assert.That(Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None).Any(x => x.text == "01 / 03"), Is.True);
            Object.Destroy(game.gameObject);
        }

        private static IEnumerator PlayRound(int count, PlayerDie die, string expectedRound)
        {
            Button countButton = Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(x => x.name == "Count " + count);
            countButton.onClick.Invoke();
            Assert.That(Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Any(x => x.name == "Card 0:0"), Is.True);
            Button dieButton = Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(x => x.name == "Die " + die);
            Assert.That(dieButton.interactable, Is.True);
            dieButton.onClick.Invoke();
            float deadline = Time.realtimeSinceStartup + 8f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None).Any(x => x.text == expectedRound))
                    yield break;
                yield return null;
            }
            Assert.Fail("Round did not finish: " + expectedRound);
        }
    }
}
