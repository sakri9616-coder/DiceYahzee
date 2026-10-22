using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace DiceAndScull
{
    public sealed class GameView : MonoBehaviour
    {
        private static readonly Color Ink = new Color(0.055f, 0.09f, 0.13f);
        private static readonly Color Paper = new Color(0.92f, 0.86f, 0.70f);
        private static readonly Color Gold = new Color(0.91f, 0.65f, 0.29f);
        private static readonly Color Rose = new Color(0.95f, 0.37f, 0.48f);
        private Match match;
        private TablePresentation table;
        private TextMeshProUGUI playerScore;
        private TextMeshProUGUI environmentScore;
        private TextMeshProUGUI roundNumber;
        private TextMeshProUGUI playerRoundScore;
        private TextMeshProUGUI environmentRoundScore;
        private TextMeshProUGUI playerRollFace;
        private TextMeshProUGUI environmentRollFace;
        private GameObject countPanel;
        private GameObject dicePanel;
        private readonly Button[] diceButtons = new Button[3];
        private bool animating;

        private void Update()
        {
            GameObject guided = dicePanel != null && dicePanel.activeSelf ? dicePanel : countPanel;
            if (guided != null && guided.activeSelf)
                guided.transform.localScale = Vector3.one * (1f + 0.025f * Mathf.Sin(Time.unscaledTime * 4.5f));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            if (FindFirstObjectByType<GameView>() != null) return;
            new GameObject("DICE&SCULL").AddComponent<GameView>();
        }

        private void Awake()
        {
            match = new Match(new System.Random());
            ConfigureScene();
            table = new GameObject("Table Presentation").AddComponent<TablePresentation>();
            BuildUi();
            Refresh();
        }

        private void ConfigureScene()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                camera = go.AddComponent<Camera>();
            }
            camera.transform.position = new Vector3(0, 10.3f, -12.6f);
            camera.transform.LookAt(Vector3.zero);
            camera.fieldOfView = 41;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Ink;
            camera.allowHDR = true;
            if (FindFirstObjectByType<Light>() == null)
            {
                var light = new GameObject("Key Light").AddComponent<Light>();
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(47, -28, 0);
                light.intensity = 1.6f;
            }
            var floor = Shape(PrimitiveType.Cylinder, "Velvet", Vector3.zero, new Vector3(7.8f, 0.12f, 6.2f), new Color(0.08f, 0.20f, 0.21f));
            floor.transform.position = new Vector3(0, -0.27f, 0);
            Shape(PrimitiveType.Cylinder, "Brass Rim", new Vector3(0, -0.38f, 0), new Vector3(8.0f, 0.12f, 6.4f), Gold);
            for (int seat = 0; seat < 4; seat++)
            {
                Vector3 at = TablePresentation.SeatPosition(seat);
                var marker = Shape(PrimitiveType.Cylinder, "Seat " + seat, at + Vector3.down * 0.12f, new Vector3(1.05f, 0.04f, 0.48f), seat == 0 ? Rose : Gold);
                marker.transform.SetParent(floor.transform, true);
            }
        }

        private void BuildUi()
        {
            var canvasGo = new GameObject("Game HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            if (FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            Label(canvasGo.transform, "DICE & SCULL", new Vector2(0, 0.5f), new Vector2(20, -24), new Vector2(420, 60), 36, Paper, TextAlignmentOptions.Left);
            roundNumber = Label(canvasGo.transform, "01 / 03", new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(240, 60), 35, Paper, TextAlignmentOptions.Center);
            playerScore = Label(canvasGo.transform, "0", new Vector2(0, 0), new Vector2(62, 48), new Vector2(210, 115), 85, Rose, TextAlignmentOptions.Left);
            environmentScore = Label(canvasGo.transform, "0", new Vector2(1, 1), new Vector2(-70, -48), new Vector2(210, 115), 85, Gold, TextAlignmentOptions.Right);
            playerRoundScore = Label(canvasGo.transform, "", new Vector2(0, 0), new Vector2(240, 69), new Vector2(130, 75), 52, Paper, TextAlignmentOptions.Left);
            environmentRoundScore = Label(canvasGo.transform, "", new Vector2(1, 1), new Vector2(-245, -64), new Vector2(130, 75), 52, Paper, TextAlignmentOptions.Right);
            playerRollFace = Label(canvasGo.transform, "", new Vector2(0, 0), new Vector2(415, 165), new Vector2(105, 105), 76, Rose, TextAlignmentOptions.Center);
            environmentRollFace = Label(canvasGo.transform, "", new Vector2(1, 1), new Vector2(-415, -165), new Vector2(105, 105), 76, Gold, TextAlignmentOptions.Center);

            countPanel = Panel(canvasGo.transform, "Card Count", new Vector2(0.5f, 0), new Vector2(0, 64), new Vector2(750, 112));
            for (int count = 1; count <= 7; count++)
            {
                int selected = count;
                Button button = MakeButton(countPanel.transform, "Count " + count, new Vector2((count - 3) * 96, 0), new Vector2(76, 92), Paper, () => SelectCount(selected));
                Label(button.transform, count.ToString(), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70, 80), 50, Ink, TextAlignmentOptions.Center);
            }

            dicePanel = Panel(canvasGo.transform, "Dice", new Vector2(0.5f, 0), new Vector2(0, 64), new Vector2(495, 112));
            for (int index = 0; index < 3; index++)
            {
                PlayerDie die = (PlayerDie)index;
                Button button = MakeButton(dicePanel.transform, "Die " + die, new Vector2((index - 1) * 155, 0), new Vector2(110, 100), index == 0 ? Paper : index == 1 ? Rose : Gold, () => SelectDie(die));
                diceButtons[index] = button;
                int pips = index == 0 ? 4 : index == 1 ? 2 : 6;
                for (int pip = 0; pip < pips; pip++)
                {
                    var dot = new GameObject("Pip", typeof(RectTransform), typeof(Image));
                    dot.transform.SetParent(button.transform, false);
                    var image = dot.GetComponent<Image>();
                    image.color = Ink;
                    image.raycastTarget = false;
                    var rt = dot.GetComponent<RectTransform>();
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = new Vector2((pip % 2 == 0 ? -1 : 1) * 23, (pip / 2 - 1) * 26);
                    rt.sizeDelta = new Vector2(14, 14);
                }
            }
            dicePanel.SetActive(false);
        }

        private void SelectCount(int count)
        {
            if (animating || match.IsComplete || dicePanel.activeSelf) return;
            table.Place(match.Prepare(count));
            countPanel.SetActive(false);
            dicePanel.SetActive(true);
            Refresh();
        }

        private void SelectDie(PlayerDie die)
        {
            if (animating || !dicePanel.activeSelf || !match.CanUse(die)) return;
            animating = true;
            dicePanel.SetActive(false);
            RoundResult result = match.Play(die);
            playerRoundScore.text = "+0";
            environmentRoundScore.text = "+0";
            StartCoroutine(AnimateRoll(result.PlayerRoll, result.EnvironmentRoll));
            table.Play(result, (player, environment) =>
            {
                playerRoundScore.text = "+" + player;
                environmentRoundScore.text = "+" + environment;
            }, () =>
            {
                animating = false;
                playerRoundScore.text = "";
                environmentRoundScore.text = "";
                playerRollFace.text = "";
                environmentRollFace.text = "";
                Refresh();
            });
        }

        private IEnumerator AnimateRoll(int player, int environment)
        {
            float until = Time.unscaledTime + 0.22f;
            while (Time.unscaledTime < until)
            {
                playerRollFace.text = UnityEngine.Random.Range(1, 7).ToString();
                environmentRollFace.text = UnityEngine.Random.Range(1, 7).ToString();
                yield return new WaitForSecondsRealtime(0.035f);
            }
            playerRollFace.text = player.ToString();
            environmentRollFace.text = environment.ToString();
        }

        private void Refresh()
        {
            playerScore.text = match.PlayerScore.ToString();
            environmentScore.text = match.EnvironmentScore.ToString();
            roundNumber.text = match.IsComplete ? "03 / 03" : (match.Round + 1).ToString("00") + " / 03";
            for (int i = 0; i < diceButtons.Length; i++)
                diceButtons[i].interactable = match.CanUse((PlayerDie)i);
            if (animating) return;
            if (match.IsComplete)
            {
                countPanel.SetActive(false);
                dicePanel.SetActive(false);
                Color winner = match.Winner == MatchWinner.Player ? Rose : match.Winner == MatchWinner.Environment ? Gold : Paper;
                roundNumber.text = "◆";
                roundNumber.color = winner;
                StartCoroutine(RestartAfterDelay());
            }
            else if (!dicePanel.activeSelf) countPanel.SetActive(true);
        }

        private IEnumerator RestartAfterDelay()
        {
            yield return new WaitForSeconds(4f);
            match = new Match(new System.Random());
            table.Clear();
            roundNumber.color = Paper;
            Refresh();
        }

        private static GameObject Panel(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return go;
        }

        private static Button MakeButton(Transform parent, string name, Vector2 position, Vector2 size, Color color, Action callback)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.color = color;
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => callback());
            return button;
        }

        private static TextMeshProUGUI Label(Transform parent, string value, Vector2 anchor, Vector2 position, Vector2 size, float fontSize, Color color, TextAlignmentOptions alignment)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            var text = go.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }

        internal static GameObject Shape(PrimitiveType kind, string name, Vector3 position, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(kind);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = scale;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader);
            material.color = color;
            go.GetComponent<Renderer>().sharedMaterial = material;
            Destroy(go.GetComponent<Collider>());
            return go;
        }
    }
}
