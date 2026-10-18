# DiceYahzee Ver.0.1 確定仕様書（正本）

本書は `ParvaMirabiliaDice` の現行実装が持つ確定仕様を、コードの実数値から書き出した正本である。用語は `GAME_DESIGN.md` に合わせ、食い違いは末尾の「不整合メモ」にだけ記す。数値はすべてコード上の定数・式を根拠とする。

## 1. 概要と版号

| 項目 | 値 |
| --- | --- |
| タイトル | DiceYahzee |
| 副題 | D I C E   D U E L |
| 版号表示 | `Ver.0.1` |
| ジャンル | 5d6 を振り直して役を作り、同時ダメージで相手の HP を削る 1 人用の短い対戦 |

タイトル画面は縦に、見出し「DiceYahzee」（58px・Aoboshi One、y=220）、副題（13px、y=166）、例示の 5 ダイス（86px 角、間隔 106、y=67、出目 1・1・1・4・5 のうち左 3 個を保持表示）、相手 5 種のボタン列（y=-65）、相手の説明行（y=-112）、「対局開始」（y=-162）、「練習」（y=-241・左）、「遊び方」（y=-241・右）を並べる。`Ver.0.1` は 14px・右寄せ（box 170×26）で、画面下段 y=-319 に、メニューの ≡ 記号（(-554,-319)・46×38・22px アイコン）と音量バー（(451,-319)）・消音トグル（(554,-319)・46×38・24px アイコン）と同じ帯に置く。版号はこの帯の空き位置（x=280）に収まる。

| 下段 chrome | 位置 | 寸法 | 内容 |
| --- | --- | --- | --- |
| メニュー | (-554, -319) | 46×38 | ≡ 記号（icon-menu・22px） |
| 版号 | (280, -319) | 170×26 | 「Ver.0.1」14px・右寄せ |
| 音量バー | (451, -319) | スライダー | 連続音量 |
| 消音トグル | (554, -319) | 46×38 | icon-sound / icon-sound-off・24px |

タイトル下部には「オンライン対戦」ボタン（(0,-300)・320×42・19px）も並ぶ。

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
| 開始 | `Begin` が Round=1、自分の HP=300、相手 HP=難易度別値にし、`PrepareRound` で相手の手を先に確定する |
| ロール | 人間は 5d6。相手の出目は人間の投擲前に確定済みで、公開情報である |
| 保持 | 残すダイスをクリック・横ドラッグで選ぶ |
| 振り直し | 保持しなかった目を最大 2 回振る（ヤッツィー成立の翌ラウンドは 3 回） |
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

人間の振り直しはラウンド開始時に 2 回（前ラウンドのヤッツィー成立があれば 3 回、上限 3）。相手は難易度によらず 2 回の期待値方針で打ち、人間の振り直し回数は相手の将来の出目を変えない（乱数ストリームが別）。9 欄を使い切った側だけが次ラウンドの開始時に欄を戻す（HP と保存の使用状態は戻さない）。

被害は両方の点数の低い方を P・N として共通軽減 `A = floor(min(P,N)/2)` を両方の攻撃から同じだけ引き、同時に与える。HP は 0 で止め、どちらかが 0 になった時点で決着する。同時 KO は丸める前の残 HP の大きい側の勝ち、等しければ引き分け（「奇跡」）とする。`SafetyRoundBound = ceil(300/13) = 24` は自動試合の内部上限であり、ゲームのターン上限でもターン数判定でもない。

**根拠**: `Core/ScoreDuel.cs`（`Begin`, `PrepareRound`, `Roll`, `Reroll`, `Resolve`, `SafetyRoundBound`）, `Core/VersusDuel.cs`（`Prepare`, `Resolve`）

## 4. 役・得点・ダメージ

得点は `floor((20＋5 ダイスの出目合計)×選択役の倍率)`。倍率は 1/4 刻みの整数（`Quarters`）で持つ。未成立の欄も選べ、その場合は倍率 1（倍率 4/4）で得点は `20＋出目合計` になり、その欄を消費する。ヤッツィー成立だけは倍率を使わず 150 点固定である。

| 役 | enum | 成立条件 | Quarters | 倍率 | 備考 |
| --- | --- | --- | --- | --- | --- |
| ペア | Pair | 同じ目が 2 個以上 | 6 | ×1.5 | マスクは最小 2 個 |
| ツーペア | TwoPair | 異なる 2 組のペア | 8 | ×2 | |
| スリーカインド | Three | 同じ目が 3 個以上 | 8 | ×2 | |
| フルハウス | FullHouse | 3 個＋2 個（異なる目） | 10 | ×2.5 | マスクは全 5 個 |
| 小ストレート | SmallStraight | 異なる 4 連続 | 8 | ×2 | |
| フォーカインド | Four | 同じ目が 4 個以上 | 12 | ×3 | |
| 大ストレート | LargeStraight | 異なる 5 連続 | 10 | ×2.5 | |
| ヤッツィー | Five | 同じ目が 5 個 | 12 | 成立時 150 点固定 | 未成立なら ×3 の式 |
| チャンス | Chance | 条件なし（常に成立扱い） | 4 | ×1 | 第 5 章 |

役名・倍率タグ・得点の表記は次のとおり。右の役レールは「役名 → 倍率タグ → 得点」の 3 列で、倍率タグはヤッツィーが「150点」、チャンスが「逆転」、他は「×1.5」「×2」「×2.5」「×3」を出す。役レールの点数列は選択中だけ金色の印を伴い、使用済みは「—」と暗色で示す。

| 役 | 倍率タグ | 遊び方の説明 |
| --- | --- | --- |
| ペア | ×1.5 | 同じ目 2 つ · ×1.5 |
| ツーペア | ×2 | ペア 2 組 · ×2 |
| スリーカインド | ×2 | 同じ目 3 つ · ×2 |
| フルハウス | ×2.5 | 3 つ＋2 つ · ×2.5 |
| 小ストレート | ×2 | 4 連続 · ×2 |
| フォーカインド | ×3 | 同じ目 4 つ · ×3 |
| 大ストレート | ×2.5 | 5 連続 · ×2.5 |
| ヤッツィー | 150点 | 同じ目 5 つ · 150点 |
| チャンス | 逆転 | 条件なし · ×1 |

得点の計算は、確定表示が `基礎点 ×倍率`（例「37 ×2」）の形で内訳を出し、ヤッツィー成立だけは「150」を出す。遊び方は「得点＝（20＋出目合計）×倍率、端数切捨て。」を最上段に掲げる。

ダメージは、P を自分の得点、N を相手の予告点数として次で決まる。

| 項目 | 式 |
| --- | --- |
| 共通軽減 | `A = floor(min(P,N)/2)` |
| 相手が受ける被害 | `DamageToNpc = P − A` |
| 自分が受ける被害 | `DamageToPlayer = N − A` |

例として、{2,3,3,4,5} の小ストレートは総和 17 で 74 点、同じ出目の大ストレートは未成立で 37 点。{6,2,5,3,4} の大ストレートは総和 20 で 100 点。{3,3,3,3,3} のチャンスは 35 点である。

| 出目 | 選ぶ役 | 成立 | 総和 | 得点 |
| --- | --- | --- | --- | --- |
| 1・1・1・1・6 | フォーカインド | 成立 | 10 | (20+10)×3 = 90 |
| 6・6・5・4・2 | ペア | 成立 | 23 | floor((20+23)×1.5) = 64 |
| 2・3・3・4・5 | 小ストレート | 成立 | 17 | (20+17)×2 = 74 |
| 2・3・3・4・5 | 大ストレート | 未成立 | 17 | 20+17 = 37 |
| 6・2・5・3・4 | 大ストレート | 成立 | 20 | (20+20)×2.5 = 100 |
| 3・3・3・3・3 | チャンス | 成立扱い | 15 | 20+15 = 35 |
| 1・1・1・1・1 | ヤッツィー | 成立 | 5 | 150（固定） |

ダメージの例として、P=56・N=70 なら `A = 28` で相手へ 28・自分へ 42。同点 32 なら双方 16。低い側の攻撃も残る。

**根拠**: `Core/ScoreDuel.cs`（`ScoreRules`、`Names`、`Quarters`、`BuildTable`、`Add`、`ScoreForecast`）, `Presentation/GameFlow.ScoreDuelLayout.cs`（`SDMultiplier`, `SDRailMultiplier`, `SDCalculation`）, `Presentation/GameFlow.ScoreDuel.cs`（`SDBuildRules`, `SDBuildLiveScorecard`）, `Tests/EditMode/ScoreDuelTests.cs`（`StraightMasksContainOneOfEachNecessaryFaceAndNeverDuplicates`, `AnUnformedCategoryKeepsTheBaseAndDiceTotalAndConsumesItsSlot`）

## 5. Chance 直前宣言の XOR 反転規則

チャンスは条件を持たず、どの出目からも選べる。`ScoreRules` の内部では常に `IsFormed = true`・マスク 31・得点 `20＋出目合計` として作られる（ユーザー決定 2026-10-18 の設計意図）。倍率は 4/4 の ×1 なので、出目が役を成していなくてもチャンスは点数を持つ。チャンスも 9 欄の 1 つで、使えばその対戦では再使用できない（9 欄を使い切れば戻る）。

被害の受け手の入れ替えは、両者の宣言の XOR で決まる。

| 自分の宣言 | 相手の宣言 | 反転 | 結果 |
| --- | --- | --- | --- |
| チャンス以外 | チャンス以外 | なし | `DamageToNpc = P − A`、`DamageToPlayer = N − A` |
| チャンス | チャンス以外 | あり | 受け手が入れ替わり `DamageToPlayer = P − A`、`DamageToNpc = N − A` |
| チャンス以外 | チャンス | あり | 同上 |
| チャンス | チャンス | なし | 打ち消し合い、通常どおり |

`ScoreDuel.Forecast` は `reversed = (kind == Chance) != (Intent.Kind == Chance)` を立て、`ScoreForecast` がその真偽で受け手を入れ替える。`VersusDuel.Resolve` も同じ XOR（`(players[0].Kind == Chance) != (players[1].Kind == Chance)`）を書く。両者は同一規則である。

相手 NPC もチャンスを宣言し得る。`ScoreHoldPolicy.Utility` はチャンスの効用を `offer.Score` とし、ラウンド 5 未満では 4 を引く。`Chance` を候補から外す処理も `IsFormed` での門番も置かない（設計意図コメント 2026-10-18）。

反転の実例（テストの固定値）：自分のヤッツィー 150 点に対し相手の予告がチャンス 51 点なら `A = 25`、反転により自分が 125、相手が 26 の被害を受ける。自分のチャンス 25 点に対し相手の予告が 101 点なら `A = 12`、自分が 13、相手が 89。双方チャンス（25 点と 51 点）なら反転せず、自分が 39、相手が 13。

表示では、対戦中にチャンスを選ぶと中央の 5 ダイスすべてに金色の輪と立ち上る火花が出て、入れ替えの対象を示す（控えめ設定では静止した輪のみ）。反転規則の文は遊び方の「チャンス：ダメージ計算式を反転させます。」が担う。オンライン画面も規則と予告の欄が担い、浮遊する告知行は置かない。

**根拠**: `Core/ScoreDuel.cs`（`BuildTable` の Chance 行, `Forecast`, `ScoreHoldPolicy.Utility`）, `Core/VersusDuel.cs`（`Resolve`）, `Presentation/GameFlow.ScoreDuelEffects.cs`（`SDTickChanceGlow`）, `Presentation/GameFlow.ScoreDuel.cs`（`SDBuildRules`）, `Tests/EditMode/ScoreDuelTests.cs`（`ChanceReversesDamageAndCanBeCombinedWithCarry`, `NpcChanceDeclarationInvertsDamageLikeOnline`, `ChanceIsASingleUseSafetyNetRatherThanAnAlwaysAvailableAttack`）, `Tests/PlayMode/ScoreDuelFeedbackTests.cs`（`ChanceSelectionGathersTheHandInGoldAndLeavesNoCueLine`）, `Tests/PlayMode/OnlineResultDisplayTests.cs`（`ChanceDeclarationInvertsTheDamageAndNamesNoCueLine`）

## 6. 相手5種

難易度は相手の初期 HP だけを変える。相手の打ち方は 5 種とも同じ上級（Expert）の期待値方針である。自分の HP はどの難易度でも 300。

| ボタン | 内部の型 | 相手 HP |
| --- | --- | --- |
| 入門 | Expert | 250 |
| 初級 | Expert | 350 |
| 中級 | Expert | 400 |
| 上級 | Expert | 450 |
| 超級 | Expert | 550 |

相手 HP の表は `NpcHpByTier = {250, 350, 400, 450, 550}` の 1 箇所で持つ。ボタンも対局もこの表を参照するため、表示と実際の HP は食い違わない。難易度名は `NpcPersonality` の `Expert` だけを使い、`Balanced`（標準）・`Aggressive`（大役）・`Careful` は内部互換として残るが画面には出さない。

`Expert` の相手は、残り振り直し回数の全出目を確率付きで列挙する有限ダイスの厳密方針（252 の目のヒストグラム、462 の保持、4368 の遷移を 1 度だけ生成し、DP を 2 回通す）で役と保持を選ぶ。振り直しは 2 回（前ラウンドのヤッツィー成立があれば 3 回）。相手の手は人間の投擲前に確定し、役名・点数・実際の 5 出目を最初から公開し、人間の投擲や保持を見ても変えない。

**根拠**: `Core/ScoreDuel.cs`（`NpcHpByTier`, `NpcHpForDifficulty`, `StartingHp`, `PrepareNpcTurn`, `NpcPersonality`）, `Core/ScoreHoldPolicyExact.cs`, `Presentation/GameFlow.ScoreDuel.cs`（`SDOpponentProfiles`, `SDSelectOpponent`）

## 7. 影響演出

攻撃の演出は 1 本の明示的な時間軸で進む。5 ダイスは 0.30 秒で相手へ接触し、全体は段階に応じて `SDImpactDuration = 0.76 + tier×0.03` 秒（0.76 / 0.79 / 0.82 / 0.85）で終わる。

段階は攻撃側の値で決まる。実被害は 2 つの大きい方、発射の punch は 2 つの点数の大きい方を使う。

| 段階 | 実被害の条件（`max(DamageToNpc, DamageToPlayer)`） |
| --- | --- |
| 0 | 30 未満 |
| 1 | 30 以上 50 未満 |
| 2 | 50 以上 80 未満 |
| 3 | 80 以上 |

| punch | 点数の条件（`max(PlayerScore, NpcScore)`） |
| --- | --- |
| 0 | 50 未満 |
| 1 | 50 以上 100 未満 |
| 2 | 100 以上 130 未満 |
| 3 | 130 以上 |

発射の挙動は punch で決まる。

| 項目 | 式 |
| --- | --- |
| ため（wind） | `0.14 + 0.02×punch` |
| 沈み（dip） | `0.14 + 0.05×punch` |
| 振りの強さ（power） | `1.6 + 0.35×punch` |
| 伸びの頂点 | `1.10 + 0.02×punch` |

卓の揺れは接触の瞬間から 0.42 秒の窓で収まり、振幅は `(5 + 4.5×punch) × decay²`、X は `sin(after×54)`、Y は `sin(after×63 + 1.1)×0.72` を使う。点数が 100 未満なら punch は 0 で振幅は 5 に留まる。控えめ設定では揺れも飛行も行わない。接触の瞬間に HP の減少と音（相手への実被害があれば `hit`、自分への被害があれば `hurt`、どちらも無ければ `guard`）がそろう。ダメージの数字は別枠へ散らさず、被害を受けた側の HP バーの 1 行に出す。

中央の得点数字は確定表示のときだけ点数で大きさを変える。

| 条件 | フォント |
| --- | --- |
| 50 未満 | 60 |
| 50 以上 100 未満 | 64 |
| 100 以上 130 未満 | 72 |
| 130 以上 | 80 |

確定前の予告数字は 64 に固定し、約束の数字が踊らないようにする。相手の予告数字は 50 である。HP バーは厚さ 36 で、減少は 0.85 秒かけて書き換える。行の形式は「あなた 300/300 −35」「相手 250/250 −35」の 1 行で、被害だけを `SDDamageInk`(193,39,45) の赤で示す。

役成立の演出は役ごとの段階で規模を変える。

| 役 | 段階 | 演出時間（通常 / 控えめ） | 火花 |
| --- | --- | --- | --- |
| ペア | 0 | 0.36 / 0.30 | 3 |
| ツーペア・スリーカインド | 1 | 0.55 / 0.30 | 8 |
| フルハウス・小ストレート | 2 | 0.74 / 0.30 | 14 |
| フォーカインド・大ストレート | 3 | 0.84 / 0.30 | 19 |
| ヤッツィー | 4 | 1.02 / 0.30 | 28 |

**根拠**: `Presentation/GameFlow.ScoreDuelImpact.cs`（`SDImpactContactDelay`, `SDImpactDuration`, `SDAnimateImpactPresentation`）, `Presentation/GameFlow.ScoreDuel.cs`（`SDScoreFontFor`, `SDRefresh`, `SDBuildHudDetails`）, `Presentation/GameFlow.ScoreDuelEffects.cs`（`SDPlayHandEffects`, `SDHandEffectTier`）, `Presentation/GameFlow.ScoreDuelLayout.cs`（`SDHealthLine`, `SDDamageInk`, `SDAnimateHpLoss`）, `Tests/PlayMode/ScoreDuelFeedbackTests.cs`（`FourOutgoingDamageTiersDrawDifferentContactGeometry_AndRecoverOriginalDice`, `TheLaunchCocksBackThenWhipsAndTheBigScoresShakeTheTable`, `TheCentralNumeralSwellsWithTheScore`）

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

操作ガイドは右側の x=350 に置く。マウス記号は下引きの 3 段の山形（18px 下げて 42px 移動）を示し、3 個保持すると右クリックの図形へ変わる。ホイール記号は内側だけ上下 4px 動く。攻撃後は「次へ」の輪を示し、ボタン以外の背景・ダイス・無効領域のクリックで次ラウンドへ進む。演出終了前の押下と同じ入力の二重実行は `_sdInputFrame` で防ぐ。投擲中・確定後・モーダルではガイドを隠す。

「成立時の動き：控えめ」を選ぶと、移動・拡大・光・迷いを抑え、成立した役と点数は同じように伝える。設定は `arcane.reducedMotion` に保存する。控えめのときはガイドを静止表示し、揺れ・飛行・パルスを行わない。

入力が止まる条件は次のとおり。ダイスの保持・ホイール・右クリック・発射は、対戦面が入力可能で、投擲中・確定後・相手の再生中・モーダル中でないときに限る。振り直しは残り回数が 0 のときも止まる。モーダルを開いた時点で進行中のドラッグは元の保持と位置へ戻す。

**根拠**: `Presentation/GameFlow.ScoreDuelInput.cs`（`SDFlickDistance`, `SDPointerMove`, `SDPointerEnd`, `SDHandleWheel`, `SDHandleRightClick`）, `Presentation/GameFlow.ScoreDuelGuides.cs`（`SDGuideX`, `SDTickGestureGuides`, `SDDrawGestureGuide`）, `Presentation/GameFlow.ScoreDuelNavigation.cs`（`SDTableClick`）, `Presentation/GameFlow.ScoreDuel.cs`（`SDHandleShortcut`, `SDToggleMotion`, `SDRefreshMotionLabel`）

## 10. オンライン対戦

実装済みの通信は Unity Multiplayer Services（セッション）と Netcode for GameObjects（ゲーム通信）である。Unity Cloud の `DiceYahzee` プロジェクトに紐付け、匿名認証と招待コード参加を使う非公開 2 人セッションを Relay で張る。専用サーバーは置かない。

| 層 | 実装 |
| --- | --- |
| 進行 | Unity 非依存の `VersusDuel` が二人分の HP・ダイス・保持・残り振り直し・役の使用・持越・ラウンド・勝敗を持つ |
| 乱数 | ホストだけが持つ |
| 通信要求 | 保持マスクと振り直し、役と持越の提出、次ラウンドへの同意、振り直し終えの 4 種 |
| 検証 | 各要求にプレイヤー・ラウンド・単調増加する操作番号を付け、ホストが接続 ID・段階・残り回数・役の使用状態・番号を検証する |
| メッセージ | `parva.duel.v1.hello` / `welcome` / `command` / `snapshot`、最大 24576 |
| 切断 | 進行を止め、残った側に中断を表示する。自動再接続・ホスト移譲は行わない |

規則は CPU 戦と同じである。1 ラウンドに各自 2 回振り直し（ヤッツィー成立の次ラウンドは 3 回、`MaxRerolls = 3`）、役と持越（各自 1 回）を提出し、双方の提出後に同時ダメージと勝敗を確定する。チャンスの入れ替えは CPU 戦と同じ XOR である。同時 KO は丸める前の残 HP を比較し、等しければ引き分けとする。

公開の段階は次のとおり。ラウンド開始の初回出目は両者へ公開し、振り直しの最中は相手の出目を開始時の値で伏せ、双方が振り直しを終えた時点で最終の出目をそろって公開する。提出した役・得点・持越と、その役を使った事実は双方の提出まで相手向けスナップショットから隠す。

オンライン画面はロビーと対戦を持つ。ロビーは「部屋を作る」「招待コード」入力欄（上限 16 文字）「コードで参加」「タイトルへ」。対戦は左右の役レール、中央の役名と得点、上下の HP バー（636×31、y=±249）、自分のダイス（104px 角・間隔 116・y=-76）と相手のダイス（82px 角・間隔 96・y=130）、中央の持越、「振り直す」「振り直しを終える」で構成する。引き分けは「奇跡」を 132px の黄色（`SDMiracle` = 255,216,82）で示し、状態行は「あなたの勝利」「あなたの敗北」「引き分け」「相手を待っています」を出す。

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
| EditMode 全回帰 | `logs/validation-editmode-20261018-125618-395.xml` | 158/158 成功・失敗 0 |
| PlayMode 全回帰 | `logs/validation-playmode-20261018-125642-861.xml` | 103/103 成功・失敗 0 |
| 実行版スモーク | `logs/batch-20261018i/smoke/player-smoke.json` | passed=true・445 チェック・errors なし |
| 実行環境 | 同スモーク | Unity 6000.3.25f1・Direct3D11・version 0.10.0 |
| 画面 PNG | `logs/batch-20261018i/smoke/*.png`、`logs/batch-20261018i/title-bottom.png` | タイトル下段・4 段階の被害・持越・練習・結果を含む |

主な確認と対応する自動テストは次のとおり。

| 確認内容 | テスト |
| --- | --- |
| 版号「Ver.0.1」と下段の位置 | `ScoreDuelFeedbackTests.TheTitleNamesItsBuild` |
| 得点数字の段階（60/64/72/80）と予告の 64 固定 | `ScoreDuelFeedbackTests.TheCentralNumeralSwellsWithTheScore` |
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
| HP バー 1 行の赤い被害 | `ScoreDuelReadabilityTests.HpDropIsNamedInRedInsideTheDrainingBar` |
| 難易度 5 種の初期 HP 表示 | `ScoreDuelReadabilityTests.FiveDifficultiesEachStartWithTheirAdvertisedEnemyHealthOnScreen` |

実行版の実 Relay 対戦（2 プロセス）は `docs/TEST_REPORT.md` §21・§23〜§27 に、招待コード参加から勝敗までの状態一致・切断時の中断・`-nographics` の停止解消として記録されている。異なる PC・回線での対人プレイ、初見の操作理解、`-nographics` 以外のヘッドレス環境は未計測である。

**根拠**: `logs/validation-editmode-20261018-125618-395.xml`, `logs/validation-playmode-20261018-125642-861.xml`, `logs/batch-20261018i/smoke/player-smoke.json`, `logs/batch-20261018i/`, `docs/TEST_REPORT.md` §21〜§27

## 13. 付録 Ver.0.1 調整一覧

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
| 版号 | 現行仕様 0.10.0 | タイトル下段は「Ver.0.1」。`ProjectSettings.asset` の `bundleVersion` とスモークの `version` は 0.10.0 |
| 攻撃演出の段階 | 相手への実被害（`DamageToNpc`）を 30 / 50 / 80 で 4 段階 | `max(DamageToNpc, DamageToPlayer)` を 30 / 50 / 80 で 4 段階（自分が受ける大被害でも段階が上がる） |
| ヤッツィーの倍率表記 | 役表の成立倍率は「成立時 150 点固定」 | 未成立のヤッツィー欄は ×3 の式（`20＋出目合計`）で計算し、成立時だけ 150 点固定 |
| チャンスの性質 | 役表の条件は「条件なし」 | 内部では常に成立扱い（マスク 31・得点 `20＋出目合計`）で、どの出目からも選べる |

**根拠**: `docs/GAME_DESIGN.md` §1〜§3, `Presentation/GameFlow.ScoreDuel.cs`（`SDBuildTitle`）, `Presentation/GameFlow.ScoreDuelImpact.cs`（`SDBeginImpactPresentation`）, `Core/ScoreDuel.cs`（`BuildTable`, `Quarters`）, `ProjectSettings/ProjectSettings.asset`, `logs/batch-20261018i/smoke/player-smoke.json`
