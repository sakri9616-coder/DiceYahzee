using UnityEngine;
using UnityEngine.UI;
using ParvaMirabilia.Core;

namespace ParvaMirabilia.Presentation
{
    public sealed partial class GameFlow
    {
        private readonly Image[] _duelSparks = new Image[24];
        private readonly Vector2[] _sparkOrigins = new Vector2[24], _sparkVelocity = new Vector2[24];
        private readonly Image[] _duelBolts = new Image[2], _duelBursts = new Image[2];
        private float _duelFxAge = -1;
        private bool _reducedMotion;
        private Text _motionCaption;
        private RectTransform _creditsRoot;

        private void BuildDuelEffects()
        {
            _reducedMotion = PlayerPrefs.GetInt("arcane.reducedMotion", 0) != 0;
            for (int i = 0; i < 24; i++)
            {
                _duelSparks[i] = UiFactory.CreatePanel("DuelSpark" + i, _battleRoot, new Vector2(5, 5),
                    Vector2.zero, UiFactory.Gold);
                _duelSparks[i].sprite = UiFactory.CircleSprite;
                _duelSparks[i].raycastTarget = false;
                _duelSparks[i].gameObject.SetActive(false);
            }
            for (int i = 0; i < 2; i++)
            {
                _duelBolts[i] = DuelArt.Icon(_battleRoot, "DuelBolt" + i, "comet-spark", Vector2.zero, 44, UiFactory.Gold);
                _duelBursts[i] = DuelArt.Icon(_battleRoot, "DuelBurst" + i, "crowned-explosion", Vector2.zero, 70, UiFactory.Gold);
                _duelBolts[i].gameObject.SetActive(false);
                _duelBursts[i].gameObject.SetActive(false);
            }
            var help = _menuRoot.Find("KeyboardHelp") as RectTransform;
            help.anchoredPosition = new Vector2(0, -231);
            Button motion = UiFactory.CreateButton("Motion", _menuRoot, new Vector2(280, 34),
                new Vector2(-153, -177), "", 16, ToggleReducedMotion);
            ResizeDuelButton(motion, new Vector2(280, 34));
            StyleDuelChoice(motion, false);
            _motionCaption = motion.GetComponentInChildren<Text>();
            _motionCaption.text = _reducedMotion ? "演出：控えめ" : "演出：通常";
            Button credits = UiFactory.CreateButton("Credits", _menuRoot, new Vector2(280, 34),
                new Vector2(153, -177), "素材とクレジット", 16, OpenCredits);
            ResizeDuelButton(credits, new Vector2(280, 34));
            StyleDuelChoice(credits, false);
            _creditsRoot = MakeModal("CreditsOverlay", new Vector2(960, 620));
            _creditsRoot.Find("Panel").GetComponent<Image>().color = DuelPanel;
            _creditsRoot.Find("Panel/Gloss").gameObject.SetActive(false);
            UiFactory.CreateText("CreditsTitle", _creditsRoot, "素材とクレジット", 32, TextAnchor.MiddleCenter,
                UiFactory.Gold, new Vector2(0, 243), new Vector2(900, 48));
            UiFactory.CreateText("CreditsBody", _creditsRoot,
                "UI装飾：Fantasy UI Borders — Kenney (kenney.nl) / CC0\n\n" +
                "アイコン：Lorc (game-icons.net) / CC BY 3.0\n" +
                "Crystal Ball, Edged Shield, Fire Silhouette, Crossed Swords,\n" +
                "Burning Embers, Lightning Helix, Comet Spark, Crowned Explosion\n" +
                "表示時に色とサイズを調整しています。\n\n" +
                "卓の質感：Leather White — Rob Tuytel / Poly Haven / CC0\n" +
                "見出し：Aoboshi One — The Aoboshi Font Project Authors / SIL OFL 1.1\n\n" +
                "音楽・効果音：既存のStepFun生成音声\n\n" +
                "詳しい出典とライセンスは、同梱の権利表記をご覧ください。\n" +
                "ASSET_LICENSES.md  /  Licenses", 18,
                TextAnchor.UpperLeft, UiFactory.Text, new Vector2(0, -7), new Vector2(858, 414));
            Button close = UiFactory.CreateButton("CloseCredits", _creditsRoot, new Vector2(260, 46),
                new Vector2(0, -267), "ゲームに戻る", 20, CloseExperienceOverlays);
            ResizeDuelButton(close, new Vector2(260, 46));
            StyleDuelChoice(close, false);
            _creditsRoot.gameObject.SetActive(false);
        }

        private void OpenCredits()
        {
            CloseExperienceOverlays();
            _creditsRoot.gameObject.SetActive(true);
            SelectDefault(_creditsRoot, "CloseCredits");
        }

        private void ToggleReducedMotion()
        {
            _reducedMotion = !_reducedMotion;
            PlayerPrefs.SetInt("arcane.reducedMotion", _reducedMotion ? 1 : 0);
            _motionCaption.text = _reducedMotion ? "演出：控えめ" : "演出：通常";
            ResetDuelEffects();
            _menuNote.text = _reducedMotion ? "画面の揺れ・飛翔エフェクトを抑えます。数値と音は残ります。" : "通常の演出に戻しました。";
        }

        private void ResetDuelEffects()
        {
            _duelFxAge = -1;
            foreach (Image spark in _duelSparks) if (spark != null) spark.gameObject.SetActive(false);
            foreach (Image bolt in _duelBolts) if (bolt != null) bolt.gameObject.SetActive(false);
            foreach (Image burst in _duelBursts) if (burst != null) burst.gameObject.SetActive(false);
        }

        private void StartDuelEffects(RoundOutcome outcome)
        {
            if (!_engine.Config.TacticalEnabled || _reducedMotion) return;
            _duelFxAge = 0;
            for (int side = 0; side < 2; side++)
            {
                int damage = side == 0 ? outcome.DamageToNpc : outcome.DamageToPlayer;
                var tint = damage > 0 ? (side == 0 ? UiFactory.Gold : new Color32(235, 151, 128, 255)) : new Color32(125, 196, 208, 255);
                _duelBolts[side].color = tint;
                _duelBolts[side].gameObject.SetActive(damage > 0);
                _duelBursts[side].sprite = DuelArt.Load("Icons/" + (damage > 0 ? "crowned-explosion" : "edged-shield"));
                _duelBursts[side].color = tint;
                _duelBursts[side].gameObject.SetActive(true);
                Vector2 origin = new Vector2(0, side == 0 ? 216 : -219);
                _duelBursts[side].rectTransform.anchoredPosition = origin;
                for (int j = 0; j < 12; j++)
                {
                    int index = side * 12 + j;
                    float angle = j * Mathf.PI / 6;
                    _sparkOrigins[index] = origin;
                    _sparkVelocity[index] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (40 + j % 3 * 24);
                    _duelSparks[index].color = tint;
                    _duelSparks[index].gameObject.SetActive(true);
                }
            }
        }

        private void TickDuelEffects(float dt)
        {
            if (_duelFxAge < 0) return;
            _duelFxAge += dt;
            float travel = Mathf.Clamp01(_duelFxAge / .28f);
            float burst = Mathf.Clamp01((_duelFxAge - .22f) / .52f);
            for (int side = 0; side < 2; side++)
            {
                var bolt = _duelBolts[side];
                bolt.rectTransform.anchoredPosition = Vector2.Lerp(new Vector2(side == 0 ? -90 : 90, side == 0 ? 0 : 155),
                    new Vector2(0, side == 0 ? 216 : -219), travel) + Vector2.right * Mathf.Sin(travel * Mathf.PI) * (side == 0 ? -38 : 38);
                bolt.rectTransform.localRotation = Quaternion.Euler(0, 0, side == 0 ? 45 : -135);
                bolt.gameObject.SetActive(bolt.gameObject.activeSelf && travel < 1);
                var image = _duelBursts[side];
                var color = image.color;
                color.a = travel < 1 ? 0 : (1 - burst) * .75f;
                image.color = color;
                image.rectTransform.localScale = Vector3.one * (.55f + burst * .6f);
            }
            for (int i = 0; i < 24; i++)
            {
                var spark = _duelSparks[i];
                spark.rectTransform.anchoredPosition = _sparkOrigins[i] + _sparkVelocity[i] * burst;
                Color color = spark.color;
                color.a = travel < 1 ? 0 : 1 - burst;
                spark.color = color;
            }
            if (_duelFxAge > .85f) ResetDuelEffects();
        }
    }
}
