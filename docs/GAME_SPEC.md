# DiceYahzee Ver.0.2 確定仕様書（正本）

本書は `ParvaMirabiliaDice` の現行実装が持つ確定仕様を、コードの実数値から書き出した正本である。用語は `GAME_DESIGN.md` に合わせ、食い違いは末尾の「不整合メモ」にだけ記す。数値はすべてコード上の定数・式を根拠とする。

## 1. 概要と版号

| 項目 | 値 |
| --- | --- |
| タイトル | DiceYahzee |
| 副題 | D I C E   D U E L |
| 版号表示 | `Ver.0.2`（ビルド版号 `0.2.3`） |
| ジャンル | 5d6 を振り直して役を作り、同時ダメージで相手の HP を削る 1 人用の短い対戦 |

タイトル画面は縦に、見出し「DiceYahzee」（58px・Aoboshi One、y=220）、副題（13px、y=166）、例示の 5 ダイス（86px 角、間隔 106、y=67、出目 1・1・1・4・5 のうち左 3 個を保持表示）、相手 5 種のボタン列（y=-65）、相手の説明行（y=-112）、「対局開始」（y=-162）、「練習」（y=-241・左）、「遊び方」（y=-241・右）を並べる。`Ver.0.2` は 14px・右寄せ（box 170×26）で、画面下段 y=-319 に、メニューの ≡ 記号（(-554,-319)・46×38・22px アイコン）と音量バー（(451,-319)）・消音トグル（(554,-319)・46×38・24px アイコン）と同じ帯に置く。版号はこの帯の空き位置（x=280）に収まる。

| 下段 chrome | 位置 | 寸法 | 内容 |
| --- | --- | --- | --- |
| メニュー | (-554, -319) | 46×38 | ≡ 記号（icon-menu・22px） |
| 版号 | (280, -319) | 170×26 | 「Ver.0.2」14px・右寄せ |
| 音量バー | (451, -319) | スライダー | 連続音量 |
| 消音トグル | (554, -319) | 46×38 | icon-sound / icon-sound-off・24px |

タイトル下部には「オンライン対戦」ボタン（(0,-300)・320×42・19px）も並ぶ。
左側の「更新確認」から、公開 GitHub Release の更新を認証なしで確認できる。起動時の確認、ZIP の検証と差し替えは `docs/PRIVATE_RELEASE_UPDATE.md` に記す。

タイトル直下の相手 5 種は 176 間隔・164×52・18px で並び、選択中の 1 つだけが金色で強調される。説明行は「相手の体力 N　／　全難易度が上級の読みで最善手を比べる。」の形で、選んだ難易度の実 HP を出す。

**根拠**: `Presentation/GameFlow.ScoreDuel.cs`（`SDBuildTitle`、`SDScoreFontFor` 周辺）, `Presentation/GameFlow.ScoreDuelLayout.cs`（`SDStyleRemainingScreens`）, `Presentation/GameFlow.Online.cs`（`BuildOnlineEntry`）, `Tests/PlayMode/ScoreDuelFeedbackTests.cs`（`TheTitleNamesItsBuild`）

## 2. 画面遷移

`GameFlow` は 1 枚の Canvas の下に `ScoreDuel` ルートを作り、その中に `SDTitle` / `SDBattle` / `SDResult` の 3 面と、モーダル `SDRules` / `SDScorecard` / `SDMenu` / `SDCredits` / `SDPractice`、別ルート `OnlineDuel` を置く。

| 画面 | 開く操作 | 閉じる・進む操作 |
| --- | --- | --- |
| タイトル | 起動直後 | 対局開始・練習・遊び方・オンライン対戦 |
| 対戦 | 対局開始 | 決着で結果、≡ / Escape でメニュー |
| 結果 | 決着 | 記録・再戦・タイトル |
| メニュー | ≡ / Escape | 戻る・遊び方・タイトル・使用素材・クレジット・成立時の動き |
| 遊び方 | タイトル / 対戦右上 / メニュー | 練習・戻る |
| 使用素材・クレジット | メニュー | 戻る |
| 練習 | タイトル / 遊び方 / 対戦から | 戻る、または「対局を始める」「対局へ戻る」 |
| オンライン | タイトル | タイトルへ |

各モーダルは暗幕の上に 1 枚のパネルを置く。

| モーダル | 見出し | パネル |
| --- | --- | --- |
| `SDRules` | 遊び方 | 980×660 |
| `SDScorecard` | 使う役を変える（確定後は「使った役」） | 920×638 |
| `SDMenu` | メニュー | 700×550 |
| `SDCredits` | 使用素材・クレジット | 980×620 |
| `SDPractice` | 練習 | 980×600 |

対局開始（`SDStartMatch`）は ScoreDuel を作り、初回を自動で振り、タイトルを隠して対戦面を出す。決着後は結果面へ移り、「再戦」は同じ入口を使い、「記録」は `SDScorecard` を開く。モーダルが 1 つでも開いている間は `SDModalOpen` が真になり、対戦のポインタ・キー・ホイール入力と演出時間が止まる。`SDRefreshScope` は 3 面の CanvasGroup の `interactable` / `blocksRaycasts` をモーダル状態へ合わせる。練習は対戦から開いた場合だけ「対局へ戻る」を出し、タイトルから開いた場合は「対局を始める」で新しい対局に入る。

**根拠**: `Presentation/GameFlow.ScoreDuel.cs`（`BuildScoreDuelPresentation`, `SDStartMatch`, `SDAdvance`, `SDShowResult`, `SDCloseModals`, `SDRefreshScope`, `SDOpenPractice`）, `Presentation/GameFlow.ScoreDuelNavigation.cs`

## 3. ラウンドの基本の流れ

1 ラウンドは「ロール → 保持 → コミット → 被ダメ交換」の順で進む。`ScoreDuel.Phase` が進行を持つ。

| 段階 | 内容 |
| --- | --- |
| 開始 | `Begin` が Round=1、自分の HP=1500、相手 HP=難易度別値にし、`PrepareRound` で相手の手を先に確定する |
| ロール | 人間は 5d6。相手の出目は人間の投擲前に確定済みで、公開情報である |
| 保持 | 残すダイスをクリック・横ドラッグで選ぶ |
| 振り直し | 保持しなかった目を最大 2 回振る（ヤッツィー成立の翌ラウンドは 3 回）。残した回数×40 は確定時に回復する |
| コミット | 使う役を選び、下引きで発射して確定する |
| 被ダメ交換 | 双方の得点から共通軽減を引いた被害を同時に与える |
| 次ラウンド | クリックで `NextRound` へ進む |

進行は 5 つの段階を持つ。

| 段階 | 意味 |
| --- | --- |
| AwaitingRoll | 初回の投擲前。持越で戻した場合はここを飛ばす |
| AwaitingReroll | 振り直しと役の選択ができる |
| AwaitingSplit | 振り直しを使い切った。役の選択と発射だけができる |
| Resolving | 被害を確定した。次への入力待ち |
| Finished | 決着済み |

人間の振り直しはラウンド開始時に 2 回（前ラウンドのヤッツィー成立があれば 3 回、上限 3）。画面で使う相手（Expert）も同じく 2 回（同条件で 3 回）で、人間の振り直し回数は相手の将来の出目を変えない（乱数ストリームが別）。内部互換の Balanced だけは従来どおり 1 回の簡易方針を保つ。9 欄を使い切った側だけが次ラウンドの開始時に欄を戻す（HP と保存の使用状態は戻さない）。

確定時に使わなかった振り直しは、被害を適用する前に 1 回につき 40 の回復へ変わる（4 章）。両者とも同じ規則で、プレイヤー／NPC の非対称はない。

被害は両方の点数の低い方を P・N として共通軽減 `A = floor(min(P,N)/2)` を両方の攻撃から同じだけ引き、同時に与える。HP は 0 で止め、どちらかが 0 になった時点で決着する。回復は被害より先に適用し、丸める前の残 HP（同時 KO の比較対象）にも含める。同時 KO は丸める前の残 HP の大きい側の勝ち、等しければ引き分け（「奇跡」）とし、理由行は「同時KO（残HP判定）」と表示する。`SafetyRoundBound = ceil(2750/25) = 110` は自動試合のループガードであり、ゲームのターン上限でもターン数判定でもない。最大 HP の相手と最小被害 25 の交換を想定した値で、回復が続けばこれを超え得るため、呼び出し側は実際の決着で必ず抜ける。

**根拠**: `Core/ScoreDuel.cs`（`Begin`, `PrepareRound`, `Roll`, `Reroll`, `Resolve`, `SafetyRoundBound`）, `Core/VersusDuel.cs`（`Prepare`, `Resolve`）

## 4. 役・得点・ダメージ

得点は `floor(5 ダイスの出目合計 × 選択役の倍率)`。倍率は `Multipliers` の整数で持つ。未成立の欄も選べ、その場合は一律 ×10 で得点は `出目合計 × 10` になり、その欄を消費する。ヤッツィー成立だけは倍率を使わず 900 点固定である。端数は出ない（総和も倍率も整数）。

| 役 | enum | 成立条件 | 倍率 | 備考 |
| --- | --- | --- | --- | --- |
| ペア | Pair | 同じ目が 2 個以上 | ×15 | マスクは最小 2 個 |
| ツーペア | TwoPair | 異なる 2 組のペア | ×20 | |
| スリーカインド | Three | 同じ目が 3 個以上 | ×20 | |
| フルハウス | FullHouse | 3 個＋2 個（異なる目） | ×25 | マスクは全 5 個 |
| 小ストレート | SmallStraight | 異なる 4 連続 | ×20 | |
| フォーカインド | Four | 同じ目が 4 個以上 | ×30 | |
| 大ストレート | LargeStraight | 異なる 5 連続 | ×25 | |
| ヤッツィー | Five | 同じ目が 5 個 | 成立時 900 点固定 | 未成立なら ×30 の式 |
| チャンス | Chance | 条件なし（常に成立扱い） | ×10 | 第 5 章 |

役名・倍率タグ・得点の表記は次のとおり。右の役レールは「役名 → 倍率タグ → 得点」の 3 列で、倍率タグはヤッツィーが「900点」、チャンスが「逆転」、他は「×15」「×20」「×25」「×30」を出す。役レールの点数列は選択中だけ金色の印を伴い、使用済みは「—」と暗色で示す。

| 役 | 倍率タグ | 遊び方の説明 |
| --- | --- | --- |
| ペア | ×15 | 同じ目 2 つ · ×15 |
| ツーペア | ×20 | ペア 2 組 · ×20 |
| スリーカインド | ×20 | 同じ目 3 つ · ×20 |
| フルハウス | ×25 | 3 つ＋2 つ · ×25 |
| 小ストレート | ×20 | 4 連続 · ×20 |
| フォーカインド | ×30 | 同じ目 4 つ · ×30 |
| 大ストレート | ×25 | 5 連続 · ×25 |
| ヤッツィー | 900点 | 同じ目 5 つ · 900点 |
| チャンス | 逆転 | 条件なし · ×10 |

得点の計算は、中央に `出目合計 × 倍率`（例「17 × 20」）の内訳を出す。ヤッツィー成立だけは「900」を出す。遊び方は「得点＝出目合計×倍率」を最上段に掲げる。

ダメージは、P を自分の得点、N を相手の予告点数として次で決まる。

| 項目 | 式 |
| --- | --- |
| 共通軽減 | `A = floor(min(P,N)/2)` |
| 相手が受ける被害 | `DamageToNpc = P − A` |
| 自分が受ける被害 | `DamageToPlayer = N − A` |

例として、{2,3,3,4,5} の小ストレートは総和 17 で 340 点、同じ出目の大ストレートは未成立で 170 点。{6,2,5,3,4} の大ストレートは総和 20 で 500 点。{3,3,3,3,3} のチャンスは 150 点である。

| 出目 | 選ぶ役 | 成立 | 総和 | 得点 |
| --- | --- | --- | --- | --- |
| 1・1・1・1・6 | フォーカインド | 成立 | 10 | 10×30 = 300 |
| 6・6・5・4・2 | ペア | 成立 | 23 | 23×15 = 345 |
| 2・3・3・4・5 | 小ストレート | 成立 | 17 | 17×20 = 340 |
| 2・3・3・4・5 | 大ストレート | 未成立 | 17 | 17×10 = 170 |
| 6・2・5・3・4 | 大ストレート | 成立 | 20 | 20×25 = 500 |
| 3・3・3・3・3 | チャンス | 成立扱い | 15 | 15×10 = 150 |
| 1・1・1・1・1 | ヤッツィー | 成立 | 5 | 900（固定） |

ダメージの例として、P=560・N=700 なら `A = 280` で相手へ 280・自分へ 420。同点 320 なら双方 160。低い側の攻撃も残る。

**根拠**: `Core/ScoreDuel.cs`（`ScoreRules`、`Names`、`Multipliers`、`UnformedMultiplier`、`YahtzeeScore`、`BuildTable`、`Add`、`ScoreForecast`、`HealthSettlement`）, `Presentation/GameFlow.ScoreDuelLayout.cs`（`SDMultiplier`, `SDRailMultiplier`, `SDCalculation`）, `Presentation/GameFlow.ScoreDuel.cs`（`SDBuildRules`, `SDBuildLiveScorecard`）, `Tests/EditMode/ScoreDuelTests.cs`（`StraightMasksContainOneOfEachNecessaryFaceAndNeverDuplicates`, `AnUnformedCategoryPaysTheDiceTotalAtTheFlatRateAndConsumesItsSlot`）

### 振り直し温存による回復

確定時、ダメージを適用する前に、使わなかった振り直し 1 回につき 40 回復する（`ScoreRules.RerollHeal = 40`、`ScoreRules.HealFor`）。回復はその側の最大 HP（自分 1500、相手は難易度別の初期 HP）で止まり、超えた分は捨てる。両者とも基本 2 回、ヤッツィー成立の翌ラウンドは 3 回なので、最大 120 まで回復できる。プレイヤーと NPC の間に非対称な扱いはない。

例：HP 1400 で振り直しを 2 回残して確定すると 80 回復して 1480 になり、そこから被害を引く。HP 1490 なら 10 だけ回復して 1500 で止まる。回復後の HP が被害で 0 以下になれば KO である。

**根拠**: `Core/ScoreDuel.cs`（`ScoreRules.RerollHeal`, `ScoreRules.HealFor`, `HealthSettlement`, `ScoreRound.PlayerHeal`, `ScoreRound.NpcHeal`, `ScoreDuel.Resolve`）, `Core/VersusDuel.cs`（`VersusRound.Heal`, `VersusRound.HpBefore`, `Resolve`）

## 5. Chance 直前宣言の XOR 反転規則

チャンスは条件を持たず、どの出目からも選べる。`ScoreRules` の内部では常に `IsFormed = true`・マスク 31・得点 `出目合計 × 10` として作られる（ユーザー決定 2026-10-18 の設計意図、倍率は 2026-10-20 に ×10 へ統一）。出目が役を成していなくてもチャンスは点数を持つ。チャンスも 9 欄の 1 つで、使えばその対戦では再使用できない（9 欄を使い切れば戻る）。

被害の受け手の入れ替えは、両者の宣言の XOR で決まる。

| 自分の宣言 | 相手の宣言 | 反転 | 結果 |
| --- | --- | --- | --- |
| チャンス以外 | チャンス以外 | なし | `DamageToNpc = P − A`、`DamageToPlayer = N − A` |
| チャンス | チャンス以外 | あり | 受け手が入れ替わり `DamageToPlayer = P − A`、`DamageToNpc = N − A` |
| チャンス以外 | チャンス | あり | 同上 |
| チャンス | チャンス | なし | 打ち消し合い、通常どおり |

`ScoreDuel.Forecast` は `reversed = (kind == Chance) != (Intent.Kind == Chance)` を立て、`ScoreForecast` がその真偽で受け手を入れ替える。`VersusDuel.Resolve` も同じ XOR（`(players[0].Kind == Chance) != (players[1].Kind == Chance)`）を書く。両者は同一規則である。

相手 NPC もチャンスを宣言し得る。`ScoreHoldPolicy.Utility` はチャンスの効用を `offer.Score` とし、ラウンド 5 未満では 4 を引く。`Chance` を候補から外す処理も `IsFormed` での門番も置かない（設計意図コメント 2026-10-18）。

反転の実例（テストの固定値）：自分のヤッツィー 900 点に対し相手の予告がチャンス 51 点なら `A = 25`、反転により自分が 875、相手が 26 の被害を受ける。自分のチャンス 50 点に対し相手の予告が 101 点なら `A = 25`、自分が 25、相手が 76。双方チャンス（50 点と 51 点）なら反転せず、自分が 26、相手が 25。

表示では、対戦中にチャンスを選ぶと中央の 5 ダイスすべてに金色の輪と立ち上る火花が出て、入れ替えの対象を示す（控えめ設定では静止した輪のみ）。反転規則の文は遊び方の「チャンス：ダメージ計算式を反転させます。」が担う。オンライン画面も規則と予告の欄が担い、浮遊する告知行は置かない。

**根拠**: `Core/ScoreDuel.cs`（`BuildTable` の Chance 行, `Forecast`, `ScoreHoldPolicy.Utility`）, `Core/VersusDuel.cs`（`Resolve`）, `Presentation/GameFlow.ScoreDuelEffects.cs`（`SDTickChanceGlow`）, `Presentation/GameFlow.ScoreDuel.cs`（`SDBuildRules`）, `Tests/EditMode/ScoreDuelTests.cs`（`ChanceReversesDamageAndCanBeCombinedWithCarry`, `NpcChanceDeclarationInvertsDamageLikeOnline`, `ChanceIsASingleUseSafetyNetRatherThanAnAlwaysAvailableAttack`）, `Tests/PlayMode/ScoreDuelFeedbackTests.cs`（`ChanceSelectionGathersTheHandInGoldAndLeavesNoCueLine`）, `Tests/PlayMode/OnlineResultDisplayTests.cs`（`ChanceDeclarationInvertsTheDamageAndNamesNoCueLine`）

## 6. 相手5種

難易度は相手の初期 HP だけを変える。相手の打ち方は 5 種とも同じ上級（Expert）の期待値方針である。自分の HP はどの難易度でも 1500。

| ボタン | 内部の型 | 相手 HP |
| --- | --- | --- |
| 入門 | Expert | 1250 |
| 初級 | Expert | 1750 |
| 中級 | Expert | 2000 |
| 上級 | Expert | 2250 |
| 超級 | Expert | 2750 |

相手 HP の表は `NpcHpByTier = {1250, 1750, 2000, 2250, 2750}` の 1 箇所で持つ。ボタンも対局もこの表を参照するため、表示と実際の HP は食い違わない。難易度名は `NpcPersonality` の `Expert` だけを使い、`Balanced`（標準）・`Aggressive`（大役）・`Careful` は内部互換として残るが画面には出さない。

`Expert` の相手は、残り振り直し回数の全出目を確率付きで列挙する有限ダイスの厳密方針（252 の目のヒストグラム、462 の保持、4368 の遷移を 1 度だけ生成し、DP を 2 回通す）で役と保持を選ぶ。振り直しは 2 回（前ラウンドのヤッツィー成立があれば 3 回）。相手の手は人間の投擲前に確定し、役名・点数・実際の 5 出目を最初から公開し、人間の投擲や保持を見ても変えない。相手が負傷しているときは、停止して残す振り直し 1 回を 40 の回復価値として期待値に含める。満 HP では回復価値が 0 なので、従来の得点だけの方針と同じになる。

**根拠**: `Core/ScoreDuel.cs`（`NpcHpByTier`, `NpcHpForDifficulty`, `StartingHp`, `PrepareNpcTurn`, `NpcPersonality`）, `Core/ScoreHoldPolicyExact.cs`, `Presentation/GameFlow.ScoreDuel.cs`（`SDOpponentProfiles`, `SDSelectOpponent`）

## 7. 影響演出

攻撃の演出は共通の `SharedAttackPresenter` が 1 本の時間軸で進める。全体は `Duration = 0.90` 秒で、次の時刻に段階が切り替わる。

| 時刻 | 段階 | 内容 |
| --- | --- | --- |
| 0.12 | `LoadingEnd` | 5 ダイスが相手へ向かう移動を始める |
| 0.24 | `CollisionTime` | 5 ダイスが中央で接触する |
| 0.28 | `HitStopEnd` | 接触の静止が終わる |
| 0.44 | `PushEnd` | 押し込みが終わる |
| 0.54 | `BreakthroughEnd` | 勝ち側の手が相手側へ抜ける |
| 0.64 | `ContactTime` | 勝ち側の手が相手の HP へ到達する |
| 0.70 | `RecoilStart` | 原状へ戻る動きを始める |
| 0.90 | `Duration` | 原状に復帰し、暗幕と演出を消す |

勝敗は `Begin` へ渡す `ownAttacks` と `enemyAttacks` の比較で決める。両者は基礎点・役倍率を含む計算済みの手全体の攻撃値で、等しければ勝敗なし（`Winner = -1`）とし、両手を中央で接触させる。相手の HP へ抜けるのは勝ち側（大きい側）の手であり、負け側の手ではない。両者の一方だけが Chance を宣言したときは攻撃側の own／enemy の対応を入れ替え、入れ替え後の比較で勝ち側を決める。

| 項目 | 内容 |
| --- | --- |
| 表示ダイス寸法 | 60px 角（`DisplaySize`） |
| ダイスの間隔 | 64px（`SlotSpacing`） |
| 接触時の 2 手の間隔 | 2px（`BodyGap`） |
| 押し込み量 | 22px（`PushDistance`） |
| 負け側の散開距離 | 基準 150px（`SweepDistance`）。中央のダイスが最も近く 0.85〜1.29 倍で散開し、強さ段階で最大 +18% |
| 負け側の回転 | ±540度（`SweepSpin`、位置で逆向きに交互） |
| 負け側の散開方向 | 終始画面右。上下に `SweepFan`（0.55）で扇状に開く |
| 負け側の散開開始 | 押し込み完了（0.44秒）。中央のダイスから 0.025秒刻みで遅れて飛ぶ（`SweepStagger`） |
| 引き分けの跳ね返り | 14px（`TieBounce`、勝敗なしで両手が接触点から離れる量） |
| 暗幕の濃さ | 0.55（`LoadingDim`） |
| 暗幕のフェード | 0.08 秒（`DimFadeTime`） |
| 暗幕の並び | `OverlaySortOrder = 500` |
| ダイスの並び | `DieSortOrder = 510`（暗幕より上） |

標準の演出では、0.12 秒から移動を始め、0.24 秒に接触する。勝ち側は 0.28〜0.44 秒で相手側へ 22px 押し込み、押し込みの完了した 0.44 秒から相手のダイスを薙ぎ払う。負け側の 5 ダイスはそれぞれ個別に、中央のダイスから順に画面右方向へ扇状に散開して回転しながら飛び去り、0.64 秒までに消える。勝ち側の 5 ダイスは一直線の塊のまま 0.54 秒から相手の HP へ向かい、0.64 秒に到達する。勝敗なしでは両手が接触点へ同じだけ押し込み、`TieBounce` で軸方向へ 14px ずつ弾かれ、0.44 秒に元の接触間隔へ戻って止まる。左右へ滑らせない（旧 `DeflectDistance` の横スライドは廃止）。

衝撃の輪・放射線・閃光は到達先にだけ描く。勝ち側があれば到達する HP の位置、引き分けなら中央の接触点である。勝敗と無関係に画面中央へ固定で出るリングは描かない（中央リングは 2026-10-20 に削除）。

暗幕は `LoadingDim` で 0.55 の黒を敷く。標準演出の暗幕は開始時に 0.08 秒でフェードインし、全体終了前の 0.10 秒で消える。ダイスは `DieSortOrder = 510` の Canvas で暗幕より上に描く。

控えめ設定ではダイスを移動させない。接触の 0.24 秒以降、中央に静止した接触線（幅 80px・高さ 3px・強さで ±6%）を出し、火花と拡散を行わない。

`Reset` は全ダイスの親・兄弟順・ローカル位置・回転・拡大・Canvas 設定・`CanvasGroup` の値を `CaptureHand` の時点へ戻し、暗幕を透明、接触線と衝撃点を非表示、`winner=-1` にする。`Sample(Duration)` も同じ原状復帰を通るため、演出の見た目は元へ戻る。ただし `Active` は `Reset` まで真のままであり、`Sample` を通しただけでは攻撃演出が終わった扱いにはならない。判定は `Active` と `Reset` で管理する。

操作ガイドは右側の x=350 に置く。マウス記号は下引きの 3 段の山形を示し、1.8秒周期でマウスと矢印が一緒に42px下へ移動し、両方が完全に消えてから初期位置へ戻って再生する。見えている間は上へ戻らない。3 個保持すると右クリックの図形へ変わる。ホイール記号は内側だけ上下 4px 動く。攻撃後は「次へ」の輪を示し、ボタン以外の背景・ダイス・無効領域のクリックで次ラウンドへ進む。演出終了前の押下と同じ入力の二重実行は `_sdInputFrame` で防ぐ。投擲中・確定後・モーダルではガイドを隠す。

「成立時の動き：控えめ」を選ぶと、移動・拡大・光・迷いを抑え、成立した役と点数は同じように伝える。設定は `arcane.reducedMotion` に保存する。控えめのときはガイドを静止表示し、揺れ・飛行・パルスを行わない。

中央の得点数字は確定表示のときだけ点数で大きさを変える。

| 条件 | フォント |
| --- | --- |
| 150 未満 | 60 |
| 150 以上 350 未満 | 64 |
| 350 以上 600 未満 | 72 |
| 600 以上 | 80 |

確定前の予告数字は 64 に固定し、約束の数字が踊らないようにする。相手の予告数字は 50 である。HP バーは厚さ 36 で、単体戦とオンラインが共通の `HealthPresentation` を通る。1 ラウンドの表示は「回復 → 被害」の 2 段階で、`HitStopEnd = 0.28` から `ContactTime = 0.64` までに回復後の値へ上がり、`ContactTime` から `HealthPresentation.End = 1.24` までに被害後の値へ下がる。`CollisionTime = 0.24` ではまだ HP は動かない。行の形式は「あなた 1500/1500 +40 −35」「相手 1250/1250 −35」の 1 行で、回復を `SDHealInk`(27,91,64) の緑、被害を `SDDamageInk`(193,39,45) の赤で示す。ただし表示するのは今いる段階の数字だけであり、回復中は緑の `+N` だけ、被害中は赤の `−M` だけを出し、両方を同時には並べない。回復が 0 のときは緑の数字を出さない。バーは回復分を含む途中値を通るため、被害が回復より小さい場合は開始値より高い位置で止まる。予告のゴーストは回復を含む最終 HP を指し、正味で増えるときは緑、減るときは赤で描く。

役成立の演出は役ごとの段階で規模を変える。成立の判定は攻撃の前に、手が揃った時点で行い、その演出を出す。

| 役 | 段階 | 演出時間（通常 / 控えめ） | 火花 |
| --- | --- | --- | --- |
| ペア | 0 | 0.36 / 0.30 | 3 |
| ツーペア・スリーカインド | 1 | 0.55 / 0.30 | 8 |
| フルハウス・小ストレート | 2 | 0.74 / 0.30 | 14 |
| フォーカインド・大ストレート | 3 | 0.84 / 0.30 | 19 |
| ヤッツィー | 4 | 1.02 / 0.30 | 28 |

エフェクトの描画は `IScoreEffectSurface`（線・輪・楕円・消去）を境に、判定側の進行から切り離してある。いつ・どの役で・どのダイスに何を出すかは `SharedAttackPresenter` と `ScoreFormationPresenter` が持ち、境界は図形だけを運ぶ。実際に描く実装は `ScoreHandEffectGraphic` で、`UiFactory.AddVertexGraphic<T>` が生成する。この生成は CanvasRenderer を先に確保して生存を確かめる。uGUI は Graphic の CanvasRenderer を最初の再構築時に遅延生成するが、自作 Graphic 子クラスではその遅延生成が既に破棄された renderer を返すことがあり、Graphic がそれを保持したままだと以後の全再構築が `canvasRenderer == null` の早期 return に入り、メッシュが一切描かれない。組み込みの `Image` とプレイヤービルドはこの経路を通らないため、エディタ実行で自作の線・輪・衝撃波だけが消え、ダイスと数字は残る。生成側で先行確保し、破棄された場合は一度だけ作り直す。エフェクト用 Graphic は入力対象にしない（`raycastTarget = false` を生成側で設定する）。

**根拠**: `Presentation/SharedAttackPresenter.cs`（`Duration`, `LoadingEnd`, `CollisionTime`, `HitStopEnd`, `PushEnd`, `BreakthroughEnd`, `ContactTime`, `RecoilStart`, `DisplaySize`, `SlotSpacing`, `BodyGap`, `PushDistance`, `TieBounce`, `SweepDistance`, `SweepSpin`, `SweepFan`, `SweepStagger`, `LoadingDim`, `DimFadeTime`, `OverlaySortOrder`, `DieSortOrder`, `Begin`, `Sample`, `Reset`, `ApplyReduced`, `ComputePose`, `SweepProgress`, `SweepScatter`, `SweepSpinDegrees`, `RestoreAll`）, `Presentation/ScoreEffectSurface.cs`（`IScoreEffectSurface`）, `Presentation/UiFactory.cs`（`AddVertexGraphic`）, `Presentation/GameFlow.ScoreDuelEffects.cs`（`SDPlayHandEffects`, `SDHandEffectTier`, `ScoreHandEffectGraphic`）, `Presentation/GameFlow.ScoreDuelImpact.cs`（`SDAnimateImpactPresentation`）, `Presentation/GameFlow.ScoreDuel.cs`（`SDScoreFontFor`, `SDRefresh`, `SDBuildHudDetails`）, `Presentation/GameFlow.ScoreDuelLayout.cs`（`SDHealthLine`, `SDDamageInk`, `SDAnimateHpLoss`）, `Presentation/GameFlow.ScoreDuelGuides.cs`（`SDGuideX`, `SDTickGestureGuides`, `SDDrawGestureGuide`）, `Tests/PlayMode/ScoreDuelFeedbackTests.cs`（`TheCentralNumeralSwellsWithTheScore`）, `Tests/PlayMode/SharedAttackPresenterTests.cs`（`LoserDiceAreSweptAwayWhileTheWinningHandStrikesTheHp`）, `Tests/PlayMode/EffectStrokeRenderingTests.cs`（`TheFactoryStrokes_ReachTheRenderedPixels`, `APlainSpriteImage_RendersThroughTheSamePath`）

## 8. キャリー（保存枠）

出目の保存は対局中 1 回、人間だけが使える。確定前に対戦中央の持越ボタンで選択状態にし、下引きで発射したときにだけ 1 回分を消費する。被害は通常どおり共通軽減を適用した同時攻撃で、保存による追加の不利は無い。

| 部品 | 位置・寸法 |
| --- | --- |
| 持越ボタン | (0, -230)・204×48 |
| フロッピー記号 | ボタン内 (-82, 7)・20px |
| ラベル | ボタン内 (0, 10)・19px（選択中は 16px） |
| 残数表示 | ボタン内 (82, 9)・16px・「1」または「0」 |
| 収納棚 | ボタン内 (0, -13)・160×20 |
| 収納ダイス | 19px 角・間隔 27・金色の輪（外径 25） |

保存の可否は、対戦面が入力可能で、まだ保存を使っておらず、ドラッグ中でも演出中でもないときに限る。選択状態のラベルは「持越 選択中」、通常は「持越」。選択を切り替えると 0.56 秒の合図（外へ広がる輪と、選択時は手から棚へ向かう火花）が出る。控えめ設定では即座に切り替わる。選択中は中央に金色の括弧（左右 ±333、上 38、下 -92、角の長さ 21）と、棚を囲む金色の楕円を描く。

棚は、選択中で投擲していないとき、保存が成立したとき、または次ラウンドで目が戻ってくるときに見える。棚のダイスは保持に関係なく 5 個すべてを「残した」印（金色の輪）で示す。規則が手の全体を次ラウンドへ運ぶためである。

発射で保存を選んだ場合は、選択中の役で通常どおり点数 P を計算し、対象の役欄と保存の 1 回分を消費して現在の 5 出目と保持状態を記憶する。生存して次ラウンドへ進んだ場合に限り、記憶した出目と保持状態を自動で取り出し、最初の自動投擲を行わずに 2 回振り直せる。決着した場合は保存を失効させ、保存が消費済みなら再選択できない。

手順は次の 4 段である。

1. 選択中の役で通常どおり点数 P を計算し、相手の予告点数を N とする。
2. 共通軽減を適用した被害を同時に与える。
3. 双方の選択役を消費し、現在の 5 出目と保持状態を保存する。
4. 生存していれば、次ラウンドは保存した出目と保持状態を自動で取り出し、さらに 2 回振り直せる。最初の自動投擲は行わない。

**根拠**: `Core/ScoreDuel.cs`（`SaveHand`, `Resolve`, `PrepareRound`, `SavedHandUsed`, `HasSavedHand`, `Finish`）, `Presentation/GameFlow.ScoreDuelCarry.cs`（`SDBuildCarryPresentation`, `SDRefreshCarryPresentation`, `SDBeginCarryToggle`, `SDDrawCarrySelection`, `SDBeginCarryReturn`）, `Presentation/GameFlow.ScoreDuel.cs`（`SDSaveHand`, `SDBuildLiveScorecard`, `SDAdvance`）, `Presentation/DiceView.cs`（`ringPad`）, `Tests/PlayMode/ScoreDuelFeedbackTests.cs`（`ArmedCarryRackRingsEveryDieAsKept`, `SaveToggleIsReversible_AndOnlyAFullPullCarriesTheDiceIntoTheNextTurn`）

## 9. 操作

| 操作 | 内容 |
| --- | --- |
| クリック | 残すダイスを 1 個切り替える |
| 横ドラッグ | 通過したダイスを同じ保持／解除状態へ揃える |
| 下ドラッグ | 5 ダイスが下がり、離すと発射して確定する |
| 右クリック / Space | 残さなかったダイスだけを振り直す |
| ホイール上・下 | 未使用の役を 1 ノッチずつ循環選択する |
| 1〜5 | 保持の切り替え |
| Enter | 確定または次へ |
| Tab | 次のボタンへ |
| Escape | メニュー（モーダル中は閉じる） |

発射は下への移動が 28 以上で成立する。照準は `delta.y < -20` かつ `-delta.y > |delta.x| × 0.65` のときに入り、引きは 0〜55 に収める。照準に入ると保持状態を元へ戻し、離した時に成立していれば発射、届かなければキャンセルする。振り直しと結果使用のボタンは無い。

操作ガイドは右側の x=350 に置く。マウス記号は下引きの 3 段の山形を示し、1.8秒周期でマウスと矢印が一緒に42px下へ移動し、両方が完全に消えてから初期位置へ戻って再生する。見えている間は上へ戻らない。3 個保持すると右クリックの図形へ変わる。ホイール記号は内側だけ上下 4px 動く。攻撃後は「次へ」の輪を示し、ボタン以外の背景・ダイス・無効領域のクリックで次ラウンドへ進む。演出終了前の押下と同じ入力の二重実行は `_sdInputFrame` で防ぐ。投擲中・確定後・モーダルではガイドを隠す。

「成立時の動き：控えめ」を選ぶと、移動・拡大・光・迷いを抑え、成立した役と点数は同じように伝える。設定は `arcane.reducedMotion` に保存する。控えめのときはガイドを静止表示し、揺れ・飛行・パルスを行わない。

入力が止まる条件は次のとおり。ダイスの保持・ホイール・右クリック・発射は、対戦面が入力可能で、投擲中・確定後・相手の再生中・モーダル中でないときに限る。振り直しは残り回数が 0 のときも止まる。モーダルを開いた時点で進行中のドラッグは元の保持と位置へ戻す。

**根拠**: `Presentation/GameFlow.ScoreDuelInput.cs`（`SDFlickDistance`, `SDPointerMove`, `SDPointerEnd`, `SDHandleWheel`, `SDHandleRightClick`）, `Presentation/GameFlow.ScoreDuelGuides.cs`（`SDGuideX`, `SDTickGestureGuides`, `SDDrawGestureGuide`）, `Presentation/GameFlow.ScoreDuelNavigation.cs`（`SDTableClick`）, `Presentation/GameFlow.ScoreDuel.cs`（`SDHandleShortcut`, `SDToggleMotion`, `SDRefreshMotionLabel`）

## 10. オンライン対戦

実装済みの通信は Unity Multiplayer Services（セッション）と Netcode for GameObjects（ゲーム通信）である。Unity Cloud の `DiceYahzee` プロジェクトに紐付け、匿名認証と招待コード参加を使う非公開 2 人セッションを Relay で張る。専用サーバーは置かない。

| 層 | 実装 |
| --- | --- |
| 進行 | Unity 非依存の `VersusDuel` が二人分の HP・ダイス・保持・残り振り直し・役の使用・持越・ラウンド・勝敗を持つ |
| 乱数 | ホストだけが持つ |
| 通信要求 | 保持マスクと振り直し、役と持越の提出、次ラウンドへの同意、振り直し終え、再戦同意の 5 種 |
| 検証 | 各要求にプレイヤー・対局世代・ラウンド・単調増加する操作番号を付け、ホストが接続 ID・世代・段階・残り回数・役の使用状態・番号を検証する |
| メッセージ | `parva.duel.v1.hello` / `welcome` / `command` / `snapshot`、最大 24576 |
| 切断 | 進行を止め、残った側に中断を表示する。自動再接続・ホスト移譲は行わない |

規則は CPU 戦と同じである。1 ラウンドに各自 2 回振り直し（ヤッツィー成立の次ラウンドは 3 回、`MaxRerolls = 3`）、役と持越（各自 1 回）を提出し、双方の提出後に同時ダメージと勝敗を確定する。確定時に使わなかった振り直しは 1 回につき 40 回復し、被害を適用する前にその席の最大 HP で止める。チャンスの入れ替えは CPU 戦と同じ XOR である。同時 KO は丸める前の残 HP を比較し、等しければ引き分けとする。

公開の段階は次のとおり。ラウンド開始の初回出目は両者へ公開し、振り直しの最中は相手の出目を開始時の値で伏せ、双方が振り直しを終えた時点で最終の出目をそろって公開する。提出した役・得点・持越と、その役を使った事実は双方の提出まで相手向けスナップショットから隠す。

オンライン画面はロビー・対戦・結果を持つ。ロビーは「部屋を作る」、招待コード入力欄（上限16文字）、「コードで参加」、「タイトルへ」を持つ。ホストの待機中は、参加で確定した招待コードを「コードをコピー」でコピーできる。コピー後の通知は別表示で出す。対戦はシングルの共通ビルダー（`GameFlow.DuelTable.cs`）から卓・ダイス・役行・メタリック得点・HP・持越・結果操作を生成する。HPは572×36、自分のダイスは112px角・間隔132・y=-28、相手の公開ダイスは44px角・間隔51・y=180。共通の `GameAudio`、計算式と役の自動選択規則、保持・ドラッグ・28 Canvas単位の下フリックを使う。オンラインの通信状態だけは `VersusDuel` とスナップショットのアダプターが管理する。

ダイスの接触から相手の HP へ抜けるまでの攻撃演出も、シングルと同じ `SharedAttackPresenter` を共有する。`Duration = 0.90` 秒の段階（`LoadingEnd` 0.12、`CollisionTime` 0.24、`HitStopEnd` 0.28、`PushEnd` 0.44、`BreakthroughEnd` 0.54、`ContactTime` 0.64、`RecoilStart` 0.70）を持ち、暗幕は 0.55、ダイスは暗幕の上（`OverlaySortOrder = 500` / `DieSortOrder = 510`）に並ぶ。控えめ設定では移動せず、接触の 0.24 秒以降に中央の静止した接触線だけを出す。単体戦と同じく、攻撃演出の `Active` は `Reset` まで真のままで、`Sample(Duration)` を一度通しただけでは終わらない。`Reset` で両手の位置・回転・拡大・Canvas・`CanvasGroup` を元へ戻す。

オンラインの HP 表示も単体戦と同じ `HealthPresentation` と `SDApplyHealthFrame` を使う。着弾前の 0.28〜0.64 秒にスナップショットの `heal` で回復し、0.64〜1.24 秒に `damage` で減る。行は今いる段階の数字だけを出し、回復中は緑の `+N`、被害中は赤の `−M` を示す。自分の席では、まだ確定していないラウンドでも残り振り直し×40 の回復プレビューを緑で出す。相手の振り直しは解決まで伏せるため、相手の回復プレビューは出さない。成立役と振り直しもシングルと同じ `ScoreFormationPresenter`、`RollStagger`、`RollScramble` を使う。

振り直し中でも役・持越を選択し、下フリックで自分の振り直しを終了して攻撃を予約できる。相手も振り直しを終えた時点で予約した宣言を送る。右クリック／Spaceで振り直し、1〜5で保持、横ドラッグで保持をまとめて切り替える。明示的な振り直し終了ボタンは表示しない。

対戦終了の演出が終わると「再戦」「ロビーに戻る」を表示する。再戦は両者の同意後にホストが新しい `VersusDuel` を作り、HP・役欄・持越・振り直し・表示状態を初期化する。通信の操作番号とスナップショット改訂番号は継続し、対局世代を増やして古い対局の操作を拒否する。「ロビーに戻る」は既存セッションの退出完了を待って招待コード画面へ戻る。切断中断時もロビーへ戻れる。

ダイスの蓄積表現は `DuelPresentation.SDChargeOffset` を共有する。最大約1.5pxの高速な微振動、0.7度以内の傾きと小さな圧縮でエネルギーを示し、演出終了・キャンセル時に姿勢を復元する。控えめ設定では微振動と傾きを止める。

`NETWORK_DUEL_PLAN.md` が将来項目として挙げる自動マッチング・ランキング・アカウント登録は実装していない。再接続とホスト移譲も実装していない。実装で確定した構成（ホスト権威、提出までの秘匿、切断で停止）は同文書の「実装で確定した構成」節と一致する。

**根拠**: `Presentation/Network/DuelNetworkSession.cs`（`OpenAsync`, `AcceptCommand`, `PublishSnapshot`, `StopForDisconnect`）, `Core/VersusDuel.cs`（`BaseRerolls`, `MaxRerolls`, `Prepare`, `Resolve`, `Snapshot`）, `Presentation/GameFlow.Online.cs`（`BuildOnlineScreen`, `OnlineJson`, `RefreshOnline`, `SDSetOnlineHpLines`, `OnlineDieHome`）, `docs/NETWORK_DUEL_PLAN.md`, `docs/TEST_REPORT.md` §21・§23〜§27

## 11. 音声 chrome と設定

音は効果音 10 種（`dice_roll` / `die_keep` / `ui_click` / `hit` / `guard` / `hurt` / `commit` / `win` / `lose` / `formation`）と BGM 1 本で構成する。対局の規則側は音を鳴らさず、表示層だけが `GameAudio` を呼ぶ。

| 項目 | 値 |
| --- | --- |
| 旧 4 段階の音量 | `{1, 0.6, 0.3, 0}` |
| BGM ゲイン | 0.315 |
| 表示 | 0 のとき「音 OFF」、他は「音 N%」 |
| 保存 | `pv.audioVolume`（連続値）と `pv.audioStep`（旧段階） |
| 控えめ設定 | `arcane.reducedMotion` |
| 音量の書き込み | 値は即時、Save はスライダー静止後 0.5 秒に 1 度 |

音量バーは連続スライダーで、消音トグルはスピーカーアイコン（`icon-sound` / `icon-sound-off`）を切り替える。音声 chrome（トグルとバー）は、卓やモーダルの調光を含む全ての全画面層より前面に置く（`LiftAudioChrome`）。これを怠るとバーが卓の下に沈み、消音トグルだけが働く状態になる。

メニューの「成立時の動き」は `arcane.reducedMotion` を切り替え、ラベルを「成立時の動き：控えめ」「成立時の動き：通常」のいずれかにする。控えめの間は、揺れ・拡大・飛行・火花・パルスを抑え、成立した役と点数は同じように伝える。

**根拠**: `Presentation/GameAudio.cs`（`ClipNames`, `VolumeSteps`, `BgmGain`, `VolumeLabel`, `SetVolume`, `SaveVolumeWhenIdle`）, `Presentation/GameFlow.cs`（`LiftAudioChrome`）, `Presentation/GameFlow.ScoreDuel.cs`（`SDToggleMotion`, `SDRefreshMotionLabel`）, `Tests/PlayMode/ScoreDuelFeedbackTests.cs`（`VolumeBarSitsAboveTheScoreDuelTable_AndThePointerReachesIt`）

## 12. 検証状況

| 種別 | 証跡 | 結果 |
| --- | --- | --- |
| EditMode 全回帰 | `logs/validation-editmode-20261021-133315-792.xml` | 164/164 成功・失敗 0 |
| PlayMode 全回帰 | `logs/validation-playmode-20261021-133332-799.xml` | 137/137 成功・失敗 0 |
| エディタ実行スモーク | `logs/editor-smoke-20261021-135149-483/player-smoke.json` | passed=true・388 チェック・errors なし |
| Windows ビルド | `Builds/Windows-20261021-135452-923/build-report.json` | Succeeded・errors 0 |
| 実行環境 | 同スモーク | Unity 6000.3.25f1・Direct3D11・version 0.2.4 |
| 画面 PNG | `logs/editor-smoke-20261021-135149-483/*.png` | タイトル・HP の回復／被害・攻撃演出・持越・練習・結果を含む |

主な確認と対応する自動テストは次のとおり。

| 確認内容 | テスト |
| --- | --- |
| 版号「Ver.0.1」と下段の位置 | `ScoreDuelFeedbackTests.TheTitleNamesItsBuild` |
| 得点数字の段階（60/64/72/80）と予告の 64 固定 | `ScoreDuelFeedbackTests.TheCentralNumeralSwellsWithTheScore` |
| 出目合計×倍率の内訳とヤッツィー 900 固定 | `ScoreDuelTests.EveryOrderedHandUsesTheDocumentedMultiplierAndDamagePreservesTheScoreDifference`, `ScoreDuelFeedbackTests.VisibleCategoryButtonsShowLegalUnformedChoices_AndUpdateTheActualForecast` |
| 引き分けが横へ滑らず軸方向で弾かれること | `SharedAttackPresenterTests.Tie_NeverReportsAWinnerOrImpact` |
| 実被害 4 段階の幾何と原状復帰 | `ScoreDuelFeedbackTests.FourOutgoingDamageTiersDrawDifferentContactGeometry_AndRecoverOriginalDice` |
| 発射のため・振り・卓の揺れ | `ScoreDuelFeedbackTests.TheLaunchCocksBackThenWhipsAndTheBigScoresShakeTheTable` |
| 持越棚の 5 個すべての輪と外径 25 | `ScoreDuelFeedbackTests.ArmedCarryRackRingsEveryDieAsKept` |
| 持越の選択と発射時のみの消費 | `ScoreDuelFeedbackTests.SaveToggleIsReversible_AndOnlyAFullPullCarriesTheDiceIntoTheNextTurn` |
| チャンスの金色の収束と告知行の不在 | `ScoreDuelFeedbackTests.ChanceSelectionGathersTheHandInGoldAndLeavesNoCueLine` |
| チャンスの XOR（CPU 戦とオンライン） | `ScoreDuelTests.ChanceReversesDamageAndCanBeCombinedWithCarry`, `ScoreDuelTests.NpcChanceDeclarationInvertsDamageLikeOnline` |
| 同時 KO の奇跡とオンラインの入れ替え | `VersusDuelTests.IdenticalKnockoutDamageIsTheMiracleDrawWhileAnyMismatchDecides`, `OnlineResultDisplayTests.ChanceDeclarationInvertsTheDamageAndNamesNoCueLine` |
| 練習が本番の対局を変えないこと | `ReleaseExperienceTests.PracticeTeachesSelectionAndRealDamage_WithoutMutatingTheLiveMatch` |
| ホイールの 1 ノッチ循環と使用済みの除外 | `ScoreDuelFeedbackTests.WheelMovesOneRolePerNotchInBothDirections_Wraps_AndRefreshesTheForecast`, `ScoreDuelFeedbackTests.WheelSkipsAUsedRole_AndCannotChangeTheChoiceDuringDragRollCommitOrTitle` |
| 控えめ設定の拡大回避と即時復帰 | `ScoreDuelFeedbackTests.ReducedMotionAvoidsEnlargement_AndRerollAndModalResetEffectsImmediately` |
| 振り直し温存の回復（確定時・上限・両者・NPC） | `ScoreDuelTests.UnspentRerollsHealBeforeDamageAndCapAtTheMaximum`, `ScoreDuelTests.SpendingEveryRerollForgoesTheHeal`, `ScoreDuelTests.YahtzeeBonusAddsAThirdHealTheNextRound`, `ScoreDuelTests.NpcHealUsesItsOwnUnspentRerollsBeforeDamage`, `VersusDuelTests.KeptRerollsRestoreHealthForBothSeatsBeforeDamage` |
| NPC が負傷時に残り振り直しの回復を期待値へ入れること | `ScoreDuelTests.ExactPolicyKeepsAFullHouseWithOneRerollButChasesFourWithTwo` |
| HP バーの回復 → 被害の 2 段階と、各段階の緑／赤 | `ScoreDuelReadabilityTests.HealthBarNamesHealFirstThenTheDropInTheirOwnBeats` |
| HP が動き終わるまで次ラウンドを開かないこと | `ScoreDuelFeedbackTests.AdvanceWaitsForTheHealthBeatToFinish` |
| オンラインの自分の回復プレビューと相手の非公開 | `OnlineSharedDuelTests.OwnKeptRerollsPreviewGreenHealWhileOpponentStaysPrivate` |
| オンラインの回復 → 被害 → 終了後の非表示 | `OnlinePresentationParityTests.OnlineImpactKeepsThePreviewHealUntilContactThenDrains` |
| 難易度 5 種の初期 HP 表示 | `ScoreDuelReadabilityTests.FiveDifficultiesEachStartWithTheirAdvertisedEnemyHealthOnScreen` |
| 自作ストロークがエディタ実行でもピクセルに出ること | `EffectStrokeRenderingTests.TheFactoryStrokes_ReachTheRenderedPixels`, `EffectStrokeRenderingTests.APlainSpriteImage_RendersThroughTheSamePath` |

実行版の実 Relay 対戦（2 プロセス）は `docs/TEST_REPORT.md` §21・§23〜§27 に、招待コード参加から勝敗までの状態一致・切断時の中断・`-nographics` の停止解消として記録されている。異なる PC・回線での対人プレイ、初見の操作理解、`-nographics` 以外のヘッドレス環境は未計測である。

**根拠**: `logs/validation-editmode-20261021-133315-792.xml`（164/164）, `logs/validation-playmode-20261021-133332-799.xml`（137/137）, `logs/editor-smoke-20261021-135149-483/player-smoke.json`（388 checks, 0 errors）, `logs/editor-smoke-20261021-135149-483/`, `docs/TEST_REPORT.md` §21〜§27

## 13. 付録 Ver.0.1 調整一覧

この節の配置と文字は 0.1 時点の記録であり、現行 0.2 の持越ボタンと版号には適用しない。

| 調整 | 確定値 |
| --- | --- |
| 発射 punch | 点数 50 / 100 / 130 で段階 0〜3。wind `0.14+0.02p`、dip `0.14+0.05p`、power `1.6+0.35p`、伸び `1.10+0.02p` |
| 卓の揺れ | 点数 100 以上で発生。窓 0.42 秒、振幅 `(5+4.5p)×decay²` |
| 数字スケール | `SDScoreFontFor`：50 未満 60 / 100 未満 64 / 130 未満 72 / 130 以上 80。予告は 64 固定、相手は 50 |
| 版号表示 | 下段 y=-319 の x=280 に「Ver.0.1」14px・右寄せ。≡ と音量バーと同じ帯 |
| 保存アイコンの光学中心 | フロッピー (-82, 7)・ラベル (0, 10)・残数 (82, 9)。19px ラベルの下寄りに合わせる |
| 持越の上限 capsul 文字 | ボタン (0, -230)・204×48、棚 (0, -13)・160×20、収納ダイスの輪外径 25。選択中のラベルは「持越 選択中」16px |

**根拠**: `Presentation/GameFlow.ScoreDuelImpact.cs`, `Presentation/GameFlow.ScoreDuel.cs`, `Presentation/GameFlow.ScoreDuelLayout.cs`, `Presentation/GameFlow.ScoreDuelCarry.cs`, `Presentation/DiceView.cs`

## 不整合メモ

`GAME_DESIGN.md` と現行コードの間で見つかった食い違いを記す。本書では修正していない。

| 項目 | `GAME_DESIGN.md` の記述 | コードの実装 |
| --- | --- | --- |
| 攻撃演出の段階 | 相手への実被害（`DamageToNpc`）を 250 / 450 / 700 で 4 段階 | `max(DamageToNpc, DamageToPlayer)` を 250 / 450 / 700 で 4 段階（自分が受ける大被害でも段階が上がる） |
| ヤッツィーの倍率表記 | 役表の成立倍率は「成立時 900 点固定」 | 未成立のヤッツィー欄は ×30 の式（`出目合計×30`）で計算し、成立時だけ 900 点固定 |
| チャンスの性質 | 役表の条件は「条件なし」 | 内部では常に成立扱い（マスク 31・得点 `出目合計×10`）で、どの出目からも選べる |

2026-10-20 の得点式置換で `GAME_DESIGN.md` 側も同じ値へ揃えた。上の表は食い違いを見つけた時点の記録として残す。

**根拠**: `docs/GAME_DESIGN.md` §1〜§3, `Presentation/GameFlow.ScoreDuel.cs`（`SDBuildTitle`）, `Presentation/GameFlow.ScoreDuelImpact.cs`（`SDBeginImpactPresentation`）, `Core/ScoreDuel.cs`（`BuildTable`, `Multipliers`）, `ProjectSettings/ProjectSettings.asset`, `logs/batch-20261018i/smoke/player-smoke.json`
