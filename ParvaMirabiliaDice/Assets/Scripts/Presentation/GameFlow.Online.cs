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
            public int[] scores;
            public bool[] used;
            public bool submitted, carry, carryUsed, rerollsFinished, nextBonus, carried;
        }
        [Serializable] private sealed class OnlineState
        {
            public int round, phase, winner;
            public long revision;
            public OnlinePlayer[] players;
            public int[] damage;
            public bool[] ready;
            public bool[] rematchReady;
            // Which match this snapshot describes. Incremented by the host on rematch, so the
            // guest can distinguish a brand-new duel from a continued one on the same session.
            public int match;
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
        private Text _onlineDrawMiracle, _onlineFormula, _onlineEnemyFormula, _onlineEnemyScore;
        private readonly Image[] _onlineRoleMarkers = new Image[9];
        private readonly Text[] _onlineEnemyValues = new Text[9];
        private readonly Image[] _onlineRollPips = new Image[3];
        private readonly Image[] _onlineHoldMarks = new Image[5];
        private readonly Image[] _onlineEnemyMarkers = new Image[9];
        private float _onlineChargeAge;
        private bool _onlineContactPlayed, _onlineOutcomePlayed;
        private ScoreHandEffectGraphic _onlineCarryEffect;
        private ScoreHandEffectGraphic _onlineImpactGraphic;
        private SharedAttackPresenter _onlineAttackPresenter;
        private int _onlineImpactWinner = -1;
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
        // Host invite-code copy. The code is captured from the session's Joined event into a
        // private field so the button and its copied feedback can be driven deterministically
        // and cleared whenever the room ends.
        private Button _onlineCopyCode;
        private Text _onlineCopyNotice;
        private string _onlineJoinCode = string.Empty, _onlineCopyStatus = string.Empty;
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
        // Rematch state. Generation labels the current match on the host's authoritative clock;
        // _onlineSequence stays globally monotonic across matches so accepted commands never replay.
        private int _onlineMatch;
        private readonly bool[] _onlineRematchReady = new bool[2];
        private Button _onlineRematch, _onlineReturnLobby;
        private RectTransform _onlineResult;
        private Text _onlineResultTitle, _onlineResultDetail;
        private float _onlineResultAge = -1;
        private bool _onlineResultsRevealed, _onlineLeaving;

        private bool OnlineActive => _onlineRoot != null && _onlineRoot.gameObject.activeSelf;
        private int OnlineMe => _onlineSession == null ? 0 : _onlineSession.LocalPlayerIndex;
        private bool OnlinePlanning => _onlineSession != null && _onlineSession.IsReady && _onlineState != null && _onlineState.phase == (int)VersusPhase.ChoosingRoles &&
            !_onlineState.players[OnlineMe].submitted && !_onlinePending && !_onlineStopped && !OnlineHelpOpen;

        private bool OnlineRerolling => _onlineSession != null && _onlineSession.IsReady && _onlineState != null &&
            _onlineState.phase == (int)VersusPhase.Playing && !_onlineState.players[OnlineMe].rerollsFinished &&
            !_onlinePending && !_onlineStopped && !OnlineHelpOpen;

        /// <summary>
        /// True only while a created host room is still waiting for its opponent and holds a
        /// captured nonempty invite code. Connecting, leaving, a live battle, an interruption,
        /// or an empty code all keep the copy control hidden and inert.
        /// </summary>
        private bool OnlineInviteCopyAvailable => OnlineActive && !_onlineConnecting && !_onlineLeaving &&
            _onlineSession != null && _onlineSession.IsHost && !_onlineSession.IsReady && !_onlineStopped &&
            _onlineState == null && !string.IsNullOrEmpty(_onlineJoinCode);

        private void BuildOnlineEntry()
        {
            StartCoroutine(OnlineSmoke());
            SDButton("Online", _sdTitle, new Vector2(0, -300), new Vector2(320, 42), "オンライン対戦", 19, OpenOnline);
        }
        private void OpenOnline()
        {
            _sdTitle.gameObject.SetActive(false);
            if (_onlineRoot == null) BuildOnlineScreen();
            if (_onlineAttackPresenter != null) _onlineAttackPresenter.Reset();
            _onlineState = null;
            _onlineDuel = null;
            _onlinePending = _onlineStopped = _onlineCarrying = false;
            _onlineImpactAge = _onlineCarryEffectAge = -1;
            _onlineContactPlayed = _onlineOutcomePlayed = false; _onlineChargeAge = 0;
            _onlineCommitQueued = false; _onlineWheelRemainder = 0;
            CancelOnlinePointer();
            _onlineImpactWinner = -1;
            if (_onlineCarryEffect != null) { _onlineCarryEffect.Clear(); _onlineCarryEffect.Flush(); }
            if (_onlineImpactGraphic != null) { _onlineImpactGraphic.Clear(); _onlineImpactGraphic.Flush(); }
            SDResetOnlineScorePulses();
            SDResetOnlineChanceGlow();
            SDResetOnlineFormation(true);
            _onlineHeld = _onlineKind = 0;
            _onlineRerollCapacity = VersusDuel.BaseRerolls; _onlineRerollRound = -1;
            _onlineSequence = _onlineRevision = 0;
            _onlineReady[0] = _onlineReady[1] = false;
            _onlineMatch = 0;
            _onlineRematchReady[0] = _onlineRematchReady[1] = false;
            _onlineResultAge = -1; _onlineResultsRevealed = false;
            _onlineStatus.text = "オンライン対戦";
            ClearOnlineInviteCopy();
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
            var table = SDBuildTableBackground(_onlineRoot);
            table.raycastTarget = true;
            _onlineRoot.gameObject.AddComponent<ScoreDuelPointer>().Initialize(this, -1);
            SDBuildTableMaterial(_onlineRoot);
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
            _onlineCopyCode = UiFactory.CreateButton("CopyInviteCode", _onlineRoot, new Vector2(250, 44), new Vector2(0, -138), "コードをコピー", 19, CopyOnlineInviteCode);
            _onlineCopyNotice = OnlineText("CopyNotice", "", 18, -178, DiceYahzeeVisuals.Brass);
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
                _onlineTheirs[i] = SDBuildTableDie(_onlineRoot, i, 44, "OpponentDie" + i, OnlineDieHome(i, false));
                _onlineTheirs[i].SetPosition(OnlineDieHome(i, false));
                _onlineMine[i] = SDBuildTableDie(_onlineRoot, i, 112, "OwnDie" + i, OnlineDieHome(i, true));
                _onlineMine[i].SetPosition(OnlineDieHome(i, true));
                _onlineEnemyDice[i] = (RectTransform)_onlineRoot.Find("OpponentDie" + i);
                _onlineOwnDice[i] = (RectTransform)_onlineRoot.Find("OwnDie" + i);
                SDBuildDiePointerSurface(_onlineOwnDice[i]);
                _onlineOwnDice[i].gameObject.AddComponent<ScoreDuelPointer>().Initialize(this, i);
                _onlineHoldMarks[i] = SDIcon("HoldMark" + i, _onlineRoot, "icon-lock", OnlineDieHome(i, true) + Vector2.down * 74, 18, SDPositive);
                _onlineMine[i].Clicked += _ => { if (OnlineRerolling) { _onlineHeld ^= 1 << index; GameAudio.Play("die_keep"); RefreshOnline(); } };
            }
            _onlineRole = OnlineText("Role", "", 22, -156, DiceYahzeeVisuals.Parchment);
            _onlineRole.rectTransform.sizeDelta = new Vector2(650, 30);
            _onlineRole.rectTransform.anchoredPosition = new Vector2(0, -99);
            _onlineFormula = SDText("Breakdown", _onlineRoot, "", 15, new Vector2(0, -126), new Vector2(600, 26), SDRailMuted);
            _onlineScore = SDBuildScoreNumber("Score", _onlineRoot, new Vector2(0, -173), true);
            _onlineEnemy.rectTransform.anchoredPosition = new Vector2(0, 139);
            _onlineEnemy.rectTransform.sizeDelta = new Vector2(300, 29);
            _onlineEnemy.fontSize = 17;
            _onlineEnemyFormula = SDText("NpcFormula", _onlineRoot, "", 13, new Vector2(0, 105), new Vector2(300, 22), SDRailMuted);
            _onlineEnemyScore = SDBuildScoreNumber("NpcScore", _onlineRoot, new Vector2(0, 67), false);
            for (int i = 0; i < 3; i++)
            {
                _onlineRollPips[i] = UiFactory.CreatePanel("RollPip" + i, _onlineRoot, Vector2.one * 14,
                    new Vector2((i - 1) * 24, SDRollPipY), SDPositive);
                _onlineRollPips[i].sprite = UiFactory.CircleSprite;
                var edge = _onlineRollPips[i].gameObject.AddComponent<Outline>();
                edge.effectColor = new Color(.03f, .07f, .08f, .9f); edge.effectDistance = new Vector2(2, -2);
            }
            _onlineRerollDots = OnlineText("Rerolls", "", 17, -266, DiceYahzeeVisuals.Brass);
            _onlineRerollDots.rectTransform.sizeDelta = new Vector2(180, 18);
            _onlineRoll = UiFactory.CreateButton("Reroll", _onlineRoot, new Vector2(220, 45), new Vector2(-260, -220), "振り直す", 19, () => SendOnline(DuelNetworkAction.Reroll));
            _onlineCarry = SDBuildCarryButton(_onlineRoot, "Carry", () =>
            {
                if (OnlineCanChoose && !_onlineState.players[OnlineMe].carryUsed)
                {
                    _onlineCarrying = !_onlineCarrying;
                    _onlineCarryEffectAge = _sdReducedMotion ? -1 : 0;
                    GameAudio.Play("die_keep"); RefreshOnline();
                }
            }, out _);

            _onlineSubmit = UiFactory.CreateButton("Submit", _onlineRoot, new Vector2(220, 45), new Vector2(260, -220), "振り直しを終える", 19, () => SendOnline(DuelNetworkAction.FinishRerolls));


            UiFactory.CreateButton("LeaveOnline", _onlineRoot, new Vector2(150, 36), new Vector2(510, 305), "タイトルへ", 16, LeaveOnline);
            PolishOnlineLayout();
            BuildOnlineResult();
            SDBuildOnlineFormation();
            RefreshOnline();
        }
        private void BuildOnlineResult()
        {
            _onlineResult = UiFactory.CreateStretch("OnlineResult", _onlineRoot);
            var background = UiFactory.CreateStretchPanel("Table", _onlineResult, new Color(.04f, .08f, .10f, .97f));
            background.raycastTarget = true;
            _onlineResultTitle = SDBuildResultTitle(_onlineResult);
            _onlineResultDetail = SDText("Detail", _onlineResult, "", 24, new Vector2(0, 78), new Vector2(1000, 150));
            SDBuildResultActions(_onlineResult, RequestOnlineRematch, ReturnToOnlineLobby, "ReturnLobby", "ロビーに戻る", out _onlineRematch, out _onlineReturnLobby);
            _onlineResult.gameObject.SetActive(false);
        }
        private bool OnlineHelpOpen => _onlineHelp != null && _onlineHelp.gameObject.activeSelf;
        private void PolishOnlineLayout()
        {
            ((RectTransform)_onlineCreate.transform).anchoredPosition = new Vector2(0, 66);
            ((RectTransform)_onlineCode.transform).anchoredPosition = new Vector2(0, -2);
            ((RectTransform)_onlineJoin.transform).anchoredPosition = new Vector2(0, -70);
            _onlineOwnList.rectTransform.anchoredPosition = new Vector2(SDRolePanelX, 184); _onlineOwnList.rectTransform.sizeDelta = new Vector2(174, 30);
            _onlineEnemyList.rectTransform.anchoredPosition = new Vector2(-SDRolePanelX, 184); _onlineEnemyList.rectTransform.sizeDelta = new Vector2(174, 30);
            for (int k = 0; k < ScoreRules.HandCount; k++)
            {
                int role = k;
                float rowY = 148 - k * 37;
                _onlineRoles[k] = SDBuildRoleRow("Role" + k, _onlineRoot, new Vector2(SDRolePanelX, rowY), (ScoreHandKind)k,
                    () => { if (OnlineCanChoose && !OnlineHelpOpen) { _onlineKind = role; _onlineRoleManual = true; SDRefreshOnlineFormation(); GameAudio.Play("die_keep"); RefreshOnline(); } },
                    out _onlineRoleValues[k], out _onlineRoleMarkers[k]);
                var enemy = SDBuildRoleRow("EnemyCategory" + k, _onlineRoot, new Vector2(-SDRolePanelX, rowY), (ScoreHandKind)k,
                    () => { }, out _onlineEnemyValues[k], out _onlineEnemyMarkers[k]);
                enemy.interactable = false;
                enemy.GetComponent<Image>().color = Color.clear;
                enemy.GetComponent<ButtonPalette>().Face = Color.clear;
                foreach (Graphic graphic in enemy.GetComponentsInChildren<Graphic>()) graphic.raycastTarget = false;
                _onlineEnemyRoles[k] = enemy.transform.Find("Name").GetComponent<Text>();
            }
            ConfigureOnlineHp(_onlineEnemyHpFill, 245, false);
            ConfigureOnlineHp(_onlineOwnHpFill, SDPlayerHpY, true);
            _onlineHp.gameObject.SetActive(false);
            _onlineCarry.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -232);
            _onlineCarry.GetComponent<RectTransform>().sizeDelta = new Vector2(204, 48);
            var carryEffects = UiFactory.CreateStretch("OnlineCarryEffect", _onlineRoot);
            _onlineCarryEffect = carryEffects.gameObject.AddComponent<ScoreHandEffectGraphic>();
            _onlineCarryEffect.raycastTarget = false;
            var impactEffects = UiFactory.CreateStretch("OnlineImpactEffect", _onlineRoot);
            _onlineImpactGraphic = impactEffects.gameObject.AddComponent<ScoreHandEffectGraphic>();
            _onlineImpactGraphic.raycastTarget = false;
            SDBuildOnlineChanceGlow();
            var mouse = UiFactory.CreateRect("FlickGuideMouse", _onlineRoot, new Vector2(36, 36), new Vector2(SDGuideX, -30));
            _onlineGuideMouse = mouse.gameObject.AddComponent<Image>();
            _onlineGuideMouse.sprite = DiceYahzeeVisuals.LoadSprite("Art/DiceYahzee/input-mouse-left");
            _onlineGuideMouse.raycastTarget = false;
            var next = UiFactory.CreateRect("ContinueMouse", _onlineRoot, new Vector2(27, 27), new Vector2(-92, 307));
            _onlineContinueMouse = next.gameObject.AddComponent<Image>();
            _onlineContinueMouse.sprite = DiceYahzeeVisuals.LoadSprite("Art/DiceYahzee/input-mouse-left");
            _onlineContinueMouse.raycastTarget = false;
            _onlineContinueMouse.gameObject.SetActive(false);
            var wheel = UiFactory.CreateRect("RoleWheelMouse", _onlineRoot, new Vector2(25, 25), new Vector2(SDRolePanelX + 69, 184));
            _onlineWheelMouse = wheel.gameObject.AddComponent<Image>();
            _onlineWheelMouse.sprite = DiceYahzeeVisuals.LoadSprite("Art/DiceYahzee/input-mouse-scroll");
            _onlineWheelMouse.color = DiceYahzeeVisuals.Brass;
            _onlineWheelMouse.raycastTarget = false;
            var guideEffects = UiFactory.CreateStretch("OnlineGestureGuide", _onlineRoot);
            _onlineGuideGraphic = guideEffects.gameObject.AddComponent<ScoreHandEffectGraphic>();
            _onlineGuideGraphic.raycastTarget = false;
            _onlineHit = OnlineText("Impact", "", 34, -204, DiceYahzeeVisuals.Brass);
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
            var help = UiFactory.CreateButton("Help", _onlineRoot, new Vector2(100, 32), new Vector2(-538, 307), "遊び方", 16, () => OpenOnlineHelp());
            foreach (var button in _onlineRoot.GetComponentsInChildren<Button>(true))
                if (!button.name.StartsWith("Role") && !button.name.StartsWith("EnemyCategory") && !button.name.Contains("Die"))
                    DiceYahzeeVisuals.StyleButton(button, button == _onlineCarry || button == _onlineRematch);
            _onlineHelp = UiFactory.CreateStretch("OnlineHelp", _onlineRoot);
            var dim = UiFactory.CreateStretchPanel("Dim", _onlineHelp, new Color(0, 0, 0, .82f));
            dim.raycastTarget = true;
            var panel = UiFactory.CreatePanel("HelpPanel", _onlineHelp, new Vector2(650, 380), Vector2.zero, DiceYahzeeVisuals.Panel); DiceYahzeeVisuals.StylePanel(panel);
            UiFactory.CreateText("Guide", panel.transform, "オンライン対戦\n\nクリック：ダイスを保持\n右クリック：保持以外を振り直す（通常2回）\nヤッツィー成立で次のラウンドは3回\n「振り直しを終える」で残りを省略\n双方の振り直し後に出目を同時公開\nホイール／右の役：役を選ぶ\nダイスを下へドラッグして離す：役を確定\n双方の確定後に同時攻撃。持越は1度だけ。", 20, TextAnchor.MiddleCenter, DiceYahzeeVisuals.Parchment, new Vector2(0, 20), new Vector2(600, 300));
            var close = UiFactory.CreateButton("Close", panel.transform, new Vector2(160, 38), new Vector2(0, -147), "戻る", 18, () => CloseOnlineHelp()); DiceYahzeeVisuals.StyleButton(close);
            _onlineHelp.gameObject.SetActive(false);
        }
        private void ConfigureOnlineHp(Image fill, float y, bool own)
        {
            var track = (RectTransform)fill.transform.parent;
            track.anchoredPosition = new Vector2(0, y); track.sizeDelta = new Vector2(SDHpWidth, SDHpThickness);
            track.GetComponent<Image>().color = new Color32(34, 48, 55, 255);
            fill.rectTransform.anchorMin = fill.rectTransform.anchorMax = new Vector2(0, .5f);
            fill.rectTransform.pivot = new Vector2(0, .5f); fill.rectTransform.anchoredPosition = Vector2.zero;
            fill.color = own ? SDPlayerColor : SDEnemyColor;
            var loss = SDBuildHpLossBand(fill);
            var text = SDBuildHpLabel("Hp", fill);
            text.rectTransform.sizeDelta = new Vector2(SDHpWidth - 24, 30); text.fontSize = 18;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
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
            var caption = button.transform.Find("Name").GetComponent<Text>();
            Color face = selected ? DiceYahzeeVisuals.Brass : Color.clear;
            Color label = selected ? SDPaperInk : button.interactable ? SDRailInk : SDRailUsed;
            image.color = face; caption.color = label;
            button.transform.Find("Multiplier").GetComponent<Text>().color = selected ? SDPaperMuted : SDRailMuted;
            var palette = button.GetComponent<ButtonPalette>();
            palette.Face = face; palette.Label = label;
        }
        private Text OnlineText(string name, string text, int size, float y, Color color) => SDText(name, _onlineRoot, text, size, new Vector2(0, y), new Vector2(1050, 48), color);
        /// <summary>The damage a viewer's bar names for the settled round, or 0 anywhere the round has not landed.</summary>
        private int OnlineReceivedDamage(bool resolved, int side)
        {
            return resolved && _onlineState.damage != null ? Mathf.Max(0, _onlineState.damage[side]) : 0;
        }
        /// <summary>One hand's home pose: the build, the tremble while waiting and the launch all read their start from here, so the three can never drift apart.</summary>
        private static Vector2 OnlineDieHome(int index, bool mine)
        {
            return mine ? new Vector2(SDHandCenterX + (index - 2) * 132, SDPlayerDiceY) : new Vector2(SDHandCenterX + (index - 2) * 51, 180);
        }
        private async void ConnectOnline(bool create)
        {
            if (_onlineConnecting || _onlineLeaving || _onlineSession != null) return;
            _onlineConnecting = true; _onlineStopped = false; _onlineStatus.text = "接続中…";
            ClearOnlineInviteCopy();
            RefreshOnline();
            var session = gameObject.AddComponent<DuelNetworkSession>();
            _onlineSession = session;
            if (_onlineSmokeMode) session.AuthenticationProfile = _onlineSmokeHost ? "dice-net-host" : "dice-net-guest";
            session.Joined += () => { if (_onlineSession == session && _onlineSmokeMode && session.IsHost) System.IO.File.WriteAllText(_onlineSmokePath, session.JoinCode); if (_onlineSession == session) _onlineStatus.text = session.IsHost ? "招待コード： " + session.JoinCode + "　相手の参加を待っています" : "対戦相手に接続中…"; };
            session.Joined += () => { if (_onlineSession == session && session.IsHost) { _onlineJoinCode = session.JoinCode; if (OnlineActive) RefreshOnline(); } };
            session.Ready += () => { if (_onlineSession != session) return; if (session.IsHost) { _onlineDuel = new VersusDuel(FreshSeed()); _onlineDuel.Begin(); PublishOnline(); } RefreshOnline(); };
            session.CommandReceived += ReceiveOnlineCommand;
            session.SnapshotReceived += ReceiveOnlineSnapshot;
            session.Interrupted += reason =>
            {
                if (_onlineSession != session || (_onlineState != null && _onlineState.phase == (int)VersusPhase.Finished)) return;
                _onlineDuel?.Interrupt();
                _onlineStopped = true;
                _onlinePending = false;
                if (_onlineAttackPresenter != null) _onlineAttackPresenter.Reset();
                SDRefreshOnlineFormation();
                _onlineImpactAge = -1;
                _onlineImpactWinner = -1;
                _onlineContactPlayed = false;
                if (_onlineHit != null) _onlineHit.text = "";
                _onlineStatus.text = reason;
                if (_onlineSmokeMode) { TraceOnlineSmoke("DICE_NET_SMOKE INTERRUPTED " + reason); Application.Quit(3); }
                RefreshOnline();
            };
            try { if (create) await session.CreateAsync(); else await session.JoinAsync(_onlineCode.text); }
            catch (Exception ex) { if (_onlineSession == session) { _onlineStatus.text = "接続できませんでした： " + ex.Message; ClearOnlineInviteCopy(); if (_onlineSmokeMode) { TraceOnlineSmoke("DICE_NET_SMOKE connection failure " + ex.GetType().Name, true); Application.Quit(2); } _onlineSession = null; Destroy(session); } }
            finally { _onlineConnecting = false; if (OnlineActive) RefreshOnline(); }
        }
        private async void LeaveOnline()
        {
            if (_onlineLeaving || _onlineConnecting) return;
            await DisposeOnlineSessionAsync();
            _onlineRoot.gameObject.SetActive(false); _sdRoot.gameObject.SetActive(true); SDBackToTitle();
        }
        /// <summary>
        /// Tears down the active network session safely and returns to the online lobby,
        /// keeping the online screen alive. Interruptions made it here already with a stopped
        /// match, so this must tolerate null sessions and a stale in-flight teardown.
        /// </summary>
        private async void ReturnToOnlineLobby()
        {
            if (_onlineLeaving || _onlineConnecting) return;
            await DisposeOnlineSessionAsync();
            _onlineState = null;
            ResetOnlineMatchPresentation();
            _onlineStopped = false; _onlineStatus.text = "オンライン対戦";
            RefreshOnline();
        }
        private async System.Threading.Tasks.Task DisposeOnlineSessionAsync()
        {
            if (_onlineCode != null) _onlineCode.DeactivateInputField();
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject == _onlineCode?.gameObject)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            _onlineLeaving = true;
            // Reset the shared overlay tree BEFORE state is cleared so no late higher-sort
            // dim/dice linger above a lobby/result.
            if (_onlineAttackPresenter != null) _onlineAttackPresenter.Reset();
            _onlineImpactAge = -1; _onlineImpactWinner = -1; _onlineContactPlayed = false;
            var old = _onlineSession; _onlineSession = null;
            _onlineStopped = true; _onlineState = null; _onlineDuel = null; _onlinePending = false;
            SDResetOnlineFormation(true);
            ClearOnlineInviteCopy();
            RefreshOnline();
            try { if (old != null) await old.LeaveAsync(); }
            catch (Exception ex) { Debug.LogWarning("Online leave: " + ex.GetType().Name); }
            finally { if (old != null) Destroy(old); _onlineLeaving = false; }
        }
        /// <summary>
        /// Clears match-local presentation and caches so a fresh rematch (or lobby return)
        /// starts from a neutral screen. Never touches _onlineSequence or _onlineRevision:
        /// the sequence must stay monotonic across matches for AcceptCommand's replay guard,
        /// and the revision must keep advancing so older snapshots are ignored.
        /// </summary>
        private void ResetOnlineMatchPresentation()
        {
            _onlineHeld = _onlineKind = 0;
            _onlineCarrying = _onlinePending = false;
            _onlineImpactAge = _onlineCarryEffectAge = -1;
            _onlineContactPlayed = _onlineOutcomePlayed = false; _onlineChargeAge = 0;
            _onlineCommitQueued = false; _onlineWheelRemainder = 0;
            CancelOnlinePointer();
            if (_onlineAttackPresenter != null) _onlineAttackPresenter.Reset();
            _onlineImpactWinner = -1;
            if (_onlineCarryEffect != null) { _onlineCarryEffect.Clear(); _onlineCarryEffect.Flush(); }
            if (_onlineImpactGraphic != null) { _onlineImpactGraphic.Clear(); _onlineImpactGraphic.Flush(); }
            SDResetOnlineScorePulses();
            SDResetOnlineChanceGlow();
            SDResetOnlineFormation(true);
            if (_onlineHit != null) _onlineHit.text = "";
            _onlineRerollCapacity = VersusDuel.BaseRerolls; _onlineRerollRound = -1;
            _onlineReady[0] = _onlineReady[1] = false;
            _onlineRematchReady[0] = _onlineRematchReady[1] = false;
            _onlineResultAge = -1; _onlineResultsRevealed = false;
        }
        private void RequestOnlineRematch()
        {
            if (_onlineSession == null || !_onlineSession.IsReady || _onlineStopped || _onlineState == null) return;
            if (_onlineState.phase != (int)VersusPhase.Finished) return;
            if (_onlineRematchReady[OnlineMe]) return;
            if (!_onlineResultsRevealed) return;
            if (_onlinePending) return;
            _onlinePending = true;
            _onlineSession.SendCommand(new DuelNetworkCommand { Round = _onlineState.round, Sequence = ++_onlineSequence, Action = DuelNetworkAction.Rematch, MatchGeneration = _onlineMatch });
            RefreshOnline();
        }
        /// <summary>
        /// Host constructs a brand-new VersusDuel on mutual rematch consent using the existing
        /// seed pattern, bumps the match generation, and publishes the initial snapshot to both
        /// peers. The session, sequence, and revision survive intact.
        /// </summary>
        private void StartOnlineRematch()
        {
            _onlineDuel = new VersusDuel(FreshSeed());
            _onlineDuel.Begin();
            _onlineMatch++;
            ResetOnlineMatchPresentation();
            PublishOnline();
        }
        private void SendOnline(DuelNetworkAction action)
        {
            if (_onlineSession == null || !_onlineSession.IsReady || _onlineStopped || _onlinePending || _onlineState == null) return;
            if ((action == DuelNetworkAction.Reroll || action == DuelNetworkAction.FinishRerolls) && !OnlineRerolling) return;
            if (action == DuelNetworkAction.Submit && !OnlinePlanning) return;
            if (action == DuelNetworkAction.Continue && (_onlineState.phase != (int)VersusPhase.Resolved || _onlineState.ready[OnlineMe] || _onlineImpactAge >= 0)) return;
            if (action == DuelNetworkAction.Reroll || action == DuelNetworkAction.FinishRerolls || action == DuelNetworkAction.Submit) SDRefreshOnlineFormation();
            _onlinePending = true;
            _onlineSession.SendCommand(new DuelNetworkCommand { Round = _onlineState.round, Sequence = ++_onlineSequence, Action = action, HeldMask = _onlineHeld, Kind = _onlineKind, Carry = _onlineCarrying, MatchGeneration = _onlineMatch });
            RefreshOnline();
        }
        private void ReceiveOnlineCommand(int player, DuelNetworkCommand command)
        {
            // Stale rematch consents from an already-advanced match are discarded before they
            // can influence the current duel or its result overlay.
            if (command.Action == DuelNetworkAction.Rematch)
            {
                if (command.MatchGeneration != _onlineMatch) return;
                if (_onlineState == null || _onlineState.phase != (int)VersusPhase.Finished) return;
                if (_onlineDuel == null || _onlineDuel.Phase != VersusPhase.Finished) return;
                if (command.Round != _onlineDuel.Round) return;
                if (player < 0 || player > 1 || _onlineRematchReady[player]) return;
                _onlineRematchReady[player] = true;
                if (_onlineRematchReady[0] && _onlineRematchReady[1] && _onlineSession != null && _onlineSession.IsHost)
                    StartOnlineRematch();
                else PublishOnline();
                return;
            }
            if (_onlineDuel == null) return;
            if (command.MatchGeneration != _onlineMatch) return;
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
            var state = new OnlineState { round = _onlineDuel.Round, phase = (int)_onlineDuel.Phase, winner = _onlineDuel.Winner, revision = _onlineRevision, players = new OnlinePlayer[2], damage = _onlineDuel.LastRound?.Damage, ready = (bool[])_onlineReady.Clone(), match = _onlineMatch, rematchReady = (bool[])_onlineRematchReady.Clone() };
            for (int i = 0; i < 2; i++) { var p = players[i]; state.players[i] = new OnlinePlayer { hp = p.Hp, held = p.HeldMask, dice = p.Dice, scores = p.CategoryScores, used = p.Used, rerolls = p.RerollsLeft, submitted = p.Submitted, rerollsFinished = p.RerollsFinished, nextBonus = p.NextRerollBonus, kind = (int)p.Kind, score = p.Score, carry = p.Carry, carryUsed = p.CarryUsed, carried = _onlineDuel.LastRound != null && _onlineDuel.LastRound.Round < _onlineDuel.Round && _onlineDuel.LastRound.Carry[i] }; }
            return JsonUtility.ToJson(state);
        }
        private void PublishOnline() { _onlineRevision++; _onlineSession.PublishSnapshot(OnlineJson(0), OnlineJson(1)); }
        private void ReceiveOnlineSnapshot(string json)
        {
            var next = JsonUtility.FromJson<OnlineState>(json);
            if (next == null || next.players == null || next.players.Length != 2) return;
            if (_onlineState != null && (next.revision <= _onlineState.revision || next.match < _onlineState.match)) return;
            // A newer match generation means the host restarted the duel. Consume it as a fresh
            // match: reset per-match presentation before the impact/intro detection below reads it.
            bool freshMatch = _onlineState == null || next.match != _onlineState.match;
            if (freshMatch && _onlineState != null) ResetOnlineMatchPresentation();
            // Single local gate: true exactly on a ChoosingRoles -> Resolved/Finished transition.
            // Capture pre-strike HP and arm the impact clock BEFORE RefreshOnline so the new
            // snapshot cannot print the final HP ahead of the shared attack timeline.
            bool beginImpact = _onlineImpactAge < 0 && _onlineState != null && _onlineState.phase == (int)VersusPhase.ChoosingRoles &&
                (next.phase == (int)VersusPhase.Resolved || next.phase == (int)VersusPhase.Finished);
            if (beginImpact)
            {
                _onlineBeforeOwn = _onlineState.players[OnlineMe].hp;
                _onlineBeforeEnemy = _onlineState.players[1 - OnlineMe].hp;
                SDRefreshOnlineFormation();
                for (int i = 0; i < 5; i++)
                {
                    _onlineOwnDice[i].anchoredPosition = OnlineDieHome(i, true);
                    _onlineEnemyDice[i].anchoredPosition = OnlineDieHome(i, false);
                    _onlineOwnDice[i].localScale = _onlineEnemyDice[i].localScale = Vector3.one;
                    _onlineOwnDice[i].localRotation = _onlineEnemyDice[i].localRotation = Quaternion.identity;
                }
                _onlineImpactAge = 0;
                _onlineContactPlayed = false;
            }
            if (_onlineState == null || next.round != _onlineState.round) { _onlineHeld = next.players[OnlineMe].held; _onlineCarrying = false; }
            if (_onlineState != null && next.phase != _onlineState.phase) { CancelOnlinePointer(); SDRefreshOnlineFormation(); }
            bool newRound = _onlineState == null || freshMatch || next.round != _onlineState.round;
            if (newRound) SDResetOnlineFormation(true);
            bool rerolled = !newRound && next.players[OnlineMe].rerolls < _onlineState.players[OnlineMe].rerolls;
            if ((newRound && !next.players[OnlineMe].carried) || rerolled)
            {
                for (int i = 0; i < 5; i++)
                    if (newRound || (_onlineHeld & (1 << i)) == 0) _onlineMine[i].BeginRoll(i * RollStagger, RollScramble);
                GameAudio.Play("dice_roll");
            }
            if (_onlineState != null && !_onlineState.players[OnlineMe].submitted && next.players[OnlineMe].submitted) GameAudio.Play("commit");
            _onlineMatch = next.match;
            for (int i = 0; i < 2; i++) _onlineRematchReady[i] = next.rematchReady != null && next.rematchReady.Length == 2 && next.rematchReady[i];
            _onlineState = next; _onlinePending = false;
            if (newRound) SelectOnlineSuggestedRole(true);
            if (!next.players[OnlineMe].submitted && next.players[OnlineMe].used[_onlineKind]) ChangeOnlineRole(1);
            RefreshOnline();
            if (beginImpact) { SDRefreshOnlineFormation(); BeginOnlineAttack(next); }
            if (_onlineCommitQueued && next.phase == (int)VersusPhase.ChoosingRoles && !next.players[OnlineMe].submitted)
            { _onlineCommitQueued = false; SendOnline(DuelNetworkAction.Submit); }
        }
        private void ChangeOnlineRole(int direction)
        {
            if (_onlineState == null) return;
            _onlineRoleManual = true;
            for (int i = 0; i < ScoreRules.HandCount; i++) { _onlineKind = (_onlineKind + direction + ScoreRules.HandCount) % ScoreRules.HandCount; if (!_onlineState.players[OnlineMe].used[_onlineKind]) break; }
            SDRefreshOnlineFormation();
        }
        private void RefreshOnline()
        {
            if (_onlineRoot == null) return;
            bool lobby = _onlineState == null;
            bool finished = !lobby && _onlineState.phase == (int)VersusPhase.Finished;
            bool resultsReady = finished && _onlineResultsRevealed && _onlineImpactAge < 0 && !OnlineHelpOpen;
            _onlineResult.gameObject.SetActive(!_onlineLeaving && (resultsReady || _onlineStopped));
            _onlineResultTitle.text = _onlineStopped ? "対戦を中断しました" : !finished ? "" : _onlineState.winner == 2 ? "奇跡" : _onlineState.winner == OnlineMe ? "あなたの勝利" : "あなたの敗北";
            _onlineResultDetail.text = _onlineStopped ? "ロビーへ戻ってもう一度接続できます。" : !finished ? "" : _onlineRematchReady[OnlineMe] ? "相手の再戦を待っています" : "ROUND " + _onlineState.round.ToString("00");
            _onlineRematch.gameObject.SetActive(resultsReady && !_onlineStopped);
            _onlineReturnLobby.gameObject.SetActive(!_onlineLeaving && !_onlineConnecting && (_onlineStopped || resultsReady));
            _onlineRematch.interactable = !_onlinePending && !_onlineRematchReady[OnlineMe];
            _onlineLobbyTitle.gameObject.SetActive(lobby);
            _onlineCreate.gameObject.SetActive(lobby); _onlineJoin.gameObject.SetActive(lobby); _onlineCode.gameObject.SetActive(lobby);
            _onlineCreate.interactable = _onlineJoin.interactable = !_onlineConnecting && !_onlineLeaving && _onlineSession == null;
            _onlineCode.interactable = !_onlineConnecting && !_onlineLeaving && _onlineSession == null;
            bool copyAvailable = OnlineInviteCopyAvailable;
            _onlineCopyCode.gameObject.SetActive(copyAvailable);
            _onlineCopyCode.interactable = copyAvailable;
            _onlineCopyNotice.text = _onlineCopyStatus;
            _onlineCopyNotice.gameObject.SetActive(copyAvailable && !string.IsNullOrEmpty(_onlineCopyStatus));
            _onlineRoll.gameObject.SetActive(false);
            _onlineSubmit.gameObject.SetActive(false);
            _onlineCarry.gameObject.SetActive(!lobby && !finished && !_onlineState.players[OnlineMe].submitted);
            _onlineEnemyHpFill.transform.parent.gameObject.SetActive(!lobby);
            _onlineOwnHpFill.transform.parent.gameObject.SetActive(!lobby);
            _onlineEnemy.gameObject.SetActive(!lobby);
            _onlineRole.gameObject.SetActive(!lobby);
            _onlineScore.gameObject.SetActive(!lobby);
            _onlineRerollDots.gameObject.SetActive(!lobby);
            _onlineHit.gameObject.SetActive(!lobby);
            _onlineFormula.gameObject.SetActive(!lobby); _onlineEnemyFormula.gameObject.SetActive(!lobby); _onlineEnemyScore.gameObject.SetActive(!lobby);
            for (int i = 0; i < 3; i++) _onlineRollPips[i].gameObject.SetActive(!lobby && !finished && i < _onlineRerollCapacity);
            if (_onlineDrawMiracle != null)
                _onlineDrawMiracle.gameObject.SetActive(!lobby && _onlineState.phase == (int)VersusPhase.Finished && _onlineState.winner == 2);
            for (int i = 0; i < 5; i++)
            {
                _onlineOwnDice[i].gameObject.SetActive(!lobby);
                _onlineEnemyDice[i].gameObject.SetActive(!lobby);
                _onlineMine[i].SetInteractable(OnlineCanChoose);
                _onlineTheirs[i].SetInteractable(false);
                _onlineHoldMarks[i].gameObject.SetActive(!lobby && OnlineRerolling && (_onlineHeld & (1 << i)) != 0);
            }
            foreach (var role in _onlineRoles) role.gameObject.SetActive(!lobby);
            foreach (var score in _onlineRoleValues) score.gameObject.SetActive(!lobby);
            foreach (var role in _onlineEnemyRoles) role.transform.parent.gameObject.SetActive(!lobby);
            _onlineOwnList.text = lobby ? "" : "あなたの役"; _onlineEnemyList.text = lobby ? "" : "相手の役";
            if (lobby) return;
            var mine = _onlineState.players[OnlineMe]; var enemy = _onlineState.players[1 - OnlineMe];
            for (int i = 0; i < 5; i++) { _onlineMine[i].SetValue(mine.dice[i]); _onlineMine[i].SetTint(DiceTint.Plain, (_onlineHeld & (1 << i)) != 0); _onlineTheirs[i].SetValue(enemy.dice[i]); }
            if (_onlineImpactAge < 0)
            {
                SDSetTableHp(_onlineOwnHpFill, mine.hp, ScoreDuel.StartingHp);
                SDSetTableHp(_onlineEnemyHpFill, enemy.hp, ScoreDuel.StartingHp);
            }
            for (int k = 0; k < ScoreRules.HandCount; k++)
            {
                _onlineRoles[k].transform.Find("Name").GetComponent<Text>().text = ScoreRules.Name((ScoreHandKind)k);
                _onlineRoles[k].interactable = OnlineCanChoose && !mine.used[k];
                bool selected = k == _onlineKind && !mine.used[k];
                StyleOnlineRoleButton(_onlineRoles[k], selected);
                _onlineRoleValues[k].text = mine.used[k] ? (mine.scores != null ? mine.scores[k].ToString() : "—") : ScoreRules.Evaluate((ScoreHandKind)k, mine.dice).Score.ToString();
                _onlineRoleValues[k].color = selected ? DiceYahzeeVisuals.Ink : mine.used[k] ? DiceYahzeeVisuals.MutedText : DiceYahzeeVisuals.Parchment;
                _onlineEnemyRoles[k].text = (enemy.used[k] ? "✓ " : "") + ScoreRules.Name((ScoreHandKind)k);
                _onlineEnemyRoles[k].color = enemy.used[k] ? SDRailUsed : SDRailInk;
                _onlineEnemyValues[k].text = enemy.used[k] && enemy.scores != null ? enemy.scores[k].ToString() : "—";
                _onlineRoleMarkers[k].gameObject.SetActive(selected);
                _onlineEnemyMarkers[k].gameObject.SetActive(false);
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
            if (_onlineImpactAge < 0) { _onlineOwnLoss.gameObject.SetActive(false); _onlineEnemyLoss.gameObject.SetActive(false); }
            _onlineEnemy.text = resolved ? ScoreRules.Name((ScoreHandKind)enemy.kind) + (enemy.carry ? "　持越" : "") : enemy.submitted ? "相手が確定" : "相手が選択中";
            _onlineEnemyFormula.text = resolved ? SDCalculation((ScoreHandKind)enemy.kind, enemy.dice) : "";
            _onlineEnemyScore.text = resolved ? enemy.score.ToString() : "—";
            SDObserveScorePulse(_onlineEnemyScore, ref _onlineEnemyScorePulse, resolved ? enemy.score : -1);
            _onlineRole.text = ScoreRules.Name((ScoreHandKind)(mine.submitted ? mine.kind : _onlineKind));
            // One evaluate serves both the active role caption and the score numeral: the
            // per-role list above evaluates each of the nine kinds, and this is the selected one.
            int plannedScore = ScoreRules.Evaluate((ScoreHandKind)_onlineKind, mine.dice).Score;
            _onlineScore.text = (mine.submitted ? mine.score : plannedScore).ToString();
            _onlineScore.fontSize = mine.submitted ? SDScoreFontFor(mine.score) : 64;
            SDObserveScorePulse(_onlineScore, ref _onlineOwnScorePulse, mine.submitted ? mine.score : plannedScore);
            _onlineFormula.text = SDCalculation((ScoreHandKind)(mine.submitted ? mine.kind : _onlineKind), mine.dice);
            RefreshOnlineRerollPips(mine.rerolls, OnlineRerolling, mine.nextBonus);
            _onlineRerollDots.gameObject.SetActive(false);
            for (int i = 0; i < 3; i++)
            {
                _onlineRollPips[i].gameObject.SetActive(!finished && i < _onlineRerollCapacity);
                _onlineRollPips[i].color = i < mine.rerolls ? SDPositive : new Color(.62f, .66f, .67f, .5f);
            }
            _onlineRoll.interactable = OnlineRerolling && mine.rerolls > 0 && _onlineHeld != 31;
            _onlineSubmit.interactable = OnlineRerolling; _onlineCarry.interactable = OnlineCanChoose && !mine.carryUsed;
            _onlineCarry.GetComponentInChildren<Text>().text = "";
            _onlineCarry.transform.Find("SaveCount").GetComponent<Text>().text = "";
            StyleOnlineButton(_onlineCarry, !mine.carryUsed);
            _onlineCarry.transform.Find("SaveSymbol").GetComponent<Image>().color =
                _onlineCarry.interactable ? SDPaperInk : SDMuted;
            if (_onlineStopped) return;
            if (_onlineState.phase == (int)VersusPhase.Finished)
            {
                string verdict = _onlineState.winner == 2 ? "引き分け" : _onlineState.winner == OnlineMe ? "あなたの勝利" : "あなたの敗北";
                _onlineStatus.text = _onlineRematchReady[OnlineMe] ? verdict + "　相手の再戦を待っています" : verdict;
            }
            else if (resolved) _onlineStatus.text = _onlineState.ready[OnlineMe] ? "相手を待っています" : "ROUND " + _onlineState.round.ToString("00");
            else _onlineStatus.text = "ROUND " + _onlineState.round.ToString("00") + (mine.submitted || mine.rerollsFinished && _onlineState.phase == (int)VersusPhase.Playing ? "　相手を待っています" : _onlineState.phase == (int)VersusPhase.Playing ? "　振り直し" : "　役を選択");
        }
        /// <summary>
        /// Clears the captured invite code and its copied feedback. Called on a fresh lobby,
        /// a new connection attempt, a failed creation, and any leave so a stale room can never
        /// be copied after it is gone.
        /// </summary>
        private void ClearOnlineInviteCopy()
        {
            _onlineJoinCode = string.Empty;
            _onlineCopyStatus = string.Empty;
        }
        /// <summary>
        /// Writes the host room's own invite code to the system clipboard. It never copies the
        /// lobby status prose or the typed join field, and reports the copy without replacing the
        /// invite code / waiting line.
        /// </summary>
        private void CopyOnlineInviteCode()
        {
            if (!OnlineInviteCopyAvailable) return;
            GUIUtility.systemCopyBuffer = _onlineJoinCode;
            _onlineCopyStatus = "招待コードをコピーしました";
            RefreshOnline();
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
            bool rematchSmoke = Array.IndexOf(args, "-diceNetRematch") >= 0;
            int smokeMatch = 0;
            int loggedRound = -1, loggedPhase = -1;
            float deadline = Time.realtimeSinceStartup + (rematchSmoke ? 540 : 300);
            while (Time.realtimeSinceStartup < deadline && OnlineActive && !_onlineStopped)
            {
                yield return new WaitForSecondsRealtime(.7f);
                if (_onlineState == null || _onlinePending || _onlineImpactAge >= 0) continue;
                if (rematchSmoke && _onlineState.match != smokeMatch)
                {
                    smokeMatch = _onlineState.match;
                    if (_onlineState.phase != (int)VersusPhase.Playing || _onlineState.round != 1 || _onlineState.players[0].hp != 300 || _onlineState.players[1].hp != 300)
                    { TraceOnlineSmoke("DICE_NET_SMOKE rematch did not reset match state", true); Application.Quit(2); yield break; }
                    TraceOnlineSmoke("DICE_NET_SMOKE REMATCH_STARTED generation=" + smokeMatch + " hp=300,300");
                }
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
                {
                    if (rematchSmoke && !_onlineResultsRevealed) continue;
                    if (rematchSmoke && smokeMatch == 0)
                    {
                        if (!_onlineRematchReady[OnlineMe])
                        {
                            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null) SmokeCapture((_onlineSmokeHost ? "host" : "guest") + "-online-result");
                            TraceOnlineSmoke("DICE_NET_SMOKE REMATCH_REQUEST");
                            _onlineRematch.onClick.Invoke();
                        }
                        continue;
                    }
                    TraceOnlineSmoke("DICE_NET_SMOKE COMPLETE winner=" + _onlineState.winner);
                    if (rematchSmoke)
                    {
                        _onlineReturnLobby.onClick.Invoke();
                        float leaveDeadline = Time.realtimeSinceStartup + 20;
                        while (_onlineLeaving && Time.realtimeSinceStartup < leaveDeadline) yield return null;
                        if (_onlineSession != null || _onlineState != null || !_onlineCreate.IsInteractable() || !OnlineActive)
                        { TraceOnlineSmoke("DICE_NET_SMOKE return lobby failed", true); Application.Quit(2); yield break; }
                        if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null) SmokeCapture((_onlineSmokeHost ? "host" : "guest") + "-online-returned-lobby");
                        TraceOnlineSmoke("DICE_NET_SMOKE LOBBY_READY");
                    }
                    Application.Quit(0); yield break;
                }
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
        private void OpenOnlineHelp()
        {
            CancelOnlinePointer();
            if (_onlineAttackPresenter != null) _onlineAttackPresenter.Reset();
            SDRefreshOnlineFormation();
            _onlineImpactWinner = -1;
            if (_onlineHit != null) _onlineHit.text = "";
            _onlineHelp.gameObject.SetActive(true);
        }
        private void CloseOnlineHelp()
        {
            SDRefreshOnlineFormation();
            _onlineHelp.gameObject.SetActive(false);
            RefreshOnline();
            UpdateOnlineGuide();
        }
        private void TickOnlineImpactUnderModal()
        {
            if (_onlineImpactAge < 0) return;
            _onlineImpactAge += Time.unscaledDeltaTime;
            if (_onlineImpactAge < SharedAttackPresenter.Duration) return;
            _onlineImpactAge = -1;
            _onlineImpactWinner = -1;
            _onlineContactPlayed = false;
            if (_onlineAttackPresenter != null) _onlineAttackPresenter.Reset();
            RefreshOnline();
        }
        private void UpdateOnline()
        {
            if (OnlineHelpOpen)
            {
                CancelOnlinePointer();
                if (_onlineAttackPresenter != null) _onlineAttackPresenter.Reset();
                _onlineImpactWinner = -1;
                TickOnlineImpactUnderModal();
                return;
            }
            bool rolling = OnlineRolling;
            bool attackActive = _onlineAttackPresenter != null && _onlineAttackPresenter.Active;
            if (!attackActive)
                foreach (var die in _onlineMine) die?.Tick(Time.unscaledDeltaTime);
            if (rolling && !OnlineRolling) { SelectOnlineSuggestedRole(false); RefreshOnline(); }
            UpdateOnlineImpact(); TickOnlineResult(); UpdateOnlineCarryEffect(); UpdateOnlineGuide();
            SDTickOnlineScorePulses(Time.unscaledDeltaTime);
            SDTickOnlineChanceGlow(Time.unscaledDeltaTime);
            if (_onlineState == null || _onlineStopped) return;
            if (_onlineState.phase == (int)VersusPhase.Resolved)
            { if (Input.GetKeyDown(KeyCode.Return) && OnlineCanAdvance) SendOnline(DuelNetworkAction.Continue); return; }
            if (_onlineState.phase == (int)VersusPhase.Finished) return;
            UpdateOnlineWaitingDice();
            if (OnlineRerolling && !OnlineRolling && !_onlineDragging && (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Space))) SendOnline(DuelNetworkAction.Reroll);
            OnlineWheel(Input.mouseScrollDelta.y);
            if (OnlineCanChoose && !_onlineDragging)
            {
                for (int i = 0; i < 5; i++) if (OnlineRerolling && Input.GetKeyDown(KeyCode.Alpha1 + i)) { _onlineHeld ^= 1 << i; GameAudio.Play("die_keep"); RefreshOnline(); }
                if (Input.GetKeyDown(KeyCode.Return)) CommitOnlineHand();
            }
            SDTickOnlineFormation(Time.unscaledDeltaTime);
        }

        private void UpdateOnlineWaitingDice()
        {
            if (_onlineImpactAge >= 0 || _onlineState == null || OnlineRolling) return;
            if (_onlineAttackPresenter != null && _onlineAttackPresenter.Active) return;
            bool roles = _onlineState.phase == (int)VersusPhase.ChoosingRoles || _onlineState.phase == (int)VersusPhase.Playing;
            bool mineWaiting = roles && (_onlineState.players[OnlineMe].submitted || _onlineCommitQueued || _onlineAiming);
            bool theirsWaiting = roles && _onlineState.players[1 - OnlineMe].submitted;
            _onlineChargeAge = mineWaiting || theirsWaiting ? _onlineChargeAge + Time.unscaledDeltaTime : 0;
            float energy = _sdReducedMotion ? 0 : Mathf.SmoothStep(0, 1, Mathf.Clamp01(_onlineChargeAge / .65f));
            for (int i = 0; i < 5; i++)
            {
                float own = mineWaiting ? energy : 0, enemy = theirsWaiting ? energy : 0;
                _onlineMine[i].SetPosition(OnlineDieHome(i, true) + Vector2.down * _onlineDragPull + SDChargeOffset(_onlineChargeAge, i, own));
                _onlineTheirs[i].SetPosition(OnlineDieHome(i, false) + SDChargeOffset(_onlineChargeAge, i + 5, enemy));
                _onlineOwnDice[i].localScale = new Vector3(1 + own * .012f, 1 - own * .025f, 1);
                _onlineEnemyDice[i].localScale = new Vector3(1 + enemy * .012f, 1 - enemy * .025f, 1);
                _onlineOwnDice[i].localEulerAngles = new Vector3(0, 0, DuelPresentation.SDSign(_onlineChargeAge, i) * .7f * own);
                _onlineEnemyDice[i].localEulerAngles = new Vector3(0, 0, DuelPresentation.SDSign(_onlineChargeAge, i + 5) * .7f * enemy);
            }
        }

        /// <summary>
        /// Fires once when the ChoosingRoles竊坦esolved transition first lands, so the shared
        /// presenter captures the resting die transforms before any impact refresh overwrites
        /// them. Repeated snapshots while the impact is running leave this alone.
        /// </summary>
        private void BeginOnlineAttack(OnlineState state)
        {
            if (_onlineAttackPresenter == null) _onlineAttackPresenter = new SharedAttackPresenter(_onlineRoot);
            else _onlineAttackPresenter.Reset();
            _onlineImpactWinner = -1;
            if (state == null || state.players == null) return;
            var mine = state.players[OnlineMe];
            var enemy = state.players[1 - OnlineMe];
            int ownRaw = mine.score;
            int enemyRaw = enemy.score;
            int ownAttack = ownRaw;
            int enemyAttack = enemyRaw;
            bool ownChance = mine.kind == (int)ScoreHandKind.Chance;
            bool enemyChance = enemy.kind == (int)ScoreHandKind.Chance;
            if (ownChance != enemyChance) { int swap = ownAttack; ownAttack = enemyAttack; enemyAttack = swap; }
            // Strength comes from the same raw own/enemy score 50/100/130 bins the single-score
            // adapter uses, NOT cumulative HP loss, so identical rounds punch identically.
            int score = Mathf.Max(ownRaw, enemyRaw);
            int tier = score < 50 ? 0 : score < 100 ? 1 : score < 130 ? 2 : 3;
            Vector2 ownTarget = OnlineHpTarget(_onlineOwnHpFill);
            Vector2 enemyTarget = OnlineHpTarget(_onlineEnemyHpFill);
            _onlineAttackPresenter.Begin(_onlineOwnDice, _onlineEnemyDice, ownAttack, enemyAttack, ownTarget, enemyTarget, _sdReducedMotion, tier);
            _onlineImpactWinner = _onlineAttackPresenter.Winner;
        }

        private Vector2 OnlineHpTarget(Image fill)
        {
            if (fill == null || _onlineRoot == null) return Vector2.zero;
            // The full track center is stable; the colored fill's midpoint moves with HP anchors.
            RectTransform track = fill.transform.parent as RectTransform;
            RectTransform source = track != null ? track : fill.rectTransform;
            Vector3 world = source.TransformPoint(source.rect.center);
            return _onlineRoot.InverseTransformPoint(world);
        }

        private void UpdateOnlineImpact()
        {
            if (_onlineImpactAge < 0 || _onlineState == null) return;
            _onlineImpactAge += Time.unscaledDeltaTime;
            if (_onlineAttackPresenter == null) _onlineAttackPresenter = new SharedAttackPresenter(_onlineRoot);
            _onlineAttackPresenter.Sample(_onlineImpactAge);
            int ownDamage = _onlineState.damage != null ? _onlineState.damage[OnlineMe] : 0;
            int enemyDamage = _onlineState.damage != null ? _onlineState.damage[1 - OnlineMe] : 0;
            // Audio fires once at the exact .64s contact of the shared timeline. HP drain also
            // begins there so nothing changes before the strike visibly lands.
            float drainT = Mathf.Clamp01((_onlineImpactAge - SharedAttackPresenter.ContactTime) /
                Mathf.Max(0.01f, SharedAttackPresenter.Duration - SharedAttackPresenter.ContactTime));
            if (!_onlineContactPlayed && _onlineImpactAge >= SharedAttackPresenter.ContactTime)
            {
                _onlineContactPlayed = true;
                GameAudio.Play(SDImpactSound(enemyDamage, ownDamage));
            }
            if (_onlineImpactAge >= SharedAttackPresenter.ContactTime)
            {
                float eased = 1f - (1f - drainT) * (1f - drainT);
                float ownHp = Mathf.Lerp(_onlineBeforeOwn, _onlineState.players[OnlineMe].hp, eased);
                float enemyHp = Mathf.Lerp(_onlineBeforeEnemy, _onlineState.players[1 - OnlineMe].hp, eased);
                SDSetTableHp(_onlineOwnHpFill, ownHp, ScoreDuel.StartingHp);
                SDSetTableHp(_onlineEnemyHpFill, enemyHp, ScoreDuel.StartingHp);
                SDAnimateHpLoss(_onlineOwnHpFill, _onlineOwnLoss, _onlineBeforeOwn, _onlineState.players[OnlineMe].hp, ScoreDuel.StartingHp, SDPlayerColor, 1f - drainT);
                SDAnimateHpLoss(_onlineEnemyHpFill, _onlineEnemyLoss, _onlineBeforeEnemy, _onlineState.players[1 - OnlineMe].hp, ScoreDuel.StartingHp, SDEnemyColor, 1f - drainT);
                _onlineOwnDamageShown = Mathf.Max(0, ownDamage);
                _onlineEnemyDamageShown = Mathf.Max(0, enemyDamage);
                SDSetOnlineHpLines(Mathf.RoundToInt(ownHp), Mathf.RoundToInt(enemyHp));
            }
            if (_onlineSmokeMode && !_onlineSmokeImpactCaptured && _onlineState.round == 1 &&
                _onlineImpactAge > .38f && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                _onlineSmokeImpactCaptured = true;
                SmokeCapture((_onlineSmokeHost ? "host" : "guest") + "-online-impact");
            }
            _onlineHit.text = "";
            if (_onlineImpactAge >= SharedAttackPresenter.Duration)
            {
                _onlineImpactAge = -1; _onlineHit.text = "";
                // Retain the reusable presenter tree; only Reset so the next attack can reuse it.
                if (_onlineAttackPresenter != null) { _onlineAttackPresenter.Reset(); }
                _onlineImpactWinner = -1;
                for (int i = 0; i < 5; i++)
                {
                    _onlineOwnDice[i].localScale = _onlineEnemyDice[i].localScale = Vector3.one;
                    _onlineOwnDice[i].localRotation = _onlineEnemyDice[i].localRotation = Quaternion.identity;
                }
                RefreshOnline();
            }
        }
        /// <summary>
        /// Lets the result settle before either button appears: the caller wants a readable
        /// verdict first, then a decision. Runs on the finished phase only and never on the
        /// initial frames of a fresh rematch.
        /// </summary>
        private void TickOnlineResult()
        {
            if (_onlineState == null || _onlineStopped) return;
            if (_onlineState.phase != (int)VersusPhase.Finished) { _onlineResultAge = -1; _onlineResultsRevealed = false; return; }
            if (_onlineResultAge < 0) _onlineResultAge = 0;
            _onlineResultAge += Time.unscaledDeltaTime;
            if (!_onlineResultsRevealed && _onlineResultAge >= (_sdReducedMotion ? .3f : 1.4f))
            {
                _onlineResultsRevealed = true;
                if (!_onlineOutcomePlayed) { _onlineOutcomePlayed = true; GameAudio.Play(_onlineState.winner == OnlineMe ? "win" : "lose"); }
                RefreshOnline();
            }
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
            bool active = OnlineCanChoose;
            bool continueCue = _onlineState != null && _onlineState.phase == (int)VersusPhase.Resolved &&
                !_onlineState.ready[OnlineMe] && !_onlineStopped && !OnlineHelpOpen;
            _onlineGuideMouse.gameObject.SetActive(active);
            _onlineWheelMouse.gameObject.SetActive(OnlineCanChoose);
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
            float pull = SDGuidePull(phase);
            _onlineGuideMouse.sprite = DiceYahzeeVisuals.LoadSprite(reroll ? "Art/DiceYahzee/input-mouse-right" : "Art/DiceYahzee/input-mouse-left");
            _onlineGuideMouse.rectTransform.anchoredPosition = new Vector2(SDGuideX, -30 - (_sdReducedMotion || reroll ? 0 : 18 * pull));
            Color ink = DiceYahzeeVisuals.Brass; ink.a = .85f;
            _onlineGuideMouse.color = ink;
            if (reroll)
            {
                _onlineGuideGraphic.Ring(_onlineGuideMouse.rectTransform.anchoredPosition, 23 + (_sdReducedMotion ? 0 : phase * 8), 1.6f, ink, 24);
                Vector2 button = _onlineGuideMouse.rectTransform.anchoredPosition + new Vector2(5, 7);
                _onlineGuideGraphic.Line(button + Vector2.down * 4, button + Vector2.up * 4, 3.5f, ink);
            }
            else
            {
                Vector2 head = new Vector2(SDGuideX, -82 - (_sdReducedMotion ? 16 : 42 * pull));
                for (int i = 0; i < 3; i++)
                {
                    Vector2 tip = head + Vector2.up * (i * 15);
                    Color cue = ink; cue.a *= 1 - i * .22f;
                    _onlineGuideGraphic.Line(tip + new Vector2(-9, 4), tip + new Vector2(0, -5), 2.8f, cue);
                    _onlineGuideGraphic.Line(tip + new Vector2(0, -5), tip + new Vector2(9, 4), 2.8f, cue);
                }
            }
            float wheelY = _sdReducedMotion ? 0 : Mathf.Sin(Time.unscaledTime * 3.8f) * 4;
            _onlineWheelMouse.rectTransform.anchoredPosition = new Vector2(SDRolePanelX + 69, 184 + wheelY);
            _onlineGuideGraphic.Flush();
        }
    }
}
