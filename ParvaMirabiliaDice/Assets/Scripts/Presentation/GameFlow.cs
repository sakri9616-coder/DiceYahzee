using System;
using System.Collections.Generic;
using System.Text;
using ParvaMirabilia.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ParvaMirabilia.Presentation
{
    /// <summary>
    /// Hosts the production ScoreDuel presentation and the explicitly selected historical
    /// MatchEngine screens used by regression tests. Both rule engines remain Unity-free.
    /// </summary>
    public sealed partial class GameFlow : MonoBehaviour
    {
        private enum Screen
        {
            Title = 0,
            Battle = 1,
            Result = 2
        }

        private const float CanvasWidth = 1280f;
        private const float CanvasHeight = 720f;
        private const float DieSize = 84f;
        private const float DieGap = 18f;
        private const int DiceCount = 5;

        private static readonly NpcArchetype[] Opponents =
        {
            NpcArchetype.Aggressive,
            NpcArchetype.Guardian,
            NpcArchetype.Balanced
        };

        /// <summary>
        /// Test seam for <c>InteractionCostTests</c>: stands in for the shipped rule set so a run
        /// can measure the original game next to the new one. Null means "ship it".
        /// </summary>
        public static Func<GameConfig> ConfigOverride;
        // Historical UI suites explicitly opt into their original rules. Production uses ScoreDuel.
        public static bool LegacyPresentationForTests;
        // Deterministic input for tests and explicitly requested player diagnostics only.
        public static Func<int> SeedOverride;
        private static int FreshSeed() => SeedOverride != null ? SeedOverride() : Guid.NewGuid().GetHashCode();

        /// <summary>
        /// The rules as the build plays them. The three printed stakes are interpolated from the
        /// config rather than typed in: a rules screen that disagrees with the numbers the game
        /// actually uses is worse than no rules screen at all, and those three are exactly what
        /// a balance sweep is most likely to move.
        /// </summary>
        private static string BuildRulesText(GameConfig config)
        {
            return
            (config.TacticalEnabled ? "【予告・魔力・役の回復】\n" +
            "相手の役・攻撃・守りは最初から予告されています。予告は振り直しても変わりません。\n" +
            "未使用の振り直し1回につき、確定時に魔力1を蓄えます（最大" + config.MaxMana + "）。\n" +
            "魔力1で攻撃＋" + config.EmpowerAttack + "。確定前に強化をオン・オフして結果を比較できます。守護は強化できません。\n" +
            "使った役は次の1ラウンドだけ回復中。チャンスは連続使用できます。\n" +
            "ペアは守り＋" + config.TacticalPairWard + "、スリーカインドは攻撃＋" + config.TacticalTripleAttack + "。双方の同じ役に適用されます。\n" +
            "双方0ダメージの押し勝ちは、相手の出目が公開されてから決まります。\n\n" : "") +
            "【目的】\n" + (config.TacticalEnabled ? "単発決闘の基本値：\n" : "") +
            "相手のHP（" + config.StartingHp + "）を先に0にするか、" + config.MaxRounds + "ラウンド終了時に勝ってください。\n\n" +
            "【1ラウンドの流れ】\n" +
            "1. 5つのダイスが振られます。\n" +
            "2. 振り直したいダイスをクリックします（1クリックで指定、もう一度で解除）。\n" +
            "3. 「振り直す」で指定されたダイスだけやり直します（最大" + config.MaxRerolls + "回）。\n" +
            "4. 成立している役を1つ選び、「確定する」で同時に公開されます。\n" +
            "　役を選び直さずにそのまま確定しても構いません。\n\n" +
            "【役】\n" +
            "役が「何個を前へ出すか」を決めます。前へ出さなかったダイスの合計が守りになります。\n" +
            (config.TacticalEnabled ? "以下は祝福のない基本値です。三連決闘の実際の攻守は役カードと予測に表示します。\n" : "") +
            "　守護（常時）＝0個前へ／攻0・守は全部\n" +
            "　ペア（同じ目が2個）＝2個前へ／攻は合算・守は残り3個" +
            (config.TacticalEnabled ? "＋" + config.TacticalPairWard : "") + "\n" +
            "　スリーカインド（同じ目が3個）＝3個前へ／攻は合算" +
            (config.TacticalEnabled ? "＋" + config.TacticalTripleAttack : "") + "・守は残り2個\n" +
            "　チャンス（常時）＝3個前へ／攻は上位3個の合算・守は残り2個\n" +
            "　小ストレート（4つの連続）＝4個前へ／攻は固定" + config.YakuSmallRunAttack + "・守は残り1個\n" +
            "　大ストレート（5つの連続）＝5個前へ／攻は固定" + config.YakuLargeRunAttack + "・守は0\n" +
            "　ヤッツィー（同じ目が5個）＝5個前へ／攻は固定" + config.YakuFiveAttack + "・守は0\n" +
            "前へ出す個数が多いほど攻撃は厚くなり、守りは薄くなります。\n" +
            "高い役ほど前へ出す個数が増えるので、攻めに振り切るか守りを残すかは役選びです。\n\n" +
            "【ダメージの計算】\n" +
            "ダメージ ＝ max(0, 自分の攻 − 相手の守)\n" +
            "例：あなたが「ペア」で攻12、相手が「スリーカインド」で守9なら 12−9＝3。\n" +
            "双方0ダメージのときだけ、合計の高い側が押し勝ちで" + config.PushbackDamage + "ダメージを与えます。\n\n" +
            "【勝敗の決め方】\n" +
            "HP0で敗北。同時に0なら与ダメージ合計→最終ラウンドのダメージ、同点なら引き分け。\n" +
            config.MaxRounds + "ラウンドの時間切れは残HP→与ダメージ合計の順に判定します。\n\n" +
            "【コツ】\n" +
            "小ストレートは出目にかかわらず" + config.YakuSmallRunAttack + "、ヤッツィーは" + config.YakuFiveAttack +
            "と固定です。どの目で作っても同じ価値。\n" +
            "スリーカインドやチャンスは出目の合計なので、高い目を引いたほうが得になります。\n" +
            "全部前へ出す役は守りが0になり、相手の攻撃をそのまま受けます。\n" +
            (config.TacticalEnabled ? "予告を見て、被弾を抑えるか強化で押すかを比べましょう。\n\n" +
            "【三連決闘】\n祝福を3枚から1枚選び、HP28・34・40の3人を順番に倒します。\n" +
            "残りHPを持ち越し、1・2戦目の勝利後にHP16回復。最大HPで止まります。\n" +
            "魔力と役の回復は各決闘の開始時にリセット。敗北・引き分けで挑戦終了。\n" +
            "祝福は挑戦を通して続きます。「祝福の効果を見る」で確認できます。" :
            "相手がどんな役を選んだかは公開して初めて分かります。");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (FindFirstObjectByType<GameFlow>() != null)
                return;

            var host = new GameObject("ParvaMirabiliaFlow");
            host.AddComponent<GameFlow>();
        }

        // ---- state -----------------------------------------------------------
        private MatchEngine _engine;
        private NpcArchetype _opponent = NpcArchetype.Balanced;
        // The title screen's pick. _opponent is overwritten by the trial run's fixed opponents.
        private NpcArchetype _titleOpponent = NpcArchetype.Balanced;
        private int _matchIndex;
        private bool _committed;

        /// <summary>
        /// One bit per die: marked means "throw this one again". That is the only thing a die is
        /// ever asked of the player, because the declared yaku decides where every die goes.
        ///
        /// The previous build put three unrelated questions - keep or re-roll, and if kept, strike
        /// or ward - onto one three-state cycle (guard &rarr; strike &rarr; re-roll). Four of its
        /// six gestures cost two clicks and travelled through an intention the player had never
        /// had, and the cycle had to be paid again after every re-roll. Measured before this
        /// change: 41 inputs a match, 25.4 of them on the dice, 10.95 a round
        /// (logs/oplog-summary.json).
        /// </summary>
        private readonly bool[] _rerollMarked = new bool[DiceCount];

        /// <summary>
        /// The yaku currently selected, always one of the offers on the table. Written only by
        /// <see cref="ResetRoundState"/> and <see cref="OnYakuClicked"/>, so the commit can never
        /// name a hand this roll was not entitled to.
        /// </summary>
        private YakuOffer _yakuOffer = new YakuOffer(YakuKind.None, 0, 0, -1);

        private string _banner = "";
        private string _hint = "";

        // ---- throw and impact ----------------------------------------------
        // Timings live here rather than in the rules: they are the length of a feeling, not a
        // quantity the rules have any opinion about. Every one of them is a plain constant so a
        // balance pass can retune the whole screen from one place.
        private const float RollScramble = 0.34f;
        private const float RollStagger = 0.055f;
        private const float ShakeDuration = 0.34f;
        private const float FloaterLife = 0.95f;
        private const float FlashDuration = 0.26f;
        private const float BannerPunchDuration = 0.30f;
        private const float ButtonPunchDuration = 0.16f;

        private float _shakeElapsed = -1f;
        private float _shakeAmplitude;
        private float _bannerPunchElapsed = -1f;
        private float _npcFlashElapsed = -1f;
        private float _playerFlashElapsed = -1f;
        private int _yakuPunchIndex = -1;
        private float _yakuPunchElapsed = -1f;

        private Image _npcFlashBar;
        private Image _playerFlashBar;
        private int _floaterSerial;
        private readonly List<Floater> _floaters = new List<Floater>();
        private static readonly YakuNearMiss[] NoNearMisses = new YakuNearMiss[0];

        /// <summary>
        /// A damage number rising off a bar. The number is the same one the banner prints; the
        /// point of putting it on the bar is that a hit now has a place to come from, so the eye
        /// follows the change instead of being handed a result.
        /// </summary>
        private sealed class Floater
        {
            public Text Label;
            public Vector2 Origin;
            public float Rise;
            public float Elapsed;
        }

        // ---- reveal presentation --------------------------------------------
        /// <summary>
        /// The reveal moves the hit points instead of snapping them, so the eye can follow where
        /// the damage landed rather than having to re-read the number. It never blocks input and
        /// it always resolves to whatever the engine actually holds: a rule that makes a player
        /// wait for a number is a rule they learn to resent, and a tween that can be left behind
        /// by a click is a tween nobody notices. `_shownPlayerHp < 0` means "no match yet".
        /// </summary>
        private float _shownPlayerHp = -1f;
        private float _shownNpcHp = -1f;
        private float _tweenPlayerFrom;
        private float _tweenNpcFrom;
        private float _tweenElapsed;
        private bool _tweening;
        private const float HpTweenDuration = 0.45f;

        // ---- widgets ---------------------------------------------------------
        private RectTransform _titleRoot;
        private RectTransform _battleRoot;
        private RectTransform _resultRoot;
        private RectTransform _rulesRoot;
        private readonly List<Button> _opponentButtons = new List<Button>();
        private Text _opponentDescription;

        private Text _roundLabel;
        private Text _playerName;
        private Text _playerHpText;
        private Image _playerHpFill;
        private Text _npcName;
        private Text _npcHpText;
        private Image _npcHpFill;
        private Text _bannerText;
        private Text _infoText;
        private Text _hintText;
        private Text _flowNote;
        private readonly List<DiceView> _playerDice = new List<DiceView>();
        private readonly List<DiceView> _npcDice = new List<DiceView>();
        private Button _rerollButton;
        private Button _commitButton;
        private Button _advanceButton;
        private readonly List<Button> _yakuButtons = new List<Button>();

        private Text _resultTitle;
        private Text _resultReason;
        private Text _resultStats;
        private Text _soundLabel;
        private Slider _soundSlider;
        private Button _startButton;

        private void Awake()
        {
            KeepDrawingOutOfFocus();
            BuildCanvas();
            if (ConfigOverride == null && !LegacyPresentationForTests)
            {
                BuildScoreDuelPresentation();
                return;
            }
            BuildTitleScreen();
            BuildBattleScreen();
            BuildResultScreen();
            BuildRulesOverlay();
            BuildExperience();
            BuildDuelPresentation();
            ShowScreen(Screen.Title);
        }

        // ---------------------------------------------------------------- focus
        /// <summary>
        /// Out of focus the player used to stop, and a repaint Windows asked for then (a click outside the window,
        /// another window moved over it) drew the camera alone: its skybox showed instead of the game, which is
        /// all one overlay canvas. The game now goes on drawing out of focus, and the camera clears to the table
        /// colour, so that no frame shows sky. The sound still pauses out of focus, as it did while the player stopped.
        /// </summary>
        private static void KeepDrawingOutOfFocus()
        {
            Application.runInBackground = true;
            Camera view = Camera.main;
            if (view == null) return;
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = UiFactory.Background;
        }

        private void OnApplicationFocus(bool focused)
        {
            AudioListener.pause = !focused;
        }

        // ---------------------------------------------------------------- canvas
        private void BuildCanvas()
        {
            if (EventSystem.current == null)
            {
                var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                KeyboardOnlyInput.Apply(eventSystem);
                DontDestroyOnLoad(eventSystem);
            }

            var canvasObject = new GameObject("Canvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            _canvasRoot = canvasObject;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(CanvasWidth, CanvasHeight);
            // Expand, not a height match: matching height assumes the view is at least as wide as
            // 1280 units, and a 4:3 window gives 960 - which silently amputates the brand, both
            // names and the whole rules button off the left and right edges. Expand grows the
            // canvas instead, so the 1280x720 design always fits and only the letterboxing moves.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            UiFactory.CreateStretchPanel("Background", canvasObject.transform, UiFactory.Background);

            // The vignette is created before every screen, so all of them sit inside the same
            // falloff: the table reads as lit wherever the player happens to be looking.
            Image vignette = UiFactory.CreateStretchPanel("Vignette", canvasObject.transform,
                Color.white);
            vignette.sprite = UiFactory.VignetteSprite;

            _titleRoot = UiFactory.CreateStretch("TitleScreen", canvasObject.transform);
            _battleRoot = UiFactory.CreateStretch("BattleScreen", canvasObject.transform);
            _resultRoot = UiFactory.CreateStretch("ResultScreen", canvasObject.transform);
            _rulesRoot = UiFactory.CreateStretch("RulesOverlay", canvasObject.transform);

            // The volume control is a sibling of the screens, so it survives every screen switch.
            Button soundButton = UiFactory.CreateButton("SoundToggle", canvasObject.transform,
                new Vector2(150f, 40f), new Vector2(555f, -336f), GameAudio.VolumeLabel(), 18, OnSoundToggle);
            _soundLabel = soundButton.GetComponentInChildren<Text>();
            Image volumeTrack = UiFactory.CreatePanel("SoundVolume", canvasObject.transform,
                new Vector2(126f, 28f), new Vector2(400f, -336f), new Color(0f, 0f, 0f, .01f));
            volumeTrack.raycastTarget = true;
            Image volumeBed = UiFactory.CreatePanel("Bed", volumeTrack.transform,
                new Vector2(126f, 8f), Vector2.zero, new Color(.17f, .23f, .25f));
            Image volumeFill = UiFactory.CreateStretchPanel("Fill", volumeBed.transform, DiceYahzeeVisuals.Brass);
            Image volumeHandle = UiFactory.CreatePanel("Handle", volumeTrack.transform,
                new Vector2(16f, 26f), Vector2.zero, DiceYahzeeVisuals.Parchment);
            volumeHandle.raycastTarget = true;
            _soundSlider = volumeTrack.gameObject.AddComponent<Slider>();
            _soundSlider.fillRect = volumeFill.rectTransform;
            _soundSlider.handleRect = volumeHandle.rectTransform;
            // Slider stretches the handle vertically across its parent; cancel the
            // original height offset so the visible thumb stays 26 px, not 54 px.
            volumeHandle.rectTransform.sizeDelta = new Vector2(16f, -2f);
            _soundSlider.targetGraphic = volumeHandle;
            _soundSlider.minValue = 0f;
            _soundSlider.maxValue = 1f;
            _soundSlider.SetValueWithoutNotify(GameAudio.Volume);
            _soundSlider.onValueChanged.AddListener(OnSoundSliderChanged);

            transform.SetParent(canvasObject.transform, false);
        }

        // ----------------------------------------------------------------- title
        private void BuildTitleScreen()
        {
            UiFactory.CreateText("Title", _titleRoot, "PARVA MIRABILIA", 58, TextAnchor.MiddleCenter,
                UiFactory.Gold, new Vector2(0f, 214f), new Vector2(1100f, 80f));

            UiFactory.CreateText("Subtitle", _titleRoot, "ARCANE DUEL ─ ダイスの魔術決闘", 24,
                TextAnchor.MiddleCenter, UiFactory.TextDim, new Vector2(0f, 156f), new Vector2(1100f, 40f));

            UiFactory.CreatePanel("Rule", _titleRoot, new Vector2(520f, 2f), new Vector2(0f, 122f),
                UiFactory.GoldDim);

            UiFactory.CreateText("PickLabel", _titleRoot, "対戦相手を選ぶ", 22, TextAnchor.MiddleCenter,
                UiFactory.Text, new Vector2(0f, 86f), new Vector2(600f, 34f));

            for (int i = 0; i < Opponents.Length; i++)
            {
                int captured = i;
                Vector2 position = new Vector2((i - 1) * 215f, 34f);
                Button button = UiFactory.CreateButton("Opponent" + i, _titleRoot, new Vector2(200f, 56f),
                    position, NpcArchetypes.DisplayName(Opponents[i]), 21,
                    () => SelectOpponent(captured));
                _opponentButtons.Add(button);
            }

            _opponentDescription = UiFactory.CreateText("OpponentInfo", _titleRoot, "", 19,
                TextAnchor.MiddleCenter, UiFactory.TextDim, new Vector2(0f, -20f), new Vector2(940f, 70f));

            Button start = UiFactory.CreateButton("Start", _titleRoot, new Vector2(320f, 68f),
                new Vector2(0f, -116f), "決闘を始める", 26, StartMatch);
            UiFactory.StylePrimary(start);
            _startButton = start;

            UiFactory.CreateButton("ShowRules", _titleRoot, new Vector2(250f, 54f),
                new Vector2(145f, -196f), "ルールを見る", 21, () => SetRulesVisible(true));
            UiFactory.CreateButton("Learn", _titleRoot, new Vector2(250f, 54f),
                new Vector2(-145f, -196f), "遊び方を練習", 21, OpenGuide);

            UiFactory.CreateText("Footnote", _titleRoot,
                "5d6・HP32・10ラウンド。役を決めて同時に公開する、数分で終わる1戦。",
                17, TextAnchor.MiddleCenter, UiFactory.TextDim, new Vector2(0f, -254f), new Vector2(1000f, 40f));

            SelectOpponent(1);
        }

        private void SelectOpponent(int index)
        {
            _opponent = Opponents[index];
            _titleOpponent = _opponent;
            for (int i = 0; i < _opponentButtons.Count; i++)
                UiFactory.StyleSelected(_opponentButtons[i], i == index);
            _opponentDescription.text = NpcArchetypes.Description(_opponent);

            // The primary button names its opponent so nobody starts a duel without knowing
            // which of the three faces they picked.
            if (_startButton != null)
            {
                var caption = _startButton.GetComponentInChildren<Text>();
                if (caption != null)
                    caption.text = NpcArchetypes.DisplayName(_opponent) + "と決闘を始める";
            }
            if (_intentRoot != null)
            {
                RefreshOpponentCards(index);
                _opponentDescription.text = OpponentMottos[index];
                Caption(_startButton, OpponentTitles[index] + "と決闘");
            }
        }

        // ---------------------------------------------------------------- battle
        /// <summary>Where the two health bars sit, named because the damage floaters are launched
        /// at them too: a number that floats 12 units away from the bar it belongs to looks like
        /// it belongs to something else.</summary>
        private const float NpcBarY = 216f;
        private const float PlayerBarY = -198f;

        private void BuildBattleScreen()
        {
            // The battle screen is laid out inside +/-325 canvas units so that it still fits when
            // the Game view is wider than 16:9 (the scaler then gives less vertical room).
            // Oversized on purpose: the canvas is wider than 1280 whenever the Game view is
            // wider than 16:9, and a bar that reaches past both edges always spans the screen.
            // The top furniture sits 6 units higher than the dead centre of its band: what was
            // being traded away was the gap between the health bar and the NPC's dice, where the
            // tinted die rings were landing on the bar. The margin above the bar absorbs it.
            Image topBar = UiFactory.CreatePanel("TopBar", _battleRoot, new Vector2(4000f, 56f),
                new Vector2(0f, 302f), UiFactory.Panel);
            topBar.raycastTarget = false;
            // The one band that has to separate from the field below it without a border, because
            // a border would be a second rectangle and this is already one.
            UiFactory.AddDropShadow(topBar, new Vector2(0f, -4f), 0.55f);
            UiFactory.AddGloss(topBar, 4000, 56);

            // Left edge clearance matters here: the box is left-anchored, so its x is where the
            // text *begins*, and at -540 it began 110 units off the canvas - the brand mark was
            // being cut through the middle. Kept clear of the round counter at -200.
            UiFactory.CreateText("Brand", _battleRoot, "PARVA MIRABILIA", 20, TextAnchor.MiddleLeft,
                UiFactory.Gold, new Vector2(-410f, 302f), new Vector2(400f, 40f));

            _roundLabel = UiFactory.CreateText("Round", _battleRoot, "ラウンド 1 / 10", 24,
                TextAnchor.MiddleCenter, UiFactory.Text, new Vector2(0f, 302f), new Vector2(400f, 40f));

            UiFactory.CreateButton("BattleRules", _battleRoot, new Vector2(120f, 40f),
                new Vector2(555f, 302f), "ルール", 18, () => SetRulesVisible(true));

            // ---- NPC block
            // Raised with the top bar: the die rings below reach 5 units past their dice, and at
            // the old height they were sitting on the health bar.
            _npcName = UiFactory.CreateText("NpcName", _battleRoot, "NPC", 21, TextAnchor.MiddleLeft,
                UiFactory.Text, new Vector2(-290f, 246f), new Vector2(400f, 32f));
            _npcHpText = UiFactory.CreateText("NpcHp", _battleRoot, "HP 32 / 32", 20, TextAnchor.MiddleRight,
                UiFactory.Text, new Vector2(290f, 246f), new Vector2(260f, 32f));
            _npcHpFill = CreateHpBar("NpcBar", _battleRoot, new Vector2(0f, NpcBarY), UiFactory.HpGreen);
            _npcFlashBar = CreateBarFlash("NpcFlash", NpcBarY);

            CreateDiceRow(_npcDice, _battleRoot, "NpcDie", new Vector2(0f, 158f), false);

            // Three lines of arithmetic now fit here, so the font is sized for a block rather
            // than for a single headline.
            _bannerText = UiFactory.CreateText("Banner", _battleRoot, "", 17, TextAnchor.MiddleCenter,
                UiFactory.Gold, new Vector2(0f, 80f), new Vector2(1080f, 72f));

            // ---- player block
            CreateDiceRow(_playerDice, _battleRoot, "PlayerDie", new Vector2(0f, 0f), true);

            // The yaku row sits directly under the dice, because it is what those five dice are
            // about to become. Everything below it was shifted down to make room; the row is the
            // only genuinely new element on this screen.
            BuildYakuButtons();

            // Everything under the dice sits 12 units lower than it did. The screen had used up
            // its space so evenly that no two bands were more than 8 units apart while 35 units
            // sat idle at the bottom edge - the whole block read as one cramped slab instead of
            // as dice, then a decision, then an action. The slack came from the margin, not from
            // any element, so nothing got smaller.
            _infoText = UiFactory.CreateText("Info", _battleRoot, "", 19, TextAnchor.MiddleCenter,
                UiFactory.Text, new Vector2(0f, -138f), new Vector2(1040f, 24f));

            _playerName = UiFactory.CreateText("PlayerName", _battleRoot, "あなた", 21, TextAnchor.MiddleLeft,
                UiFactory.Text, new Vector2(-290f, -170f), new Vector2(400f, 30f));
            _playerHpText = UiFactory.CreateText("PlayerHp", _battleRoot, "HP 32 / 32", 20,
                TextAnchor.MiddleRight, UiFactory.Text, new Vector2(290f, -170f), new Vector2(260f, 30f));
            _playerHpFill = CreateHpBar("PlayerBar", _battleRoot, new Vector2(0f, PlayerBarY), UiFactory.HpGreen);
            _playerFlashBar = CreateBarFlash("PlayerFlash", PlayerBarY);

            _hintText = UiFactory.CreateText("Hint", _battleRoot, "", 18, TextAnchor.MiddleCenter,
                UiFactory.TextDim, new Vector2(0f, -228f), new Vector2(1040f, 28f));

            // ---- one button pair for the whole round: there is no second mode to switch into
            _rerollButton = UiFactory.CreateButton("Reroll", _battleRoot, new Vector2(250f, 56f),
                new Vector2(-145f, -274f), "振り直す", 22, DoReroll);
            _commitButton = UiFactory.CreateButton("Commit", _battleRoot, new Vector2(250f, 56f),
                new Vector2(145f, -274f), "確定する", 22, DoCommit);
            UiFactory.StylePrimary(_commitButton);

            _advanceButton = UiFactory.CreateButton("Advance", _battleRoot, new Vector2(300f, 56f),
                new Vector2(0f, -274f), "次のラウンド", 22, DoAdvance);

            // Rewritten every refresh, because the old single fixed line ("click a die to hold...")
            // stayed on screen even when no die could be clicked at all.
            _flowNote = UiFactory.CreateText("PhaseNote", _battleRoot, "", 16,
                TextAnchor.MiddleCenter, UiFactory.TextDim, new Vector2(0f, -322f), new Vector2(1140f, 30f));
        }

        // ---- yaku row --------------------------------------------------------
        private const float YakuRowY = -88f;
        private const float YakuButtonWidth = 140f;
        private const float YakuButtonHeight = 62f;
        private const float YakuButtonGap = 8f;
        /// <summary>How many yaku buttons exist: the most a five-dice hand can offer.</summary>
        private const int YakuButtonCount = 7;

        /// <summary>
        /// One button per yaku this roll may declare.
        ///
        /// This row is the decision the whole round turns on, so it is a row of buttons instead of
        /// arithmetic the player has to reconstruct from five dice and a rule table: each button
        /// prints the strike and the ward it would produce, which is the answer to "how much am I
        /// giving up to hit harder" made visible before the click rather than after it.
        /// </summary>
        private void BuildYakuButtons()
        {
            _yakuButtons.Clear();

            for (int i = 0; i < YakuButtonCount; i++)
            {
                int captured = i;
                Button button = UiFactory.CreateButton("Yaku" + i, _battleRoot,
                    new Vector2(YakuButtonWidth, YakuButtonHeight),
                    new Vector2(0f, YakuRowY), "", 17, () => OnYakuClicked(captured));
                button.gameObject.SetActive(false);
                _yakuButtons.Add(button);
            }
        }

        /// <summary>Positions and fills the buttons for what this hand may declare.</summary>
        private void LayoutYakuButtons(YakuOffer[] offers, bool revealing)
        {
            if (_engine.Config.TacticalEnabled) { LayoutSpellCards(revealing); return; }
            int shown = revealing ? 0 : offers.Length;
            float total = shown * YakuButtonWidth + Math.Max(0, shown - 1) * YakuButtonGap;
            float startX = -total * 0.5f + YakuButtonWidth * 0.5f;

            for (int i = 0; i < _yakuButtons.Count; i++)
            {
                Button button = _yakuButtons[i];
                bool visible = i < shown;
                button.gameObject.SetActive(visible);
                if (!visible)
                    continue;

                YakuOffer offer = offers[i];
                button.GetComponent<RectTransform>().anchoredPosition =
                    new Vector2(startX + i * (YakuButtonWidth + YakuButtonGap), YakuRowY);

                var caption = button.GetComponentInChildren<Text>();
                if (caption != null)
                {
                    caption.text = offer.DisplayName() + "\n攻" + offer.StrikeValue(_engine.PlayerDice) +
                                   " 守" + offer.WardValue(_engine.PlayerDice);
                }

                UiFactory.StyleSelected(button, offer.Kind == _yakuOffer.Kind);
            }
        }

        private Image CreateHpBar(string name, Transform parent, Vector2 position, Color color)
        {
            Image track = UiFactory.CreatePanel(name, parent, new Vector2(580f, 16f), position,
                UiFactory.HpTrack);
            track.raycastTarget = false;

            var fillObject = new GameObject("Fill", typeof(RectTransform));
            var fillRect = fillObject.GetComponent<RectTransform>();
            fillRect.SetParent(track.rectTransform, false);
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = new Vector2(580f, 0f);

            var fill = fillObject.AddComponent<Image>();
            fill.color = color;
            fill.raycastTarget = false;
            // The fill is cropped rather than resized. Resizing a square fill gave a bar that was
            // down a quarter square corners poking out of the track's rounded ones, so the two
            // halves of one bar disagreed about what shape a bar is.
            fill.sprite = UiFactory.RoundedSprite(Mathf.RoundToInt(580f), Mathf.RoundToInt(16f));
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            return fill;
        }

        /// <summary>
        /// A white sheet laid over a whole bar to mark a hit. It is a sibling of the bar rather
        /// than a child: the fill is resized on every frame of the health tween, so anything
        /// parented to it would be dragged around with it, and the flash has to read as the whole
        /// bar taking the blow rather than only the part still standing.
        /// </summary>
        private Image CreateBarFlash(string name, float y)
        {
            Image flash = UiFactory.CreatePanel(name, _battleRoot, new Vector2(580f, 16f),
                new Vector2(0f, y), Color.white);
            flash.raycastTarget = false;
            SetAlpha(flash, 0f);
            return flash;
        }

        private static void SetAlpha(Image image, float alpha)
        {
            if (image == null)
                return;
            Color color = image.color;
            color.a = alpha;
            image.color = color;
        }

        // ------------------------------------------------------------------ juice
        private static float EaseOut(float t)
        {
            return 1f - (1f - t) * (1f - t);
        }

        private static float EaseOutCubic(float t)
        {
            float inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        /// <summary>
        /// Starts the throw on a row of dice. <paramref name="toThrow"/> names the dice that are
        /// being thrown, not the ones being held: a die the player kept must sit perfectly still,
        /// because stillness is how the screen says "this one survived".
        ///
        /// The two readings are easy to swap - the rules want the held mask, the animation wants
        /// the thrown one - and swapping them rolls the wrong dice in front of the player while
        /// the values still land correctly, so the hand is right and the screen lies about how it
        /// got there. <c>Reroll_ThrowsExactlyTheDiceThePlayerMarked</c> pins the direction.
        /// </summary>
        private void StartThrow(List<DiceView> dice, bool[] toThrow)
        {
            for (int i = 0; i < dice.Count; i++)
            {
                if (toThrow != null && (i >= toThrow.Length || !toThrow[i]))
                    continue;

                dice[i].BeginRoll(i * RollStagger, RollScramble);
            }
        }

        /// <summary>
        /// The round being paid for: numbers lift off the bars, the struck side flashes, the
        /// screen kicks once, and the banner lands on top of it.
        ///
        /// Every number here is the same number <see cref="BuildBanner"/> prints, including the
        /// stand-off shove, so the animation can never tell a different story from the text
        /// underneath it.
        /// </summary>
        private void PlayImpact(RoundOutcome outcome)
        {
            StartDuelEffects(outcome);
            Vector2 npcBar = new Vector2(0f, NpcBarY);
            Vector2 playerBar = new Vector2(0f, _engine.Config.TacticalEnabled ? -219 : PlayerBarY);

            if (outcome.DamageToNpc > 0)
            {
                SpawnFloater(npcBar, "−" + outcome.DamageToNpc, UiFactory.HpRed, 46f);
                _npcFlashElapsed = 0f;
            }
            else
            {
                SpawnFloater(npcBar, "ミス", UiFactory.TextDim, 34f);
            }

            if (outcome.DamageToPlayer > 0)
            {
                SpawnFloater(playerBar, "−" + outcome.DamageToPlayer, UiFactory.HpRed, 46f);
                _playerFlashElapsed = 0f;
            }
            else
            {
                SpawnFloater(playerBar, "ミス", UiFactory.TextDim, 34f);
            }

            // Sized by the blow, and small enough that nothing it covers becomes unreadable: a
            // shake that costs you the health bars is a shake that made the round worse.
            int heaviest = Mathf.Max(outcome.DamageToNpc, outcome.DamageToPlayer);
            _shakeAmplitude = Mathf.Clamp(heaviest * 0.8f, 0f, 10f);
            _shakeElapsed = !_reducedMotion && _shakeAmplitude >= 0.5f ? 0f : -1f;
            _bannerPunchElapsed = 0f;
        }

        private void SpawnFloater(Vector2 origin, string text, Color color, float rise)
        {
            _floaterSerial++;
            Text label = UiFactory.CreateText("Float" + _floaterSerial, _battleRoot, text, 36,
                TextAnchor.MiddleCenter, color, origin, new Vector2(420f, 76f));
            label.raycastTarget = false;
            label.fontStyle = FontStyle.Bold;

            _floaters.Add(new Floater
            {
                Label = label,
                Origin = origin,
                Rise = rise,
                Elapsed = 0f
            });
        }

        /// <summary>Called once a frame. Everything here is self-terminating and needs no input.</summary>
        private void TickJuice(float deltaTime)
        {
            for (int i = 0; i < _playerDice.Count; i++)
                _playerDice[i].Tick(deltaTime);
            for (int i = 0; i < _npcDice.Count; i++)
                _npcDice[i].Tick(deltaTime);

            TickFloaters(deltaTime);
            TickShake(deltaTime);
            TickFlash(_npcFlashBar, ref _npcFlashElapsed, deltaTime);
            TickFlash(_playerFlashBar, ref _playerFlashElapsed, deltaTime);
            TickScale(_bannerText != null ? _bannerText.rectTransform : null,
                ref _bannerPunchElapsed, deltaTime, 1.14f, BannerPunchDuration);

            if (_yakuPunchIndex >= 0 && _yakuPunchIndex < _yakuButtons.Count)
            {
                TickScale(_yakuButtons[_yakuPunchIndex].transform as RectTransform,
                    ref _yakuPunchElapsed, deltaTime, 1.12f, ButtonPunchDuration);
            }
            else
            {
                _yakuPunchElapsed = -1f;
            }
        }

        private void TickFloaters(float deltaTime)
        {
            for (int i = _floaters.Count - 1; i >= 0; i--)
            {
                Floater floater = _floaters[i];
                floater.Elapsed += deltaTime;
                float t = Mathf.Clamp01(floater.Elapsed / FloaterLife);

                floater.Label.rectTransform.anchoredPosition =
                    floater.Origin + new Vector2(0f, EaseOutCubic(t) * floater.Rise);

                // It arrives a little larger than it settles, so the number lands instead of
                // simply appearing at full size.
                floater.Label.rectTransform.localScale =
                    Vector3.one * Mathf.Lerp(1.4f, 1f, Mathf.Clamp01(t / 0.2f));

                Color color = floater.Label.color;
                color.a = t < 0.55f ? 1f : Mathf.Clamp01(1f - (t - 0.55f) / 0.45f);
                floater.Label.color = color;

                if (floater.Elapsed >= FloaterLife)
                {
                    UnityEngine.Object.Destroy(floater.Label.gameObject);
                    _floaters.RemoveAt(i);
                }
            }
        }

        private void TickShake(float deltaTime)
        {
            if (_battleRoot == null)
                return;

            if (_shakeElapsed < 0f)
            {
                _battleRoot.anchoredPosition = Vector2.zero;
                return;
            }

            _shakeElapsed += deltaTime;
            float t = Mathf.Clamp01(_shakeElapsed / ShakeDuration);
            float falloff = 1f - t;
            float x = Mathf.Sin(t * 47f * Mathf.PI) * _shakeAmplitude * falloff;
            float y = Mathf.Cos(t * 61f * Mathf.PI) * _shakeAmplitude * 0.6f * falloff;
            _battleRoot.anchoredPosition = new Vector2(x, y);

            if (t >= 1f)
            {
                _shakeElapsed = -1f;
                _battleRoot.anchoredPosition = Vector2.zero;
            }
        }

        private void TickFlash(Image bar, ref float elapsed, float deltaTime)
        {
            if (bar == null)
            {
                elapsed = -1f;
                return;
            }

            if (elapsed < 0f)
                return;

            elapsed += deltaTime;
            float t = Mathf.Clamp01(elapsed / FlashDuration);
            SetAlpha(bar, Mathf.Lerp(0.85f, 0f, t));
            if (t >= 1f)
                elapsed = -1f;
        }

        private void TickScale(RectTransform rect, ref float elapsed, float deltaTime,
            float peak, float duration)
        {
            if (rect == null)
            {
                elapsed = -1f;
                return;
            }

            if (elapsed < 0f)
                return;

            elapsed += deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            rect.localScale = Vector3.one * Mathf.Lerp(peak, 1f, EaseOut(t));
            if (t >= 1f)
            {
                rect.localScale = Vector3.one;
                elapsed = -1f;
            }
        }

        private void CreateDiceRow(List<DiceView> target, Transform parent, string prefix,
            Vector2 centre, bool interactive)
        {
            target.Clear();
            float total = DiceCount * DieSize + (DiceCount - 1) * DieGap;
            float startX = -total * 0.5f + DieSize * 0.5f;

            for (int i = 0; i < DiceCount; i++)
            {
                var view = new DiceView(parent, i, DieSize, prefix + i);
                view.SetPosition(new Vector2(startX + i * (DieSize + DieGap), centre.y));
                int captured = i;
                if (interactive)
                    view.Clicked += OnPlayerDieClicked;
                target.Add(view);
            }
        }

        // ---------------------------------------------------------------- result
        private void BuildResultScreen()
        {
            var dimRect = UiFactory.CreateStretch("ResultDim", _resultRoot);
            var dim = dimRect.gameObject.AddComponent<Image>();
            dim.color = new Color(0.04f, 0.04f, 0.09f, 0.94f);
            dim.raycastTarget = false;

            Image resultPanel = UiFactory.CreatePanel("ResultPanel", _resultRoot,
                new Vector2(780f, 450f), Vector2.zero, UiFactory.Panel);
            UiFactory.AddDropShadow(resultPanel, new Vector2(0f, -8f), 0.5f);
            UiFactory.AddGloss(resultPanel, 780, 450);

            _resultTitle = UiFactory.CreateText("ResultTitle", _resultRoot, "勝利", 54,
                TextAnchor.MiddleCenter, UiFactory.Gold, new Vector2(0f, 140f), new Vector2(700f, 80f));

            _resultReason = UiFactory.CreateText("ResultReason", _resultRoot, "", 21,
                TextAnchor.MiddleCenter, UiFactory.TextDim, new Vector2(0f, 84f), new Vector2(700f, 40f));

            _resultStats = UiFactory.CreateText("ResultStats", _resultRoot, "", 20,
                TextAnchor.MiddleCenter, UiFactory.Text, new Vector2(0f, -10f), new Vector2(700f, 140f));

            UiFactory.CreateButton("Rematch", _resultRoot, new Vector2(300f, 62f),
                new Vector2(-165f, -140f), "もう一度勝負", 22, StartMatch);
            UiFactory.CreateButton("ChangeOpponent", _resultRoot, new Vector2(300f, 62f),
                new Vector2(165f, -140f), "相手を選び直す", 21, () => ShowScreen(Screen.Title));

            UiFactory.CreateText("ResultFootnote", _resultRoot, "再戦は毎回、新しいサイコロで始まります。",
                17, TextAnchor.MiddleCenter, UiFactory.TextDim, new Vector2(0f, -186f), new Vector2(700f, 34f));
        }

        // ----------------------------------------------------------------- rules
        /// <summary>
        /// Builds the rules overlay by fitting the frame around the text, not the text inside a
        /// frame somebody guessed at.
        ///
        /// The first version hard-coded a body box and a close button that fit the rules as they
        /// were written at the time. The rules grew, and because <see cref="UiFactory.CreateText"/>
        /// deliberately does not clip vertically, the body simply kept drawing - straight through
        /// the close button and out past the bottom of the panel. Nothing in the layout objected,
        /// because nothing in the layout knew how tall the text actually was.
        ///
        /// So the body now lives in a slot measured from the title above it and the button below
        /// it, and that slot is clipped. The text shrinks to fit the slot, and if it ever grows
        /// past it again the overflow is cut off at the slot's edge instead of landing on the
        /// button or outside the frame. Which size the font settles on is a rendering detail; the
        /// geometry no longer depends on getting it right.
        /// </summary>
        private void BuildRulesOverlay()
        {
            var blocker = UiFactory.CreateStretch("RulesBlocker", _rulesRoot);
            var blockerImage = blocker.gameObject.AddComponent<Image>();
            blockerImage.color = new Color(0f, 0f, 0f, 0.72f);

            // The canvas is always CanvasHeight units tall (the scaler matches height), so these
            // numbers are absolute and the frame always lands inside the screen.
            // The frame is 820 rather than the 950 it used to be: at 950 the longest line of the
            // rules stopped around 600 units in and left a third of the panel empty, which reads
            // as a column of text parked in a box rather than as a written page.
            const float panelWidth = 820f;
            const float panelHeight = 700f;
            const float inner = 18f;             // padding between the frame and its content
            const float gapTitleBody = 14f;
            const float gapBodyButton = 22f;
            const float titleHeight = 44f;
            const float buttonHeight = 50f;

            Image rulesPanel = UiFactory.CreatePanel("RulesPanel", _rulesRoot,
                new Vector2(panelWidth, panelHeight), Vector2.zero, UiFactory.Panel);
            UiFactory.AddDropShadow(rulesPanel, new Vector2(0f, -8f), 0.5f);
            // Added before the furniture below it, or the light would land on top of the words.
            UiFactory.AddGloss(rulesPanel, Mathf.RoundToInt(panelWidth),
                Mathf.RoundToInt(panelHeight));

            Text title = UiFactory.CreateText("RulesTitle", _rulesRoot, "遊び方", 32,
                TextAnchor.MiddleCenter, UiFactory.Gold, Vector2.zero,
                new Vector2(500f, titleHeight));

            // Whatever is left over after the fixed furniture belongs to the body slot.
            float bodyHeight = panelHeight - inner * 2f
                               - titleHeight - gapTitleBody - gapBodyButton - buttonHeight;

            // The rules do not fit on one screen. Measured on the shipped text: 858 units of
            // words against 534 units of slot, and a render of the real panel was cut straight
            // through 【ダメージの計算】 - three of the six sections the rules print were never
            // drawn at all. Best fit was supposed to prevent exactly this and did not engage:
            // the panel came out at its largest size and the mask quietly threw the rest away.
            //
            // So the body is a scrollable page rather than a fixed box. Nothing is dropped and
            // the size stays at 16, which is the difference between a rule being hard to find
            // and a rule being impossible to read.
            const float barWidth = 10f;
            const float barGap = 10f;
            float bodyWidth = panelWidth - inner * 2f - barWidth - barGap;
            const float slotShift = (barWidth + barGap) / 2f;

            RectTransform slot = UiFactory.CreateRect("RulesBodySlot", _rulesRoot,
                new Vector2(bodyWidth, bodyHeight), Vector2.zero);
            slot.gameObject.AddComponent<RectMask2D>();

            // A drag only reaches a ScrollRect through a graphic that raycasts, and the rules
            // text deliberately does not. The viewport carries the gesture instead.
            var viewport = slot.gameObject.AddComponent<Image>();
            viewport.color = Color.clear;
            viewport.raycastTarget = true;

            RectTransform content = UiFactory.CreateRect("RulesContent", slot,
                Vector2.zero, Vector2.zero);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);

            Text body = UiFactory.CreateText("RulesBody", content, BuildRulesText(ResolveConfig()), 16,
                TextAnchor.UpperLeft, UiFactory.Text, Vector2.zero,
                new Vector2(bodyWidth, bodyHeight));
            body.lineSpacing = 1.1f;
            // Top-left alignment inside a centred box puts the first line under the title.
            body.resizeTextForBestFit = false;

            // The content is as tall as the words rather than as tall as the slot; the difference
            // is what the scroll consumes, and it is the only thing standing between a long rule
            // and the mask discarding it. preferredHeight and the laid-out generator can disagree
            // about wrapping, so take whichever says the text needs more room - over-sizing the
            // content only costs a little empty space at the end, under-sizing it loses a rule.
            Canvas.ForceUpdateCanvases();
            // preferredHeight is Unity's own layout figure for this text at this size, but it is
            // measured without wrapping: a line that overruns the measure would be counted as a
            // single line and the end of the rules would silently fall outside the content. The
            // laid-out generator has already wrapped at this exact width, so take the larger.
            float needed = body.preferredHeight;
            var laidOut = body.cachedTextGenerator;
            if (laidOut != null && laidOut.lines.Count > 0)
            {
                float drawn = 0f;
                for (int i = 0; i < laidOut.lines.Count; i++)
                    drawn += laidOut.lines[i].height;
                needed = Mathf.Max(needed, drawn);
            }

            body.rectTransform.anchorMin = new Vector2(0f, 1f);
            body.rectTransform.anchorMax = new Vector2(1f, 1f);
            body.rectTransform.pivot = new Vector2(0.5f, 1f);
            body.rectTransform.sizeDelta = new Vector2(0f, needed);
            body.rectTransform.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, needed);

            var scroll = slot.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = slot;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.inertia = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30f;

            // The bar is the only sign that there is more below. A clipped page and a scrollable
            // page look identical until the reader has already given up looking.
            RectTransform bar = UiFactory.CreateRect("RulesScroll", _rulesRoot,
                new Vector2(barWidth, bodyHeight), Vector2.zero);
            var track = bar.gameObject.AddComponent<Image>();
            track.color = UiFactory.HpTrack;
            var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.targetGraphic = track;

            RectTransform handle = UiFactory.CreateRect("Handle", bar, Vector2.zero, Vector2.zero);
            handle.anchorMin = new Vector2(0.5f, 0f);
            handle.anchorMax = new Vector2(0.5f, 1f);
            handle.sizeDelta = new Vector2(barWidth - 4f, 0f);
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = UiFactory.TextDim;
            handleImage.raycastTarget = false;
            scrollbar.handleRect = handle;

            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

            Button close = UiFactory.CreateButton("CloseRules", _rulesRoot,
                new Vector2(220f, buttonHeight), Vector2.zero, "閉じる", 21,
                () => SetRulesVisible(false));

            // Laid out top-down: title at the top of the frame, slot directly beneath it, button
            // a fixed gap beneath the slot. Each position is derived from the one above, so no two
            // of them can be moved independently and end up on top of each other.
            float top = panelHeight / 2f - inner;
            title.rectTransform.anchoredPosition = new Vector2(0f, top - titleHeight / 2f);

            float bodyTop = top - titleHeight - gapTitleBody;
            // The slot sits left of centre so the bar keeps its own lane instead of lying over
            // the last characters of every line.
            slot.anchoredPosition = new Vector2(-slotShift, bodyTop - bodyHeight / 2f);
            bar.anchoredPosition = new Vector2(bodyWidth / 2f + barGap / 2f, bodyTop - bodyHeight / 2f);

            close.GetComponent<RectTransform>().anchoredPosition =
                new Vector2(0f, bodyTop - bodyHeight - gapBodyButton - buttonHeight / 2f);

            _rulesRoot.gameObject.SetActive(false);
        }

        private void SetRulesVisible(bool visible)
        {
            _shortcutSuppressedFrame = Time.frameCount;
            if (visible)
                CloseExperienceOverlays();
            LogInput("rules", visible ? "open" : "close");

            if (visible)
                GameAudio.Play("ui_click");

            _rulesRoot.gameObject.SetActive(visible);
            if (visible) SelectDefault(_rulesRoot, "CloseRules");
            else ClearKeyboardSelection();
        }

        // ----------------------------------------------------------------- flow
        private void ShowScreen(Screen screen)
        {
            _screen = screen;
            CloseExperienceOverlays();
            _titleRoot.gameObject.SetActive(screen == Screen.Title);
            _battleRoot.gameObject.SetActive(screen == Screen.Battle);
            _resultRoot.gameObject.SetActive(screen == Screen.Result);
            _rulesRoot.gameObject.SetActive(false);
            RefreshSoundLabel();
        }

        // ------------------------------------------------------------------ sound
        private void Start()
        {
            // Every Awake has run by now, so GameAudio knows the saved volume step.
            RefreshSoundLabel();
            StartPrivateUpdateCheck();
            BeginPlayerSmokeIfRequested();
        }

        private void RefreshSoundLabel()
        {
            if (_soundLabel != null)
                _soundLabel.text = GameAudio.VolumeLabel();
            if (_soundSlider != null)
                _soundSlider.SetValueWithoutNotify(GameAudio.Volume);
        }

        private void OnSoundToggle()
        {
            GameAudio.ToggleMute();
            RefreshSoundLabel();

            if (GameAudio.Volume > 0f)
                GameAudio.Play("ui_click");
        }

        private void OnSoundSliderChanged(float value)
        {
            GameAudio.SetVolume(value);
            RefreshSoundLabel();
        }

        /// <summary>
        /// The audio chrome (toggle + volume bar) is a sibling of the screens, built before
        /// the later full-canvas screens drew their own opaque tables. Every screen that
        /// covers the canvas - the SD duels, the online lobby, the SD modal dims - must
        /// raise the pair above itself, or the volume bar vanishes under the leather and
        /// only the mute toggle keeps working.
        /// </summary>
        private void LiftAudioChrome()
        {
            _canvasRoot.transform.Find("SoundToggle")?.SetAsLastSibling();
            _canvasRoot.transform.Find("SoundVolume")?.SetAsLastSibling();
        }

        private void StartMatch()
        {
            _matchIndex++;
            _opponent = Opponents[Mathf.Clamp(Array.IndexOf(Opponents, _opponent), 0, Opponents.Length - 1)];
            // A wall-clock tick alone would repeat whenever two matches start inside the same
            // tick, so the seed comes from the platform RNG; the match counter keeps the rest
            // apart. Tests still build their own seeds through SeededRandom.
            int seed = SeedOverride != null ? FreshSeed() : unchecked(FreshSeed() * 397 ^ (_matchIndex * 7919));
            // The shipped build plays with the pattern layer on; the GameConfig default stays off
            // so the original suite can still measure the original game. ConfigOverride lets a
            // test put the original rules back and measure them against the shipped ones.
            var config = ResolveConfig();
            if (_run != null && _run.Phase == DuelRunPhase.Fighting)
            {
                _opponent = _run.Opponent;
                _engine = _run.BeginFight(new SeededRandom(seed));
            }
            else
            {
                _run = null;
                _opponent = _titleOpponent;
                _engine = new MatchEngine(config, new SeededRandom(seed), _opponent);
            }
            _engine.Roll();
            GameAudio.Play("dice_roll");

            // A new match starts with the reveal already at rest: a tween out of an empty screen
            // would read as damage that never happened.
            _shownPlayerHp = _engine.PlayerHp;
            _shownNpcHp = _engine.NpcHp;
            _tweening = false;

            _committed = false;
            _banner = "";
            _hint = "";
            ResetRoundState();
            LogInput("start_match", "opponent=" + _opponent);
            ShowScreen(Screen.Battle);
            RefreshBattle();

            // The opening throw. Nothing is decided yet, so all five dice go: this is the one
            // moment in the round where every die is still alive.
            StartThrow(_playerDice, null);
        }

        /// <summary>
        /// The rule set in force, resolved in one place for the two callers that need it before a
        /// match exists - the rules overlay and the round itself. Two copies of this decision
        /// would be one more than is needed to drift apart.
        /// </summary>
        private static GameConfig ResolveConfig()
        {
            return ConfigOverride != null
                ? ConfigOverride()
                : new GameConfig().ApplyTacticalDuelLayer();
        }

        // ----------------------------------------------------------- input log
        /// <summary>
        /// One line per player input, so the click cost of a round is measured on the real build
        /// instead of estimated from the code. Observation only, never changes game state.
        /// </summary>
        private void LogInput(string action, string detail)
        {
            var info = new MatchEngineInfo();
            if (_engine != null)
            {
                info.Phase = _engine.Phase;
                info.Round = _engine.Round;
                info.RerollsLeft = _engine.PlayerRerollsLeft;
                info.PlayerHp = _engine.PlayerHp;
                info.NpcHp = _engine.NpcHp;
            }
            info.SplitMode = _engine != null && _engine.Phase != MatchPhase.Finished &&
                             _engine.Outcome == null;
            InputLog.Record(action, info, detail);
        }

        /// <summary>
        /// Marking a die for the re-roll. One click adds the mark, one click takes it away - the
        /// destination is known in advance and is always the same distance away, which is what
        /// the old three-state cycle could not promise.
        /// </summary>
        private void OnPlayerDieClicked(int index)
        {
            if (ModalOpen || _engine == null || _engine.Outcome != null || _committed)
                return;

            if (_engine.Phase != MatchPhase.AwaitingReroll && _engine.Phase != MatchPhase.AwaitingSplit)
                return;

            _rerollMarked[index] = !_rerollMarked[index];

            LogInput("die", "index=" + index + " marked=" + _rerollMarked[index]);
            GameAudio.Play("die_keep");
            _playerDice[index].Punch();          // the mark has to land on something
            RefreshBattle();
        }

        /// <summary>
        /// Picking the hand to declare. The die tinting, the sums and the commit button all read
        /// from this one selection, so choosing a yaku is choosing the whole round in a single
        /// gesture rather than walking five dice into the right columns.
        /// </summary>
        private void OnYakuClicked(int index)
        {
            if (ModalOpen || _engine == null || _engine.Outcome != null || _committed)
                return;

            if (_engine.Phase != MatchPhase.AwaitingReroll && _engine.Phase != MatchPhase.AwaitingSplit)
                return;

            if (_engine.Config.TacticalEnabled) { SelectDuelSpell(index); return; }

            var offers = Yaku.Evaluate(_engine.PlayerDice, _engine.Config);
            if (index < 0 || index >= offers.Length)
                return;

            _yakuOffer = offers[index];

            LogInput("yaku", "index=" + index + " kind=" + _yakuOffer.Kind +
                             " forward=" + _yakuOffer.ForwardCount);
            GameAudio.Play("die_keep");
            RefreshBattle();

            // After the refresh, not before: laying the row out rewrites the buttons, and a punch
            // handed out first would be spent on a position the screen had not taken yet.
            _yakuPunchIndex = index;
            _yakuPunchElapsed = 0f;
        }

        private void DoReroll()
        {
            if (ModalOpen || _engine == null || _engine.Phase != MatchPhase.AwaitingReroll ||
                _engine.PlayerRerollsLeft <= 0)
                return;

            // The two masks are complements and they are read by different machines: the rules
            // take the held dice, the animation takes the thrown ones. Keeping them in one array
            // and inverting at the call site is what made the screen roll the dice the player had
            // just decided to keep, so they are built side by side here instead.
            var kept = new bool[DiceCount];
            var throwing = new bool[DiceCount];
            int marked = 0;
            for (int i = 0; i < DiceCount; i++)
            {
                kept[i] = !_rerollMarked[i];
                throwing[i] = _rerollMarked[i];
                if (throwing[i])
                    marked++;
            }

            if (marked == 0)
            {
                // Re-rolling all five would throw the whole hand away without a word of
                // confirmation. Say what is missing instead of doing it.
                _hint = "振り直したいダイスをクリックして指定してください。";
                LogInput("reroll_blocked", "nothing marked");
                RefreshBattle();
                return;
            }

            LogInput("reroll", "left=" + _engine.PlayerRerollsLeft + " marked=" + marked);

            bool rolled = false;
            try
            {
                _engine.SetKept(kept);
                _engine.Reroll();

                // The marks are a one-roll instruction, so they go. The declared hand is kept if
                // the new dice still qualify for it and falls back to the default if they do not
                // - the player's choice survives a re-roll instead of being silently rebuilt out
                // of a hand that no longer exists.
                for (int i = 0; i < DiceCount; i++)
                    _rerollMarked[i] = false;

                _yakuOffer = Yaku.Resolve(_engine.PlayerDice, _engine.Config, _yakuOffer.Kind);
                _hint = "";
                rolled = true;
                GameAudio.Play("dice_roll");
            }
            catch (Exception exception)
            {
                _hint = exception.Message;
            }

            RefreshBattle();

            // Only the dice the player gave up move. The kept ones stay perfectly still, and that
            // stillness is what tells you which ones survived the decision. A throw that was
            // refused by the rules never starts, so an error cannot hide the hand it is about.
            if (rolled)
                StartThrow(_playerDice, throwing);
        }

        /// <summary>
        /// A fresh hand: nothing marked, and one yaku already selected.
        ///
        /// The selection matters as much as the cleared marks. Because the hand is declarable the
        /// moment it exists, "確定する" is a valid first click and a round with no decisions costs
        /// exactly one input - the same promise the old default split made, kept in the new terms.
        /// </summary>
        private void ResetRoundState()
        {
            ResetDuelEffects();
            _empower = false;
            for (int i = 0; i < _rerollMarked.Length; i++)
                _rerollMarked[i] = false;

            _yakuOffer = _engine != null && _engine.PlayerDice != null
                ? (_engine.Config.TacticalEnabled ? DefaultReadyOffer(_engine.PlayerOffers()) :
                    Yaku.Default(_engine.PlayerDice, _engine.Config))
                : new YakuOffer(YakuKind.None, 0, 0, -1);
        }

        private void DoCommit()
        {
            if (ModalOpen || _engine == null || _committed)
                return;

            // Under the declared-yaku rules there is no split left to assemble and nothing that
            // can be half-built: the hand was declared the moment a yaku was selected, and the
            // yaku says where every die goes. The three rejections that used to live here
            // (unassigned die, one-sided split) have no meaning against these rules, and a
            // player could hit them with a hand that was already perfectly legal.
            int playerHpBefore = _engine.PlayerHp;
            int npcHpBefore = _engine.NpcHp;
            RoundOutcome outcome;

            try
            {
                outcome = _engine.CommitYaku(_yakuOffer.Kind, _engine.Config.TacticalEnabled && _empower);
            }
            catch (Exception exception)
            {
                _hint = exception.Message;
                LogInput("commit_rejected", _yakuOffer.Kind.ToString());
                RefreshBattle();
                return;
            }

            _committed = true;
            _hint = "";
            _banner = BuildBanner(outcome);
            LogInput("commit", "yaku=" + outcome.PlayerYaku +
                               " attack=" + outcome.PlayerAttackSum +
                               " guard=" + outcome.PlayerGuardSum +
                               " npcYaku=" + outcome.NpcYaku);

            // The reveal starts from where each side stood a moment ago, so the change is
            // something the eye follows instead of a number that merely appears elsewhere.
            StartHpTween(playerHpBefore, npcHpBefore);
            PlayImpact(outcome);

            // Sound follows the same numbers the banner prints, never the other way round.
            GameAudio.Play("commit");
            GameAudio.Play(outcome.DamageToNpc > 0 ? "hit" : "guard");
            if (outcome.DamageToPlayer > 0)
                GameAudio.Play("hurt");
            if (IsPrintedYaku(outcome.PlayerYaku) || IsPrintedYaku(outcome.NpcYaku))
                GameAudio.Play("formation");

            if (_engine.Outcome != null && _engine.Outcome.Result != MatchResult.Draw)
                GameAudio.Play(_engine.Outcome.Result == MatchResult.PlayerWin ? "win" : "lose");

            RefreshBattle();
        }

        private void DoAdvance()
        {
            if (ModalOpen || _engine == null || !_committed)
                return;

            LogInput("advance", _engine.Outcome != null ? "to_result" : "to_next_round");

            // Leaving a half-finished reveal behind would let the next screen inherit a number
            // the animation never reached.
            SnapHp();

            if (_engine.Outcome != null)
            {
                if (_run != null)
                {
                    if (_run.Phase == DuelRunPhase.Fighting) _run.CompleteFight(_engine);
                    if (_run.Phase == DuelRunPhase.ChoosingBlessing)
                    {
                        ShowTrialChoices();
                        return;
                    }
                }
                ShowResult();
                return;
            }

            _engine.NextRound();
            _engine.Roll();
            GameAudio.Play("dice_roll");

            _committed = false;
            _banner = "";
            _hint = "";
            ResetRoundState();
            RefreshBattle();
        }

        private string BuildBanner(RoundOutcome outcome)
        {
            // The headline alone ('you dealt 12') never explained where 12 came from, so the
            // arithmetic that produced it is printed underneath, on both sides, in the order it
            // is applied: the declared hand, its strike, the opposing ward, the damage.
            var builder = new StringBuilder();
            builder.Append("ラウンド").Append(outcome.Round).Append(" ─ あなた ");
            builder.Append(outcome.DamageToNpc);
            builder.Append(" ダメージ ／ NPC ");
            builder.Append(outcome.DamageToPlayer);
            builder.Append(" ダメージ");
            if (outcome.Pushback)
                builder.Append("　（押し勝ち 合計 ").Append(Scoring.Sum(outcome.PlayerDice))
                    .Append(" vs ").Append(Scoring.Sum(outcome.NpcDice)).Append("）");

            builder.Append('\n');
            AppendBreakdown(builder, "あなた",
                PatternLabel(outcome.PlayerYaku, outcome.PlayerFormation, outcome.PlayerFormationCashed),
                outcome.PlayerAttackSum, outcome.NpcGuardSum, outcome.PlayerBonus, outcome.DamageToNpc);
            builder.Append("　｜　振り直し ").Append(outcome.PlayerRerollsUsed).Append("回");

            builder.Append('\n');
            AppendBreakdown(builder, "NPC",
                PatternLabel(outcome.NpcYaku, outcome.NpcFormation, outcome.NpcFormationCashed),
                outcome.NpcAttackSum, outcome.PlayerGuardSum, outcome.NpcBonus, outcome.DamageToPlayer);
            builder.Append("　｜　").Append(outcome.NpcSummary);

            return builder.ToString();
        }

        /// <summary>
        /// The name of what a seat put forward: its declared yaku when one exists, otherwise the
        /// formation it happened to cash on the hand-driven path. Empty when neither, so a plain
        /// sum does not get a label invented for it.
        /// </summary>
        private static string PatternLabel(YakuKind yaku, Formation formation, bool cashed)
        {
            if (yaku != YakuKind.None)
                return Yaku.DisplayName(yaku);

            return cashed && formation.Exists ? formation.DisplayName() : "";
        }

        /// <summary>
        /// The yaku that pay a printed stake rather than a pip total. Worth marking out: they are
        /// the rare hands, and they are what a re-roll is being chased for.
        /// </summary>
        private static bool IsPrintedYaku(YakuKind kind)
        {
            return kind == YakuKind.SmallRun || kind == YakuKind.LargeRun || kind == YakuKind.Five;
        }

        private static void AppendBreakdown(StringBuilder builder, string who, string pattern,
            int attackSum, int opposingGuard, int bonus, int damage)
        {
            builder.Append(who).Append("：");
            if (pattern.Length > 0)
                builder.Append(pattern).Append(' ');
            builder.Append("攻 ").Append(attackSum);

            if (bonus > 0)
                builder.Append(" ＋ ").Append(bonus);

            builder.Append(" − 守 ").Append(opposingGuard);

            if (bonus > 0)
            {
                // The bonus lands outside the punch-through test, so show it as its own step.
                builder.Append(" ＝ ").Append(Scoring.Damage(attackSum, opposingGuard))
                    .Append(" ＋ ").Append(bonus);
            }

            builder.Append(" → ").Append(damage);
        }

        private void ShowResult()
        {
            MatchOutcome outcome = _engine.Outcome;
            switch (outcome.Result)
            {
                case MatchResult.PlayerWin:
                    _resultTitle.text = "勝利";
                    _resultTitle.color = UiFactory.Gold;
                    break;
                case MatchResult.NpcWin:
                    _resultTitle.text = "敗北";
                    _resultTitle.color = UiFactory.HpRed;
                    break;
                default:
                    _resultTitle.text = "引き分け";
                    _resultTitle.color = UiFactory.TextDim;
                    break;
            }

            _resultReason.text = outcome.Reason;

            _resultStats.text =
                "ラウンド数：" + outcome.Rounds + " / " + _engine.Config.MaxRounds + "\n" +
                "あなたの与ダメージ：" + _engine.PlayerDamageDealt +
                "　／　NPCの与ダメージ：" + _engine.NpcDamageDealt + "\n" +
                "対戦相手：" + NpcArchetypes.DisplayName(_opponent) +
                "（HP " + Mathf.Max(0, _engine.PlayerHp) + " - " + Mathf.Max(0, _engine.NpcHp) + "）";

            ShowScreen(Screen.Result);
            RefreshRunResult();
        }

        // ---------------------------------------------------------- reveal motion
        /// <summary>
        /// Advances the hit-point reveal. Nothing here can block or reject an input, and the
        /// values are always taken from the engine when the tween ends, so the animation can
        /// never disagree with the rules - it only decides how fast the screen catches up.
        /// </summary>
        private void Update()
        {
            if (OnlineActive) { UpdateOnline(); return; }
            if (ScoreDuelPresentationActive)
            {
                UpdateScoreDuelPresentation();
                return;
            }
            PollShortcuts();
            // The juice runs whether or not a health tween is in progress: a throw that starts
            // the same frame a reveal ends still has to land.
            float deltaTime = Time.deltaTime;
            TickJuice(deltaTime);
            TickDuelEffects(deltaTime);
            foreach (DiceView die in _practiceDice) die.Tick(deltaTime);

            if (_engine == null || !_tweening)
                return;

            _tweenElapsed += deltaTime;
            float t = HpTweenDuration <= 0f ? 1f : Mathf.Clamp01(_tweenElapsed / HpTweenDuration);
            // Ease-out: the number falls quickly and then settles, which reads as damage landing
            // rather than as a bar sliding at constant speed.
            float eased = 1f - (1f - t) * (1f - t);

            _shownPlayerHp = Mathf.Lerp(_tweenPlayerFrom, _engine.PlayerHp, eased);
            _shownNpcHp = Mathf.Lerp(_tweenNpcFrom, _engine.NpcHp, eased);

            if (t >= 1f)
                SnapHp();
            else
                ApplyShownHp();
        }

        private void StartHpTween(int fromPlayer, int fromNpc)
        {
            _tweenPlayerFrom = fromPlayer;
            _tweenNpcFrom = fromNpc;
            _shownPlayerHp = fromPlayer;
            _shownNpcHp = fromNpc;
            _tweenElapsed = 0f;
            _tweening = true;
        }

        /// <summary>Jump the reveal to its final state. Called before anything that reads HP.</summary>
        private void SnapHp()
        {
            _tweening = false;
            if (_engine == null || _shownPlayerHp < 0f)
                return;
            _shownPlayerHp = _engine.PlayerHp;
            _shownNpcHp = _engine.NpcHp;
            ApplyShownHp();
        }

        private void ApplyShownHp()
        {
            if (_engine == null || _shownPlayerHp < 0f)
                return;
            int max = _engine.Config.StartingHp;
            SetHp(_playerHpFill, _playerHpText, Mathf.RoundToInt(_shownPlayerHp), max);
            SetHp(_npcHpFill, _npcHpText, Mathf.RoundToInt(_shownNpcHp),
                _engine.Config.NpcStartingHp > 0 ? _engine.Config.NpcStartingHp : max);
        }

        // --------------------------------------------------------------- refresh
        private void RefreshBattle()
        {
            if (_engine == null)
                return;

            GameConfig config = _engine.Config;
            _roundLabel.text = "ラウンド " + _engine.Round + " / " + config.MaxRounds;
            _npcName.text = "NPC：" + NpcArchetypes.DisplayName(_opponent);
            _playerName.text = "あなた";

            ApplyShownHp();

            bool finished = _engine.Outcome != null;
            bool revealing = _committed || finished;

            // What this hand may declare. Read once: the dice tint, the button row, the sums and
            // the commit guard all have to agree on it, and a yaku the new dice no longer qualify
            // for would put an illegal hand behind a legal-looking screen.
            YakuOffer[] offers = _engine.PlayerOffers();
            if (!revealing && !HasOffer(offers, _yakuOffer.Kind))
                _yakuOffer = DefaultReadyOffer(offers);

            // One throw from a printed yaku. This is the whole appetite of the re-roll made
            // concrete, but it is only honest while a throw is still left to spend - naming
            // 大ストレート with no re-rolls remaining would be a hint that lies.
            YakuNearMiss[] nearMisses = NoNearMisses;
            int nearMask = 0;
            bool canThrow = !revealing &&
                            _engine.Phase == MatchPhase.AwaitingReroll &&
                            _engine.PlayerRerollsLeft > 0;
            if (canThrow)
            {
                YakuNearMiss[] all = Yaku.NearMisses(_engine.PlayerDice, config);
                if (config.TacticalEnabled)
                {
                    var readyTargets = new List<YakuNearMiss>();
                    foreach (var target in all) if (_engine.IsYakuReady(target.Kind)) readyTargets.Add(target);
                    all = readyTargets.ToArray();
                }
                // Two is the most the line has ever needed: three printed yaku are simultaneously
                // one throw away so rarely that a third entry would be dead width most of the time.
                int shown = Mathf.Min(all.Length, 2);
                if (shown > 0)
                {
                    nearMisses = new YakuNearMiss[shown];
                    for (int i = 0; i < shown; i++)
                    {
                        nearMisses[i] = all[i];
                        nearMask |= all[i].DieMask;
                    }
                }
            }

            // ---- player dice
            for (int i = 0; i < _playerDice.Count; i++)
            {
                DiceView view = _playerDice[i];
                int value = i < _engine.PlayerDice.Length ? _engine.PlayerDice[i] : 0;
                if (value <= 0)
                {
                    view.SetHidden();
                    view.SetInteractable(false);
                    continue;
                }

                view.SetValue(value);

                // The tint answers the two questions this screen is deciding: which dice the
                // selected yaku would send forward, and which are queued for the re-roll. Both
                // are visible at once, so there is no second mode to fall out of and no state
                // the player has to remember between clicks.
                DiceTint tint;
                if (!revealing && _rerollMarked[i])
                    tint = DiceTint.Reroll;
                else if ((_yakuOffer.AttackMask & (1 << i)) != 0)
                    tint = DiceTint.Attack;
                else
                    tint = DiceTint.Guard;

                view.SetTint(tint, (nearMask & (1 << i)) != 0);
                view.SetInteractable(!revealing);
            }

            // ---- NPC dice (hidden until the commit, so the reveal stays simultaneous)
            bool npcRevealed = _engine.NpcDice != null;
            for (int i = 0; i < _npcDice.Count; i++)
            {
                DiceView view = _npcDice[i];
                if (!npcRevealed)
                {
                    view.SetHidden();
                }
                else
                {
                    view.SetValue(i < _engine.NpcDice.Length ? _engine.NpcDice[i] : 0);
                    bool npcAttack = (i < 32) && ((_engine.NpcSplit.AttackMask >> i) & 1) != 0;
                    view.SetTint(npcAttack ? DiceTint.Attack : DiceTint.Guard, false);
                }
                view.SetInteractable(false);
            }

            // ---- info line: the hand's total, the declaration on the table and how many
            // re-rolls are left, all on screen for the whole round rather than hidden behind a
            // mode the player had to enter.
            if (!revealing)
            {
                int total = Scoring.Sum(_engine.PlayerDice);
                int marked = 0;
                for (int i = 0; i < _rerollMarked.Length; i++)
                {
                    if (_rerollMarked[i])
                        marked++;
                }

                var line = new StringBuilder(128);
                line.Append("合計 ").Append(total)
                    .Append("　／　選択中 ").Append(_yakuOffer.DisplayName())
                    .Append("（攻 ").Append(_yakuOffer.StrikeValue(_engine.PlayerDice))
                    .Append("・守 ").Append(_yakuOffer.WardValue(_engine.PlayerDice)).Append("）")
                    .Append("　／　前へ出す ").Append(_yakuOffer.ForwardCount).Append("個")
                    .Append("　／　振り直し残り ").Append(_engine.PlayerRerollsLeft).Append("回");
                if (marked > 0)
                    line.Append("　／　").Append(marked).Append("個を振り直す");

                _infoText.text = line.ToString();
                _infoText.color = IsPrintedYaku(_yakuOffer.Kind) ? UiFactory.Gold : UiFactory.Text;

                // A declaration always exists - 守護 and チャンス are unconditional - so the button
                // is never grey for lack of a complete hand. The hint below stays the place where
                // "why can't I do this" gets answered.
                UiFactory.SetButtonEnabled(_commitButton, true);
            }
            else
            {
                _infoText.text = "";
            }

            LayoutYakuButtons(offers, revealing);

            _bannerText.text = _banner;
            SetHintLine(nearMisses);
            _flowNote.text = BuildFlowNote(revealing);
            RefreshQuickActions(canThrow, nearMisses);

            // ---- buttons: one pair for the whole round, no second mode to enter or leave
            _rerollButton.gameObject.SetActive(!revealing);
            _commitButton.gameObject.SetActive(!revealing);
            _advanceButton.gameObject.SetActive(revealing);

            UiFactory.SetButtonEnabled(_rerollButton, !revealing &&
                                                      _engine.Phase == MatchPhase.AwaitingReroll &&
                                                      _engine.PlayerRerollsLeft > 0);

            if (revealing)
            {
                var caption = _advanceButton.GetComponentInChildren<Text>();
                if (caption != null)
                    caption.text = finished ? "勝敗を確認" : "次のラウンド";

                var face = _advanceButton.GetComponent<Image>();
                if (face != null)
                    face.color = finished ? UiFactory.Gold : UiFactory.ButtonFace;
                if (caption != null)
                    caption.color = finished ? UiFactory.FromHex(0x24, 0x1E, 0x10) : UiFactory.Text;
            }
            RefreshDuelPresentation(revealing);
        }

        private static bool HasOffer(YakuOffer[] offers, YakuKind kind)
        {
            for (int i = 0; i < offers.Length; i++)
            {
                if (offers[i].Kind == kind)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// The line under the buttons: an error when there is one, and otherwise the best offer
        /// still on the table. The offer is gold because that is the only register this screen
        /// has for "this could happen" - the same gold that marks the dice it needs.
        /// </summary>
        private void SetHintLine(YakuNearMiss[] nearMisses)
        {
            if (_hint.Length > 0)
            {
                _hintText.text = _hint;
                _hintText.color = UiFactory.TextDim;
                return;
            }

            if (nearMisses == null || nearMisses.Length == 0)
            {
                _hintText.text = "";
                _hintText.color = UiFactory.TextDim;
                return;
            }

            var line = new StringBuilder(96);
            for (int i = 0; i < nearMisses.Length; i++)
            {
                if (i > 0)
                    line.Append("　");
                line.Append(nearMisses[i].Hint());
            }

            _hintText.text = line.ToString();
            _hintText.color = UiFactory.Gold;
        }

        /// <summary>One line telling the player what this screen is for, rewritten every refresh.</summary>
        private string BuildFlowNote(bool revealing)
        {
            if (revealing)
                return "公開結果です。「次のラウンド」で続けます。";

            int marked = 0;
            for (int i = 0; i < _rerollMarked.Length; i++)
            {
                if (_rerollMarked[i])
                    marked++;
            }

            if (_engine.Phase == MatchPhase.AwaitingReroll && _engine.PlayerRerollsLeft > 0)
            {
                if (marked > 0)
                    return marked + "個を振り直せます。役を選び直しても構いません。「確定する」で同時に公開。";

                return "ダイスをクリックして振り直しを指定、役を1つ選んで「確定する」。" +
                       "もう一度の判断もまだ残っています。";
            }

            return "役を1つ選んで「確定する」。守護とチャンスはどんな手でも選べます。";
        }

        private static void SetHp(Image fill, Text label, int hp, int maxHp)
        {
            float ratio = Mathf.Clamp01(hp / (float)maxHp);
            fill.fillAmount = ratio;
            fill.color = ratio > 0.5f ? UiFactory.HpGreen : (ratio > 0.25f ? UiFactory.Gold : UiFactory.HpRed);
            label.text = "HP " + Mathf.Max(0, hp) + " / " + maxHp;
        }
    }
}
