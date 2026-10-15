using System.Collections.Generic;
using System.Text;
using ParvaMirabilia.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ParvaMirabilia.Presentation
{
    public sealed partial class GameFlow
    {
        private GameObject _canvasRoot;
        private Screen _screen;
        private RectTransform _menuRoot, _guideRoot, _journalRoot;
        private Text _menuTitle, _menuNote, _guideTitle, _guideBody, _guideFeedback, _journalBody;
        private Button _menuReturn, _menuQuit, _guideNext, _quickLow, _quickAim;
        private bool _confirmReturn, _confirmQuit;
        private int _guideStep;
        private readonly int[] _practiceHand = { 1, 2, 3, 4, 6 };
        private readonly List<DiceView> _practiceDice = new List<DiceView>();
        private readonly List<Button> _practiceChoices = new List<Button>();
        private bool _practiceMarked;
        private bool _practiceBoost;
        private YakuOffer _practiceOffer;
        private YakuNearMiss[] _quickTargets = NoNearMisses;
        private int _shortcutSuppressedFrame = -1;
        private static readonly KeyCode[] ShortcutKeys = { KeyCode.Escape, KeyCode.Tab, KeyCode.R,
            KeyCode.H, KeyCode.Q, KeyCode.Space, KeyCode.Return, KeyCode.KeypadEnter,
            KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5 };

        private bool ModalOpen => (_rulesRoot != null && _rulesRoot.gameObject.activeSelf) ||
            (_creditsRoot != null && _creditsRoot.gameObject.activeSelf) ||
            (_runRoot != null && _runRoot.gameObject.activeSelf) ||
            (_menuRoot != null && _menuRoot.gameObject.activeSelf) ||
            (_guideRoot != null && _guideRoot.gameObject.activeSelf) ||
            (_journalRoot != null && _journalRoot.gameObject.activeSelf);

        private void BuildExperience()
        {
            _quickLow = UiFactory.CreateButton("QuickLow", _battleRoot, new Vector2(210, 54),
                new Vector2(-485, 0), "1〜3を一括指定", 18, MarkLowDice);
            _quickAim = UiFactory.CreateButton("QuickAim", _battleRoot, new Vector2(210, 66),
                new Vector2(485, 0), "役を狙う", 17, MarkNearMiss);
            UiFactory.CreateButton("BattleHistory", _battleRoot, new Vector2(120, 40),
                new Vector2(415, 302), "対戦履歴", 18, OpenJournal);
            UiFactory.CreateButton("ResultHistory", _resultRoot, new Vector2(200, 40),
                new Vector2(0, -248), "対戦を振り返る", 18, OpenJournal);
            UiFactory.CreateButton("Menu", _canvasRoot.transform, new Vector2(150, 40),
                new Vector2(-555, -336), "メニュー Esc", 17, OpenMenu);

            _menuRoot = MakeModal("MenuOverlay", new Vector2(700, 520));
            _menuTitle = Label(_menuRoot, "MenuTitle", "メニュー", 32, 196, 600, 52);
            _menuNote = Label(_menuRoot, "MenuNote", "", 19, 128, 600, 66);
            Button resume = UiFactory.CreateButton("Resume", _menuRoot, new Vector2(360, 52),
                new Vector2(0, 46), "ゲームに戻る", 21, CloseExperienceOverlays);
            UiFactory.StylePrimary(resume);
            UiFactory.CreateButton("MenuLearn", _menuRoot, new Vector2(360, 48),
                new Vector2(0, -14), "遊び方を練習", 20, OpenGuide);
            _menuReturn = UiFactory.CreateButton("ReturnTitle", _menuRoot, new Vector2(360, 48),
                new Vector2(0, -72), "タイトルへ戻る", 20, ReturnToTitle);
            _menuQuit = UiFactory.CreateButton("Quit", _menuRoot, new Vector2(360, 48),
                new Vector2(0, -130), "ゲームを終了", 20, RequestQuit);
            Label(_menuRoot, "KeyboardHelp", "1〜5：ダイス指定　Space：振り直し　Tab・矢印：ボタン選択\nEnter：選択を実行　Q：低い目　R：ルール　H：履歴　Esc：戻る", 16, -208, 640, 52);

            _journalRoot = MakeModal("JournalOverlay", new Vector2(1120, 650));
            Label(_journalRoot, "JournalTitle", "決闘の記録", 30, 278, 1000, 46);
            _journalBody = UiFactory.CreateText("JournalBody", _journalRoot, "", 17,
                TextAnchor.UpperLeft, UiFactory.Text, new Vector2(0, 5), new Vector2(1020, 470));
            UiFactory.CreateButton("CloseJournal", _journalRoot, new Vector2(260, 46),
                new Vector2(0, -280), "ゲームに戻る", 20, CloseExperienceOverlays);

            _guideRoot = MakeModal("GuideOverlay", new Vector2(1080, 610));
            _guideTitle = Label(_guideRoot, "GuideTitle", "", 30, 246, 980, 48);
            _guideBody = Label(_guideRoot, "GuideBody", "", 20, 163, 950, 92);
            CreateDiceRow(_practiceDice, _guideRoot, "PracticeDie", new Vector2(0, 34), false);
            for (int i = 0; i < _practiceDice.Count; i++)
                _practiceDice[i].Clicked += OnPracticeDie;
            _guideFeedback = Label(_guideRoot, "GuideFeedback", "", 20, -136, 960, 92);
            _guideNext = UiFactory.CreateButton("GuideNext", _guideRoot, new Vector2(300, 52),
                new Vector2(170, -240), "次へ", 21, AdvanceGuide);
            UiFactory.StylePrimary(_guideNext);
            UiFactory.CreateButton("CloseGuide", _guideRoot, new Vector2(250, 52),
                new Vector2(-170, -240), "練習を閉じる", 20, CloseExperienceOverlays);
            CloseExperienceOverlays();
        }

        private RectTransform MakeModal(string name, Vector2 size)
        {
            RectTransform root = UiFactory.CreateStretch(name, _canvasRoot.transform);
            Image blocker = UiFactory.CreateStretchPanel("Blocker", root, new Color(0, 0, 0, 0.8f));
            blocker.raycastTarget = true;
            Image panel = UiFactory.CreatePanel("Panel", root, size, Vector2.zero, UiFactory.Panel);
            UiFactory.AddDropShadow(panel, new Vector2(0, -8), 0.5f);
            UiFactory.AddGloss(panel, (int)size.x, (int)size.y);
            return root;
        }

        private static Text Label(Transform root, string name, string text, int fontSize, float y,
            float width, float height) => UiFactory.CreateText(name, root, text, fontSize,
                TextAnchor.MiddleCenter, UiFactory.Text, new Vector2(0, y), new Vector2(width, height));

        private static void Caption(Button button, string text) => button.GetComponentInChildren<Text>().text = text;

        private void CloseExperienceOverlays()
        {
            _shortcutSuppressedFrame = Time.frameCount;
            if (_menuRoot != null) _menuRoot.gameObject.SetActive(false);
            if (_guideRoot != null) _guideRoot.gameObject.SetActive(false);
            if (_journalRoot != null) _journalRoot.gameObject.SetActive(false);
            if (_creditsRoot != null) _creditsRoot.gameObject.SetActive(false);
            _confirmReturn = _confirmQuit = false;
            ClearKeyboardSelection();
        }

        private void OpenMenu()
        {
            if (ModalOpen)
                return;
            _menuTitle.text = "メニュー";
            _menuNote.text = "ダイスも相手も、あなたの判断を待っています。";
            Caption(_menuReturn, "タイトルへ戻る");
            Caption(_menuQuit, "ゲームを終了");
            _menuReturn.gameObject.SetActive(_screen != Screen.Title);
            _menuRoot.gameObject.SetActive(true);
            SelectDefault(_menuRoot, "Resume");
            GameAudio.Play("ui_click");
        }

        private void ReturnToTitle()
        {
            if (_screen == Screen.Battle && !_confirmReturn)
            {
                _confirmReturn = true;
                _confirmQuit = false;
                _menuNote.text = "途中の対戦は保存されません。タイトルへ戻りますか？";
                Caption(_menuReturn, "対戦を終了してタイトルへ");
                return;
            }
            SnapHp();
            _engine = null;
            _run = null;
            _committed = false;
            ShowScreen(Screen.Title);
        }

        private void RequestQuit()
        {
            if (!_confirmQuit)
            {
                _confirmQuit = true;
                _confirmReturn = false;
                _menuNote.text = "ゲームを終了しますか？ 途中の対戦は保存されません。";
                Caption(_menuQuit, "終了する（もう一度押す）");
                return;
            }
            InputLog.Close();
            Application.Quit();
        }

        private void RefreshQuickActions(bool canThrow, YakuNearMiss[] targets)
        {
            _quickTargets = targets ?? NoNearMisses;
            _quickLow.gameObject.SetActive(canThrow);
            _quickAim.gameObject.SetActive(canThrow && _quickTargets.Length > 0);
            bool low = false;
            foreach (int die in _engine.PlayerDice) low |= die <= 3;
            UiFactory.SetButtonEnabled(_quickLow, canThrow && low);
            if (_quickTargets.Length > 0)
                Caption(_quickAim, _quickTargets[0].DisplayName() + "を狙う\n振り直す1個を指定");
        }

        private void MarkLowDice()
        {
            if (ModalOpen || _engine == null || _committed || _engine.PlayerRerollsLeft <= 0) return;
            for (int i = 0; i < DiceCount; i++) _rerollMarked[i] = _engine.PlayerDice[i] <= 3;
            _hint = "1〜3のダイスを指定しました。変更してから振り直しても構いません。";
            LogInput("quick_low", "faces <= 3");
            GameAudio.Play("die_keep");
            RefreshBattle();
        }

        private void MarkNearMiss()
        {
            if (ModalOpen || _engine == null || _committed || _engine.PlayerRerollsLeft <= 0 ||
                _quickTargets.Length == 0) return;
            YakuNearMiss target = _quickTargets[0];
            for (int i = 0; i < DiceCount; i++) _rerollMarked[i] = i == target.ExampleDie;
            _hint = target.DisplayName() + "を狙う1個を指定。必要な目は" + target.ExampleFace + "、成功は保証されません。";
            LogInput("quick_aim", "kind=" + target.Kind + " die=" + target.ExampleDie);
            GameAudio.Play("die_keep");
            RefreshBattle();
        }

        private void OpenJournal()
        {
            _journalRoot.Find("JournalTitle").GetComponent<Text>().text = "決闘の記録";
            if (_engine == null) return;
            CloseExperienceOverlays();
            _rulesRoot.gameObject.SetActive(false);
            var text = new StringBuilder();
            text.AppendLine("あなた　　　　　　　　　　　　　　　　相手：" + NpcArchetypes.DisplayName(_opponent));
            if (_engine.History.Count == 0)
                text.Append("公開結果はまだありません。確定後に役と攻守の内訳を記録します。\n" +
                    (_engine.Config.TacticalEnabled ? "相手の役と攻守は予告済みです。出目は確定後に公開されます。" :
                    "現在の相手の手は公開されるまで見えません。"));
            foreach (RoundOutcome round in _engine.History)
            {
                text.Append("R").Append(round.Round).Append("　あなた：").Append(Yaku.DisplayName(round.PlayerYaku))
                    .Append(" 攻").Append(round.PlayerAttackSum).Append(" 守").Append(round.PlayerGuardSum)
                    .Append("　／　相手：").Append(Yaku.DisplayName(round.NpcYaku))
                    .Append(" 攻").Append(round.NpcAttackSum).Append(" 守").Append(round.NpcGuardSum).AppendLine();
                text.Append("　　 与えたダメージ ").Append(round.DamageToNpc).Append("　受けたダメージ ")
                    .Append(round.DamageToPlayer).Append(round.Pushback ? "　［押し勝ち］" : "").AppendLine();
            }
            _journalBody.text = text.ToString();
            _journalRoot.gameObject.SetActive(true);
            SelectDefault(_journalRoot, "CloseJournal");
        }

        private void OpenGuide()
        {
            CloseExperienceOverlays();
            _rulesRoot.gameObject.SetActive(false);
            _guideStep = 0;
            _practiceMarked = false;
            _practiceBoost = false;
            _practiceHand[4] = 6;
            _practiceOffer = Yaku.Default(_practiceHand, ResolveConfig());
            _guideRoot.gameObject.SetActive(true);
            RefreshGuide();
            SelectDefault(_guideRoot, "CloseGuide");
        }

        private void OnPracticeDie(int index)
        {
            if (_guideStep != 0 || index != 4) return;
            _practiceMarked = !_practiceMarked;
            _practiceDice[4].Punch();
            RefreshGuide();
        }

        private void AdvanceGuide()
        {
            if (_guideStep == 0 && !_practiceMarked) return;
            if (_guideStep >= (ResolveConfig().TacticalEnabled ? 3 : 2))
            {
                CloseExperienceOverlays();
                if (_screen == Screen.Title) StartMatch();
                return;
            }
            _guideStep++;
            if (_guideStep == 1)
            {
                _practiceHand[4] = 5;
                _practiceMarked = false;
                _practiceOffer = Yaku.Resolve(_practiceHand, ResolveConfig(), YakuKind.SmallRun);
                _practiceDice[4].Punch();
                GameAudio.Play("dice_roll");
            }
            if (_guideStep == 3)
            {
                _practiceOffer = Yaku.Resolve(_practiceHand, ResolveConfig(), YakuKind.LargeRun);
                _practiceBoost = false;
            }
            RefreshGuide();
        }

        private void RefreshGuide()
        {
            GameConfig config = ResolveConfig();
            _guideTitle.text = new[] { "1　残したい目を決める", "2　攻めるか、守りを残すか", "3　結果を読む", "4　次の一手を残す" }[_guideStep];
            _guideBody.text = new[] {
                "1・2・3・4を残せば、5が出たとき大ストレート。\n右端の「6」をクリックして、振り直す1個を指定してみましょう。",
                "練習では5に変わります。本番の振り直しは公平な1〜6の乱数です。\n同じ手でも役で攻守が変わります。下の役を選んで比べてください。",
                "練習相手はペア：攻12・守9。攻撃が守りを超えた分だけHPを減らします。\n本番では相手が先に行動を固定し、攻撃と守りを予告します。",
                "大ストレートを、魔力1で強化するか温存するか比べます（練習では振り直しを1回残しています）。\n使った役は次Rだけ休み、チャンスは連続使用できます。"
            }[_guideStep];
            for (int i = 0; i < DiceCount; i++)
            {
                _practiceDice[i].SetValue(_practiceHand[i]);
                _practiceDice[i].SetInteractable(_guideStep == 0 && i == 4);
                DiceTint tint = _guideStep == 0
                    ? (i == 4 && _practiceMarked ? DiceTint.Reroll : DiceTint.Plain)
                    : ((_practiceOffer.AttackMask & (1 << i)) != 0 ? DiceTint.Attack : DiceTint.Guard);
                _practiceDice[i].SetTint(tint, false);
            }
            foreach (Button choice in _practiceChoices) Destroy(choice.gameObject);
            _practiceChoices.Clear();
            if (_guideStep == 1)
            {
                YakuKind[] kinds = { YakuKind.Guard, YakuKind.SmallRun, YakuKind.LargeRun };
                for (int i = 0; i < kinds.Length; i++)
                {
                    YakuOffer offer = Yaku.Resolve(_practiceHand, config, kinds[i]);
                    Button choice = UiFactory.CreateButton("PracticeYaku" + i, _guideRoot,
                        new Vector2(240, 56), new Vector2((i - 1) * 254, -54),
                        offer.DisplayName() + "　攻" + offer.StrikeValue(_practiceHand) + " 守" + offer.WardValue(_practiceHand),
                        18, () => { _practiceOffer = offer; RefreshGuide(); });
                    UiFactory.StyleSelected(choice, offer.Kind == _practiceOffer.Kind);
                    _practiceChoices.Add(choice);
                }
            }
            if (_guideStep == 3)
            {
                for (int option = 0; option < 2; option++)
                {
                    bool boost = option == 1;
                    Button choice = UiFactory.CreateButton("PracticePower" + option, _guideRoot,
                        new Vector2(310, 56), new Vector2(option == 0 ? -170 : 170, -54),
                        boost ? "魔力1で攻 ＋" + config.EmpowerAttack : "魔力を温存", 21,
                        () => { _practiceBoost = boost; RefreshGuide(); });
                    UiFactory.StyleSelected(choice, _practiceBoost == boost);
                    _practiceChoices.Add(choice);
                }
            }
            if (_guideStep == 0)
                _guideFeedback.text = _practiceMarked ? "「振」マークが指定の合図。残り4個はそのまま残ります。" : "本番でも、もう一度クリックすれば指定を解除できます。";
            else if (_guideStep == 1)
                _guideFeedback.text = _practiceOffer.DisplayName() + "：攻" + _practiceOffer.StrikeValue(_practiceHand) +
                    "・守" + _practiceOffer.WardValue(_practiceHand) + "\n全部前へ出す大ストレートは、強い攻撃と引き換えに守りが0になります。";
            else
            {
                int dealt = Scoring.Damage(_practiceOffer.StrikeValue(_practiceHand), 9);
                int taken = Scoring.Damage(12, _practiceOffer.WardValue(_practiceHand));
                Side? push = Scoring.ApplyPushback(Scoring.Sum(_practiceHand), 21,
                    config.PushbackDamage, ref dealt, ref taken);
                _guideFeedback.text = _practiceOffer.DisplayName() + "：与えるダメージ " + dealt +
                    "　／　受けるダメージ " + taken + (push.HasValue ? "［相手の押し勝ち］" : "") +
                    (config.TacticalEnabled ? "\n予告の攻守は本番でも比較できます。出目の合計だけは公開後に分かります。" :
                    "\n本番は相手の手が公開まで見えません。HPと役を見て、あなたの方針を選びましょう。");
            }
            Caption(_guideNext, _guideStep == 0 ? "指定した1個を振り直す" :
                _guideStep == 1 ? "この役で公開する" : _guideStep == 2 && config.TacticalEnabled ? "魔力と役の回復へ" :
                _screen == Screen.Title ? "決闘を始める" : "対戦に戻る");
            UiFactory.SetButtonEnabled(_guideNext, _guideStep != 0 || _practiceMarked);
            if (_guideStep == 3)
            {
                int attack = _practiceOffer.StrikeValue(_practiceHand) + (_practiceBoost ? config.EmpowerAttack : 0);
                _guideFeedback.text = "攻 " + attack + " − 相手の守9 → 与えるダメージ " + Scoring.Damage(attack, 9) +
                    "　／　受けるダメージ 12\n確定後の魔力 " + (_practiceBoost ? 1 : 2) +
                    "。振り直しを残す価値と、今の一撃の価値を比べましょう。";
            }
        }

        private void PollShortcuts()
        {
            if (_shortcutSuppressedFrame == Time.frameCount) return;
            // Consume one action per frame. A key that opened a modal cannot also commit a hand.
            foreach (KeyCode key in ShortcutKeys)
                if (Input.GetKeyDown(key)) { HandleShortcut(key); break; }
        }

        private void HandleShortcut(KeyCode key)
        {
            if (_shortcutSuppressedFrame == Time.frameCount &&
                (key == KeyCode.Return || key == KeyCode.KeypadEnter || key == KeyCode.Space)) return;
            if (key == KeyCode.Escape)
            {
                if (_runRoot != null && _runRoot.gameObject.activeSelf) { CancelTrial(); return; }
                if (ModalOpen) { _rulesRoot.gameObject.SetActive(false); CloseExperienceOverlays(); }
                else OpenMenu();
                return;
            }
            if (key == KeyCode.Tab) { SelectNextButton(); return; }
            if (ModalOpen) return;
            if (key == KeyCode.R) { SetRulesVisible(true); return; }
            if (key == KeyCode.H) { OpenJournal(); return; }
            if (_screen != Screen.Battle) return;
            // StandaloneInputModule owns Enter when a UI button is focused. Do not execute both
            // that button's submit event and the global commit shortcut on the same press.
            if (EventSystemHasSelection() && (key == KeyCode.Return || key == KeyCode.KeypadEnter)) return;
            if ((int)key >= (int)KeyCode.Alpha1 && (int)key <= (int)KeyCode.Alpha5)
            {
                ClearKeyboardSelection();
                OnPlayerDieClicked((int)key - (int)KeyCode.Alpha1);
            }
            else if (key == KeyCode.Q) MarkLowDice();
            else if (key == KeyCode.Space) DoReroll();
            else if (key == KeyCode.Return || key == KeyCode.KeypadEnter)
            {
                if (_committed) DoAdvance(); else DoCommit();
            }
        }

        private static bool EventSystemHasSelection() => UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject != null;

        private static void ClearKeyboardSelection()
        {
            var events = UnityEngine.EventSystems.EventSystem.current;
            if (events != null) events.SetSelectedGameObject(null);
        }

        private static void SelectDefault(Transform root, string name)
        {
            var events = UnityEngine.EventSystems.EventSystem.current;
            var target = root.Find(name);
            if (events != null && target != null) events.SetSelectedGameObject(target.gameObject);
        }

        private void SelectNextButton()
        {
            var events = UnityEngine.EventSystems.EventSystem.current;
            if (events == null) return;
            Transform scope = _runRoot != null && _runRoot.gameObject.activeSelf ? _runRoot :
                _creditsRoot != null && _creditsRoot.gameObject.activeSelf ? _creditsRoot :
                _guideRoot.gameObject.activeSelf ? _guideRoot :
                _journalRoot.gameObject.activeSelf ? _journalRoot : _menuRoot.gameObject.activeSelf ? _menuRoot :
                _rulesRoot.gameObject.activeSelf ? _rulesRoot : _canvasRoot.transform;
            var buttons = new List<Button>();
            foreach (Button button in scope.GetComponentsInChildren<Button>())
                if (button.isActiveAndEnabled && button.IsInteractable()) buttons.Add(button);
            if (buttons.Count == 0) return;
            int current = buttons.FindIndex(button => button.gameObject == events.currentSelectedGameObject);
            events.SetSelectedGameObject(buttons[(current + 1) % buttons.Count].gameObject);
        }

        private void OnDestroy()
        {
            InputLog.Close();
            if (_canvasRoot != null) Destroy(_canvasRoot);
        }

        private void OnApplicationQuit() => InputLog.Close();
    }
}
