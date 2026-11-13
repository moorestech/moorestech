# 調査 tests-ladder: 検証資産の棚卸しと段階ごとの完了条件に使うテスト

- 調査日: 2026-11-06。worktree `uitk-execution-plan`（HEAD `54c8a4c8c8` = `origin/feature/uitk` と同じ）を読むだけで調べた。Unity Editor・uloop・テストの実行はしていない
- 計画の正本は `origin/master:docs/uitk-migration/plan.md`（以下 plan）。行番号はその版のもの
- 件数は正規表現で数えた値。vitest と Playwright は `it(`／`test(`（`.each`・`.only`・`.skip` 付きを含む。ループで展開されるケースは 1 件）、C# は `[Test]`・`[UnityTest]`・`[TestCase…]` の行。数え方の都合で、ランナーの実測とは少しずれる
- 機能ごとの割り振りと P/C/L/I/S/W の種別は、パスとファイル内容（`react-test-renderer`・`readFileSync` の有無など）を機械で分けた結果で、誤差がある（推測を含む）。分類スクリプトは scratchpad にしか置いていない

---

## 0. 要点

1. **Web のテストは 2026-10-22 の調査（`docs/uitk-migration/research/understand-tests.md` §2、plan §4.1 の表の出典）から約 15% 増えた。** 今は 1,237 件（Playwright 243・vitest と mock-host 994）。種別では P 243（前回 213）・C 269（235）・L 353（311）・I 47（61）・S 117（91）・W 208（167）。束 4（持ち物）が 466 件で全体の 4 割近くを持つ。plan §2 の束ごとの「移すテスト」の数（段階 3: buildMenu P19・C22・L30 など）は、束 3・4 については今の値とほぼ同じか少し多い（§1.3）
2. **CEF 無し起動ラダー 6 本のうち、あるのは 1 本目の `uitk-lifeline` だけ。** 合格の記録もある（`docs/uitk/TODO.md:160`）。ただし起動は全体スイッチ（albof.17、未着手）ではなく `WebUiHostEditorSkip.Apply()` を使い、要素探しも「UITK を先に探し、無ければ Web」の合成ロケータのまま。albof.17 のあとで流し直す必要がある（plan:212）。残る 5 本（`uitk-opening-hud`・`uitk-build-bp`・`uitk-production-loop`・`uitk-research-train`・`uitk-challenge-gate`）は未作成
3. **L1 と L2 の土台は feature/uitk にそろっている。** L1 は `UiTestHost`（1280×720・本番テーマ）、画面用の `ScreenTestHost`、`FakeModel<T>`・`CommandRecorder<T>` がある。L2 は `UiStyleConventionTest` などの規約テストがある。`Client.Tests/Ui` には 67 ファイル・272 件があり、CI の client-remainder shard で通っている（S13 と R-16 の結論）。**L0 の「Unity の外で `dotnet test`」（S12）は手を付けていない。** `Client.Ui.State` が `Client.Game`・`MessagePack`・`UniRx` などに依存しているので、今の asmdef のままでは Unity の外でビルドできない見込み（推測）。L0 は当面 Unity の EditMode NUnit で回すことになる
4. **欠けている検証の道具が 3 つある。** (a) plan §4.1 で、束の最後のレーンで使うとされているテスト名の突き合わせスクリプトがまだ無い。`docs/uitk/disposition.md` の表も空のまま。(b) アンカーのカバレッジテスト（plan §4.5 の「全静的 ID と全 prefix が、どこかの L1 マウントで登録される」）が無い。(c) 録画シナリオ 110 本のうち 10 本は壊れている（bd `moorestech-wwxy`）。そのうち 4 本は、束 3 のラダーの下敷きになる UI 経路のシナリオである

---

## 1. Web のテストの棚卸し（UITK へ写す仕様）

### 1.1 全体量（実測）

| 資産 | 量 | CI | 根拠 |
|---|---|---|---|
| Playwright（`e2e/tests/**/*.spec.ts`） | 64 ファイル・243 件 | 走らない（型検査だけ） | `.github/workflows/run_test.yml:27-` の `webui-test` は「vitest + typecheck」 |
| vitest（`src/**/*.test.ts(x)`） | 171 ファイル・982 件 | `webui-test` で走る | 同上 |
| mock-host・uitk-reference の vitest | 4 ファイル・12 件 | 同上 | `e2e/mock-host/**/*.test.ts`、`e2e/uitk-reference/captureLifecycle.test.ts` |
| 合計 | 239 ファイル・1,237 件 | | |

件数が 0 と出たファイルが 3 本ある（`guidValidation.test.ts`・`rocketLaunchPadLogic.test.ts`・`GaugeBar/index.test.ts`）。どれもループの中で `it` を呼ぶ形で、本数を数え切れていない（推測）。

### 1.2 種別ごとの件数を plan §4.1 と比べる

| 種別 | plan §4.1（2026-10-22 の数） | 今（2026-11-06） | 差 | 行き先（plan §4.1） |
|---|---:|---:|---:|---|
| P 画面フロー | 213 | 243 | +30 | L1（レイアウトの主張は L1 のレイアウト照合か L4 の目視） |
| C コンポーネント | 235 | 269 | +34 | L1 |
| L ロジック | 311 | 353 | +42 | L0 |
| I 多言語 | 61 | 47 | −14 | L0 と L2 |
| S 静的規約 | 91 | 117 | +26 | L2（意図だけ） |
| W 通信契約 | 167 | 208 | +41 | 捨てる |

I が減って S が増えたのは、主に分類の違いによる（`localizationKeysFreshness`・`noJsxVisibleLiteral` を今回は S に入れた。推測）。増えた分の多くは束 1 と束 4 で、Tier9 のブロック UI（ロケット・宇宙エレベーター・清浄機）、BP、バグ報告の配送が加わった（ファイル名から判断）。

### 1.3 束と機能ごとの件数（UITK に写す仕様）

束は `docs/uitk/TODO.md:21-27` と行の表（`:31-80`）の割り振りに合わせた。「横断」は束 0、通信は束 9（捨てる）とした。

| 束 | 行（機能） | P | C | L | I | S | W | 計 | plan に書かれた数 |
|---|---|---|---|---|---|---|---|---|---|
| 0 横断 | uiState（ルーティング・入力層） | 10 | 6 | 29 | - | - | - | 45 | |
| 0 | shared/ui（枠・スロット・ボタン・ゲージ） | - | 12 | 34 | 3 | 18 | - | 67 | |
| 0 | i18n | 2 | - | - | 37 | 19 | - | 58 | |
| 0 | tutorialAnchor | - | - | 7 | - | 4 | - | 11 | |
| 0 | app（殻・トークン・層） | 2 | 1 | - | - | 14 | - | 17 | |
| 0 | uiScale・reference（撮影の準備） | 9 | - | - | - | - | - | 9 | |
| 1 | pauseMenu（ポーズ・バグ報告） | 7 | 37 | - | - | - | - | 44 | |
| 1 | settings | - | 15 | - | - | - | - | 15 | |
| 1 | toast | - | - | 2 | - | - | - | 2 | |
| 2 | notification | 6 | - | 26 | - | 15 | - | 47 | |
| 2 | modeHud | 6 | - | - | 6 | 5 | - | 17 | |
| 2 | skit・背景スキット（phaseC4） | 9 | - | 5 | - | - | - | 14 | |
| 2 | commonHud・keyHint・progress | 9 | 4 | - | - | 3 | - | 16 | |
| 2 | challenge の HUD（hudLayout） | 5 | - | - | - | - | - | 5 | |
| 2 | tutorial: KeyControlHint・WorldPin | 6 | 4 | - | - | - | - | 10 | |
| 2 | CursorTooltip | - | 7 | 1 | - | - | - | 8 | |
| 3 | buildMenu（`placementIconCentering` を含む） | 21 | 22 | 30 | - | - | - | 73 | P19・C22・L30（plan:290） |
| 3 | hotbar | 6 | 7 | 3 | - | - | - | 16 | P6・C7・L3（plan:290） |
| 3 | modal（BP 名） | 4 | - | 8 | - | - | - | 12 | vitest 8（plan:290） |
| 3 | placementTarget・pointerGesture | - | - | 14 | - | - | - | 14 | |
| 4 | blockInventory（列車を除く） | 63 | 59 | 61 | - | 18 | - | 201 | P42・C27・L45（plan:326） |
| 4 | inventory・装備 | 20 | 16 | 25 | - | - | - | 61 | P20・C16・L57（plan:326。itemMove 込みの数と推測） |
| 4 | itemMove・ownedCounts | 2 | - | 42 | - | - | - | 44 | |
| 4 | recipe | 19 | 8 | 24 | 1 | - | - | 52 | P18・C10・L19（plan:326） |
| 4 | research（treeView を除く） | 19 | 14 | 22 | - | - | - | 55 | P18・C21・L37（plan:326。treeView 込みの数と推測） |
| 4 | treeView | - | 17 | 12 | - | - | - | 29 | |
| 4 | train（時刻表ほか） | 4 | 14 | 6 | - | - | - | 24 | P6・C6・L14（plan:326） |
| 5 | tutorial: Overlay（ハイライト枠・注目演出） | 7 | 17 | - | - | 21 | - | 45 | |
| 5 | challengePanel | 7 | - | 2 | - | - | - | 9 | |
| 5 | eventLanguageGate | - | 9 | - | - | - | - | 9 | |
| 9 捨てる | bridge・mock-host・接続・actionErrors | - | - | - | - | - | 208 | 208 | W 167（plan:597） |

**束ごとの合計**

| 束 | P | C | L | I | S | 写す計（W を除く） |
|---|---:|---:|---:|---:|---:|---:|
| 0 横断 | 23 | 19 | 70 | 40 | 55 | 207 |
| 1 | 7 | 52 | 2 | 0 | 0 | 61 |
| 2 | 41 | 15 | 32 | 6 | 23 | 117 |
| 3 | 31 | 29 | 55 | 0 | 0 | 115 |
| 4 | 127 | 128 | 192 | 1 | 18 | 466 |
| 5 | 14 | 26 | 2 | 0 | 21 | 63 |

読み取れること:
- **ブロック UI（束 4）は plan の 114 件（P42・C27・L45）から 201 件に増えた。** Tier9 の特殊族（清浄機・ロケット・宇宙エレベーター）、機械のレシピ選択とスロットの影（ghost）、入力の詰まり表示のテストが増えたため（ファイル名から判断）。段階 4 のレーンの切り方と規模を見積もるときは、今の数を使う
- 束 2 には plan に件数が書かれていない。notification の L 26（ストアと文言）と S 15（演出の設計規約）が大きい
- 束 1 の Web 側の C 37（pauseMenu）と C 15（settings）は、UITK 側の L1 に写し終えている（§3.3）。ただし名前の突き合わせはまだ行っていない（§3.4）

### 1.4 UITK に写すときに気をつける Web テスト（ファイル名と件数からの推測）

- **レイアウトの主張**（L1 のレイアウト照合か L4 の目視へ回す）: `block/cleanRoom/*`（8 件。高さ 720 以下のスクロールとフッタ）、`regression/buildMenuLayout.spec.ts`（7）、`challenge/hudLayout.spec.ts`（5）、`system/pauseMenuSettingsLayout.spec.ts`（2）、`system/portalLayerScaling.spec.ts`（4）、`research/researchViewport.spec.ts`（5）
- **捨てる候補**: `regression/connection.spec.ts`（3。再接続）、`research/tmp-icon-click.spec.ts`（1）、`reference/captureReadiness.spec.ts`（5。撮影の準備）、`system/textSelection.spec.ts`（2。ブラウザの文字選択。意図は L1 で「ラベルを選択できない」として残すかどうかを決める）
- **2 つの画面にまたがる DnD**（両方が UITK にそろってから写す）: `hotbar/hotbar.spec.ts` のビルドメニューからホットバーへのドラッグ（束 3 の中で閉じる）、`tutorial` の DragGuide（束 4）

---

## 2. CEF 無し起動ラダー 6 本の現状（plan §4.4、plan:633-646）

| 段階 | シナリオ | 状態 | 根拠と、下敷きにできる既存シナリオ |
|---|---|---|---|
| 1 | `uitk-lifeline` | **ある。合格の記録あり** | `scenarios/uitk/stage1/uitk-lifeline.cs`。合格は `docs/uitk/TODO.md:160`。起動前に `WebUiHostEditorSkip.Apply()`（`Client.Starter/Initialization/Boot/WebUiStartup.cs:50`、`#if UNITY_EDITOR`）と、受け口の差し替え `MOORESTECH_PLAYTEST_RECEIVER_BASE=http://127.0.0.1:9` が要る。「セーブして終了」で PlayMode が止まるので、save.json の更新は呼び出し側のシェルで確かめる（ファイル冒頭のコメント） |
| 2 | `uitk-opening-hud` | 未作成 | 下敷きの候補: `tutorial-pebble-challenge.cs`・`tutorial-equip-challenge.cs`（開幕のスキットとチャレンジ。ただし GUID 切れで壊れている。bd `moorestech-wwxy`）、`misc/skit-opening-spawn-relative-origin.cs`、`server-notification-smoke.cs`（`notification-host`。これも壊れている） |
| 3 | `uitk-build-bp` | 未作成 | 下敷きの候補: `misc/webui-buildmenu-click.cs`（`build-menu-panel`）、`building/blueprint-copy-paste-via-ui.cs`（`hotbar-slot-0`）、`building/blueprint-right-click-delete-via-ui.cs`。後の 2 本は uGUI の型を参照していてコンパイルできない（bd `moorestech-wwxy`） |
| 4 | `uitk-production-loop` | 未作成 | 下敷きの候補: `misc/sprint/sprint-toggle-mode.cs`（`inventory-sort`）、`tier9/task31-r3/t31r3b-cable-output-space-acceptance.cs`（`machine-input-slots`・`main-slot-`）、`tier9/tier9-4b-rocket-cargo-shift-reject.cs`（Shift の拒否） |
| 4 | `uitk-research-train` | 未作成 | 下敷きの候補: `train/train-timetable-via-ui.cs`（`train-timetable-*` を 13 回操作）、`train/train-station-default-name.cs`（`train-station-name-input`）、`tier9/tier9-5-compute-research.cs` |
| 5 | `uitk-challenge-gate` | 未作成 | 下敷きにできるシナリオは見つからない。チャレンジ一覧と出展モードの言語ゲートに触る録画シナリオは無い（testid の grep で 0 件） |

ラダーに関わる前提の状態:
- **全体スイッチ（albof.17）は未着手。** 段階 1 の画面ごとの所有表 `Client.Ui.State/Routing/UiOwnershipTable.cs` と、そのテスト `UiOwnershipTableTest`（Editor での上書き）がまだ残っている。plan:212・226 のとおり、作り直したあとに `uitk-lifeline` と入れ子ポーズの録画 2 本・IME の L1 を流し直す
- **R2（片側だけを読む）とロケータ。** 今のロケータ `Client.Playtest/UiElements/CompositeUiElementLocator.cs` は UITK を先に探し、無ければ Web を探し、両方で見つかれば失敗にする。この挙動は `CompositeUiElementLocatorTest` の 10 件が固定している。plan §4.4（plan:624）は作り直しでこれを外すと書いている。外したら、この 10 件は「スイッチの系だけで探す」テストへ書き換えることになる
- **段階 1 の付随シナリオ**（ラダーではない）: `uitk-nested-pause-skit.cs`・`uitk-nested-pause-train.cs`（R-13、`TODO.md:161`）、`uitk-pause-web-action-count.cs`（R-14、`TODO.md:162`。CEF ありの一時計測で、`[R14-MEASURE]` の行はすでに外してある。全体スイッチにしたあとは役目を終える見込み〈推測〉）

---

## 3. L0・L1・L2・L3 の土台（feature/uitk の現状）

### 3.1 L0（純ロジック）と S12

| 項目 | 状態 | 根拠 |
|---|---|---|
| Unity の外で `dotnet test`（S12） | **未実施**。台帳の「実測の結論」（`docs/uitk/TODO.md:142-`）に S12 の行は無い | plan:559・696 は「L0 レーンの先頭で確かめる」としている |
| 前例 | `mooresmaster/mooresmaster.Tests`（`.github/workflows/mooresmaster_test.yml:41` で `dotnet test mooresmaster/`） | |
| UITK の状態層の asmdef | `Client.Ui.State`（38 ファイル）は `Client.Game`・`Client.Network`・`Core.Master`・`Server.Protocol`・`MessagePack`・`UniRx`・`UniTask`・`VContainer` を参照し、`noEngineReferences` は false | `moorestech_client/Assets/Scripts/Client.Ui.State/*.asmdef` |
| 見込み | 今の構成のままでは Unity の外でビルドできない（推測）。純ロジックを `noEngineReferences: true` の asmdef（前例 `Client.Build.Policy`）へ分けない限り、S12 は「回らない → Editor のレーンで回す」（plan:559）に落ちる見込み | |
| すでにある L0 相当（EditMode NUnit） | `Ui/Routing/UiScreenRoutingTest`（13 件。Web の `uiScreenRouting.test.ts` 12 件の写し）、`Ui/Input/UiInputExclusivity*`（16 件）、`Ui/State/*`（32 件）、`Ui/Motion/*`（26 件）、`Ui/Text/UiNumberFormatTest`（3 件） | `moorestech_client/Assets/Scripts/Client.Tests/Ui/` |

### 3.2 L1（UiTestHost）

| 部品 | パス | 中身 |
|---|---|---|
| `UiTestHost` | `Client.Tests/Ui/TestHost/UiTestHost.cs` | `UITestFixture` を継承する。`Assets/UI Toolkit/GameUi/GameUiPanelSettings.asset` の themeStyleSheet と 1280×720 を当て、見つからなければ `Assert.Fail` で止める |
| `ScreenTestHost` | `Client.Tests/Ui/Screens/ScreenTestHost.cs` | `UiTestHost` を継承する。本番のカタログ `GameUiAssetCatalog.asset` を読み、言語を英語に固定して TearDown で戻す。出たトーストを集める |
| 偽モデルと記録 | `Client.Tests/Ui/TestDoubles/FakeModel.cs`（`FakeModel<T>`）、`CommandRecorder.cs`（`CommandRecorder<TCommand>`） | 雛形の使い方は `TestDoubles/Samples/CounterSampleViewTest` |
| 画面ごとの偽物 | `Screens/PauseMenu/FakePauseMenuModel`・`RecordingPauseMenuCommands`、`Screens/Settings/FakeSettingsModel`・`RecordingSettingsCommands` | 束 2 以降の画面も同じ形で作る |
| asmdef | `Client.Tests/Tests.asmdef` が `Unity.UI.TestFramework.Runtime` と `.Editor` を参照している | |
| CI | S13 合格（`TODO.md:158`）、R-16 合格（`TODO.md:163`。client-remainder で PauseMenuViewTest 10・SettingsViewTest 10・BugReportFormViewTest 10・ToastHostViewTest 7 が通った）。`GameUiSkeletonTest` は batchmode の入力の都合で `EditorBackgroundInputScope` を当てて直したが、**CI での再実行はしていない**（`TODO.md:158`） | |

plan §4.3 の罠のうち、`MouseMove(from, to)` が途中の要素に PointerEnter を出さないかと、ScrollView の子の `GeometryChangedEvent` は、台帳に結論が無い。段階 4 のインベントリ（右ドラッグ）と、アンカーのスクロール追従の前に確かめる必要がある。

### 3.3 L2（規約テスト）

| テスト | 件数 | 守るもの |
|---|---:|---|
| `Ui/Conventions/UiStyleConventionTest`（規則は `UiStyleConventionRules.cs`・`UiStyleRule.cs`） | 20 | 色リテラル（`Tokens.uss`・`SkitTokens.uss`・`SlotTokens.uss`・`ControlTokens.uss` の外）、構造擬似クラス、z-index、UXML の `text=` のリテラル、`name` の重複、ビットマップの参照、合成の太字。違反を含む一時文字列で検査関数が落ちることも確かめている |
| `Ui/Text/UxmlLiteralTextTest` | 4 | UXML に表示文字列のリテラルを書かない |
| `Ui/Theme/TokenFloorAndTranslucencyTest` | 2 | 13px 未満の文字を使わない、半透明トークンの表 |
| `Ui/Theme/WebTokenParityTest` | 2 | CSS のトークン名と USS のトークン名が、除外分を除いて一致する |
| `Ui/Anchor/AnchorIdsWebParityTest` | 3 | C# の `AnchorIds` と Web の `anchorIds.ts` の一致 |
| `Ui/Input/WebSilenceThresholdWebParityTest` | 1 | 無応答の閾値が Web と同じ |
| `Ui/Assets/UiAssetCatalogTest` | 2 | 本番カタログに全 USS がある（plan:405 の参照パスの実在） |

plan にあってまだ無い L2: `calc()` を置き換えた定数の検算（plan:459）、アンカーのカバレッジテスト（plan:655。これは L1）。

### 3.4 名前の突き合わせ（plan §4.1）

- `docs/uitk/disposition.md` の表は見出しだけで、行が 0 件
- Web のテスト名と NUnit のテスト名を突き合わせるスクリプトは repo に無い（`git ls-files` で該当なし）。plan §4.1 では束の最後のレーンでこのスクリプトを使うことになっているので、束 2 のレーンより前に作る必要がある
- 束 1 の UITK 側の L1 はテスト名を日本語の挙動文にしているが（例: `PauseMenuViewTest`・`BugReportFormViewTest`・`SettingsViewTest`）、Web の名前をそのまま写してはいない。突き合わせは「名前の一致」ではなく、由来の対応表で行うことになる（推測）

### 3.5 L3（EditModeInPlayingTest）

| テスト | shard | 中身 |
|---|---|---|
| `EditModeInPlayingTest/Ui/GameUiSkeletonTest`「骨組みが層を持ちWeb休止と入力素通しが実ゲームで効く」 | CiShardClientPlay1 | |
| `…/Ui/GameUiInputTest`「UI資産の差し替え前後でWASDと矢印とSpaceとEnterがナビゲーションイベントを出すかを数える」「実ゲームでGameUiの入力欄がフォーカスとIMEとESCとポインタとDSLに結線されている」 | CiShardClientPlay1 | S4 の証拠 |
| `…/Ui/PauseMenuGameUiTest`「EscでUITKのポーズが出てWebが休止し設定へ行き来して閉じるとWebが戻る」 | CiShardClientPlay1 | 全体スイッチにすると「Web が休止して戻る」は成り立たなくなる。書き直しが要る（推測） |
| `…/Hostless/WebUiHostlessStateAvailabilityTest`「Webホストを起動しなくても移した状態がDIから取れる」 | | probe 6 本（BlockDetail・Notification・Research・Timetable・Modal・Challenge）。ラダー 2〜5 で使う状態が CEF 無しでも取れることの L3 版 |

---

## 4. 録画シナリオのうち UI に触るもの（段階との対応）

`.agents/skills/unity-playmode-recorded-playtest/scenarios/` には 110 本ある。DSL の UI 操作（`ClickUi`・`UntilUiElement`・`WaitUiElement`・`SetText`・`ClickBuildMenuBlock`・`ScrollDownUntilUiElementIsClickable`・`RightShortClick`・`IsWebUiAvailable`、または testid の直書き）を含むものは 50 本。testid から束を当てると次のとおり（括弧内は UI 操作の行数）。

| 束 | シナリオ |
|---|---|
| 1 ポーズ・設定・バグ報告 | `uitk/stage1/uitk-lifeline`(16)・`uitk/stage1/uitk-nested-pause-skit`(5)・`uitk/stage1/uitk-nested-pause-train`(5)・`uitk/uitk-pause-web-action-count`(8)・`misc/pause-menu-hierarchy`(14)・`misc/feedback-report-labels`(7)・`misc/localization-language-switch-via-ui`(22。ビルドメニューにも触るので束 3 にもまたがる)・`graphics-quality/quality-ui-switching`(6)・`graphics-quality/quality-recovery-ui`(4)・`misc/sprint/sprint-invalid-recovery`(4)・`misc/sprint/sprint-toggle-mode`(4。インベントリにも触るので束 4 にもまたがる)・`playtest-receiver/developer-report-delivery`(11)・`playtest-receiver/unsent-report-export`(10)・`playtest-receiver/developer-report-interrupt`(2) |
| 2 常駐表示 | `server-notification-smoke`(3。壊れている)・`misc/skit-opening-spawn-relative-origin`(5)・`misc/cursor-tooltip-follows-crosshair`(3)・`misc/right-short-press-cancel`(2)・`misc/walk-while-menus-open`(1)。チュートリアル系の `tutorial-*.cs` 4 本は Hud・Highlight を参照するが、DSL の UI 操作は数えていない（4 本とも壊れている） |
| 3 建築 | `misc/webui-buildmenu-click`(5)・`connect/electric-wire-tool-*`(5 本・各 3)・`connect/electric-wire-connect-via-ui`(2。壊れている)・`building/blueprint-copy-paste-via-ui`(6。壊れている)・`building/blueprint-right-click-delete-via-ui`(6。壊れている)・`building/blueprint-place-like-block`(3)・`building/blueprint-large-paste-flicker`(1)・`building/blueprint-terrain-sink-partial-paste`(1)・`building/delete-tool/delete-hold-text-focus`(1)・`misc/fps-tps-view-toggle-via-ui`(2。壊れている) |
| 4 持ち物・ブロック UI・研究・列車 | `tier9/*` 11 本（task31-r3 の 1 本を含む。`block-inventory-close`・`machine-*-slots`・`rocket-launch-pad-*`・`generator-fluid-slots`・`machine-input-stall` など）・`train/train-timetable-via-ui`(13)・`train/train-station-default-name`(2)・`train/fluid-inventory-via-interact`(4)・`connect/rotary-generator-ui`(4)・`misc/cleanroom-room-usability`(6)・`misc/sprint/sprint-hold-restart`(1) |
| 5 チャレンジ・言語ゲート | 該当なし |

壊れている 10 本（bd `moorestech-wwxy`。`moorestech-dww3`〈EDC がコンパイルできなくても黙って通る〉に依存）のうち、`blueprint-copy-paste-via-ui`・`blueprint-right-click-delete-via-ui`・`electric-wire-connect-via-ui`・`fps-tps-view-toggle-via-ui` の 4 本は束 3 の UI 経路そのもので、`tutorial-*` 3 本と `server-notification-smoke` は束 2 の UI に触る。plan:720 は、切替 PR の完了条件として「壊れている 10 本は直すか削除して wwxy を閉じる」と書いている。束 2・3 のラダーを作る前に、`dww3` を閉じておかないと、ラダーが黙って通ってしまうおそれがある（推測。`dww3` の説明文から）。

---

## 5. 段階ごとの完了条件に使うテストの名前

plan §2 の共通条件（§2.0）とラダーに加えて、次のテストを各段階の完了条件の「名前付きの物差し」として使える。束 2 以降の Web 側は、写す元のファイル名（件数）を書いた。全テスト名は付録 A（Playwright）と付録 B（vitest のファイル）にある。

### 段階 1（feature/uitk 済み。albof.17 のあとで流し直す）
- ラダー: `uitk-lifeline`。録画: `uitk-nested-pause-skit`・`uitk-nested-pause-train`
- L1: `Client.Tests.Ui.Screens.PauseMenu.PauseMenuViewTest`(10)・`PauseMenuViewFocusTest`(1)・`Screens.Settings.SettingsViewTest`(10)・`GraphicsQualityRowsTest`(3)・`Screens.BugReport.BugReportFormViewTest`(11)・`BugReportRecoveryControlsViewTest`(1)・`BugReportDescriptionHeightTest`(2)・`Screens.Toast.ToastHostViewTest`(7)・`Components.*`(32)・`Layers.*`(5)・`Motion.PanelTimeAdvanceTest`(2)
- L0: `Ui.State.*`(32)・`Ui.Routing.UiScreenRoutingTest`(13)・`Ui.Input.UiInputExclusivity*`(16)・`Ui.Input.GameUiInputRulesTest`(8。Space・Enter・Tab と ESC のラッチ)・`Ui.Motion.*`
- L2: §3.3 の 7 クラス
- L3: `GameUiSkeletonTest`・`GameUiInputTest`・`PauseMenuGameUiTest`（全体スイッチにしたら書き直す）
- 作り直しで書き換える: `UiOwnershipTableTest`(2)・`CompositeUiElementLocatorTest`(10)・`UiInputExclusivityWebSuspensionTest`(2)（全体スイッチで「委譲中の休止」が無くなるため。推測）
- Web 側の写し元: `src/features/pauseMenu/**`(37)・`src/features/settings/**`(15)・`e2e/tests/system/pauseMenu.spec.ts`(4)・`pauseMenuSettingsLayout.spec.ts`(2)・`feedbackReportLabels.spec.ts`(1)・`src/features/toast/toastStore.test.ts`(2)

### 段階 2（束 2）
- ラダー: `uitk-opening-hud`（未作成）＋ `uitk-lifeline`
- 写し元 P: `notification/{animation,fixedWidth,itemEarned,layering}.spec.ts`(6)・`modeHud/operation-mode-hud.spec.ts`(6)・`progress.spec.ts`(3)・`skit.spec.ts`(4)・`skitPauseInput.spec.ts`(3)・`phaseC4.spec.ts`(2)・`system/commonHud.spec.ts`(6)・`system/worldPin.spec.ts`(6)・`challenge/hudLayout.spec.ts`(5)
- 写し元 L・C・S: `notification/notificationStore.test.ts`(11)・`notificationMessages.test.ts`(15)・`notificationServerIdCoverage.test.ts`(5)・`notificationAnimationDesign.test.ts`(7)・`notificationLayering.test.ts`(3)・`modeHud/*`(11)・`skit/interaction.test.ts`(5)・`keyHint/KeyHintHud.test.ts`(4)・`tutorial/KeyControlHintHud.test.ts`(4)・`shared/tooltip/*`(8)・`commonHud/alwaysOnHudWheel.test.ts`(3)
- 段階 2 で同時に要る道具: アンカーのカバレッジテスト（plan:655）、名前の突き合わせスクリプト（§3.4）

### 段階 3（束 3）
- ラダー: `uitk-build-bp`（未作成）＋前の 2 本
- 写し元 P: `regression/buildMenu.spec.ts`(11)・`buildMenuLayout.spec.ts`(7)・`buildMenuRestore.spec.ts`(1)・`placementIconCentering.spec.ts`(2)・`hotbar/hotbar.spec.ts`(6)・`modal.spec.ts`(4)
- 写し元 L・C: `buildMenu/logic/buildMenuGrouping.test.ts`(14)・`buildMenuScrollSpy.test.ts`(12)・`hooks/useBuildMenuCategoryScroll.test.ts`(13。plan:282 で「先に L1 で固定」とされている)・`sessionState/buildMenuSessionState.test.ts`(4)・`views/*`(9)・`hotbar/hotbarDnd.test.ts`(3)・`useHotbarDragSource.test.ts`(7)・`modal/modalLogic.test.ts`(8)・`shared/placementTarget/selectableTarget.test.ts`(9)・`shared/pointerGesture/dragThreshold.test.ts`(5)
- 録画（再走）: `misc/webui-buildmenu-click`・`connect/electric-wire-tool-*` 5 本・`building/blueprint-place-like-block`。壊れている BP 系 2 本は wwxy で直してから

### 段階 4（束 4）
- ラダー: `uitk-production-loop`・`uitk-research-train`（どちらも未作成）＋前の 3 本
- 写し元 P: `inventory/{inventory,equipment,grabOverlay}.spec.ts`(20)・`recipe/*`(19)・`block/**`(63)・`regression/{itemSlot,sectionStack}.spec.ts`(3)・`research/*`(19。`tmp-icon-click` は捨てる)・`train.spec.ts`・`train/timetable.spec.ts`(4)
- 写し元 L: `shared/itemMove/*`(33)・`recipe/logic/holdCraftLogic.test.ts`(9)・`craftLogic.test.ts`(9)・`research/researchLogic.test.ts`(21)・`shared/treeView/**`(29)・`blockInventory/**/*Logic*.test.ts`・`train/timetableEditLogic.test.ts`(6)・`inventory/slotActions.test.ts`(8)・`blockSlotRestriction.test.ts`(7)
- 録画（再走）: `train/train-timetable-via-ui`・`train/train-station-default-name`・`train/fluid-inventory-via-interact`・`tier9/*` 11 本・`misc/cleanroom-room-usability`・`connect/rotary-generator-ui`
- 先に確かめる罠: `MouseMove` の PointerEnter（plan §4.3）。全本の録画を流すのは段階 4 の終わり（plan:92）

### 段階 5（束 5 と切替 PR）
- ラダー: `uitk-challenge-gate`（未作成）＋ 5 本。切替 PR では CEF の存在しないビルドで 6 本を流す（plan:373）
- 写し元: `tests/challenge.spec.ts`(7)・`challenge/challengeLogic.test.ts`(2)・`eventLanguageGate/*`(9)・`system/tutorial.spec.ts`(6)・`system/tutorialHighlightClip.spec.ts`(1)・`tutorial/overlay/TutorialOverlay.test.ts`(17)・`tutorialAttentionDesign.test.ts`(21)・`system/i18n.spec.ts`(2)・`shared/i18n/allScreensI18n.test.ts`(6)・`uiState.spec.ts`(5)・`regression/inputLayer.spec.ts`(5)
- 切替 PR の物差し: 名前の突き合わせスクリプトで差分 0（または disposition に理由付きで記録）、G4（`WebUi`・`Cef`・`webui` のトークンが許可リストの外で 0）、壊れている 10 本を直すか削除する（plan:720）

---

## 付録 A: Playwright のテスト名（e2e/tests、ファイルごと）

テンプレート文字列の `${…}` は展開前のまま載せた。複数行の `test(` も拾う別の正規表現で抜いたため、ファイルごとの件数の合計は 248 件で、§1 の 243 件とは少しずれる。

- `block/blockDetails.spec.ts`（8）: renders ${type} detail section ／ 歯車機械は消費トルクと基準付きRPMを赤なしで出し、網の需給行を持つ ／ ${type} は充足率不足でも充足率行を赤にしない ／ 電力駆動の採掘機は充足率不足を赤で出す ／ 歯車発電機は発生トルクと現在RPMだけを出す ／ 機械recipeの出力個数と秒数から分間生産数を表示する ／ 油井は電力充足率と公称生成速度を出し鉱脈外なら警告行に切り替わる ／ 歯車ポンプは電力行を持たず歯車行を出す
- `block/blockInventory.spec.ts`（3）: chest の block inventory パネルが描画される ／ filled スロット左クリックで block→grab の move_item を送り、grab オーバーレイが出る ／ ブロックを閉じると panel が消える
- `block/blockInventoryGestures.spec.ts`（5）: 空手の右クリックで block_inventory.split を送り grab に半分入る ／ grab 保持中の右クリックで block スロットへ1個置く ／ ダブルクリックで block_inventory.collect を送る ／ Shift+クリックで block→main へ配分移動する ／ block 開時は main の Shift+クリックが block へ配分移動する
- `block/blockRegistryCoverage.spec.ts`（1）: v8マスタで専用UIを持つ全blockTypeがレジストリまたは意図的Genericに分類される
- `block/cleanRoom/cleanRoomAirFilterPanel.spec.ts`（2）: 清浄機パネル(${room.fixture})・高さ${windowHeight}で部屋欄の末尾とフィルタ枠へ届き、枠がホットバーへ潜らない ／ 清浄機2台・高さ720は自然高のままスクロールしない
- `block/cleanRoom/cleanRoomMachineLayout.spec.ts`（2）: 清浄機2台の室内機械で液体行が電力行・状態行に重ならない ／ 低いウィンドウで清浄機が多い部屋でもフッタは見え、本文はスクロールで液体行へ届く
- `block/cleanRoom/cleanRoomManyFilters.spec.ts`（4）: 清浄機${room.filters}台・高さ${windowHeight}でも部屋欄ごと本文がスクロールし、レシピ・スロット・液体に届きフッタが読める ／ 清浄機20台・高さ${windowHeight}のレシピ選択でも部屋欄と同じ視口で最後のレシピ行へ届く ／ 清浄機${room.filters}台・高さ720の境目でも本文とフッタが使える ／ 部屋の無い大型機械は部屋欄なしで本文に届き、フッタが読める
- `block/electric/electricPole.spec.ts`（1）: 電柱専用ビューに発電量・需要量・消費者数・供給率を表示する
- `block/electric/electricToGear.spec.ts`（1）: 出力モード行とStateDetail値を表示し、選択indexをaction送信する
- `block/filterSplitter.spec.ts`（3）: mode switch sends explicitly selected mode ／ right click on a filter slot sends clear ／ last direction's filter slot is reachable by scrolling on a short window
- `block/fluidSlot.spec.ts`（5）: tank の block inventory パネルに Fluid Tank が表示される ／ 水の流体スロットが量 500 を表示する ／ Water 名のツールチップが hover で表示される ／ progress-arrow-bar のフィル幅が 50% になる ／ 通常のe2eでは液体アイコンが404になり背面フィルが残る
- `block/machineGestures.spec.ts`（3）: 機械入力スロットの空手右クリックで block_inventory.split を送る ／ 機械出力スロットのダブルクリックで collect を送る ／ 機械入力スロットの Shift+クリックで main へ配分移動する
- `block/machineRecipe.spec.ts`（10）: 選択済機械は大型パネルでヘッダ＋レシピ分スロット＋ゴーストを出し、タブを持たない ／ ヘッダクリックでレシピ選択モードへ戻り、行クリックでインベントリモードへ戻る ／ レシピ未選択の機械はレシピ選択モードで開く ／ レシピ無しブロックは小型パネルのまま ／ レシピが溢れても選択リストの視口高は変わらず、そこだけがスクロールする ／ フッタは選択モードとインベントリモードで同じ高さに出る ／ 行密度は控えめ設定どおりで、本文に3.8〜4.5行分が入る ／ レシピ選択行の液体はアイテムスロットと同寸の枠に収まり、量バッジを出す ／ 液体アイコンが404なら辞書の液体名を枠内に収めて出す ／ 液体のみ出力のレシピを選ぶと選択中レシピ表示が液体スロットになる
- `block/machineRequiredCount.spec.ts`（4）: 必要数を満たした素材スロットは入っている数/必要数を黒字で出し、個数バッジを重ねない ／ 不足している素材スロットは数字だけ赤くし、入れたアイテムは減光しない ／ 出力スロットには実物が入っていても必要数を出さない ／ 必要数の数字の上で右クリックしても split を送る
- `block/spaceElevator/shiftRejection.spec.ts`（2）: 受け付けないアイテムのShiftは送らずに拒否の通知を出す ／ 詳細の取得待ちのShiftは送らずに取得待ちを通知する
- `block/spaceElevator/spaceElevator.spec.ts`（6）: 接続ボタンでactionを送り、接続後は進みを出す ／ ${fixture} の状態と進捗を描画する ／ 入口への投入は元slot0を送り出口を変えない ／ 出口${outputIndex + 1}の回収は元slot${outputIndex + 1}を送る ／ 詳細の取得待ちから接続可能へ遷移する ／ ホストが並べ替えた入口と出口の添字を描画と操作に使う
- `block/trainPlatform.spec.ts`（2）: PFスロットを表示し、積込から卸しへ目標モードActionを送る ／ 液体PFは液体名・アイコン・量容量と卸しモードを表示する
- `challenge.spec.ts`（7）: challenge.current完了eventで進行HUDを更新する ／ チャレンジ画面が開きツリーだけを翻訳済み表示する ／ 常駐HUDをインベントリ・メニュー・操作モードで維持する ／ 言語辞書世代の更新だけで進行中チャレンジを再解決する ／ blockingスキット中だけ進行中チャレンジを隠す ／ 背景スキット中は進行中チャレンジの描画契約を維持する ／ チャレンジ画面は進行中のチャレンジを中央にして開き、カテゴリ切替でもその進行中へ寄せる
- `challenge/hudLayout.spec.ts`（5）: 進行中チャレンジを内部キーを出さずインベントリ同族の面付きで表示する ／ 面付きHUDは目標3件でもメニュー上端の安全帯に収まる ／ 横長画面で進行HUDを実画面左上へ置き罫線を約3分の1へ縮める ／ 複数目標を受信順で表示し長文をHUD幅内へ折り返す ／ 複数の長文目標を受信順かつHUD幅内で表示する
- `hotbar/hotbar.spec.ts`（6）: ホットバー9枠が常時表示され、fixtureのselectedSlotがハイライトされる ／ 空き枠のクリックはhotbar.select{index}を送る ／ ビルドメニューエントリを空き枠へドラッグするとhotbar.assign{slot,id}を送る ／ 未解決の割当枠は使用不可表示になり、枠外ドラッグで外せる ／ blockingスキット中はホットバーHUDだけが退き、会話窓は残る ／ 枠外へドラッグするとhotbar.clear{slot}を送る
- `inventory/equipment.spec.ts`（11）: 小さいホイール入力を累積し閾値を越えた時だけ切り替える ／ ホイールは末尾スロットの次に先頭スロットへ回る ／ メインのアイテムをUI操作だけで装備スロットへ移せる ／ 装備枠のクリックは選択ではなくアイテム移動になる ／ 装備へ移したアイテムをホイールで選択できる ／ 装備HUDの上でもホイールで装備が切り替わる ／ ホットバーHUDの上でもホイールで装備が切り替わる ／ 画面中央のクロスヘアの上でもホイールで装備が切り替わる ／ ホットバー帯のスロット列外でもホイールで装備が切り替わる ／ 持ち物画面の一覧スクロールでは装備が持ち替わらない ／ 装備枠0でも装備HUDは面積を保ちアンカーが解決する
- `inventory/grabOverlay.spec.ts`（2）: アイテムを掴んだ瞬間にオーバーレイがクリック座標へ表示される ／ 拡大表示でもgrab中心がカーソル位置へ追従する
- `inventory/inventory.spec.ts`（7）: 接続後にインベントリが描画される ／ 左クリックで grab オーバーレイが追従する ／ ダブルクリックで同種を集約し、collect はクリックされたスロットを送る ／ 右クリックで inventory.split を送る ／ grab 中の右ドラッグで通過スロットへ1個ずつ配置する ／ grab 中の左ドラッグは配分先だけを host へ送る ／ grab 中に別アイテムのスロットを押すと入れ替えの move を送りトーストを出さない
- `modal.spec.ts`（4）: 接続後にモーダルが描画される ／ OK クリックで confirm を送りモーダルが消える ／ 背景クリックで cancel を送る ／ BP名入力fixtureは入力後の確定値を送る
- `modeHud/operation-mode-hud.spec.ts`（6）: 配置モードHUDを右上のクラフト枠で表示する ／ 配置対象connectToolをGuidだけの配信から辞書表示名へ解決する ／ 配置対象trainCarをGuidだけの配信から辞書表示名へ解決する ／ 横長画面でも配置モードHUDを実画面右上へ固定する ／ 削除モードをuGUI準拠の上下警告帯で表示する ／ 削除箱の高さだけを右上に表示して更新する ${viewport.width}
- `notification/animation.spec.ts`（1）: 通知は左からのスライドとフェードで入場し生存尺の終端で退場する
- `notification/fixedWidth.spec.ts`（1）: 長さの違う通知が同時に出ても各行の横幅は固定値のまま
- `notification/itemEarned.spec.ts`（1）: アイテム獲得通知はアイコンとアイテム名と獲得数を出し連続獲得で数値が伸びる
- `notification/layering.spec.ts`（3）: インベントリを開いている間、通知はパネルの描画を一切変えない ／ GameScreenでも通知はマウントされ続け、矩形を持つ ／ 通知ホストはstageより後ろの層に立つ
- `phaseC4.spec.ts`（2）: background skit snapshot renders speaker and body ／ cutscene state withdraws every web UI layer
- `progress.spec.ts`（3）: ラベル付き汎用進捗を維持する ／ 採掘進捗は対象名を出さずホットバー全幅の12px上に1本だけ描画する ／ 縮小viewportでも採掘進捗とホットバーの相対配置を維持する
- `recipe/itemListScroll.spec.ts`（6）: スクロール領域はパネル本文いっぱいで、既定件数では溢れずバーも出ない ／ パネルを超える件数で初めて縦バーが出る。横は常に出ない ／ 溢れていない一覧の最上段でもチュートリアルのラベルが出る ／ アンカーがスクロールで隠れ始めたらラベルを引っ込め、完全に隠れたら枠も消す ／ クリップ境界はグリッドからハイライトの逃げぶん離れ、枠が削られない ／ 7段は溢れず、最下段までスクロールしても最終段に逃げが残る
- `recipe/recipe.spec.ts`（9）: 秒数は矢印の上に出し、クラフトボタンは秒数を持たない ／ 正本のヘッダ装飾、1段時の無スクロールバー、主要構造を保つ ／ アイテム選択でレシピ表示、長押しで素材が尽きるまで連続クラフトする ／ リスト上をドラッグしてもアイテム選択は変わらない（スクロール操作扱い） ／ 押下後にボタンから外れるとクラフトが止まり経過時間がリセットされる ／ 複数レシピはクラフト優先の単一リストで同時に表示される ／ クラフトエントリが複数でもチュートリアルアンカーは1件だけ付く ／ クラフト可能数0のアイテムは個数バッジを出さない ／ 機械レシピの一覧に液体の入力を量付きで出す
- `recipe/recipeListScroll.spec.ts`（2）: レシピが溢れてもリストの視口高は変わらず、縦バーだけが出る ／ クリップ境界はエントリからハイライトの逃げぶん離れ、最下段までスクロールしても保たれる
- `recipe/slotTooltip.spec.ts`（2）: ツールチップはMantine既定の白い角丸ではなくCursorTooltipと同じ面を使う ／ 祖先のスクロールでツールチップを引っ込め、ポインタを動かせば開き直す
- `reference/captureReadiness.spec.ts`（5）: every capture has expectations and block fixtures are derived ／ delayed snapshot waits for content, mismatched content fails ／ video excludes a four-second snapshot setup from a three-second clip ／ notification clip retains entry and complete exit ／ world pin reference selects the animated off-screen arrow
- `regression/actionErrors.spec.ts`（2）: block_inventory.splitの${error}はtoastを出さない ／ block_inventory.splitのinvalid_slotはtoastを出す
- `regression/buildMenu.spec.ts`（11）: ui_stateでビルドメニューを開閉し全カテゴリのエントリが1本スクロールに載る ／ エントリ選択とBP右クリック削除のアクション契約 ／ 閉じるボタンはGameScreen遷移を要求する ／ 全カテゴリが1本スクロールに並び、サイドバー押下で見出しへジャンプしハイライトが追従する ／ 末尾カテゴリへジャンプしても見出しが視口上端に来る ／ 検索は同じリストを絞り込み、ヒットの無いカテゴリだけサイドバーで無効になる ／ 検索0件は該当なし表示 ／ 詳細サイドバーはホバー後にstickyで残る ／ エントリの無いカテゴリはサイドバーに出ない ／ 残り設置数は1セット複数個のエントリだけに表示される ／ 検索文字列も閉じて開き直すと復元される
- `regression/buildMenuLayout.spec.ts`（7）: パネルは画面水平中央に表示される ／ パネルは上部安全帯の直下にバンド高いっぱいで立つ ／ カテゴリボタンは全ボタン同一の固定高 ／ カテゴリサイドバーは全カテゴリとラベルを固定高のまま収める ／ 英語ロケールでもカテゴリ名は1行のまま固定高に収まる ／ グリッドは8列を保ち中央列に収まる ／ スクロール領域のクリップ境界をハイライト枠の逃げぶんセルから離す
- `regression/buildMenuRestore.spec.ts`（1）: 閉じて開き直すと検索・スクロール・詳細stickyが復元される
- `regression/connection.spec.ts`（3）: 切断後は全topic snapshotをrestoring中に復元し旧世代snapshotを破棄する ／ inventory未受信中はconnecting placeholderを表示する ／ WS切断時は再接続バナーをz-reconnect層に表示する
- `regression/inputLayer.spec.ts`（5）: block inventory上のEscapeはGameScreen遷移を要求する ／ Tabはブラウザのフォーカスを動かさない ／ ボタンにフォーカスしてSpaceを押してもclickせずキーハンドラにも届かない ／ checkboxではSpaceでトグルせず、contenteditableには空白が入る ／ GameScreenのホイールは最新equipment値から次スロットを選ぶ
- `regression/itemSlot.spec.ts`（2）: 素材不足スロットはdata-insufficientと40% opacityを持つ ／ インベントリスロットがマスタGuidの辞書名をtooltip表示する
- `regression/placementIconCentering.spec.ts`（2）: ビルドメニューのアイコンは枠の中央に描かれる ／ ホットバーのアイコンは枠の中央に描かれる
- `regression/sectionStack.spec.ts`（1）: ${entry.type}は対応セクションだけを表示する
- `research/research.spec.ts`（12）: research tree renders nodes when uiState enters ResearchTree ／ ノードカードが4状態のdata属性で描き分けられる ／ 研究報酬itemの個数をtopic payloadどおり詳細ペインで表示する ／ 詳細ペインに解放物が種類別ラベル付きで並ぶ ／ translate後のグリップ矩形だけに重なる境界buttonをexpectCraftGripが検出する ／ padding box外の境界buttonをexpectCraftGripが重なりなしと判定する ／ research button sends research.complete and node becomes completed ／ 研究パネルは持ち物の右隣から画面端までを占有し持ち物と重ならない ／ 研究画面では持ち物がstage左paddingぶん左へ寄る ／ 研究パネル展開中も常駐チャレンジHUDとキーヒントが遮蔽されない ／ 研究パネル展開中も採掘進捗バーが遮蔽されない（.viewportOverlayのz封じ込めに依存する回帰ガード） ／ 研究画面ではホットバーと装備HUDを描画しない
- `research/researchMachineUpgradeUnlock.spec.ts`（1）: 研究ノードの詳細にスロット上限と製作上限の解放行が出る
- `research/researchViewport.spec.ts`（5）: research tree opens centered on the researchable node ／ research tree keeps its pan position across close and reopen ／ research tree zooms with the wheel and pans by dragging its empty background ／ ノードを掴んだドラッグもパンになり、詳細ペインは開かない ／ 閾値未満のノード押下はタップとして詳細ペインを開く
- `research/tmp-icon-click.spec.ts`（1）: tmp: 実画像アイコン上のクリック
- `skit.spec.ts`（4）: blocking skit reveals, advances, and selects by choiceId ／ 会話窓にフォーカスがあってもSpaceでは送らずEnterで送る ／ 横長画面で会話帯を全幅に広げツールを実画面右上へ固定する ／ Escでポーズメニューを開いても会話UIは背後で表示され続ける
- `skitPauseInput.spec.ts`（3）: ポーズ中は会話窓とツールを実クリックしても全文表示も操作も出さず、再開後は操作できる ／ ポーズ中は選択肢を実クリックしても選ばず、再開後は選べる ／ フォーカス後のポーズ中はEnterで全文表示しない（許可空=${emptyIntents}）
- `system/commonHud.spec.ts`（6）: 設置情報と削除警告帯を操作モードへ反映する ／ 横長画面でビネットを実viewportの四辺へ沿わせる ／ 採掘進捗・クロスヘア・tooltipのtopic eventを表示する ／ ツールチップは複数行を順序どおり縦積みで表示する ／ ツールチップは非表示中に届いたpointer位置へ初回表示から出る ／ ui.visibility=falseでPortalを含む全UIを退避し復帰する
- `system/feedbackReportLabels.spec.ts`（1）: 感想送信のボタンと完了通知: ${label.locale}
- `system/i18n.spec.ts`（2）: localization.current切替でlocale別辞書を再取得し表示を更新する ／ ポーズメニューの言語選択で英語と日本語を往復する
- `system/pauseMenu.spec.ts`（4）: PauseMenu遷移で表示しセーブとセーブして終了actionを送る ／ pause_menu.currentの切断状態を表示する ／ トップから設定画面へ進み言語を選べ、戻るでトップへ戻る ／ トップからバグ報告画面へ進み送信すると成功を表示する
- `system/pauseMenuSettingsLayout.spec.ts`（2）: 設定画面は${viewport.width}x${viewport.height}で枠内に収まり本文だけスクロールする ／ 復旧表示は${viewport.width}x${viewport.height}で収まり同値操作と解除通知を受ける
- `system/portalLayerScaling.spec.ts`（4）: 通知の幅はstage比のまま解像度に依らず、高解像度でも画面幅の2割に収まる ／ ワールドピンは高解像度で拡大しても先端が射影座標に留まる ／ チュートリアルの枠線ラベルは高解像度で拡大しても枠線に付いたまま ／ 画面端矢印は高解像度で拡大しても余白ごと広がり画面内に収まる
- `system/textSelection.spec.ts`（2）: 入力欄以外はテキスト選択できない ／ 建設メニューの検索入力はテキスト選択できる
- `system/tutorial.spec.ts`（6）: tutorial outline highlights the target without dimming the rest of the screen ／ outline with a label renders the label text beside the outline ／ keyControl hint renders a kbd and text above the hotbar while uiState matches ／ keyControl hint actually plays the strong 1.08 pulse in the browser ／ tutorial outline actually plays the subtle 1.03 pulse in the browser ／ outline never outlives its anchor even when no frame arrives
- `system/tutorialHighlightClip.spec.ts`（1）: 研究ノードのハイライトが祖先のoverflowクリップに合わせてマスクされる
- `system/worldPin.spec.ts`（6）: on-screen world pin renders with its marker tip at the projected viewport position ／ world pin follows updated projections ／ off-screen world pin renders a 56px filled shaft arrow at the screen edge ／ off-screen arrow follows a diagonal direction to the corner region ／ off-screen arrow remains inside the viewport at all four diagonal corners ／ clearing world pins removes the overlay
- `train.spec.ts`（2）: 乗車HUDと分岐選択を表示し、入れ子Pauseへ遷移する ／ 貨車スロットとコンテナ不在エラーを表示する
- `train/timetable.spec.ts`（2）: 駅の追加・上下移動・削除を即時反映し、再読込でも保持する ／ 拒否された追加を巻き戻し、次の追加で復帰する
- `uiState.spec.ts`（5）: 既定(PlayerInventory)でインベントリ画面が表示される ／ GameScreen でパネルが消え、PlayerInventory への event で再表示される ／ SubInventory でインベントリ+ブロックパネルが出てレシピビューアは消える ／ ブロックパネルの✕で ui_state.request(GameScreen) を送り画面が閉じる ／ 許可表で拒否されたui_state.requestはtransition_not_allowedを返し画面を維持する

## 付録 B: vitest のファイルと件数

種別（機械分類）・件数の順。パスは `moorestech_web/webui/src/` から。

- `app/App.webUiSuspended.test.ts`（C・1）
- `app/App.architecture.test.ts`（S・2）
- `app/iconTextOutlineDesign.test.ts`（S・6）
- `app/zLayerTokens.test.ts`（S・6）
- `features/blockInventory/details/spaceElevator/__tests__/SpaceElevatorInventory.test.tsx`（C・1）
- `features/blockInventory/views/__tests__/TrainPlatformInventory.test.tsx`（C・1）
- `features/blockInventory/details/machine/ModuleSlotStackLimitText.test.ts`（C・2）
- `features/blockInventory/details/machine/SelectedRecipeHeader.test.ts`（C・2）
- `features/blockInventory/details/__tests__/BeltThroughputSection.test.ts`（C・3）
- `features/blockInventory/details/__tests__/GearSection.test.ts`（C・3）
- `features/blockInventory/details/cleanRoom/__tests__/CleanRoomYieldSection.test.ts`（C・3）
- `features/blockInventory/details/machineInputStall/__tests__/MachineInputStallSection.test.ts`（C・3）
- `features/blockInventory/details/cleanRoom/__tests__/CleanRoomSection.test.ts`（C・4）
- `features/blockInventory/details/spaceElevator/__tests__/SpaceElevatorSection.test.tsx`（C・4）
- `features/blockInventory/details/machine/recipeSelection/MachineRecipeSelectionRow.test.ts`（C・5）
- `features/blockInventory/views/__tests__/RocketLaunchPadInventory.test.tsx`（C・5）
- `features/blockInventory/details/__tests__/GearToElectricSection.test.ts`（C・7）
- `features/blockInventory/views/__tests__/SectionStackView.test.tsx`（C・7）
- `features/blockInventory/details/MachineSection.test.ts`（C・9）
- `features/blockInventory/details/rocket/rocketLaunchPadLogic.test.ts`（L・0）
- `features/blockInventory/details/blockInfoFetch/__tests__/blockInfoFetchStateLogic.test.ts`（L・1）
- `features/blockInventory/details/LackHighlightText/index.test.ts`（L・2）
- `features/blockInventory/details/cleanRoom/__tests__/cleanRoomLogic.test.ts`（L・2）
- `features/blockInventory/details/machineInputStall/__tests__/machineInputStallLogic.test.ts`（L・2）
- `features/blockInventory/blockInventoryKeydown.test.ts`（L・4）
- `features/blockInventory/details/cleanRoom/__tests__/cleanRoomYieldLogic.test.ts`（L・4）
- `features/blockInventory/filterSplitterLogic.test.ts`（L・4）
- `features/blockInventory/details/machine/machineRecipeSelectionLogic.test.ts`（L・5）
- `features/blockInventory/details/__tests__/beltThroughputLogic.test.ts`（L・7）
- `features/blockInventory/details/detailLogic.test.ts`（L・7）
- `features/blockInventory/registry/blockComponentRegistry.test.ts`（L・10）
- `features/blockInventory/details/machine/machineSlotGhosts.test.ts`（L・13）
- `features/blockInventory/details/__tests__/GearToElectricSection.i18n.test.ts`（S・1）
- `features/blockInventory/BlockInventoryPanel.localization.test.ts`（S・2）
- `features/blockInventory/blockInventoryDesign.test.ts`（S・15）
- `bridge/contract/schemas/__tests__/guidValidation.test.ts`（W・0）
- `bridge/contract/schemas/__tests__/contentIdentity.test.ts`（W・1）
- `bridge/contract/actionNames.test.ts`（W・2）
- `bridge/contract/eventLanguageGateContract.test.ts`（W・2）
- `bridge/contract/wireContract.trainTimetable.test.ts`（W・2）
- `bridge/playtest/playtestTopicResponder.test.ts`（W・2）
- `bridge/contract/contentGuidContracts.test.ts`（W・3）
- `bridge/contract/wireContract.machineUpgrade.test.ts`（W・3）
- `bridge/contract/wireContract.research.test.ts`（W・3）
- `bridge/contract/schemas/rocketLaunchPad/index.test.ts`（W・4）
- `bridge/store/languageList/languageListStore.test.ts`（W・4）
- `bridge/store/topicEventDelivery.test.ts`（W・4）
- `bridge/contract/schemas/__tests__/inventory.test.ts`（W・5）
- `bridge/contract/schemas/spaceElevator/index.test.ts`（W・5）
- `bridge/store/fluidMasterStore.test.ts`（W・6）
- `bridge/store/topicStore.test.ts`（W・6）
- `bridge/transport/httpEndpoints.test.ts`（W・6）
- `bridge/playtest/domSetTextResponder.test.ts`（W・7）
- `bridge/store/itemMasterStore.test.ts`（W・7）
- `bridge/transport/subscriptionManager.test.ts`（W・7）
- `bridge/transport/tests/webSocketClient.test.ts`（W・9）
- `bridge/contract/schemas/__tests__/buildMenu.test.ts`（W・10）
- `bridge/contract/schemas/__tests__/presentation.test.ts`（W・10）
- `bridge/contract/__tests__/blockInventoryValidators.test.ts`（W・11）
- `bridge/playtest/domQueryResponder.test.ts`（W・11）
- `bridge/transport/actions.test.ts`（W・12）
- `bridge/contract/schemas/__tests__/ui.test.ts`（W・15）
- `bridge/contract/validators.test.ts`（W・15）
- `bridge/contract/wireContract.test.ts`（W・19）
- `features/buildMenu/views/BuildMenuCategoryList.test.ts`（C・2）
- `features/buildMenu/views/BuildMenuDetailSidebar.test.ts`（C・3）
- `features/buildMenu/views/BuildMenuSlot.test.ts`（C・4）
- `features/buildMenu/hooks/useBuildMenuCategoryScroll.test.ts`（C・13）
- `features/buildMenu/sessionState/buildMenuSessionState.test.ts`（L・4）
- `features/buildMenu/logic/buildMenuScrollSpy.test.ts`（L・12）
- `features/buildMenu/logic/buildMenuGrouping.test.ts`（L・14）
- `features/challenge/challengeLogic.test.ts`（L・2）
- `features/commonHud/alwaysOnHudWheel.test.ts`（S・3）
- `features/eventLanguageGate/useLanguageSelectionAnswer.test.ts`（C・1）
- `features/eventLanguageGate/EventLanguageGate.test.ts`（C・8）
- `features/hotbar/useHotbarDragSource.test.ts`（C・7）
- `features/hotbar/hotbarDnd.test.ts`（L・3）
- `shared/placementTarget/selectableTarget.test.ts`（L・9）
- `shared/i18n/provider/dictionaryGeneration.test.ts`（I・1）
- `shared/i18n/diagnostics/translationWarnings.test.ts`（I・3）
- `shared/i18n/noJsxVisibleLiteral.test.ts`（I・5）
- `shared/i18n/provider/dictionaryRetry.test.ts`（I・6）
- `shared/i18n/itemName/useItemName.test.ts`（I・8）
- `shared/i18n/useI18n.test.ts`（I・14）
- `shared/i18n/contentKeys.test.ts`（S・3）
- `shared/i18n/allScreensI18n.test.ts`（S・6）
- `shared/i18n/localizationKeysFreshness.test.ts`（S・10）
- `features/inventory/InventoryPanel/index.test.ts`（C・2）
- `features/inventory/EquipmentPanel/index.test.ts`（C・14）
- `features/inventory/splitDrag.test.ts`（L・1）
- `features/inventory/blockSlotRestriction.test.ts`（L・7）
- `features/inventory/slotActions.test.ts`（L・8）
- `features/inventory/EquipmentPanel/equipmentLogic.test.ts`（L・9）
- `features/keyHint/KeyHintHud.test.ts`（C・4）
- `features/modal/modalLogic.test.ts`（L・8）
- `features/modeHud/PlacementModeHud.localization.test.ts`（I・6）
- `features/modeHud/modeHudDesign.test.ts`（S・5）
- `features/notification/notificationStore.test.ts`（L・11）
- `features/notification/notificationMessages.test.ts`（L・15）
- `features/notification/notificationLayering.test.ts`（S・3）
- `features/notification/notificationServerIdCoverage.test.ts`（S・5）
- `features/notification/notificationAnimationDesign.test.ts`（S・7）
- `features/pauseMenu/BugReportFormTests/hostSubmissionLock.test.ts`（C・1）
- `features/pauseMenu/BugReportFormTests/productionSendingLabels.test.ts`（C・1）
- `features/pauseMenu/BugReportFormTests/deliveryStatus.test.ts`（C・3）
- `features/pauseMenu/BugReportFormTests/reportKindLabels.test.ts`（C・5）
- `features/pauseMenu/PauseMenuPanel.test.ts`（C・5）
- `features/pauseMenu/BugReportFormTests/submissionResults.test.ts`（C・6）
- `features/pauseMenu/BugReportFormTests/inputAndAvailability.test.ts`（C・8）
- `features/pauseMenu/BugReportFormTests/recoveryControls.test.ts`（C・8）
- `features/recipe/views/MachineRecipeEntry.fluids.test.ts`（C・1）
- `features/recipe/panels/useDragScroll.test.ts`（C・2）
- `features/recipe/views/RecipeContent.test.ts`（C・2）
- `features/recipe/views/CraftRecipeEntry.test.ts`（C・3）
- `features/recipe/views/MachineRecipeEntry.localization.test.ts`（I・1）
- `features/recipe/logic/levelInheritanceBadge.test.ts`（L・3）
- `features/recipe/logic/recipeSlotLayout.test.ts`（L・3）
- `features/recipe/logic/craftLogic.test.ts`（L・9）
- `features/recipe/logic/holdCraftLogic.test.ts`（L・9）
- `features/research/ResearchNodeCard.test.ts`（C・1）
- `features/research/ResearchTreePanel.test.ts`（C・2）
- `shared/treeView/TreeView.test.ts`（C・2）
- `shared/treeView/TreeViewNodeGesture.test.ts`（C・7）
- `shared/treeView/TreeViewViewportState.test.ts`（C・8）
- `features/research/ResearchDetailPane.test.ts`（C・11）
- `features/research/researchAllocation.test.ts`（L・1）
- `shared/treeView/treeGeometry.test.ts`（L・3）
- `shared/treeView/viewport/viewport.test.ts`（L・9）
- `features/research/researchLogic.test.ts`（L・21）
- `features/settings/LanguageSelect.test.ts`（C・2）
- `features/settings/SprintModeSelect.test.ts`（C・6）
- `features/settings/GraphicsQualitySettings.test.ts`（C・7）
- `shared/ui/IconButton/index.test.ts`（C・2）
- `shared/ui/FluidSlot/FluidSlot.icon.test.ts`（C・4）
- `shared/ui/ModeSwitch/index.test.ts`（C・6）
- `shared/tooltip/CursorTooltip.test.ts`（C・7）
- `shared/ui/FluidSlot/FluidSlot.localization.test.ts`（I・3）
- `shared/ui/GaugeBar/index.test.ts`（L・0）
- `shared/tooltip/tooltipPosition.test.ts`（L・1）
- `shared/ui/BarSeries/index.test.ts`（L・2）
- `shared/ui/SlotFrame/index.test.ts`（L・2）
- `shared/ui/useSlotMouse.test.ts`（L・2）
- `shared/ui/BlockSlot/index.test.ts`（L・3）
- `shared/ui/FluidSlot/fluidLogic.test.ts`（L・3）
- `shared/clamp01.test.ts`（L・4）
- `shared/pointerGesture/dragThreshold.test.ts`（L・5）
- `shared/ui/ProgressArrowGlyph/index.test.ts`（L・5）
- `shared/ui/FluidAmountSlot/index.test.ts`（L・6）
- `shared/itemMove/planDirectMoves.test.ts`（L・7）
- `shared/ui/slotAmountFormat.test.ts`（L・7）
- `shared/ownedCounts.test.ts`（L・9）
- `shared/itemMove/blockSlotPlan.test.ts`（L・10）
- `shared/itemMove/playerSlotPlan.test.ts`（L・16）
- `shared/ui/iconEndpointArchitecture.test.ts`（S・1）
- `shared/ui/GamePanel/hudVariantDesign.test.ts`（S・3）
- `shared/ui/ItemSlot/index.test.ts`（S・14）
- `features/skit/interaction.test.ts`（L・5）
- `features/toast/toastStore.test.ts`（L・2）
- `features/blockInventory/train/TrainInventoryBody.test.tsx`（C・1）
- `features/blockInventory/train/TrainTimetableSection.test.ts`（C・5）
- `features/blockInventory/train/TrainTimetableImmediate.test.ts`（C・8）
- `features/blockInventory/train/timetableEditLogic.test.ts`（L・6）
- `features/tutorial/KeyControlHintHud.test.ts`（C・4）
- `features/tutorial/overlay/TutorialOverlay.test.ts`（C・17）
- `shared/tutorialAnchor/tutorialAnchor.test.ts`（L・3）
- `shared/tutorialAnchor/resolveAnchor.test.ts`（L・4）
- `shared/tutorialAnchor/anchorIds.test.ts`（S・4）
- `features/tutorial/tutorialAttentionDesign.test.ts`（S・21）
- `shared/uiState/inputExclusivity/useWebInputExclusivity.test.ts`（C・6）
- `shared/uiState/uiScreenRouting.test.ts`（L・12）
- `shared/uiState/activeLayer.test.ts`（L・17）

