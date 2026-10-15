# INTEGRATION — 独立プロジェクトとしての位置づけと取り込み方針

## 1. これは独立プロジェクト

| 項目 | 値 |
| --- | --- |
| プロジェクト | `D:\MimoGame\ParvaMirabiliaDice` |
| プロダクト名 | Parva Mirabilia Dice |
| Unity | 6000.3.25f1 |
| レンダリング | URPではなくビルトイン（2D UIのみ、カメラはデフォルト） |
| シーン | `Assets/Scenes/Main.unity`（空。UIは起動時にコード生成） |

**主ゲーム `D:\dev\Unity-AutoBattle-RPG` には一切依存していません。**

- Battle Runtime / Execution Builder / 既存の戦闘スクリプトを参照していない
- `ParvaMirabilia.Core` の asmdef は `references: []`、`noEngineReferences: true`
- 他エージェントの `D:\runner\*` 配下のプロジェクトにも触れていない
- 外部への push・公開・データ送信は行っていない

## 2. レイヤー分離

```
┌──────────────────────────────────────────────┐
│ Presentation（Assembly-CSharp, UnityEngine） │
│   GameFlow / DiceView / UiFactory            │
│   - 画面遷移・入力・描画                       │
│   - MatchEngine の public API だけを呼ぶ       │
├──────────────────────────────────────────────┤
│ Core（ParvaMirabilia.Core, noEngine refs）    │
│   MatchEngine / NpcBrain / Scoring / RNG      │
│   - ルールとNPC判断。UnityEngine を知らない     │
└──────────────────────────────────────────────┘
```

依存は**上から下**だけ。Core は UI を一切知らないので、
同じコードがNUnit・バッチシミュレーション・ゲーム内UIの3箇所で動きます。

### 主なAPI（取り込み時に必要な範囲）

```csharp
var engine = new MatchEngine(new GameConfig(), new SeededRandom(seed), NpcArchetype.Guardian);
engine.Roll();                                   // プレイヤーの出目が決まる
engine.ToggleKeep(i); engine.Reroll();           // 最大 GameConfig.MaxRerolls 回
RoundOutcome r = engine.CommitSplit(atkIdx, grdIdx);  // 同時公開と解決
engine.NextRound();                              // 次ラウンドへ
MatchOutcome end = engine.Outcome;               // null の間は継続
```

- `IRandomSource` を渡すので、シード固定・差し替え・検証用ダブルの注入が可能
- `GameConfig` のフィールド（`StartingHp` / `MaxRounds` / `PushbackDamage` / `MaxRerolls`）を
  変えるだけで調整できる
- `BeginMatch()` で同じエンジンを初期化し直せばリマッチにも使える

## 3. 実行方法

### 検証（この作業で実際に使った再現手順）

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File D:\MimoGame\scripts\Run-EditmodeTests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File D:\MimoGame\scripts\Run-PlaymodeTests.ps1
```

> この2本だけで足りる。`Run-PlaymodeTests.ps1` は実行中に
> 実画面を `logs/unity-*.png` に書き出す（`TEST_REPORT.md` 第10節）。
> **1回に1本ずつ、別プロジェクトの Unity と重ねないこと。**
> 結果は `logs/editmode-results.xml` / `logs/playmode-results.xml` に残る。

### 開発中（GUI + MCP）

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File D:\MimoGame\scripts\Start-UnityMcp.ps1 -Wait -NoScreenshot
powershell -NoProfile -ExecutionPolicy Bypass -File D:\MimoGame\scripts\Connect-KitwrightMcp.ps1
```

> Phase 0 では接続に成功したが、**画面批評以降は MCP が使えていない**。
> 検証は上記の batchmode に一本化しており、MCP に依存する手順は無い
> （`ENVIRONMENT_SETUP.md` §5-11）。

### ヘッドレスのテスト実行（等価な単発コマンド）

```powershell
Start-Process -FilePath "C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe" `
  -ArgumentList @('-batchmode','-projectPath','D:\MimoGame\ParvaMirabiliaDice',
                  '-runTests','-testPlatform','EditMode',
                  '-testResults','D:\MimoGame\logs\editmode-results.xml',
                  '-logFile','D:\MimoGame\logs\test-run.log') -Wait -PassThru
```

> GUIの `Unity.exe` は `Start-Process -Wait` で待つこと。`&` で起動すると即座に戻り、
> 子プロセスがハーネスに巻き取られて終了する。

### プレイ

Unity Hub でプロジェクトを開き、`Assets/Scenes/Main.unity` を開いて Play。
シーンに置き物は不要で、`GameFlow.Boot()`（`RuntimeInitializeOnLoadMethod`）が
起動時にタイトル画面を構築します。

## 4. 主ゲームへ組み込む場合の推奨手順

1. `Assets/Scripts/Core/` を丸ごとコピー（asmdef を含む）。`noEngineReferences: true` のままでよい
2. UI は主ゲームのUIフレームワークに合わせて作り直す前提。`GameFlow` は
   画面レイアウトに密結合しているため、そのままでは使わない
3. `ParvaMirabilia.Core.Tests` のうち `MatchRunner` / `SimulationTests` は
   ヘッドレスで動くので、組み込み後も同じ形で検証できる
4. 依存パッケージは追加不要（`com.unity.ugui` は標準）

## 5. このリポジトリの主要ファイル

```
docs/ENVIRONMENT_SETUP.md   環境・Phase0検証・制約・補助スクリプト
docs/GAME_DESIGN.md         ルールと設計
docs/DESIGN_HISTORY.md      批評と採否、改善サイクル
docs/TEST_REPORT.md         テストとシミュレーション、実機プレイ、画面写真の批評（第10節）
docs/ASSET_LICENSES.md      素材の出所（画像・フォントは外部素材ゼロ、音声はAI生成の来歴、比較参照画像の取扱い）
docs/STEP5_REVIEW.md        StepFun `step-5-preview` による独立批評3回と処置一覧
docs/INTEGRATION.md         本ファイル
docs/FINAL_REPORT.md        総括
scripts/                    Unity / MCP / 音声生成 / バッチテストのスクリプト
logs/                       テスト結果・シミュレーション・批評応答・音声プロンプト・
                            実画面写真 `unity-*.png`・幾何ダンプ `screen-*.json`・比較参照 `ref-ywb-*.png`
ParvaMirabiliaDice/Assets/Resources/Audio/   AI生成のSFX 10種＋BGM
ParvaMirabiliaDice/Playthrough/              証跡スクリーンショット 20〜34（15点、v1 ビルド）
```
