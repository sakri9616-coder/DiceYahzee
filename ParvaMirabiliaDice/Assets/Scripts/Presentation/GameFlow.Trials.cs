using System;
using System.Text;
using ParvaMirabilia.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ParvaMirabilia.Presentation
{
    public sealed partial class GameFlow
    {
        private DuelRun _run;
        private RectTransform _runRoot;
        private Button _blessingsButton;
        private Text _runTitle, _runStatus;
        private bool _confirmTrialExit;
        private readonly Text[] _boonNames = new Text[3], _boonDescriptions = new Text[3];
        private readonly Image[] _boonIcons = new Image[3];
        private static readonly string[] BlessingIcons = { "crossed-swords", "burning-embers", "comet-spark",
            "crystal-ball", "crowned-explosion", "edged-shield", "fire-silhouette", "crystal-ball", "crowned-explosion" };

        private void BuildTrials()
        {
            var footnote = _titleRoot.Find("Footnote").GetComponent<Text>();
            footnote.rectTransform.anchoredPosition = new Vector2(0, -332);
            footnote.fontSize = 13;
            Button trials = UiFactory.CreateButton("Trials", _titleRoot, new Vector2(540, 44),
                new Vector2(0, -292), "三連決闘  ·  祝福を選び、体力を残して3人を倒す", 18, StartTrial);
            ResizeDuelButton(trials, new Vector2(540, 44));
            StyleDuelChoice(trials, false);
            DuelArt.Frame(trials.transform, new Vector2(540, 44), UiFactory.GoldDim);
            _blessingsButton = UiFactory.CreateButton("Blessings", _battleRoot, new Vector2(206, 56),
                new Vector2(-485, -286), "祝福の効果を見る", 18, OpenBlessings);
            ResizeDuelButton(_blessingsButton, new Vector2(206, 56));
            StyleDuelChoice(_blessingsButton, false);
            DuelArt.Frame(_blessingsButton.transform, new Vector2(206, 56), UiFactory.GoldDim);
            _blessingsButton.gameObject.SetActive(false);
            _runRoot = MakeModal("TrialChoices", new Vector2(1100, 620));
            _runRoot.Find("Panel").GetComponent<Image>().color = DuelPanel;
            _runRoot.Find("Panel/Gloss").gameObject.SetActive(false);
            _runTitle = UiFactory.CreateText("RunTitle", _runRoot, "祝福を選ぶ", 34,
                TextAnchor.MiddleCenter, UiFactory.Gold, new Vector2(0, 243), new Vector2(1010, 52));
            _runStatus = UiFactory.CreateText("RunStatus", _runRoot, "", 19,
                TextAnchor.MiddleCenter, UiFactory.Text, new Vector2(0, 158), new Vector2(1010, 94));
            for (int i = 0; i < 3; i++)
            {
                int selected = i;
                Button choice = UiFactory.CreateButton("Blessing" + i, _runRoot, new Vector2(302, 244),
                    new Vector2((i - 1) * 322, -27), "選んで決闘へ", 19, () => PickBlessing(selected));
                ResizeDuelButton(choice, new Vector2(302, 244));
                StyleDuelChoice(choice, false);
                var label = choice.GetComponentInChildren<Text>();
                label.rectTransform.anchoredPosition = new Vector2(0, -93);
                label.rectTransform.sizeDelta = new Vector2(290, 34);
                label.color = UiFactory.Gold;
                _boonIcons[i] = DuelArt.Icon(choice.transform, "BoonSeal", "crystal-ball", new Vector2(0, 75), 55, UiFactory.Gold);
                _boonNames[i] = UiFactory.CreateText("BoonName", choice.transform, "", 24,
                    TextAnchor.MiddleCenter, UiFactory.Text, new Vector2(0, 21), new Vector2(282, 35));
                _boonDescriptions[i] = UiFactory.CreateText("BoonDescription", choice.transform, "", 17,
                    TextAnchor.MiddleCenter, UiFactory.TextDim, new Vector2(0, -32), new Vector2(276, 70));
                DuelArt.Frame(choice.transform, new Vector2(302, 244), UiFactory.GoldDim);
            }
            UiFactory.CreateText("RunHelp", _runRoot, "", 16, TextAnchor.MiddleCenter,
                UiFactory.TextDim, new Vector2(0, -190), new Vector2(1000, 50));
            Button cancel = UiFactory.CreateButton("CancelTrial", _runRoot, new Vector2(260, 44),
                new Vector2(0, -264), "試練を終了して戻る", 18, CancelTrial);
            ResizeDuelButton(cancel, new Vector2(260, 44));
            StyleDuelChoice(cancel, false);
            Button rematch = _resultRoot.Find("Rematch").GetComponent<Button>();
            rematch.onClick.RemoveAllListeners();
            rematch.onClick.AddListener(() => { if (_run != null) StartTrial(); else StartMatch(); });
            _runRoot.gameObject.SetActive(false);
        }

        private void StartTrial()
        {
            if (ModalOpen) return;
            _run = new DuelRun(new SeededRandom(FreshSeed()));
            ShowTrialChoices();
        }

        private void OpenBlessings()
        {
            if (_run == null) return;
            CloseExperienceOverlays();
            _journalRoot.Find("JournalTitle").GetComponent<Text>().text = "この挑戦の祝福";
            var text = new StringBuilder("選んだ効果は、この三連決闘を通して続きます。\n\n");
            foreach (DuelBlessing blessing in _run.Blessings)
                text.Append(DuelRun.Name(blessing)).Append("\n").Append(DuelRun.Description(blessing)).Append("\n\n");
            text.Append("残りHPを持ち越し、1・2戦目の勝利後に16回復。\n役カードとダメージ比較には、祝福の効果が含まれています。");
            _journalBody.text = text.ToString();
            _journalRoot.gameObject.SetActive(true);
            SelectDefault(_journalRoot, "CloseJournal");
        }

        private string RunBlessingSummary()
        {
            var text = new StringBuilder("祝福：");
            foreach (DuelBlessing blessing in _run.Blessings) text.Append(DuelRun.Name(blessing)).Append("  ");
            return text.Append("· 効果は左のボタンで確認").ToString();
        }

        private void ShowTrialChoices()
        {
            CloseExperienceOverlays();
            _runRoot.gameObject.SetActive(true);
            _confirmTrialExit = false;
            Caption(_runRoot.Find("CancelTrial").GetComponent<Button>(), "試練を終了して戻る");
            _runTitle.text = _run.Fight == 1 ? "三連決闘  /  最初の祝福" : "決闘を越えて  /  次の祝福";
            _runStatus.text = "決闘 " + _run.Fight + " / 3   ·   あなた HP " + _run.Life + " / " + _run.MaxLife +
                "\n次の相手：" + OpponentTitles[(int)_run.Opponent] + "   HP " + _run.OpponentLife;
            for (int i = 0; i < 3; i++)
            {
                DuelBlessing blessing = _run.Offers[i];
                _boonNames[i].text = DuelRun.Name(blessing);
                _boonDescriptions[i].text = DuelRun.Description(blessing);
                _boonIcons[i].sprite = DuelArt.Load("Icons/" + BlessingIcons[(int)blessing]);
            }
            var build = new StringBuilder("選んだ祝福は3戦を通して続きます。体力は持ち越し、勝利後にHP16回復。\n");
            if (_run.Blessings.Count > 0)
            {
                build.Append("現在の祝福：");
                foreach (DuelBlessing blessing in _run.Blessings) build.Append(DuelRun.Name(blessing)).Append("  ");
            }
            else build.Append("敗北・引き分けで挑戦終了。強化を選び、自分の方針を組み立てましょう。");
            _runRoot.Find("RunHelp").GetComponent<Text>().text = build.ToString();
            SelectDefault(_runRoot, "Blessing0");
        }

        private void PickBlessing(int index)
        {
            if (_run == null || !_runRoot.gameObject.activeSelf || _run.Phase != DuelRunPhase.ChoosingBlessing) return;
            _run.ChooseBlessing(index);
            _runRoot.gameObject.SetActive(false);
            _shortcutSuppressedFrame = Time.frameCount;
            ClearKeyboardSelection();
            StartMatch();
        }

        private void CancelTrial()
        {
            if (_run != null && _run.Fight > 1 && !_confirmTrialExit)
            {
                _confirmTrialExit = true;
                _runStatus.text = "試練を終了しますか？ 選んだ祝福と途中の進行は失われます。";
                Caption(_runRoot.Find("CancelTrial").GetComponent<Button>(), "試練を終了（もう一度）");
                return;
            }
            _runRoot.gameObject.SetActive(false);
            _run = null;
            _engine = null;
            ShowScreen(Screen.Title);
            _shortcutSuppressedFrame = Time.frameCount;
        }

        private void RefreshRunResult()
        {
            if (_runRoot == null) return;
            Caption(_resultRoot.Find("Rematch").GetComponent<Button>(), _run != null ? "もう一度三連決闘" : "もう一度勝負");
            _resultRoot.Find("ResultFootnote").GetComponent<Text>().text = _run != null ?
                "新しい祝福、新しいダイスで、別の戦い方を。" : "再戦は毎回、新しいサイコロで始まります。";
            bool won = _run != null ? _run.Phase == DuelRunPhase.Cleared : _engine.Outcome.Result == MatchResult.PlayerWin;
            _resultSeal.sprite = DuelArt.Load("Icons/" + (won ? "crowned-explosion" : "edged-shield"));
            _resultSeal.color = won ? UiFactory.Gold : SealColors[(int)_opponent];
            if (_run == null) return;
            _resultTitle.text = _run.Phase == DuelRunPhase.Cleared ? "三連決闘 制覇" : "挑戦終了";
            _resultReason.text = _run.Phase == DuelRunPhase.Cleared ? "選んだ祝福と五つのダイスで、3人を越えた。" :
                "第" + _run.Fight + "戦で終了。別の祝福で、別の一手を。";
            var text = new StringBuilder();
            text.Append("合計 ").Append(_run.TotalRounds).Append("ラウンド   ·   与ダメージ ").Append(_run.TotalDamage)
                .Append("   ·   残りHP ").Append(_run.Life).AppendLine();
            foreach (DuelBlessing blessing in _run.Blessings) text.Append(DuelRun.Name(blessing)).Append("  ");
            _resultStats.text = text.ToString();
        }
    }
}
