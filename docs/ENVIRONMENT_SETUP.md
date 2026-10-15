# ENVIRONMENT_SETUP — 開発環境構成と接続検証

最終更新: 2026-10-09

## 1. 構成概要

| 要素 | 内容 |
|---|---|
| OS | Windows 11（2画面構成、メイン 1920x1080） |
| Unity Editor | 6000.3.25f1（Unity 6.3 LTS） `C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe` |
| ライセンス | Unity Entitlement License（`UnityEntitlementLicense.xml`） |
| ゲームプロジェクト | `D:\MimoGame\ParvaMirabiliaDice`（productName: Parva Mirabilia Dice） |
| エージェント本体 | MiMo-V2.6-Flash Free（`opencode/mimo-v2.6-flash-free`） |
| サブエージェント | `code-reviewer` / `game-critic`（`.opencode/agents/`、編集権限 deny） |
| MCP サーバー | KitWright MCP for Unity（https://github.com/kitwright/unity-mcp） |
| CLI | `C:\Users\sakur\AppData\Roaming\ai.opencode.desktop\cli\2.0.26\opencode-cli.exe`（PATH 未登録） |
| 補助ツール | Python 3.14.7 / git 2.55.0 / Node / uv（すべて利用可） |

設定ファイル:

- `D:\MimoGame\opencode.json` — メインモデル指定 + MCP サーバー登録
- `D:\MimoGame\.opencode\agents\code-reviewer.md` — model `opencode/nemotron-3.5-lightning-free`
- `D:\MimoGame\.opencode\agents\game-critic.md` — model `opencode/longcat-2.5-preview-free`
- `D:\MimoGame\scripts\` — Unity 起動・MCP 接続・検証用 PowerShell スクリプト
- `D:\MimoGame\logs\` — プリフライト／接続検証の生ログとスクリーンショット

## 2. プリフライト検証結果

| # | 検証項目 | 結果 | 備考 |
|---|---|---|---|
| 1 | インストール済み Unity Editor の特定 | ✅ 成功 | 6000.3.25f1、エンタイトルメントライセンス確認済み |
| 2 | 新規独立プロジェクトの作成 | ✅ 成功 | `-batchmode -createProject`、exit code 0 |
| 3 | CLI からの Unity Editor 起動 | ✅ 成功 | `-projectPath` + `-logFile` で GUI 起動、グレースフル終了 code 0 |
| 4 | エディタスクリプトによるシーン生成 | ✅ 成功 | `-executeMethod PreflightCli.BuildSampleScene`、11 オブジェクト、exit 0 |
| 5 | EditMode テスト実行と結果保存 | ✅ 成功 | 4/4 Passed、`logs/editmode-results.xml`、exit 0 |
| 6 | スクリーンショット取得と画像解析 | ✅ 成功 | batchmode の RenderTexture でも GPU 描画可、エディタ画面は PrintWindow で取得。画像はモデルのビジョンで直接解析可能 |
| 7 | サブエージェントの実呼び出し | ⚠️ 一部成功 | `game-critic` は正常、`code-reviewer` は長文プロンプトで応答破損（短文では正常）→ 後述 |

補足:

- batchmode でも GPU 描画（RenderTexture → PNG）に成功。`preflight-scene.png` に実画像あり。
- 画像解析ツール（tesseract / magick / ffmpeg）は未導入。画像の視覚評価はモデル自身のビジョン能力で行う。
- 既存プロジェクト `D:\dev\Unity-AutoBattle-RPG` は一切変更していない（読み取りのみ）。

## 3. kitwright/unity-mcp の導入手順

### 3.1 パッケージ導入

`Packages/manifest.json` に追加:

```json
"com.kitwright.unity.mcp": "https://github.com/kitwright/unity-mcp.git",
"com.unity.test-framework": "1.6.0",
"com.unity.ugui": "2.0.0"
```

依存パッケージ（`com.unity.nuget.newtonsoft-json` など）は UPM が自動解決する。

### 3.2 サーバーの有効化（初回のみ手動）

`MCPServerService` は **batchmode では起動を拒否**する（`Application.isBatchMode` チェック）。また自動起動の条件は

```
RootScopeServices.cs: if (settings.MCPServerEnabled && settings.MCPAutostartEnabled) StartAsync()
```

であり、`MCPServerEnabled` の**デフォルトは false**。初回のみ

`{Project}/UserSettings/KitWrightMcpSettings.json` の `"enabled": true` を設定する（以降は自動起動）。

### 3.3 接続 URL の構成

エディタの起動後、トークンとピンを取得して URL を組み立てる。

- **port**: `8765 + (uint32(sha256(正規化パス)[0..8], 16) % 100) * 10`（プロジェクトごとにオフセット）
- **pin**: `sha256(小文字・スラッシュ区切りの絶対パス)` の先頭 8 文字
- **token**: EditorPrefs キー `KitWright.MCP.ServerToken.<pin>`（レジストリ `HKCU\Software\Unity Technologies\Unity Editor 5.x`、バイト列を ASCII 解釈）
- **URL**: `http://127.0.0.1:<port>/p/<pin>/t/<token>/`

トークンなしアクセスは `401` になる。ピン不一致は `404`。

### 3.4 OpenCode への登録

```powershell
opencode mcp add kitwright --url "http://127.0.0.1:<port>/p/<pin>/t/<token>/"
opencode reload     # 確認: opencode mcp list → kitwright connected
```

導入・接続・再登録は `scripts/Connect-KitwrightMcp.ps1` に自動化してある。

### 3.5 起動手順（毎回）

```powershell
# エディタ起動（バックグラウンド維持が必須 — 後述の制約参照）
powershell -File D:\MimoGame\scripts\Start-UnityMcp.ps1 -Wait -NoScreenshot
# 別ターミナルで接続確立
powershell -File D:\MimoGame\scripts\Connect-KitwrightMcp.ps1
# 終了
powershell -File D:\MimoGame\scripts\Stop-UnityMcp.ps1
```

## 4. MCP 接続テスト結果（Phase 0 合格条件）

プロファイルは `core`（47 ツール）から `full`（247 ツール）へ拡張済み。

| # | 項目 | 結果 | 検証内容 |
|---|---|---|---|
| 1 | シーン情報の取得 | ✅ 成功 | `get_scene_info` でルートオブジェクト列挙 |
| 2 | GameObject の生成 | ✅ 成功 | `create_game_object` → instanceId 返却、Hierarchy 表示を画像確認 |
| 3 | C# コードの実行 | ✅ 成功 | `execute_code`（Roslyn、`Executed (legacy Run())`、`exec:True`） |
| 4 | コンパイル結果の確認 | ✅ 成功 | `request_recompile` → "No compilation errors"、`get_compilation_errors` → 0 件 |
| 5 | Play Mode への移行 | ✅ 成功 | `enter_play_mode` → `isPlaying: true`。**domain reload でリクエストが中断**されたが broker モードが HTTP を維持し `get_reload_recovery_status` で回復可能 |
| 6 | 入力シミュレーション | ✅ 成功 | `simulate_mouse_click`（**Play Mode と EventSystem が必須**）。uGUI ボタンを生成 → 実クリック → `onClick` が発火し `[MCP-INPUT] button clicked` をコンソールで確認 |
| 7 | スクリーンショットの取得 | ✅ 成功 | `capture_scene_view` / `capture_game_view`（PNG ファイル保存）。`capture_editor_window` はウィンドウ全体ではなくマッチした子ウィンドウを掴むため、エディタ全体画像は PowerShell 側で取得 |
| 8 | Console ログの取得 | ✅ 成功 | `get_console_logs`（filter / count / source 指定可） |
| 9 | Play Mode からの復帰 | ✅ 成功 | `exit_play_mode` → `isPlaying: false` |

### MCP に存在しない機能（実測）

- **キーボード入力のシミュレーションは無い**。入力系ツールは `simulate_mouse_click` のみ（`full` プロファイルでも追加されない）。
- テスト実行系ツールは **`full` プロファイルでは `run_tests` / `get_test_job` / `cancel_test_run` が存在する**（`core` では未確認）。
  エディタ内で完結し、結果はジョブ ID で取得できる。ただし**永続的な結果ファイルは書かれない**ため、
  成果物として残す再現性のある実行は batchmode CLI（下記 `Run-EditmodeTests.ps1`）で行う。

### 本プロジェクトで使った補助スクリプト（`D:\MimoGame\scripts\`）

| スクリプト | 用途 |
|---|---|
| `Start-UnityMcp.ps1` / `Connect-KitwrightMcp.ps1` / `Stop-UnityMcp.ps1` | エディタの起動・接続・終了 |
| `Run-EditmodeTests.ps1` / `Run-PlaymodeTests.ps1` | batchmode で EditMode / PlayMode テストを実行し、NUnit 集計と失敗一覧を表示。**PlayMode は実画面を `logs/unity-*.png` に書き出す撮影も含む** |
| `ShowLogTail.ps1` | 直近のテスト実行ログの要約表示（インライン PowerShell は `$` を解釈するため、生コマンドで tail せずこのスクリプトを使う） |
| `RenderScreens.ps1` | `logs/screen-*.json`（幾何ダンプ）を `logs/screens.html` へ整形。**図形を四角く描くので形状判定には使わない**（判定は PNG 側） |
| `List-Unity.ps1` | Unity プロセスの一覧表示（ID・起動時刻・ウィンドウタイトル） |
| `StepAudio.ps1` | StepFun 公式 API（`api.stepfun.ai/v1`）の SFX 生成・BGM 提出/照会・Chat（`-Mode sfx\|bgm-submit\|bgm-poll\|chat`）。キーは認証ストアから実行時読出し、保持しない |
| `Query-BgmTask.ps1` / `Poll-BgmTasks.ps1` / `Save-BgmBase64.ps1` | BGM タスクの照会・完了待ちポーリング・base64 音声の取得とプロジェクトへの差し替え |
| `Foreground-Unity.ps1` | 画面キャプチャの前にエディタを前面に出す補助 |

## 5. 環境上の制約・注意事項

1. **MCP サーバーは GUI エディタが生きている間しか up ではない**。batchmode では起動しない。したがってテストと MCP 操作は同時に行えない（プロジェクトロック）。
2. **シェルコマンドの子プロセスはコマンド終了時に kill される**。エディタは `background: true` のシェルで `-Wait` して维持する必要がある。
3. **複数モニター構成**でエディタが画面外に起動することがある。`SetWindowPos` で主画面に移動してから画面座標の操作を行う。
4. **他エージェント／他プロジェクトの Unity インスタンスが同一マシンで動いている**（例: `D:\runner\desk-project`）。ウィンドウのフォーカス・クリック操作は必ず自プロジェクトのウィンドウを確認してから行う。`D:\dev\Unity-AutoBattle-RPG` は読取専用。
5. **`code-reviewer` は長文プロンプトで応答が破損する**（2 回実測）。短いプロンプト（数百字）では正常。批評依頼は簡潔に分割して送る。
6. `simulate_mouse_click` は Play Mode 以外では `PLAY_MODE_REQUIRED`、EventSystem が無いと `NO_EVENT_SYSTEM` を返す。
7. プロジェクトの移動（リネーム）すると pin / token / port が全て変わるため、`Connect-KitwrightMcp.ps1` で再登録が必要。
8. エディタウィンドウのタイトルが `*` 付きのとき、シーンまたはプロジェクトに未保存の変更がある。
9. **StepFun 公式 API の接続先は国際版 `https://api.stepfun.ai/v1`**。保存済みキーは中国版 `api.stepfun.com/v1` に対して **401** を返すため、`.com` は使わない。接続は `scripts/StepAudio.ps1` に集約し、キーは認証ストア（`opencode-cli auth export`）から実行時読出し・保持しない。
10. **Chat 系エンドポイントはプロンプトファイルの UTF-8 BOM で 400** を返す（送信前に除去）。`max_tokens` を 3000台にすると reasoning だけで `finish=length` になり本文が切れる（12000以上を推奨）。生成系 `POST` は本文を**素の JSON 文字列**で送る（PowerShell の `ConvertTo-Json` は要素1つの配列を展開して 400 になる）。

11. **MCP は Phase 0（第4節）で接続に成功したが、後続の作業では使えたという記録が無い。**
    画面批評以降の検証は**すべて batchmode CLI で実行**しており、
    再現手順も `scripts/Run-EditmodeTests.ps1` / `Run-PlaymodeTests.ps1` の2本だけである。
    MCP の有無に依存する検証は1つも無い。
12. **実画面の撮影はエディタのキャプチャに頼らない。** `ScreenCaptureTests` が
    PlayMode 内で Canvas をカメラ → `RenderTexture` → PNG に書き出すため、
    ヘッドレステストの中に収まる（`logs/unity-*.png`）。
    エディタウィンドウを前面に出す操作は不要になった。
13. **2つの Unity を同時に走らせない。** 別プロジェクトのエディタが動いている状態で
    EditMode と PlayMode を1本のシェルに連結するとタイムアウトし、
    バッファ済みの出力ごと失われる。**1回に1本、背景シェルを1つずつ**にする。

## 6. 再現手順（ゼロから環境を作る）

> 既に存在するプロジェクトに下の `-createProject` を実行しないこと。
> 現行の通常検証・配布ビルド手順はルートの `README.md` と本書 §7 を参照。

```powershell
# 1. プロジェクト作成
& "C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe" `
   -batchmode -quit -createProject "D:\MimoGame\ParvaMirabiliaDice" `
   -logFile "D:\MimoGame\logs\create.log"

# 2. Packages/manifest.json に kitwright / test-framework / ugui を追加
# 3. UserSettings/KitWrightMcpSettings.json に "enabled": true を設定
# 4. エディタ起動 + 接続確立
powershell -File D:\MimoGame\scripts\Start-UnityMcp.ps1 -Wait -NoScreenshot
powershell -File D:\MimoGame\scripts\Connect-KitwrightMcp.ps1
```

## 7. v3.1 候補の通常検証とWindows配布

既存Editorを閉じたり別作業のUnityプロセスを終了したりせず、必要なら専用コピーで検証する。

```powershell
pwsh -File scripts/Prepare-ValidationProject.ps1
pwsh -File scripts/Test-Game.ps1 -Platform EditMode -ProjectPath D:\MimoGame\.validation\ParvaMirabiliaDice
pwsh -File scripts/Test-Game.ps1 -Platform PlayMode -ProjectPath D:\MimoGame\.validation\ParvaMirabiliaDice
pwsh -File scripts/Build-Windows.ps1 -ProjectPath D:\MimoGame\.validation\ParvaMirabiliaDice
```

Unityは順次実行する。同一プロジェクトのバッチとGUIを同時起動しない。
テストは `logs/validation-<platform>-<日時>.xml` と同名 `.log` に出力し、既存証跡を削除しない。
失敗・0件実行・結果欠落・Unityの非ゼロ終了はスクリプトも失敗する。
`Run-EditmodeTests.ps1` と `Run-PlaymodeTests.ps1` もこの共通実行器を使う。

ビルドは `ReleaseBuild.Windows`、Mainシーン、Windows x64、非Development、StrictMode。
`Builds/Windows-<日時>/ArcaneDuel.exe` と `build-report.json`、説明書・素材出所を保存する。
保存場所はソースのAssets外。通常の起動ではローカル検証用MCP接続も外部APIも不要。

実行版の自動スモークは `-arcaneSmokeOutput <出力フォルダー> -logFile <ログ>` で明示的に有効化する。
練習、3種NPCとの対戦終了、再戦、中断確認、入力遮断、4:3／ワイドの描画を確認する。
非表示起動時はバックバッファを取れない場合があるため、スクリーンショットは実行版のCanvasを
一時的にカメラとRenderTextureへ描画して保存する。物理入力や人間の音響評価の証跡とは区別する。
