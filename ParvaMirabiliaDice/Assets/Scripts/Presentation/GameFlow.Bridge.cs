using System;
using ParvaMirabilia.Core;
using UnityEngine;

namespace ParvaMirabilia.Presentation
{
    /// <summary>
    /// What the ASIST Game Bridge may read and do in the dice duel. Values are derived from the engine and the
    /// presentation fields the screen already shows; actions go through the handlers the player's input calls,
    /// so every guard and animation stays the same. Nothing here changes state except through those handlers.
    /// </summary>
    public sealed partial class GameFlow
    {
        // The duel is on screen: the table, or the result after it. Not the title, not before the first match.
        private bool NephiaInDuel => _sdEngine != null && ScoreDuelPresentationActive &&
            (_sdBattle.gameObject.activeInHierarchy || _sdResult.gameObject.activeInHierarchy);

        // The dice and the role list show nothing real while a throw is in the air.
        private bool NephiaFacesKnown => NephiaInDuel && _sdBattle.gameObject.activeInHierarchy && !_sdRolling;

        // ---------------------------------------------------------------- values
        [AsistBridge.NephiaVisible("対戦の状態")]
        [UnityEngine.Scripting.Preserve]
        private string NephiaPhase
        {
            get
            {
                if (!NephiaInDuel) return null;
                if (_sdResult.gameObject.activeInHierarchy) return "結果";
                if (SDModalOpen) return "画面を開いている（閉じるまで操作できない）";
                if (_sdCommitted) return _sdAttackAge >= 0 ? "攻撃演出中" : "攻撃後（次へで進む）";
                if (_sdNpcReplayAge >= 0) return "相手の番（相手の振りを表示中）";
                if (_sdRolling) return "振り直し中";
                return "自分の番";
            }
        }

        [AsistBridge.NephiaVisible("自分の出目")]
        [UnityEngine.Scripting.Preserve]
        private int[] NephiaDice => NephiaFacesKnown ? (int[])_sdEngine.PlayerDice.Clone() : null;

        [AsistBridge.NephiaVisible("保持している出目")]
        [UnityEngine.Scripting.Preserve]
        private bool[] NephiaHeld
        {
            get
            {
                if (!NephiaFacesKnown) return null;
                var held = new bool[5];
                if (!_sdCommitted) Array.Copy(_sdEngine.Held, held, 5);
                return held;
            }
        }

        [AsistBridge.NephiaVisible("残り振り直し")]
        [UnityEngine.Scripting.Preserve]
        private int? NephiaRerollsLeft => NephiaFacesKnown && !_sdCommitted ? _sdEngine.RerollsLeft : (int?)null;

        [AsistBridge.NephiaVisible("自分のHP")]
        [UnityEngine.Scripting.Preserve]
        private int? NephiaPlayerHp => NephiaInDuel ? _sdEngine.PlayerHp : (int?)null;

        [AsistBridge.NephiaVisible("相手のHP")]
        [UnityEngine.Scripting.Preserve]
        private int? NephiaNpcHp => NephiaInDuel ? _sdEngine.NpcHp : (int?)null;

        [AsistBridge.NephiaVisible("選んでいる役")]
        [UnityEngine.Scripting.Preserve]
        private string NephiaSelectedRole
        {
            get
            {
                if (!NephiaFacesKnown) return null;
                return ScoreRules.Name(_sdCommitted ? _sdEngine.LastRound.PlayerKind : _sdSelectedKind);
            }
        }

        [AsistBridge.NephiaVisible("選んでいる役の得点")]
        [UnityEngine.Scripting.Preserve]
        private int? NephiaSelectedScore
        {
            get
            {
                if (!NephiaFacesKnown) return null;
                if (_sdCommitted) return _sdEngine.LastRound.PlayerScore;
                return _sdEngine.Offers[(int)_sdSelectedKind].Score;
            }
        }

        [AsistBridge.NephiaVisible("使える役と今の得点")]
        [UnityEngine.Scripting.Preserve]
        private string[] NephiaAvailableRoles
        {
            get
            {
                if (!NephiaFacesKnown) return null;
                var roles = new System.Collections.Generic.List<string>();
                foreach (ScoreOffer offer in _sdEngine.Offers)
                    if (offer.IsAvailable) roles.Add(ScoreRules.Name(offer.Kind) + " " + offer.Score + "点");
                return roles.ToArray();
            }
        }

        [AsistBridge.NephiaVisible("持越を使える")]
        [UnityEngine.Scripting.Preserve]
        private bool? NephiaSaveAvailable => NephiaInDuel ? !_sdEngine.SavedHandUsed : (bool?)null;

        [AsistBridge.NephiaVisible("持越を予約中")]
        [UnityEngine.Scripting.Preserve]
        private bool? NephiaSaveArmed => NephiaInDuel ? _sdSaveArmed : (bool?)null;

        [AsistBridge.NephiaVisible("難易度")]
        [UnityEngine.Scripting.Preserve]
        private string NephiaDifficulty => NephiaInDuel ? SDOpponentProfiles[_sdOpponentIndex].Label : null;

        // ---------------------------------------------------------------- actions
        /// <summary>The same wall the player's own input meets: a modal, a throw in the air, a committed turn, a drag.</summary>
        private void NephiaRequirePlanning()
        {
            if (!NephiaInDuel || !_sdBattle.gameObject.activeInHierarchy) throw new InvalidOperationException("対戦中ではありません");
            if (SDModalOpen) throw new InvalidOperationException("画面が開いています。閉じてから操作してください");
            if (_sdCommitted) throw new InvalidOperationException("この番は発射済みです。次へで進めてください");
            if (_sdNpcReplayAge >= 0) throw new InvalidOperationException("相手の振りを表示中です");
            if (_sdRolling) throw new InvalidOperationException("ダイスが転がっています");
            if (_sdDragging) throw new InvalidOperationException("ダイスをドラッグ中です");
            if (_sdInputFrame == Time.frameCount) throw new InvalidOperationException("直前の操作の直後です。もう一度試してください");
        }

        [AsistBridge.NephiaAction("保持の切り替え", "ダイスを残す／解除する（index は1〜5）")]
        [UnityEngine.Scripting.Preserve]
        private void NephiaToggleHold(int index)
        {
            NephiaRequirePlanning();
            if (index < 1 || index > 5) throw new InvalidOperationException("index は1〜5です");
            if (_sdEngine.RerollsLeft == 0) throw new InvalidOperationException("振り直しがないので保持は変えられません");
            SDHandleShortcut((KeyCode)((int)KeyCode.Alpha1 + index - 1));
        }

        [AsistBridge.NephiaAction("振り直し", "残していないダイスを振り直す")]
        [UnityEngine.Scripting.Preserve]
        private void NephiaReroll()
        {
            NephiaRequirePlanning();
            if (_sdEngine.RerollsLeft == 0) throw new InvalidOperationException("振り直しは残っていません");
            bool any = false;
            for (int i = 0; i < 5; i++) any |= !_sdEngine.Held[i];
            if (!any) throw new InvalidOperationException("全部残しているので振り直せません。保持を外してください");
            SDHandleRightClick();
        }

        [AsistBridge.NephiaAction("役を選ぶ", "使う役を役名で選ぶ（使える役と今の得点にある名前）")]
        [UnityEngine.Scripting.Preserve]
        private void NephiaSelectRole(string role)
        {
            NephiaRequirePlanning();
            int index = Array.IndexOf(ScoreRules.Names, (role ?? "").Trim());
            if (index < 0) throw new InvalidOperationException("そういう役はありません：" + role);
            if (_sdEngine.PlayerUsed[index]) throw new InvalidOperationException("その役は使用済みです");
            SDSelectCategory(index);
        }

        [AsistBridge.NephiaAction("発射", "選んでいる役で5個全体を発射して番を終える")]
        [UnityEngine.Scripting.Preserve]
        private void NephiaFire()
        {
            NephiaRequirePlanning();
            // The same state a downward drag release leaves: the shortest throw that counts, then the commit.
            _sdLaunchPull = Mathf.Clamp(SDFlickDistance * .65f, 0, 55);
            _sdInputFrame = Time.frameCount;
            SDCommit();
        }

        [AsistBridge.NephiaAction("次へ", "攻撃演出のあと、次のターン（または結果）へ進む")]
        [UnityEngine.Scripting.Preserve]
        private void NephiaNext()
        {
            if (!NephiaInDuel || !_sdBattle.gameObject.activeInHierarchy) throw new InvalidOperationException("対戦中ではありません");
            if (SDModalOpen) throw new InvalidOperationException("画面が開いています。閉じてから操作してください");
            if (!_sdCommitted) throw new InvalidOperationException("まだ発射していません");
            if (_sdAttackAge >= 0) throw new InvalidOperationException("攻撃演出中です。終わるまで待ってください");
            if (_sdInputFrame == Time.frameCount) throw new InvalidOperationException("直前の操作の直後です。もう一度試してください");
            SDTableClick();
        }

        [AsistBridge.NephiaAction("持越の切り替え", "持越を予約する／解除する（対戦中1回。発射した時に消費）")]
        [UnityEngine.Scripting.Preserve]
        private void NephiaToggleSave()
        {
            NephiaRequirePlanning();
            if (_sdEngine.SavedHandUsed) throw new InvalidOperationException("持越はもう使いました");
            // The button's own click records the frame before it acts.
            _sdInputFrame = Time.frameCount;
            SDSaveHand();
        }
    }
}
