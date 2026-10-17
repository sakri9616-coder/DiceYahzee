using System;
using ParvaMirabilia.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ParvaMirabilia.Presentation
{
    public sealed partial class GameFlow
    {
        // Explicit field DTOs: JsonUtility does not serialize the core snapshot properties.
        [Serializable] private sealed class OnlinePlayer
        {
            public int hp, rerolls, kind, score, held;
            public int[] dice;
            public bool[] used;
            public bool submitted, carry, carryUsed, rerollsFinished, nextBonus;
        }
        [Serializable] private sealed class OnlineState
        {
            public int round, phase, winner;
            public long revision;
            public OnlinePlayer[] players;
            public int[] damage;
            public bool[] ready;
        }
        private RectTransform _onlineRoot;
        private DuelNetworkSession _onlineSession;
        private VersusDuel _onlineDuel;
        private OnlineState _onlineState;
        private Text _onlineStatus, _onlineLobbyTitle, _onlineHp, _onlineRole, _onlineScore, _onlineRerollDots, _onlineEnemy, _onlineOwnList, _onlineEnemyList;
        private Image _onlineOwnHpFill, _onlineEnemyHpFill;
        private Image _onlineOwnLoss, _onlineEnemyLoss;
        private Text _onlineOwnHpText, _onlineEnemyHpText, _onlineHit;
        // The drop each online bar is printing, so both readings can be reprinted together.
        private int _onlineOwnDamageShown, _onlineEnemyDamageShown;
        private Text _onlineDrawMiracle;
        /// <summary>Explains the Chance swap where it matters: before the declaration and right after it lands.</summary>
        private Text _onlineChanceCue;
        private ScoreHandEffectGraphic _onlineCarryEffect;
        private ScoreHandEffectGraphic _onlineImpactGraphic;
        private float _onlineCarryEffectAge = -1;
        private ScoreHandEffectGraphic _onlineGuideGraphic;
        private Image _onlineGuideMouse, _onlineWheelMouse, _onlineContinueMouse;
        private RectTransform _onlineHelp;
        private readonly Button[] _onlineRoles = new Button[9];
        private readonly Text[] _onlineRoleValues = new Text[9];
        private readonly Text[] _onlineEnemyRoles = new Text[9];
        private float _onlineImpactAge = -1;
        private int _onlineBeforeOwn, _onlineBeforeEnemy;
        private InputField _onlineCode;
        private Button _onlineCreate, _onlineJoin, _onlineRoll, _onlineSubmit, _onlineCarry;
        private readonly DiceView[] _onlineMine = new DiceView[5], _onlineTheirs = new DiceView[5];
        // Cached transforms: RefreshOnline shows and hides ten dice on every snapshot and a
        // name lookup per die per refresh is more searching than the stable screen deserves.
        private readonly RectTransform[] _onlineOwnDice = new RectTransform[5];
        private readonly RectTransform[] _onlineEnemyDice = new RectTransform[5];
        private readonly bool[] _onlineReady = new bool[2];
        private int _onlineHeld, _onlineKind;
        // Rerolls per round are VersusDuel.BaseRerolls normally and one more after a formed
        // Five. The snapshot only carries the remaining count, so the capacity is remembered
        // per round (never stored, because rerolls only ever decrease inside one round).
        private int _onlineRerollCapacity = VersusDuel.BaseRerolls;
        private int _onlineRerollRound = -1;
        private long _onlineSequence;
        private long _onlineRevision;
        private bool _onlineCarrying, _onlinePending, _onlineStopped, _onlineConnecting;
        private Vector2 _onlinePress;
        private bool _onlinePressed;
        private int _onlinePressFrame;
        private bool OnlineActive => _onlineRoot != null && _onlineRoot.gameObject.activeSelf;
        private int OnlineMe => _onlineSession == null ? 0 : _onlineSession.LocalPlayerIndex;
        private bool OnlinePlanning => _onlineSession != null && _onlineSession.IsReady && _onlineState != null && _onlineState.phase == (int)VersusPhase.ChoosingRoles &&
            !_onlineState.players[OnlineMe].submitted && !_onlinePending && !_onlineStopped && !OnlineHelpOpen;

        private bool OnlineRerolling => _onlineSession != null && _onlineSession.IsReady && _onlineState != null &&
            _onlineState.phase == (int)VersusPhase.Playing && !_onlineState.players[OnlineMe].rerollsFinished &&
            !_onlinePending && !_onlineStopped && !OnlineHelpOpen;

        private void BuildOnlineEntry()
        {
            StartCoroutine(OnlineSmoke());
            SDButton("Online", _sdTitle, new Vector2(0, -300), new Vector2(320, 42), "オンライン対戦", 19, OpenOnline);
        }
        private void OpenOnline()
        {
            _sdTitle.gameObject.SetActive(false);
            if (_onlineRoot == null) BuildOnlineScreen();
            _onlineState = null;
            _onlineDuel = null;
            _onlinePending = _onlineStopped = _onlineCarrying = _onlinePressed = false;
            _onlineImpactAge = _onlineCarryEffectAge = -1;
            if (_onlineCarryEffect != null) { _onlineCarryEffect.Clear(); _onlineCarryEffect.Flush(); }
            if (_onlineImpactGraphic != null) { _onlineImpactGraphic.Clear(); _onlineImpactGraphic.Flush(); }
            _onlineHeld = _onlineKind = 0;
            _onlineRerollCapacity = VersusDuel.BaseRerolls; _onlineRerollRound = -1;
            _onlineSequence = _onlineRevision = 0;
            _onlineReady[0] = _onlineReady[1] = false;
            _onlineStatus.text = "オンライン対戦";
            if (_onlineHelp != null) _onlineHelp.gameObject.SetActive(false);
            _sdRoot.gameObject.SetActive(false);
            _onlineRoot.gameObject.SetActive(true);
            // The online screen is created after the persistent audio controls.
            // Keep the slider and mute icon above its full-screen table.
            LiftAudioChrome();
            RefreshOnline();
            UpdateOnlineGuide();
        }
        private void BuildOnlineScreen()
        {
            _onlineRoot = UiFactory.CreateStretch("OnlineDuel", _canvasRoot.transform);
            var table = UiFactory.CreateStretchPanel("Table", _onlineRoot, new Color32(12, 21, 25, 255));
            var grain = UiFactory.CreateStretch("Leather", table.transform).gameObject.AddComponent<RawImage>();
            grain.texture = Resources.Load<Texture2D>("Art/Textures/leather-white");
            grain.color = new Color(.09f, .16f, .18f, .28f); grain.uvRect = new Rect(0, 0, 2.5f, 1.5f); grain.raycastTarget = false;
            var light = UiFactory.CreateStretchPanel("TableLight", _onlineRoot, Color.white); light.sprite = UiFactory.VignetteSprite; light.raycastTarget = false;
            var rim = UiFactory.CreatePanel("Rim", _onlineRoot, new Vector2(1200, 560), new Vector2(0, -8), DiceYahzeeVisuals.Brass);
            DiceYahzeeVisuals.StylePanel(rim); rim.color = DiceYahzeeVisuals.Brass;
            var mat = UiFactory.CreatePanel("Mat", _onlineRoot, new Vector2(1194, 554), new Vector2(0, -8), DiceYahzeeVisuals.Ink);
            DiceYahzeeVisuals.StylePanel(mat); mat.color = DiceYahzeeVisuals.Ink;
            _onlineStatus = OnlineText("Status", "オンライン対戦", 18, 307, DiceYahzeeVisuals.Brass);
            _onlineLobbyTitle = OnlineText("LobbyTitle", "DiceYahzee", 46, 178, DiceYahzeeVisuals.Brass);
            _onlineLobbyTitle.font = Resources.Load<Font>("Fonts/AoboshiOne-Regular") ?? _onlineLobbyTitle.font;
            _onlineCreate = UiFactory.CreateButton("CreateRoom", _onlineRoot, new Vector2(250, 48), new Vector2(-300, 175), "部屋を作る", 20, () => ConnectOnline(true));
            var inputPanel = UiFactory.CreatePanel("InviteCode", _onlineRoot, new Vector2(250, 48), new Vector2(0, 175), DiceYahzeeVisuals.Panel);
            // CreatePanel is decorative by default. An InputField needs its own Graphic
            // to receive the pointer, otherwise clicking the visible box never focuses it.
            inputPanel.raycastTarget = true;
            _onlineCode = inputPanel.gameObject.AddComponent<InputField>();
            var codeText = UiFactory.CreateText("Text", inputPanel.transform, "", 23, TextAnchor.MiddleCenter, UiFactory.Text, Vector2.zero, new Vector2(240, 44));
            _onlineCode.textComponent = codeText;
            _onlineCode.characterLimit = 16;
            _onlineCode.placeholder = UiFactory.CreateText("Placeholder", inputPanel.transform, "招待コード", 19, TextAnchor.MiddleCenter, UiFactory.TextDim, Vector2.zero, new Vector2(240, 44));
            _onlineJoin = UiFactory.CreateButton("JoinRoom", _onlineRoot, new Vector2(250, 48), new Vector2(300, 175), "コードで参加", 20, () => ConnectOnline(false));
            _onlineOwnList = UiFactory.CreateText("AvailableRoles", _onlineRoot, "", 21, TextAnchor.MiddleLeft, UiFactory.Text, new Vector2(490, -30), new Vector2(235, 330));
            _onlineEnemyList = UiFactory.CreateText("EnemyUsedRoles", _onlineRoot, "", 21, TextAnchor.MiddleLeft, UiFactory.Text, new Vector2(-490, -30), new Vector2(235, 330));
            var enemyTrack = UiFactory.CreatePanel("EnemyHpTrack", _onlineRoot, new Vector2(420, 9), new Vector2(0, 143), UiFactory.HpTrack);
            _onlineEnemyHpFill = UiFactory.CreateStretchPanel("Fill", enemyTrack.transform, UiFactory.HpGreen);
            var ownTrack = UiFactory.CreatePanel("OwnHpTrack", _onlineRoot, new Vector2(420, 9), new Vector2(0, -143), UiFactory.HpTrack);
            _onlineOwnHpFill = UiFactory.CreateStretchPanel("Fill", ownTrack.transform, UiFactory.HpGreen);
            _onlineEnemy = OnlineText("Opponent", "相手を待っています", 20, 204, DiceYahzeeVisuals.Parchment);
            _onlineHp = OnlineText("Hp", "", 26, -5, UiFactory.Gold);
            for (int i = 0; i < 5; i++)
            {
                int index = i;
                _onlineTheirs[i] = new DiceView(_onlineRoot, i, 82, "OpponentDie" + i);
                _onlineTheirs[i].SetPosition(OnlineDieHome(i, false));
                _onlineMine[i] = new DiceView(_onlineRoot, i, 104, "OwnDie" + i);
                _onlineMine[i].SetPosition(OnlineDieHome(i, true));
                _onlineEnemyDice[i] = (RectTransform)_onlineRoot.Find("OpponentDie" + i);
                _onlineOwnDice[i] = (RectTransform)_onlineRoot.Find("OwnDie" + i);
                DiceYahzeeVisuals.StyleDie(_onlineEnemyDice[i]);
                DiceYahzeeVisuals.StyleDie(_onlineOwnDice[i]);
                _onlineMine[i].Clicked += _ => { if (OnlineRerolling) { _onlineHeld ^= 1 << index; RefreshOnline(); } };
            }
            _onlineRole = OnlineText("Role", "", 22, -156, DiceYahzeeVisuals.Parchment);
            _onlineRole.rectTransform.sizeDelta = new Vector2(650, 30);
            _onlineScore = OnlineText("Score", "", 44, -203, DiceYahzeeVisuals.Brass);
            _onlineScore.rectTransform.sizeDelta = new Vector2(650, 50);
            _onlineScore.font = Resources.Load<Font>("Fonts/AoboshiOne-Regular") ?? _onlineScore.font;
            _onlineScore.verticalOverflow = VerticalWrapMode.Overflow;
            _onlineRerollDots = OnlineText("Rerolls", "", 17, -266, DiceYahzeeVisuals.Brass);
            _onlineRerollDots.rectTransform.sizeDelta = new Vector2(180, 18);
            _onlineRoll = UiFactory.CreateButton("Reroll", _onlineRoot, new Vector2(220, 45), new Vector2(-260, -220), "振り直す", 19, () => SendOnline(DuelNetworkAction.Reroll));
            _onlineCarry = UiFactory.CreateButton("Carry", _onlineRoot, new Vector2(220, 45), new Vector2(0, -220), "持越", 19, () => { if (OnlinePlanning && !_onlineState.players[OnlineMe].carryUsed) { _onlineCarrying = !_onlineCarrying; _onlineCarryEffectAge = _sdReducedMotion ? -1 : 0; RefreshOnline(); } });
            _onlineSubmit = UiFactory.CreateButton("Submit", _onlineRoot, new Vector2(220, 45), new Vector2(260, -220), "振り直しを終える", 19, () => SendOnline(DuelNetworkAction.FinishRerolls));

            UiFactory.CreateButton("LeaveOnline", _onlineRoot, new Vector2(150, 36), new Vector2(510, 305), "タイトルへ", 16, LeaveOnline);
            PolishOnlineLayout();
            RefreshOnline();
        }
        private bool OnlineHelpOpen => _onlineHelp != null && _onlineHelp.gameObject.activeSelf;
        private void PolishOnlineLayout()
        {
            ((RectTransform)_onlineCreate.transform).anchoredPosition = new Vector2(0, 66);
            ((RectTransform)_onlineCode.transform).anchoredPosition = new Vector2(0, -2);
            ((RectTransform)_onlineJoin.transform).anchoredPosition = new Vector2(0, -70);
            _onlineOwnList.rectTransform.anchoredPosition = new Vector2(466, 207); _onlineOwnList.rectTransform.sizeDelta = new Vector2(210, 30);
            _onlineEnemyList.rectTransform.anchoredPosition = new Vector2(-466, 207); _onlineEnemyList.rectTransform.sizeDelta = new Vector2(210, 30);
            for (int k = 0; k < 9; k++)
            {
                int role = k;
                float rowY = 166 - k * 39;
                _onlineRoles[k] = UiFactory.CreateButton("Role" + k, _onlineRoot, new Vector2(210, 34), new Vector2(466, rowY), "", 18, () => { if (OnlinePlanning && !OnlineHelpOpen) { _onlineKind = role; RefreshOnline(); } });
                var caption = _onlineRoles[k].GetComponentInChildren<Text>();
                caption.alignment = TextAnchor.MiddleLeft;
                caption.rectTransform.anchoredPosition = new Vector2(-22, 0);
                caption.rectTransform.sizeDelta = new Vector2(155, 34);
                _onlineRoleValues[k] = UiFactory.CreateText("RoleScore" + k, _onlineRoot, "", 19, TextAnchor.MiddleRight, DiceYahzeeVisuals.Parchment, new Vector2(550, rowY), new Vector2(40, 34));
                _onlineRoleValues[k].font = _onlineScore.font;
                _onlineRoleValues[k].raycastTarget = false;
                _onlineEnemyRoles[k] = UiFactory.CreateText("EnemyRole" + k, _onlineRoot, "", 18, TextAnchor.MiddleLeft, DiceYahzeeVisuals.Parchment, new Vector2(-466, rowY), new Vector2(210, 34));
                var leftLine = UiFactory.CreatePanel("EnemyDivider" + k, _onlineRoot, new Vector2(210, 1), new Vector2(-466, rowY - 19), new Color(.34f, .49f, .50f, .46f));
                var rightLine = UiFactory.CreatePanel("OwnDivider" + k, _onlineRoot, new Vector2(210, 1), new Vector2(466, rowY - 19), new Color(.34f, .49f, .50f, .46f));
                leftLine.raycastTarget = rightLine.raycastTarget = false;
            }
            ConfigureOnlineHp(_onlineEnemyHpFill, 249, false);
            ConfigureOnlineHp(_onlineOwnHpFill, -249, true);
            _onlineHp.gameObject.SetActive(false);
            _onlineCarry.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 31);
            _onlineCarry.GetComponent<RectTransform>().sizeDelta = new Vector2(204, 48);
            var carryEffects = UiFactory.CreateStretch("OnlineCarryEffect", _onlineRoot);
            _onlineCarryEffect = carryEffects.gameObject.AddComponent<ScoreHandEffectGraphic>();
            _onlineCarryEffect.raycastTarget = false;
            var impactEffects = UiFactory.CreateStretch("OnlineImpactEffect", _onlineRoot);
            _onlineImpactGraphic = impactEffects.gameObject.AddComponent<ScoreHandEffectGraphic>();
            _onlineImpactGraphic.raycastTarget = false;
            var mouse = UiFactory.CreateRect("FlickGuideMouse", _onlineRoot, new Vector2(36, 36), new Vector2(320, -30));
            _onlineGuideMouse = mouse.gameObject.AddComponent<Image>();
            _onlineGuideMouse.sprite = DiceYahzeeVisuals.LoadSprite("Art/DiceYahzee/input-mouse-left");
            _onlineGuideMouse.raycastTarget = false;
            var next = UiFactory.CreateRect("ContinueMouse", _onlineRoot, new Vector2(27, 27), new Vector2(-92, 307));
            _onlineContinueMouse = next.gameObject.AddComponent<Image>();
            _onlineContinueMouse.sprite = DiceYahzeeVisuals.LoadSprite("Art/DiceYahzee/input-mouse-left");
            _onlineContinueMouse.raycastTarget = false;
            _onlineContinueMouse.gameObject.SetActive(false);
            var wheel = UiFactory.CreateRect("RoleWheelMouse", _onlineRoot, new Vector2(25, 25), new Vector2(579, 207));
            _onlineWheelMouse = wheel.gameObject.AddComponent<Image>();
            _onlineWheelMouse.sprite = DiceYahzeeVisuals.LoadSprite("Art/DiceYahzee/input-mouse-scroll");
            _onlineWheelMouse.color = DiceYahzeeVisuals.Brass;
            _onlineWheelMouse.raycastTarget = false;
            var guideEffects = UiFactory.CreateStretch("OnlineGestureGuide", _onlineRoot);
            _onlineGuideGraphic = guideEffects.gameObject.AddComponent<ScoreHandEffectGraphic>();
            _onlineGuideGraphic.raycastTarget = false;
            _onlineHit = OnlineText("Impact", "", 34, -204, DiceYahzeeVisuals.Brass);
            // チャンスは被害の受け手を入れ替える。その一行は宣言する前と入れ替わった直後の両方で
            // 必要になるため、持越ボタンの上・相手ダイスの下という中央の空きへ一行だけ置く。
            _onlineChanceCue = OnlineText("ChanceCue", "", 15, 72, DiceYahzeeVisuals.Brass);
            _onlineChanceCue.rectTransform.sizeDelta = new Vector2(700, 24);
            AddOnlineTextEdge(_onlineChanceCue);
            _onlineChanceCue.gameObject.SetActive(false);
            // An exact simultaneous raw-HP draw (winner 2) is the rare, memorable ending, so it gets
            // its own name on the table instead of being whispered in the status line. It is a plain
            // label with no graphic and no button, so it can never swallow a click.
            _onlineDrawMiracle = UiFactory.CreateText("DrawMiracle", _onlineRoot, "奇跡", 132, TextAnchor.MiddleCenter,
                SDMiracle, new Vector2(0, 24), new Vector2(1000, 190));
            _onlineDrawMiracle.font = Resources.Load<Font>("Fonts/AoboshiOne-Regular") ?? _onlineDrawMiracle.font;
            _onlineDrawMiracle.fontStyle = FontStyle.Bold;
            _onlineDrawMiracle.raycastTarget = false;
            _onlineDrawMiracle.horizontalOverflow = HorizontalWrapMode.Overflow;
            AddOnlineTextEdge(_onlineDrawMiracle, 4f);
            _onlineDrawMiracle.gameObject.SetActive(false);
            var help = UiFactory.CreateButton("Help", _onlineRoot, new Vector2(100, 32), new Vector2(-538, 307), "遊び方", 16, () => _onlineHelp.gameObject.SetActive(true));
            foreach (var button in _onlineRoot.GetComponentsInChildren<Button>(true)) DiceYahzeeVisuals.StyleButton(button, false, true);
            _onlineHelp = UiFactory.CreateStretch("OnlineHelp", _onlineRoot);
            var dim = UiFactory.CreateStretchPanel("Dim", _onlineHelp, new Color(0, 0, 0, .82f));
            dim.raycastTarget = true;
            var panel = UiFactory.CreatePanel("HelpPanel", _onlineHelp, new Vector2(650, 380), Vector2.zero, DiceYahzeeVisuals.Panel); DiceYahzeeVisuals.StylePanel(panel);
            UiFactory.CreateText("Guide", panel.transform, "オンライン対戦\n\nクリック：ダイスを保持\n右クリック：保持以外を振り直す（通常2回）\nヤッツィー成立で次のラウンドは3回\n「振り直しを終える」で残りを省略\n双方の振り直し後に出目を同時公開\nホイール／右の役：役を選ぶ\nダイスを下へドラッグして離す：役を確定\n双方の確定後に同時攻撃。持越は1度だけ。", 20, TextAnchor.MiddleCenter, DiceYahzeeVisuals.Parchment, new Vector2(0, 20), new Vector2(600, 300));
            var close = UiFactory.CreateButton("Close", panel.transform, new Vector2(160, 38), new Vector2(0, -147), "戻る", 18, () => _onlineHelp.gameObject.SetActive(false)); DiceYahzeeVisuals.StyleButton(close);
            _onlineHelp.gameObject.SetActive(false);
        }
        private void ConfigureOnlineHp(Image fill, float y, bool own)
        {
            var track = (RectTransform)fill.transform.parent; track.anchoredPosition = new Vector2(0, y); track.sizeDelta = new Vector2(636, 31);
            DiceYahzeeVisuals.StylePanel(track.GetComponent<Image>());
            var loss = UiFactory.CreateStretchPanel("Loss", track, new Color(.88f, .68f, .32f, .8f)); loss.transform.SetAsFirstSibling(); loss.raycastTarget = false;
            fill.color = own ? new Color(.25f, .48f, .45f) : new Color(.58f, .31f, .28f);
            // One centred reading: side, health, then the drop that side takes in red. A second,
            // bolder numeral pinned near the right end read as a label stuck onto the bar rather
            // than as this bar's own figure, and it had to be enlarged to survive the loss band.
            var text = UiFactory.CreateText("Hp", track, "", 16, TextAnchor.MiddleCenter, DiceYahzeeVisuals.Parchment, Vector2.zero, new Vector2(600, 31));
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            // A light edge only: the red carries the drop, so the glyphs do not need fattening.
            AddOnlineTextEdge(text, 1f);
            if (own) { _onlineOwnLoss = loss; _onlineOwnHpText = text; }
            else { _onlineEnemyLoss = loss; _onlineEnemyHpText = text; }
        }

        /// <summary>
        /// Reprints both online health lines. They carry the same reading as the solo duel's bars —
        /// side, health, then the drop that side received in red — so the damage is named on the bar
        /// it happened to instead of floating at the end of it.
        /// </summary>
        private void SDSetOnlineHpLines(int ownHp, int enemyHp)
        {
            if (_onlineOwnHpText == null) return;
            _onlineOwnHpText.text = SDHealthLine("あなた", Mathf.Max(0, ownHp), ScoreDuel.StartingHp, _onlineOwnDamageShown);
            _onlineEnemyHpText.text = SDHealthLine("相手", Mathf.Max(0, enemyHp), ScoreDuel.StartingHp, _onlineEnemyDamageShown);
        }
        /// <summary>
        /// A dark edge behind an in-bar label so it survives the bright fill, the gold loss band and
        /// the leather grain it can be drawn over. The health line takes the lightest setting, since
        /// its red already separates the drop from the band behind it.
        /// </summary>
        private static void AddOnlineTextEdge(Text label, float distance = 2f)
        {
            if (label == null) return;
            var edge = label.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color(0.03f, 0.07f, 0.08f, .9f);
            edge.effectDistance = new Vector2(distance, -distance);
            edge.useGraphicAlpha = true;
        }
        private static void StyleOnlineButton(Button button, bool selected = false)
        {
            DiceYahzeeVisuals.StyleButton(button, selected, true);
            if (button.interactable) return;
            var image = button.GetComponent<Image>();
            if (image != null) image.color = DiceYahzeeVisuals.Ink;
            var caption = button.GetComponentInChildren<Text>();
            if (caption != null) caption.color = DiceYahzeeVisuals.MutedText;
        }
        private static void StyleOnlineRoleButton(Button button, bool selected)
        {
            var image = button.GetComponent<Image>();
            var caption = button.GetComponentInChildren<Text>();
            Color face = selected ? DiceYahzeeVisuals.Brass : Color.clear;
            Color label = selected ? DiceYahzeeVisuals.Ink : button.interactable ? DiceYahzeeVisuals.Parchment : DiceYahzeeVisuals.MutedText;
            image.color = face;
            caption.color = label;
            var palette = button.GetComponent<ButtonPalette>();
            palette.Face = face;
            palette.Label = label;
        }
        private Text OnlineText(string name, string text, int size, float y, Color color) => UiFactory.CreateText(name, _onlineRoot, text, size, TextAnchor.MiddleCenter, color, new Vector2(0, y), new Vector2(1050, 48));
        /// <summary>The damage a viewer's bar names for the settled round, or 0 anywhere the round has not landed.</summary>
        private int OnlineReceivedDamage(bool resolved, int side)
        {
            return resolved && _onlineState.damage != null ? Mathf.Max(0, _onlineState.damage[side]) : 0;
        }
        /// <summary>One hand's home pose: the build, the tremble while waiting and the launch all read their start from here, so the three can never drift apart.</summary>
        private static Vector2 OnlineDieHome(int index, bool mine)
        {
            return mine ? new Vector2((index - 2) * 116, -76) : new Vector2((index - 2) * 96, 130);
        }
        private async void ConnectOnline(bool create)
        {
            if (_onlineConnecting || _onlineSession != null) return;
            _onlineConnecting = true; _onlineStopped = false; _onlineStatus.text = "接続中…";
            RefreshOnline();
            var session = gameObject.AddComponent<DuelNetworkSession>();
            _onlineSession = session;
            if (_onlineSmokeMode) session.AuthenticationProfile = _onlineSmokeHost ? "dice-net-host" : "dice-net-guest";
            session.Joined += () => { if (_onlineSession == session && _onlineSmokeMode && session.IsHost) System.IO.File.WriteAllText(_onlineSmokePath, session.JoinCode); if (_onlineSession == session) _onlineStatus.text = session.IsHost ? "招待コード： " + session.JoinCode + "　相手の参加を待っています" : "対戦相手に接続中…"; };
            session.Ready += () => { if (_onlineSession != session) return; if (session.IsHost) { _onlineDuel = new VersusDuel(FreshSeed()); _onlineDuel.Begin(); PublishOnline(); } RefreshOnline(); };
            session.CommandReceived += ReceiveOnlineCommand;
            session.SnapshotReceived += ReceiveOnlineSnapshot;
            session.Interrupted += reason => { if (_onlineSession != session || (_onlineState != null && _onlineState.phase == (int)VersusPhase.Finished)) return; _onlineDuel?.Interrupt(); _onlineStopped = true; _onlinePending = false; _onlineStatus.text = reason; if (_onlineSmokeMode) { TraceOnlineSmoke("DICE_NET_SMOKE INTERRUPTED " + reason); Application.Quit(3); } RefreshOnline(); };
            try { if (create) await session.CreateAsync(); else await session.JoinAsync(_onlineCode.text); }
            catch (Exception ex) { if (_onlineSession == session) { _onlineStatus.text = "接続できませんでした： " + ex.Message; if (_onlineSmokeMode) { TraceOnlineSmoke("DICE_NET_SMOKE connection failure " + ex.GetType().Name, true); Application.Quit(2); } _onlineSession = null; Destroy(session); } }
            finally { _onlineConnecting = false; if (OnlineActive) RefreshOnline(); }
        }
        private async void LeaveOnline()
        {
            if (_onlineCode != null) _onlineCode.DeactivateInputField();
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject == _onlineCode?.gameObject)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            var old = _onlineSession; _onlineSession = null;
            _onlineStopped = true; _onlineState = null; _onlineDuel = null; _onlinePending = false;
            _onlineRoot.gameObject.SetActive(false); _sdRoot.gameObject.SetActive(true); SDBackToTitle();
            if (old != null) { try { await old.LeaveAsync(); } catch (Exception) { } Destroy(old); }
        }
        private void SendOnline(DuelNetworkAction action)
        {
            if (_onlineSession == null || !_onlineSession.IsReady || _onlineStopped || _onlinePending || _onlineState == null) return;
            if ((action == DuelNetworkAction.Reroll || action == DuelNetworkAction.FinishRerolls) && !OnlineRerolling) return;
            if (action == DuelNetworkAction.Submit && !OnlinePlanning) return;
            if (action == DuelNetworkAction.Continue && (_onlineState.phase != (int)VersusPhase.Resolved || _onlineState.ready[OnlineMe] || _onlineImpactAge >= 0)) return;
            _onlinePending = true;
            _onlineSession.SendCommand(new DuelNetworkCommand { Round = _onlineState.round, Sequence = ++_onlineSequence, Action = action, HeldMask = _onlineHeld, Kind = _onlineKind, Carry = _onlineCarrying });
            RefreshOnline();
        }
        private void ReceiveOnlineCommand(int player, DuelNetworkCommand command)
        {
            if (_onlineDuel == null) return;
            string error;
            if (command.Action == DuelNetworkAction.Continue)
            {
                if (_onlineDuel.Phase == VersusPhase.Resolved && command.Round == _onlineDuel.Round)
                {
                    _onlineReady[player] = true;
                    if (_onlineReady[0] && _onlineReady[1]) { _onlineReady[0] = _onlineReady[1] = false; _onlineDuel.NextRound(); }
                }
            }
            else if (command.Action == DuelNetworkAction.Reroll) _onlineDuel.TryReroll(player, command.Round, command.Sequence, command.HeldMask, out error);
            else if (command.Action == DuelNetworkAction.FinishRerolls) _onlineDuel.TryFinishRerolls(player, command.Round, command.Sequence, command.HeldMask, out error);
            else if (command.Action == DuelNetworkAction.Submit) _onlineDuel.TrySubmit(player, command.Round, command.Sequence, (ScoreHandKind)command.Kind, command.Carry, command.HeldMask, out error);
            PublishOnline();
        }
        private string OnlineJson(int viewer)
        {
            var players = _onlineDuel.Snapshot(viewer);
            var state = new OnlineState { round = _onlineDuel.Round, phase = (int)_onlineDuel.Phase, winner = _onlineDuel.Winner, revision = _onlineRevision, players = new OnlinePlayer[2], damage = _onlineDuel.LastRound?.Damage, ready = (bool[])_onlineReady.Clone() };
            for (int i = 0; i < 2; i++) { var p = players[i]; state.players[i] = new OnlinePlayer { hp = p.Hp, held = p.HeldMask, dice = p.Dice, used = p.Used, rerolls = p.RerollsLeft, submitted = p.Submitted, rerollsFinished = p.RerollsFinished, nextBonus = p.NextRerollBonus, kind = (int)p.Kind, score = p.Score, carry = p.Carry, carryUsed = p.CarryUsed }; }
            return JsonUtility.ToJson(state);
        }
        private void PublishOnline() { _onlineRevision++; _onlineSession.PublishSnapshot(OnlineJson(0), OnlineJson(1)); }
        private void ReceiveOnlineSnapshot(string json)
        {
            var next = JsonUtility.FromJson<OnlineState>(json);
            if (next == null || next.players == null || next.players.Length != 2) return;
            if (_onlineState != null && next.revision <= _onlineState.revision) return;
            if (_onlineState != null && _onlineState.phase == (int)VersusPhase.ChoosingRoles && (next.phase == (int)VersusPhase.Resolved || next.phase == (int)VersusPhase.Finished))
            { _onlineBeforeOwn = _onlineState.players[OnlineMe].hp; _onlineBeforeEnemy = _onlineState.players[1 - OnlineMe].hp; _onlineImpactAge = 0; }
            if (_onlineState == null || next.round != _onlineState.round) { _onlineHeld = next.players[OnlineMe].held; _onlineCarrying = false; }
            if (_onlineState == null || next.phase != _onlineState.phase) _onlinePressed = false;
            _onlineState = next; _onlinePending = false;
            if (next.players[OnlineMe].used[_onlineKind]) ChangeOnlineRole(1);
            RefreshOnline();
        }
        private void ChangeOnlineRole(int direction)
        {
            if (_onlineState == null) return;
            for (int i = 0; i < ScoreRules.HandCount; i++) { _onlineKind = (_onlineKind + direction + ScoreRules.HandCount) % ScoreRules.HandCount; if (!_onlineState.players[OnlineMe].used[_onlineKind]) break; }
        }
        private void RefreshOnline()
        {
            if (_onlineRoot == null) return;
            bool lobby = _onlineState == null;
            _onlineLobbyTitle.gameObject.SetActive(lobby);
            _onlineCreate.gameObject.SetActive(lobby); _onlineJoin.gameObject.SetActive(lobby); _onlineCode.gameObject.SetActive(lobby);
            _onlineCreate.interactable = _onlineJoin.interactable = !_onlineConnecting && _onlineSession == null;
            _onlineCode.interactable = !_onlineConnecting && _onlineSession == null;
            _onlineRoll.gameObject.SetActive(false);
            _onlineSubmit.gameObject.SetActive(!lobby && _onlineState.phase == (int)VersusPhase.Playing);
            _onlineCarry.gameObject.SetActive(!lobby && _onlineState.phase == (int)VersusPhase.ChoosingRoles &&
                !_onlineState.players[OnlineMe].submitted);
            _onlineEnemyHpFill.transform.parent.gameObject.SetActive(!lobby);
            _onlineOwnHpFill.transform.parent.gameObject.SetActive(!lobby);
            _onlineEnemy.gameObject.SetActive(!lobby);
            _onlineRole.gameObject.SetActive(!lobby);
            _onlineScore.gameObject.SetActive(!lobby);
            _onlineRerollDots.gameObject.SetActive(!lobby);
            _onlineHit.gameObject.SetActive(!lobby);
            _onlineChanceCue.gameObject.SetActive(false);
            if (_onlineDrawMiracle != null)
                _onlineDrawMiracle.gameObject.SetActive(!lobby && _onlineState.phase == (int)VersusPhase.Finished && _onlineState.winner == 2);
            for (int i = 0; i < 5; i++)
            {
                _onlineOwnDice[i].gameObject.SetActive(!lobby);
                _onlineEnemyDice[i].gameObject.SetActive(!lobby);
                _onlineMine[i].SetInteractable(OnlineRerolling);
                _onlineTheirs[i].SetInteractable(false);
            }
            foreach (var role in _onlineRoles) role.gameObject.SetActive(!lobby);
            foreach (var score in _onlineRoleValues) score.gameObject.SetActive(!lobby);
            foreach (var role in _onlineEnemyRoles) role.gameObject.SetActive(!lobby);
            _onlineOwnList.text = lobby ? "" : "あなたの役"; _onlineEnemyList.text = lobby ? "" : "相手の役";
            if (lobby) return;
            var mine = _onlineState.players[OnlineMe]; var enemy = _onlineState.players[1 - OnlineMe];
            for (int i = 0; i < 5; i++) { _onlineMine[i].SetValue(mine.dice[i]); _onlineMine[i].SetTint(DiceTint.Plain, (_onlineHeld & (1 << i)) != 0); _onlineTheirs[i].SetValue(enemy.dice[i]); }
            if (_onlineImpactAge < 0)
            {
                _onlineOwnHpFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(mine.hp / (float)ScoreDuel.StartingHp), 1);
                _onlineEnemyHpFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(enemy.hp / (float)ScoreDuel.StartingHp), 1);
            }
            for (int k = 0; k < ScoreRules.HandCount; k++)
            {
                _onlineRoles[k].GetComponentInChildren<Text>().text = ScoreRules.Name((ScoreHandKind)k);
                _onlineRoles[k].interactable = OnlinePlanning && !mine.used[k];
                bool selected = k == _onlineKind && !mine.used[k];
                StyleOnlineRoleButton(_onlineRoles[k], selected);
                _onlineRoleValues[k].text = mine.used[k] ? "—" : ScoreRules.Evaluate((ScoreHandKind)k, mine.dice).Score.ToString();
                _onlineRoleValues[k].color = selected ? DiceYahzeeVisuals.Ink : mine.used[k] ? DiceYahzeeVisuals.MutedText : DiceYahzeeVisuals.Parchment;
                _onlineEnemyRoles[k].text = (enemy.used[k] ? "✓ " : "") + ScoreRules.Name((ScoreHandKind)k);
                _onlineEnemyRoles[k].color = enemy.used[k] ? DiceYahzeeVisuals.Brass : DiceYahzeeVisuals.Parchment;
            }
            bool resolved = _onlineState.phase == (int)VersusPhase.Resolved || _onlineState.phase == (int)VersusPhase.Finished;
            int ownReceived = OnlineReceivedDamage(resolved, OnlineMe);
            int enemyReceived = OnlineReceivedDamage(resolved, 1 - OnlineMe);
            if (_onlineImpactAge < 0)
            {
                // No drain is running, so the round's damage is the figure each bar names until the
                // next round replaces it.
                _onlineOwnDamageShown = ownReceived;
                _onlineEnemyDamageShown = enemyReceived;
                SDSetOnlineHpLines(mine.hp, enemy.hp);
            }
            if (_onlineImpactAge < 0) { _onlineOwnLoss.rectTransform.anchorMax = _onlineOwnHpFill.rectTransform.anchorMax; _onlineEnemyLoss.rectTransform.anchorMax = _onlineEnemyHpFill.rectTransform.anchorMax; }
            _onlineEnemy.text = resolved ? ScoreRules.Name((ScoreHandKind)enemy.kind) + "　" + enemy.score + (enemy.carry ? "　持越" : "") : enemy.submitted ? "相手が確定" : "相手が選択中";
            _onlineRole.text = ScoreRules.Name((ScoreHandKind)(mine.submitted ? mine.kind : _onlineKind));
            // One evaluate serves both the active role caption and the score numeral: the
            // per-role list above evaluates each of the nine kinds, and this is the selected one.
            int plannedScore = ScoreRules.Evaluate((ScoreHandKind)_onlineKind, mine.dice).Score;
            _onlineScore.text = (mine.submitted ? mine.score : plannedScore).ToString();
            RefreshOnlineRerollPips(mine.rerolls, OnlineRerolling, mine.nextBonus);
            _onlineRoll.interactable = OnlineRerolling && mine.rerolls > 0 && _onlineHeld != 31;
            _onlineSubmit.interactable = OnlineRerolling; _onlineCarry.interactable = OnlinePlanning && !mine.carryUsed;
            _onlineCarry.GetComponentInChildren<Text>().text = mine.carryUsed ? "持越　済" : _onlineCarrying ? "持越　選択中" : "持越　1";
            StyleOnlineButton(_onlineCarry, !mine.carryUsed);
            // The declaration is private until both sides submit, so the cue names the swap while
            // the choice is still changeable and again once the exchange has actually landed.
            bool chanceDeclared = !resolved && (ScoreHandKind)(mine.submitted ? mine.kind : _onlineKind) == ScoreHandKind.Chance;
            bool chanceSwapped = resolved && (mine.kind == (int)ScoreHandKind.Chance) != (enemy.kind == (int)ScoreHandKind.Chance);
            _onlineChanceCue.text = chanceDeclared ? "チャンス：被害の受け手を入れ替える" : chanceSwapped ? "被害の受け手が入れ替わった" : "";
            _onlineChanceCue.gameObject.SetActive(chanceDeclared || chanceSwapped);
            if (_onlineStopped) return;
            if (_onlineState.phase == (int)VersusPhase.Finished) _onlineStatus.text = _onlineState.winner == 2 ? "引き分け" : _onlineState.winner == OnlineMe ? "あなたの勝利" : "あなたの敗北";
            else if (resolved) _onlineStatus.text = _onlineState.ready[OnlineMe] ? "相手を待っています" : "ROUND " + _onlineState.round.ToString("00");
            else _onlineStatus.text = "ROUND " + _onlineState.round.ToString("00") + (mine.submitted || mine.rerollsFinished && _onlineState.phase == (int)VersusPhase.Playing ? "　相手を待っています" : _onlineState.phase == (int)VersusPhase.Playing ? "　振り直し" : "　役を選択");
        }
        /// <summary>
        /// Draws one pip per reroll the round offers: 2 normally, 3 after a formed Yahtzee.
        /// The snapshot carries only the remaining count, but the host publishes the round's
        /// starting count before any reroll and rerolls only ever decrease inside a round, so
        /// the highest value seen this round is that round's capacity.
        /// </summary>
        private void RefreshOnlineRerollPips(int rerolls, bool rerolling, bool nextBonus)
        {
            if (_onlineState.round != _onlineRerollRound)
            {
                _onlineRerollRound = _onlineState.round;
                _onlineRerollCapacity = 0;
            }
            _onlineRerollCapacity = Mathf.Clamp(Mathf.Max(_onlineRerollCapacity, rerolls), VersusDuel.BaseRerolls, VersusDuel.MaxRerolls);
            int available = rerolling ? Mathf.Clamp(rerolls, 0, _onlineRerollCapacity) : 0;
            if (available <= 0)
            {
                // Empty pips read like a broken zero counter, so state the end plainly and
                // advertise the pending Yahtzee bonus instead of drawing misleading dots.
                bool carriesBonus = nextBonus && _onlineState.phase != (int)VersusPhase.Finished && _onlineState.phase != (int)VersusPhase.Interrupted;
                _onlineRerollDots.text = carriesBonus ? "次の振り直し+1回" : "振り直し終了";
                _onlineRerollDots.color = carriesBonus ? DiceYahzeeVisuals.Brass : DiceYahzeeVisuals.MutedText;
                return;
            }
            string pips = "";
            for (int i = 0; i < _onlineRerollCapacity; i++) pips += (i > 0 ? "  " : "") + (i < available ? "●" : "○");
            _onlineRerollDots.text = pips;
            _onlineRerollDots.color = DiceYahzeeVisuals.Brass;
        }
        private bool _onlineSmokeMode, _onlineSmokeHost;
        private string _onlineSmokePath;
        private bool _onlineSmokePlayingCaptured, _onlineSmokeResolvedCaptured, _onlineSmokeImpactCaptured;
        private void TraceOnlineSmoke(string message, bool error = false)
        {
            System.IO.File.AppendAllText(System.IO.Path.Combine(_smokeOutput, _onlineSmokeHost ? "host.trace" : "guest.trace"), message + Environment.NewLine);
            if (error) Debug.LogError(message); else Debug.Log(message);
        }
        private System.Collections.IEnumerator OnlineSmoke()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-diceNetHost" || args[i] == "-diceNetJoin")
                { _onlineSmokeMode = true; _onlineSmokeHost = args[i] == "-diceNetHost"; _onlineSmokePath = args[i + 1]; break; }
            if (!_onlineSmokeMode) yield break;
            _smokeOutput = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(_onlineSmokePath));
            yield return null;
            OpenOnline();
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                yield return new WaitForEndOfFrame();
                SmokeCapture((_onlineSmokeHost ? "host" : "guest") + "-online-lobby");
            }
            if (!_onlineSmokeHost)
            {
                float until = Time.realtimeSinceStartup + 120;
                while (!System.IO.File.Exists(_onlineSmokePath) && Time.realtimeSinceStartup < until) yield return new WaitForSecondsRealtime(.5f);
                if (!System.IO.File.Exists(_onlineSmokePath)) { TraceOnlineSmoke("DICE_NET_SMOKE code file timeout", true); Application.Quit(2); yield break; }
                string invite = System.IO.File.ReadAllText(_onlineSmokePath).Trim();
                // The visual path reaches this check one frame later than headless, because it waits
                // for the lobby capture's end of frame first. A same-resume raycast can run before the
                // canvas layout pass has given the brand-new screen any screen geometry, so a
                // graphics-less guest must spend one frame and force the canvases itself.
                yield return null;
                Canvas.ForceUpdateCanvases();
                var events = UnityEngine.EventSystems.EventSystem.current;
                var pointer = new UnityEngine.EventSystems.PointerEventData(events) {
                    button = UnityEngine.EventSystems.PointerEventData.InputButton.Left,
                    position = RectTransformUtility.WorldToScreenPoint(null, _onlineCode.transform.position)
                };
                var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                events.RaycastAll(pointer, hits);
                if (hits.Count == 0 || UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IPointerDownHandler>(hits[0].gameObject) != _onlineCode.gameObject)
                { TraceOnlineSmoke("DICE_NET_SMOKE invite field cannot be clicked screen=" + UnityEngine.Screen.width + "x" + UnityEngine.Screen.height + " point=" + (Vector2)pointer.position + " hits=" + hits.Count, true); Application.Quit(2); yield break; }
                UnityEngine.EventSystems.ExecuteEvents.Execute(_onlineCode.gameObject, pointer, UnityEngine.EventSystems.ExecuteEvents.pointerDownHandler);
                UnityEngine.EventSystems.ExecuteEvents.Execute(_onlineCode.gameObject, pointer, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
                yield return null;
                foreach (char digit in invite)
                {
                    char upper = char.ToUpperInvariant(digit);
                    KeyCode key = upper >= 'A' && upper <= 'Z' ? (KeyCode)((int)KeyCode.A + upper - 'A') :
                        upper >= '0' && upper <= '9' ? (KeyCode)((int)KeyCode.Alpha0 + upper - '0') : KeyCode.None;
                    _onlineCode.ProcessEvent(new Event { type = EventType.KeyDown, keyCode = key, character = upper });
                }
                _onlineCode.ForceLabelUpdate();
                // A hidden batch-mode player may lose OS focus between frames. The
                // pointer hit, typed value, and visible placeholder state are the
                // stable player-level checks; focus itself is covered in PlayMode.
                if (_onlineCode.text != invite || _onlineCode.placeholder.enabled)
                { TraceOnlineSmoke("DICE_NET_SMOKE invite field rejected typed code focus=" + _onlineCode.isFocused + " length=" + _onlineCode.text.Length + " expectedLength=" + invite.Length + " placeholder=" + _onlineCode.placeholder.enabled, true); Application.Quit(2); yield break; }
                TraceOnlineSmoke("DICE_NET_SMOKE invite code entered through clickable UI");
                _onlineCode.DeactivateInputField();
                events.SetSelectedGameObject(null);
            }
            ConnectOnline(_onlineSmokeHost);
            int loggedRound = -1, loggedPhase = -1;
            float deadline = Time.realtimeSinceStartup + 300;
            while (Time.realtimeSinceStartup < deadline && OnlineActive && !_onlineStopped)
            {
                yield return new WaitForSecondsRealtime(.7f);
                if (_onlineState == null || _onlinePending || _onlineImpactAge >= 0) continue;
                if (loggedRound != _onlineState.round || loggedPhase != _onlineState.phase)
                {
                    loggedRound = _onlineState.round; loggedPhase = _onlineState.phase;
                    // Declared kinds are appended once both are visible, so a recorded match
                    // documents whether Chance actually happened instead of leaving it unknown.
                    int ownKind = _onlineState.players[OnlineMe].kind;
                    int enemyKind = _onlineState.players[1 - OnlineMe].kind;
                    bool kindsRevealed = _onlineState.phase == (int)VersusPhase.Resolved || _onlineState.phase == (int)VersusPhase.Finished;
                    TraceOnlineSmoke("DICE_NET_SMOKE player=" + OnlineMe + " round=" + loggedRound + " phase=" + loggedPhase + " hp=" + _onlineState.players[0].hp + "," + _onlineState.players[1].hp + (kindsRevealed ? " kind=" + ownKind + "," + enemyKind : ""));
                }
                if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null && _onlineState.round == 1)
                {
                    if (_onlineState.phase == (int)VersusPhase.Playing && !_onlineSmokePlayingCaptured)
                    { _onlineSmokePlayingCaptured = true; SmokeCapture((_onlineSmokeHost ? "host" : "guest") + "-online-playing"); }
                    if (_onlineState.phase == (int)VersusPhase.Resolved && !_onlineSmokeResolvedCaptured)
                    { _onlineSmokeResolvedCaptured = true; SmokeCapture((_onlineSmokeHost ? "host" : "guest") + "-online-resolved"); }
                }
                if (_onlineState.phase == (int)VersusPhase.Finished)
                { TraceOnlineSmoke("DICE_NET_SMOKE COMPLETE winner=" + _onlineState.winner); Application.Quit(0); yield break; }
                // Keep one reroll in hand before finishing so a 3-reroll Yahtzee round does
                // not end immediately; at the base 2 this is the previous behaviour exactly.
                if (OnlineRerolling) SendOnline(_onlineState.players[OnlineMe].rerolls > 1 ? DuelNetworkAction.Reroll : DuelNetworkAction.FinishRerolls);
                else if (OnlinePlanning)
                {
                    // The guest's only submission override: open round 1 with Chance, so every
                    // recorded match contains a one-sided declaration of kind=0,8 (guest kind=8,0)
                    // and the Chance swap lands through a real connection instead of staying unverified.
                    // Relies on joiner == LocalPlayerIndex == 1; if the transport ever renumbers
                    // seats, this silently stops declaring instead of erroring, so the round-1
                    // resolved trace would collapse back to kind=0,0 across every duel.
                    if (OnlineMe == 1 && _onlineState.round == 1) _onlineKind = (int)ScoreHandKind.Chance;
                    SendOnline(DuelNetworkAction.Submit);
                }
                else if (_onlineState.phase == (int)VersusPhase.Resolved && !_onlineState.ready[OnlineMe]) SendOnline(DuelNetworkAction.Continue);
            }
            TraceOnlineSmoke("DICE_NET_SMOKE did not complete", true); Application.Quit(2);
        }
        private void UpdateOnline()
        {
            foreach (var die in _onlineMine) die?.Tick(Time.deltaTime);
            UpdateOnlineImpact();
            UpdateOnlineCarryEffect();
            UpdateOnlineGuide();
            if (OnlineHelpOpen) return;
            if (_onlineState == null || _onlineStopped) return;
            if (_onlineState.phase == (int)VersusPhase.Resolved) { if (_onlineImpactAge < 0 && Input.GetMouseButtonDown(0) && !_onlineState.ready[OnlineMe] && Input.mousePosition.y < UnityEngine.Screen.height * .85f) SendOnline(DuelNetworkAction.Continue); return; }
            UpdateOnlineWaitingDice();
            if (OnlineRerolling) { if (Input.GetMouseButtonDown(1)) SendOnline(DuelNetworkAction.Reroll); return; }
            if (!OnlinePlanning) { _onlinePressed = false; return; }
            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > .01f) { ChangeOnlineRole(wheel > 0 ? 1 : -1); RefreshOnline(); }
            if (Input.GetMouseButtonDown(0)) { RectTransformUtility.ScreenPointToLocalPointInRectangle(_onlineRoot, Input.mousePosition, null, out Vector2 local); _onlinePress = Input.mousePosition; _onlinePressed = Mathf.Abs(local.x) < 310 && local.y < 0 && local.y > -140; _onlinePressFrame = Time.frameCount; }
            if (Input.GetMouseButtonUp(0) && _onlinePressed) { _onlinePressed = false; Vector2 delta = (Vector2)Input.mousePosition - _onlinePress; if (Time.frameCount > _onlinePressFrame && delta.y < -45 && -delta.y > Mathf.Abs(delta.x)) SendOnline(DuelNetworkAction.Submit); }
        }
        private void UpdateOnlineWaitingDice()
        {
            if (_onlineImpactAge >= 0 || _onlineState == null) return;
            bool roles = _onlineState.phase == (int)VersusPhase.ChoosingRoles;
            bool mineWaiting = roles && _onlineState.players[OnlineMe].submitted;
            bool theirsWaiting = roles && _onlineState.players[1 - OnlineMe].submitted;
            for (int i = 0; i < 5; i++)
            {
                // Whoever has committed holds the pre-launch pose and trembles there until the
                // other side commits: each hand pulls back away from the centre and shivers, so
                // the wait reads as stored power instead of a frozen screen. The opponent's
                // shiver is also the only tell that their declaration is already in.
                var own = Vector2.zero;
                var enemy = Vector2.zero;
                if (mineWaiting && !_sdReducedMotion)
                {
                    float phase = Time.unscaledTime * 38 + i * 1.7f;
                    own = new Vector2(Mathf.Sin(phase) * 4.6f, -2.6f + Mathf.Sin(phase * .83f) * 3.0f);
                }
                if (theirsWaiting && !_sdReducedMotion)
                {
                    float phase = Time.unscaledTime * 38 + i * 1.7f + 2.1f;
                    enemy = new Vector2(Mathf.Sin(phase) * 4.2f, 2.6f + Mathf.Sin(phase * .83f) * 3.0f);
                }
                _onlineMine[i].SetPosition(OnlineDieHome(i, true) + own);
                _onlineTheirs[i].SetPosition(OnlineDieHome(i, false) + enemy);
            }
        }
        private void UpdateOnlineImpact()
        {
            if (_onlineImpactAge < 0 || _onlineState == null) return;
            _onlineImpactAge += Time.unscaledDeltaTime;
            int ownDamage = _onlineState.damage[OnlineMe], enemyDamage = _onlineState.damage[1 - OnlineMe];
            int damage = Mathf.Max(ownDamage, enemyDamage);
            int tier = damage < 30 ? 0 : damage < 50 ? 1 : damage < 80 ? 2 : 3;
            // Even the quietest tier reaches visible contact between the two hands.
            float launch = _sdReducedMotion ? 0 : Mathf.Sin(Mathf.Clamp01(_onlineImpactAge / .52f) * Mathf.PI) * (76 + tier * 20);
            for (int i = 0; i < 5; i++)
            {
                _onlineMine[i].SetPosition(OnlineDieHome(i, true) + Vector2.up * launch);
                _onlineTheirs[i].SetPosition(OnlineDieHome(i, false) + Vector2.down * launch);
            }
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01((_onlineImpactAge - .22f) / .6f));
            float ownHp = Mathf.Lerp(_onlineBeforeOwn, _onlineState.players[OnlineMe].hp, t);
            float enemyHp = Mathf.Lerp(_onlineBeforeEnemy, _onlineState.players[1 - OnlineMe].hp, t);
            _onlineOwnHpFill.rectTransform.anchorMax = new Vector2(ownHp / ScoreDuel.StartingHp, 1);
            _onlineEnemyHpFill.rectTransform.anchorMax = new Vector2(enemyHp / ScoreDuel.StartingHp, 1);
            float lossT = Mathf.Clamp01((_onlineImpactAge - .65f) / .65f);
            _onlineOwnLoss.rectTransform.anchorMax = new Vector2(Mathf.Lerp(_onlineBeforeOwn, _onlineState.players[OnlineMe].hp, lossT) / ScoreDuel.StartingHp, 1);
            _onlineEnemyLoss.rectTransform.anchorMax = new Vector2(Mathf.Lerp(_onlineBeforeEnemy, _onlineState.players[1 - OnlineMe].hp, lossT) / ScoreDuel.StartingHp, 1);
            // The drain keeps naming the round's own damage while the bar catches up with it.
            _onlineOwnDamageShown = _onlineState.damage != null ? Mathf.Max(0, _onlineState.damage[OnlineMe]) : 0;
            _onlineEnemyDamageShown = _onlineState.damage != null ? Mathf.Max(0, _onlineState.damage[1 - OnlineMe]) : 0;
            SDSetOnlineHpLines(Mathf.RoundToInt(ownHp), Mathf.RoundToInt(enemyHp));
            _onlineImpactGraphic.Clear();
            if (!_sdReducedMotion && _onlineImpactAge < 1.1f)
            {
                float burst = Mathf.Sin(Mathf.Clamp01((_onlineImpactAge - .12f) / .98f) * Mathf.PI);
                Vector2 center = new Vector2(0, 27);
                Color flash = DiceYahzeeVisuals.Brass;
                flash.a = burst * (.76f + tier * .06f);
                float radius = (83 + tier * 25) * (.76f + burst * .32f);
                _onlineImpactGraphic.Ring(center, radius, 3.5f + tier * 1.1f, flash, 48);
                int rays = 10 + tier * 6;
                for (int i = 0; i < rays; i++)
                {
                    float angle = i * Mathf.PI * 2f / rays + .15f;
                    Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    float near = 68 + tier * 15;
                    float far = near + (32 + tier * 18) * burst;
                    _onlineImpactGraphic.Line(center + direction * near, center + direction * far,
                        3.1f + tier * 1.0f, flash);
                }
                if (tier >= 1)
                {
                    Color echo = flash; echo.a *= .55f;
                    _onlineImpactGraphic.Ring(center, radius * 1.28f, 2.0f + tier * .5f, echo, 48);
                }
            }
            _onlineImpactGraphic.Flush();
            if (_onlineSmokeMode && !_onlineSmokeImpactCaptured && _onlineState.round == 1 &&
                _onlineImpactAge > .38f && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                _onlineSmokeImpactCaptured = true;
                SmokeCapture((_onlineSmokeHost ? "host" : "guest") + "-online-impact");
            }
            _onlineHit.text = "";
            if (_onlineImpactAge > 1.55f) { _onlineImpactAge = -1; _onlineHit.text = ""; RefreshOnline(); }
        }
        private void UpdateOnlineCarryEffect()
        {
            if (_onlineCarryEffect == null || _onlineCarryEffectAge < 0) return;
            _onlineCarryEffectAge += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_onlineCarryEffectAge / .56f);
            Vector2 center = _onlineRoot.InverseTransformPoint(_onlineCarry.transform.position);
            Color glow = _onlineCarrying ? DiceYahzeeVisuals.Brass : DiceYahzeeVisuals.MutedText;
            glow.a = (1 - t) * .9f;
            _onlineCarryEffect.Clear();
            _onlineCarryEffect.Ellipse(center, 92 + 20 * t, 25 + 13 * t, 2.5f, glow, 32);
            if (_onlineCarrying)
            {
                for (int i = 0; i < 20; i++)
                {
                    float travel = Mathf.Clamp01((t - (i % 4) * .045f) / .82f);
                    Vector2 source = new Vector2((i % 5 - 2) * 116, -76) + new Vector2((i % 3 - 1) * 18, (i % 4) * 6);
                    Vector2 destination = center + new Vector2((i % 5 - 2) * 19, 0);
                    Vector2 head = Vector2.Lerp(source, destination, travel * travel);
                    head.y += Mathf.Sin(travel * Mathf.PI) * (13 + i % 3 * 7);
                    Color spark = DiceYahzeeVisuals.Brass;
                    spark.a = Mathf.Sin(travel * Mathf.PI) * .95f;
                    _onlineCarryEffect.Line(head + Vector2.down * 9, head, 3, spark);
                }
            }
            _onlineCarryEffect.Flush();
            if (t < 1) return;
            _onlineCarryEffectAge = -1;
            _onlineCarryEffect.Clear(); _onlineCarryEffect.Flush();
        }
        private void UpdateOnlineGuide()
        {
            if (_onlineGuideGraphic == null) return;
            bool active = OnlinePlanning || OnlineRerolling;
            bool continueCue = _onlineState != null && _onlineState.phase == (int)VersusPhase.Resolved &&
                !_onlineState.ready[OnlineMe] && !_onlineStopped && !OnlineHelpOpen;
            _onlineGuideMouse.gameObject.SetActive(active);
            _onlineWheelMouse.gameObject.SetActive(OnlinePlanning);
            _onlineContinueMouse.gameObject.SetActive(continueCue);
            _onlineGuideGraphic.Clear();
            if (!active)
            {
                if (continueCue)
                {
                    float pulse = _sdReducedMotion ? 1 : .62f + .38f * Mathf.Sin(Time.unscaledTime * 4.2f);
                    Color cue = DiceYahzeeVisuals.Brass; cue.a = pulse;
                    _onlineContinueMouse.color = cue;
                    _onlineGuideGraphic.Ring(_onlineContinueMouse.rectTransform.anchoredPosition,
                        18 + (_sdReducedMotion ? 0 : (1 - pulse) * 8), 1.6f, cue, 24);
                }
                _onlineGuideGraphic.Flush();
                return;
            }
            int held = 0;
            for (int i = 0; i < 5; i++) if ((_onlineHeld & (1 << i)) != 0) held++;
            bool reroll = OnlineRerolling;
            float phase = Mathf.Repeat(Time.unscaledTime, 1.8f) / 1.8f;
            float down = Mathf.SmoothStep(0, 1, Mathf.Clamp01((phase - .12f) / .56f));
            float alpha = _sdReducedMotion || reroll ? 1 : Mathf.Min(Mathf.Clamp01(phase / .08f), 1 - Mathf.Clamp01((phase - .80f) / .12f));
            _onlineGuideMouse.sprite = DiceYahzeeVisuals.LoadSprite(reroll ? "Art/DiceYahzee/input-mouse-right" : "Art/DiceYahzee/input-mouse-left");
            _onlineGuideMouse.rectTransform.anchoredPosition = new Vector2(320, -30 - (_sdReducedMotion || reroll ? 0 : 18 * down));
            Color ink = DiceYahzeeVisuals.Brass; ink.a = alpha * .85f;
            _onlineGuideMouse.color = ink;
            if (reroll)
            {
                _onlineGuideGraphic.Ring(_onlineGuideMouse.rectTransform.anchoredPosition, 23 + (_sdReducedMotion ? 0 : phase * 8), 1.6f, ink, 24);
                Vector2 button = _onlineGuideMouse.rectTransform.anchoredPosition + new Vector2(5, 7);
                _onlineGuideGraphic.Line(button + Vector2.down * 4, button + Vector2.up * 4, 3.5f, ink);
            }
            else
            {
                Vector2 head = new Vector2(320, -82 - (_sdReducedMotion ? 16 : 42 * down));
                for (int i = 0; i < 3; i++)
                {
                    Vector2 tip = head + Vector2.up * (i * 15);
                    Color cue = ink; cue.a *= 1 - i * .22f;
                    _onlineGuideGraphic.Line(tip + new Vector2(-9, 4), tip + new Vector2(0, -5), 2.8f, cue);
                    _onlineGuideGraphic.Line(tip + new Vector2(0, -5), tip + new Vector2(9, 4), 2.8f, cue);
                }
            }
            float wheelY = _sdReducedMotion ? 0 : Mathf.Sin(Time.unscaledTime * 3.8f) * 4;
            _onlineWheelMouse.rectTransform.anchoredPosition = new Vector2(579, 207 + wheelY);
            _onlineGuideGraphic.Flush();
        }
    }
}
