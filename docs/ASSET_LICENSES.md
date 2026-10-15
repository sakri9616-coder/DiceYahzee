# ASSET_LICENSES — 素材と権利表記

## DiceYahzee UI素材

2026-10-11にKenney公式配布から取得。画像生成AIは使用していない。既存素材の記録は以下に保持する。

| 素材 | 公式配布元 | ライセンス | 採用内容 |
| --- | --- | --- | --- |
| UI Pack 2.0 | https://kenney.nl/assets/ui-pack | CC0 1.0 | パネル・ボタン3種、矢印1種 |
| Input Prompts 1.5A | https://kenney.nl/assets/input-prompts | CC0 1.0 | 左クリック・右クリック・スクロールの輪郭表示3種 |
| Game Icons 1.0 | https://kenney.nl/assets/game-icons | CC0 1.0 | 鍵・音声・消音・メニュー4種 |

原ZIPと原文ライセンスは `ThirdParty/Kenney-UIPack`、`ThirdParty/Kenney-InputPrompts`、`ThirdParty/Kenney-GameIcons` に保存。CC0のため表示クレジットは義務ではないが出典を保管する。PNG自体は未改変。Unity Spriteとして読み込み、表示時の着色・拡縮・9-sliceを許容する。パネル4種は384×128px、slice borderは四辺16px。操作アイコンは原寸36〜128pxの範囲で、小型UI向け。コントローラー商標の付いた画像・フォント・ベクター原稿はプレイヤー用Assetsに取り込まない。

現行11点の原ZIP内パス、Resourcesパス、サイズ、SHA-256、slice borderを `ThirdParty/DiceYahzeeAssets.json` に記録。旧画面用の未参照10点は0.9.1の整理でResourcesから削除した。CC0の詳細は https://creativecommons.org/publicdomain/zero/1.0/ 。

## 0.6.0から継続して保持する素材

AI生成画像は使用しない。画像生成モデルの出力をAssets、Resources、画面素材に含めていない。
フリー素材は作者・公式配布先・ライセンスを確認し、下記を採用した。
既存のStepFun生成音声は第6節の制作記録を維持する。
現行標準画面はLeather Whiteを直接表示する。枠・紋章・英語フォントは旧画面の比較検証用として保持し、Resourcesにも残っている。以下の取得・変更・権利記録を維持する。

| 素材 | 作者 / 出典 | ライセンス | 使用・変更 |
| --- | --- | --- | --- |
| Fantasy UI Borders 1.0 | [Kenney](https://kenney.nl/assets/fantasy-ui-borders) | CC0 | panel-border-006.pngをengraved.pngに改名。表示時に着色・9-slice・拡縮 |
| 8種の紋章 | [Lorc / Game-icons.net](https://game-icons.net/1x1/lorc/crystal-ball.html) | [CC BY 3.0](https://creativecommons.org/licenses/by/3.0/) | 透明背景・白PNGを採用。元PNGは未改変、表示時に着色・拡縮 |
| Leather White 1K Diffuse | [Rob Tuytel / Poly Haven](https://polyhaven.com/a/leather_white) | [CC0](https://polyhaven.com/license) | 元JPEGは未改変、青緑色を掛けてタイル表示 |
| Aoboshi One Regular | [Aoboshi Font Project Authors / Google Fonts](https://github.com/google/fonts/tree/main/ofl/aoboshione) | SIL OFL 1.1 | 元TTFは未改変、ゲーム名と得点表示に同梱 |
| Noto Sans JP Regular | [Noto CJK / notofonts](https://github.com/notofonts/noto-cjk/tree/main/Sans/SubsetOTF/JP) | [SIL OFL 1.1](https://github.com/notofonts/noto-cjk/blob/main/Sans/LICENSE) | 元OTFは未改変、日本語UIに同梱 |
| Windows版の日本語本文 | 実行端末のOSフォント | OS提供元の条件 | OSフォントファイルは同梱しない |
| 既存SFX10種・BGM1種 | StepFun公式API | 第6節参照 | 既存素材を維持。新しいAI画像/音声の生成なし |
| Asset Store素材 | 採用なし | — | 現在は上記フリー素材を使用 |

### 1. 画像とクレジット

Lorcの8素材の公式ページ：

- [Crystal Ball](https://game-icons.net/1x1/lorc/crystal-ball.html)
- [Edged Shield](https://game-icons.net/1x1/lorc/edged-shield.html)
- [Fire Silhouette](https://game-icons.net/1x1/lorc/fire-silhouette.html)
- [Crossed Swords](https://game-icons.net/1x1/lorc/crossed-swords.html)
- [Burning Embers](https://game-icons.net/1x1/lorc/burning-embers.html)
- [Lightning Helix](https://game-icons.net/1x1/lorc/lightning-helix.html)
- [Comet Spark](https://game-icons.net/1x1/lorc/comet-spark.html)
- [Crowned Explosion](https://game-icons.net/1x1/lorc/crowned-explosion.html)

ゲーム内のメニュー→素材クレジットで作者・素材名・ライセンス・表示上の変更を示す。
元ライセンスは `ThirdParty/Kenney-FantasyUI/LICENSE.txt`、`ThirdParty/AoboshiOne/OFL.txt`、`ThirdParty/NotoSansJP/LICENSE.txt`。
一覧は `ThirdParty/CREDITS.md`、導入した画像のサイズとSHA-256は `ThirdParty/asset-manifest.json`。
Resources/Creditsにも一覧とライセンス原文を保持し、検証用実行版にはLicensesフォルダーとしてコピーする。

### 1-b. 比較参照画像

旧 `logs/ref-ywb-1..4.png` はScopelyのYahtzee With Buddies公式配布画像を批評参照した履歴。
ゲーム内素材・ビルドには含めず、再配布しない。参照以外の使用はない。

### 2. フォント

Aoboshi Oneの著作権はCopyright 2020 The Aoboshi Font Project Authors。
SIL OFL 1.1原文を同梱し、フォント単体として販売しない。
日本語本文はYu Gothic UI、Yu Gothic、Meiryo UI、Meiryo、MS Gothic、Segoe UI、Arial、
UnityのLegacyRuntime.ttfを候補に実行時に解決する。OSフォントは端末から参照し、ファイルをコピーしない。

### 3. コードによる描画

ダイスの面と目、角丸、ヴィネット、卓のベクトル線、HPバー、火花はUnityのUIとメッシュで描画する。
手続き的な描画は画像生成モデルの出力ではない。外部素材の上記権利条件は引き続き保持する。

### 4. Unity依存関係

Unity本体とPackages/manifest.jsonのパッケージは、それぞれ提供元のライセンス条件に従う。
KitWright Unity MCPは開発用で、通常のゲーム実行には不要。Editor/Testsのコードはプレイヤーに含めない。

### 5. 保管と公開

原素材・ライセンス・変更内容をこのワークスペースに保管する。外部へのpush・公開は実施していない。
以下の生成音声の記録は旧制作時点の情報として保持し、現在の料金やサービス条件を保証する記述として扱わない。


### 6. 音声素材（AI生成）

#### 6.1 生成元と条件

| 項目 | 内容 |
| --- | --- |
| 生成器 | StepFun 公式 API（国際版 `https://api.stepfun.ai/v1`） |
| 効果音用エンドポイント | `POST /v1/audio/generate`（`task=text_to_audio`, `response_format=mp3`） |
| 効果音用モデル | `stepaudio-3-gen-preview` |
| BGM用エンドポイント | `POST /v1/audio/music/submit` → `POST /v1/audio/music/query`（提出→ポーリング→取得） |
| BGM用モデル | `stepaudio-3-music-preview` |
| 課金表記（公式ドキュメント） | 両モデルとも **Billing: "Free (limited time)"**（`platform.stepfun.ai/docs/en/guides/models/stepaudio-3-gen` および `.../stepaudio-3-music`） |
| 今回の前提 | 制作期間中 StepFun 経由のモデルは無料という利用者指示の下で呼び出し。課金有無を確認できない呼び出しは行わない方針 |
| 認証キーの取扱い | 実行時に認証ストアから読出し、**ログ・成果物・スクリーンショットには一切出力していない** |
| 記録用スクリプト | `scripts/StepAudio.ps1`（キー非保持）、プロンプト原文 `logs/sfx-prompts.json`、`logs/bgm-gen-prompts.json` |

生成物はすべて本リポジトリ内の `ParvaMirabiliaDice/Assets/Resources/Audio/*.mp3` にのみ保存しており、
外部サービスへの再送・再配布は行っていません。

#### 6.2 採用した効果音（10種）とプロンプト

すべて `stepaudio-3-gen-preview` で生成。元ファイルは 1.6〜6.6 秒の MP3（24 kHz）で、
読み込み時に無音区間を自動トリムした結果と実効ゲインを併記する
（トリムとゲインは `GameAudio.PrepareSfx` が実行時に行うもので、素材自体の改変ではない）。

| ファイル | プロンプト（原文） | トリム後 | 実効ゲイン |
| --- | --- | --- | --- |
| dice_roll.mp3 | `[Five small wooden dice tumbling and clattering onto a wooden table, close-miked, bright, dry, short, no music, no voice]` | 1.70s（現行） | 実行時に再調整 |
| die_keep.mp3 | `[A single small wooden die pressed onto a table with one crisp tap, dry, close-miked, very short, no music, no voice]` | 0.38s | ×2.83 |
| ui_click.mp3 | `[A soft wooden interface click, short and clean, dry, gentle, no music, no voice]` | 0.28s | ×3.82 |
| hit.mp3 | `[A sharp magical strike impact, punchy and bright with a brief sparkling tail, short, energetic, no music, no voice]` | 0.43s | ×0.41 |
| guard.mp3 | `[A soft magical shield blocking a blow, muted low thud with a faint shimmering ring, short, calm, no music, no voice]` | 0.68s | ×0.56 |
| hurt.mp3 | `[A dull heavy hurt impact, low thud with a short tense tail, dark, short, no music, no voice]` | 0.58s | ×0.66 |
| commit.mp3 | `[A firm card slap and wooden clack, decisive and confident, short, dry, no music, no voice]` | 3.28s | ×2.70 |
| win.mp3 | `[A short triumphant victory fanfare on harp and light bells, bright and celebratory, about two seconds, no voice]` | 2.88s | ×0.46 |
| lose.mp3 | `[A short somber defeat sting on low strings and a soft mallet, melancholic and quiet, about two seconds, no voice]` | 6.45s | ×1.89 |
| **formation.mp3** | `[A short bright rising chime, three sparkling notes locking into place, magical and rewarding, about one second, dry, no music, no voice]` | **未計測** | **未計測** |

`dice_roll.mp3` の元音声には約2.9秒以降に二度目の転がり音がある。0.9.0では最初の転がり音のみを使うため、実行時に1.70秒で切り、末尾60msをフェードする。元MP3は変更していない。

**`formation.mp3` について**: v2 の陣形成立時に鳴らすスティングとして追加生成した
（`logs/sfx-prompts.json` にエントリを追加、生ファイル 26,794 バイト）。
上表の「トリム後」「実効ゲイン」は **未取得**である — 他の9種の数値は当時の計測記録だが、
この1種は生成後に自動トリムの実測を実施していない。
`GameAudio.MaxDuration` では 1.5 秒に制限している。数値は次回の音声 QC で取得する。
**主観的な試聴は全10種とも未実施**（§6.4 と同じ扱い）。

> **現行0.5.0では、役の着地後の成立通知で参照する。**
> `GameFlow.ScoreDuel.cs` の `GameAudio.Play("formation")` から
> `Resources/Audio/formation.mp3` を読み込み、実行版にも含まれる。
> 前版の「v3以降未参照・配布物に含まれない」という記述は現在には適用しない。

#### 6.3 BGM

| ファイル | 生成元／モデル | プロンプト（原文） | 尺 | 備考 |
| --- | --- | --- | --- | --- |
| `Assets/Resources/Audio/bgm_duel.mp3` | music API / `stepaudio-3-music-preview` | `Calm mystical tabletop duel underscore, loop friendly, soft plucked strings, gentle harp, light woodwind, mid tempo, warm spacious elegant, quiet and unobtrusive, instrumental, no vocals` | **125.02秒**（読み込み後 121.82秒） | **採用**。`POST /v1/audio/music/submit` で提出し、`/v1/audio/music/query` で base64 の MP3 を受領。MD5 `A778F580656C3C3C0746471C51300427`、タスク ID `01a121a7-b2fa-7b01-aef9-867367b180c3` |
| `logs/bgm_candidates/bgm_duel_gen-9s.mp3` | gen API / `stepaudio-3-gen-preview` | `[Loopable instrumental background music, minimal ambient fantasy underscore: harp arpeggios, airy synth pad, subtle soft percussion, relaxed slow tempo, elegant and quiet, no vocals, no speech, no sound effects]` | 9.17秒 | 旧素材。本番 BGM 取得前に採用していたが差し替え、**プロジェクト外に退避** |
| `logs/bgm_candidates/bgm_loop_a.mp3` | gen API / `stepaudio-3-gen-preview` | `[Loopable instrumental background music for a small fantasy tabletop duel: soft plucked strings, gentle harp and light woodwind, calm and mysterious, steady mid tempo, quiet and unobtrusive, no vocals, no speech, no sound effects]` | 10.61秒 | 採用せず。プロジェクト外に保存 |

採用 BGM は 2,001,716 バイト・48 kHz・ステレオ・クリップ率 0.00%・ピーク 0.993。
読み込み時に `GameAudio.PrepareBgm` が**先頭0.64秒と末尾1.59秒の無音をともに**トリムし、
末尾→先頭の不連続サンプル差を 0.0028 まで抑えたうえでループ再生する
（BGM は SFX の45%の音量、右下のボタンで 100/60/30/OFF を切り替え可能）。
別のタスク `01a121a7-b574-778f-848e-27c281d9720b`（ambient 版）は最後まで **PENDING** で、
成果物は得られなかった（`logs/bgm-tasks.json`）。

#### 6.4 権利・表記

- 生成物の取り扱いは StepFun の利用規約に従う。公式ドキュメント上のモデル課金表記は
  **"Free (limited time)"**（無料トライアル終了時は有料版に移行する旨の注記付き）。
- 本プロジェクトが外部へ公開・再配布する場面では、
  **「音声: generated with StepFun (stepaudio-3-gen-preview / stepaudio-3-music-preview)」**
  のクレジット表記を添えるものとする（本ファイルを権利表記の正とする）。
- 生成プロンプト原文は上表に記録しており、第三者素材を模倣する指示は含まない
  （すべて本プロジェクト用の説明文）。


