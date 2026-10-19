using System.Collections;
using System.Reflection;
using NUnit.Framework;
using ParvaMirabilia.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ParvaMirabilia.Quality.Tests
{
    /// <summary>
    /// Focused coverage for the host lobby invite-code copy control. A private captured code
    /// keeps the checks deterministic without a live relay session: the button only appears
    /// while a created host room still waits for its opponent and holds a code, it writes that
    /// exact code to the system clipboard without touching the status line or the typed join
    /// field, and every leave clears the stored code and the copied feedback.
    /// </summary>
    public sealed class OnlineInviteCopyTests
    {
        private GameFlow flow;
        private DuelNetworkSession session;
        private string clipboard;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        private T Read<T>(string name) => (T)typeof(GameFlow).GetField(name, Private).GetValue(flow);
        private void Set(string name, object value) => typeof(GameFlow).GetField(name, Private).SetValue(flow, value);
        private object Call(string name, params object[] values) => typeof(GameFlow).GetMethod(name, Private).Invoke(flow, values);
        private Button Copy => Read<Button>("_onlineCopyCode");
        private Text Notice => Read<Text>("_onlineCopyNotice");

        [UnitySetUp] public IEnumerator SetUp()
        {
            GameFlow.ConfigOverride = null; GameFlow.LegacyPresentationForTests = false;
            GameFlow.SeedOverride = () => 617;
            clipboard = GUIUtility.systemCopyBuffer;
            foreach (var old in Object.FindObjectsByType<GameFlow>(FindObjectsSortMode.None)) Object.Destroy(old.gameObject);
            Screen.SetResolution(1280, 720, false);
            yield return null; yield return null;
            flow = new GameObject("OnlineInviteCopyUnderTest").AddComponent<GameFlow>();
            yield return null;
            Call("OpenOnline");
            session = flow.gameObject.AddComponent<DuelNetworkSession>();
            typeof(DuelNetworkSession).GetProperty("IsHost").SetValue(session, true);
            Set("_onlineSession", session);
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            GUIUtility.systemCopyBuffer = clipboard;
            GameFlow.ConfigOverride = null; GameFlow.SeedOverride = null; GameFlow.LegacyPresentationForTests = false;
            if (flow != null) Object.Destroy(flow.gameObject);
            yield return null; yield return null;
        }

        [UnityTest] public IEnumerator AnEmptyCodeKeepsTheCopyControlHiddenAndInert()
        {
            Set("_onlineJoinCode", string.Empty);
            Call("RefreshOnline");
            Assert.That(Copy.gameObject.activeSelf, Is.False, "nothing to copy while the code is empty");
            Assert.That(Copy.interactable, Is.False);
            Assert.That(Notice.gameObject.activeSelf, Is.False);
            yield return null;
        }

        [UnityTest] public IEnumerator AWaitingHostCopiesTheExactCodeAndReportsItWithoutErasingTheStatus()
        {
            const string code = "AB12CD";
            string status = "STATUS:" + code;
            Set("_onlineJoinCode", code);
            Read<Text>("_onlineStatus").text = status;
            Call("RefreshOnline");
            Assert.That(Copy.gameObject.activeSelf, Is.True, "a waiting host with a code must offer the copy button");
            Assert.That(Copy.interactable, Is.True);
            Assert.That(Notice.gameObject.activeSelf, Is.False, "no feedback before the copy happens");

            GUIUtility.systemCopyBuffer = "sentinel";
            Copy.onClick.Invoke();
            Assert.That(GUIUtility.systemCopyBuffer, Is.EqualTo(code), "the exact invite code must reach the clipboard");
            Assert.That(Notice.gameObject.activeSelf, Is.True);
            Assert.That(Notice.text, Is.Not.Empty, "the copy must report itself");
            Assert.That(Read<Text>("_onlineStatus").text, Is.EqualTo(status), "copied feedback must not erase the invite code or the waiting line");

            Canvas.ForceUpdateCanvases();
            var input = Read<InputField>("_onlineCode");
            var events = EventSystem.current;
            var pointer = new PointerEventData(events) {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, input.transform.position)
            };
            var hits = new System.Collections.Generic.List<RaycastResult>();
            events.RaycastAll(pointer, hits);
            Assert.That(hits.Count, Is.GreaterThan(0));
            Assert.That(ExecuteEvents.GetEventHandler<IPointerDownHandler>(hits[0].gameObject), Is.EqualTo(input.gameObject),
                "the added copy button must not steal the invite field's pointer");
            yield return null;
        }

        [UnityTest] public IEnumerator CopyHidesWhileConnectingLeavingStoppedOrOnceTheOpponentArrives()
        {
            Set("_onlineJoinCode", "QWERTY");
            Call("RefreshOnline");
            Assert.That(Copy.gameObject.activeSelf, Is.True, "baseline: a waiting host with a code");

            Set("_onlineConnecting", true); Call("RefreshOnline");
            Assert.That(Copy.gameObject.activeSelf, Is.False, "a connecting lobby hides the copy button");
            Set("_onlineConnecting", false);

            Set("_onlineLeaving", true); Call("RefreshOnline");
            Assert.That(Copy.gameObject.activeSelf, Is.False, "a leaving lobby hides the copy button");
            Set("_onlineLeaving", false);

            Set("_onlineStopped", true); Call("RefreshOnline");
            Assert.That(Copy.gameObject.activeSelf, Is.False, "an interrupted room hides the copy button");
            Set("_onlineStopped", false);

            typeof(DuelNetworkSession).GetProperty("IsReady").SetValue(session, true);
            Call("RefreshOnline");
            Assert.That(Copy.gameObject.activeSelf, Is.False, "the opponent arriving ends the waiting window");
            typeof(DuelNetworkSession).GetProperty("IsReady").SetValue(session, false);
            yield return null;
        }

        [UnityTest] public IEnumerator AFreshLobbyAndALobbyReturnClearTheStoredCodeAndFeedback()
        {
            Set("_onlineJoinCode", "AB12CD");
            Call("RefreshOnline");
            Copy.onClick.Invoke();
            Assert.That(Notice.gameObject.activeSelf, Is.True);
            Assert.That(Read<string>("_onlineCopyStatus"), Is.Not.Empty);

            Call("ReturnToOnlineLobby");
            yield return null; yield return null;
            Assert.That(Read<string>("_onlineJoinCode"), Is.Empty, "a lobby return clears the spent code");
            Assert.That(Read<string>("_onlineCopyStatus"), Is.Empty, "a lobby return clears the copied feedback");
            Assert.That(Copy.gameObject.activeSelf, Is.False);
            Assert.That(Notice.gameObject.activeSelf, Is.False);

            Set("_onlineJoinCode", "STALE1");
            Set("_onlineCopyStatus", "stale");
            Call("RefreshOnline");
            Assert.That(Copy.gameObject.activeSelf, Is.False, "no session survives the return, so a stale code cannot show");

            Call("OpenOnline");
            Assert.That(Read<string>("_onlineJoinCode"), Is.Empty, "a fresh lobby drops any stale code");
            Assert.That(Read<string>("_onlineCopyStatus"), Is.Empty, "a fresh lobby drops any stale feedback");
            Assert.That(Copy.gameObject.activeSelf, Is.False);
            yield return null;
        }
    }
}
