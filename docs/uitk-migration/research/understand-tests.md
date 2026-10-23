# UI Toolkit 移行の調査: テスト・アンカー・録画検証

- 調査日: 2026-10-22。読むだけで調べた（Unity Editor・uloop・PlayMode には触れていない）
- 基準: moorestech `e8e611f1c0`（master）、moorestech_master `0d129c8`（`.moorestech-external-revisions.json` のピンと同じ）
- 件数の数え方: テストケース数は、ソースを正規表現で数えた値（`it(`／`test(`／`[Test]`／`[UnityTest]`／`[TestCase]`）。`it.each` やループで展開されるケースは1件と数えている。議論記録にある 10/17 時点の件数（vitest 139ファイル・809件、Playwright 52ファイル・211件）はテストランナーでの実測値で、本書の件数とは一致しない
- 「推測」と書いたものはまだ実測していない。それ以外はファイルの記述から確認した

---

## 0. 要点

1. **Web のテスト資産はおよそ 1,078 件で、約 7 割は UI Toolkit 版の受入条件やロジックテストに読み替えられる。** 種別ごとの内訳は次のとおり。
   - ロジック 311 件: C# へそのまま移植できる
   - 画面フロー 213 件: 受入条件の正本になる。テスト名が日本語の挙動文で書かれている
   - コンポーネント 235 件: UITK のビューテストへ移せる
   - 通信契約 167 件: CEF の撤去と一緒に捨てる
   - 静的規約 91 件: 意図だけを USS／UXML の規則として移す
   - 多言語 61 件: 約 4 割を残す
2. **Unity 6000.3.8f1 には UI Test Framework 1.0.0（`com.unity.ui.test-framework`）が組み込まれている。** これを使えば、EditMode（PlayMode 無し）で VisualElement ツリーを組み、クリック・ダブルクリック・ドラッグ・ホイール・キー入力・文字入力をシミュレートし、フレームとシミュレーション時間を進めて検証できる。モックホスト（mock-host）の役は「偽のモデルと、発行コマンドを記録する受け口」が引き継ぐ。ただし manifest にはまだ入っていない。
3. **C# 側の WebUi 系テスト 285 件のうち、154 件は残る。** 内訳はビューモデル構築 90・アクション 36・状態ストア 28。残り 131 件は通信路（wire）の契約で、ほぼ捨てる。EditModeInPlayingTest は 35 ファイル中 7 ファイルが Web の hub か Kestrel に依存している。
4. **プレイテスト DSL の Web 依存はおよそ 670 行（5 ファイル）。** シナリオは 90 本中 27 本・約 143 か所が Web 要素を指しており、testid は約 45 種類ある。
   - UITK 版では、testid を VisualElement の `name` に移す
   - `panel.Pick` と `worldBound` から画面座標を求め、既存の SemanticInput で注入する
   - MainGame.unity には InputSystemUIInputModule がある（GUID から判断）。そのため CEF への入力転送（CefInputForwarder）と、WebSocket 往復での DOM 問い合わせは要らなくなる見込み（推測）
5. **チュートリアルのアンカー ID は、いまの文字列を変えずに使い続けられる。**
   - マスタにアンカー ID が書かれているのは v8 では 9 か所だけ（`challenge.current-hud`・`recipe.craft-button` が 1 件ずつ、`research.node-<guid>` が 7 件）。旧 mod_3 にも 1 件ある
   - ID を維持すれば、マスタの移行は要らない
   - 2026-08-19 の裁定「Web アンカー ID を直書きし、変換も検証も持たない」とも矛盾しない
6. **移行中は Web と UITK の両方のアンカーを解決できるようにする必要がある（画面ごとに順に移す strangler 方式の場合）。** 理由と注意点は次のとおり。
   - DSL は、どちらの描画系にある要素でも押せないと、移行途中のシナリオが動かない
   - チュートリアルの表示内容を持つストア（TutorialPresentationStateStore）は描画系に依存しないので、Web と UITK がそれぞれ自分の木の中でアンカーを探せば自然に両対応になる
   - ただし、描画系をまたぐドラッグ＆ドロップと uiDragGuide は成立しない。DnD でつながる画面群は同じ段階で移す必要がある
7. **計画に入れるべき見落としが 2 点ある。**
   - 遠隔実行（ADR 0072）の待ち受けが Web UI の Kestrel に相乗りしている。そのため完了の定義にある「Kestrel 削除」をすると遠隔実行も壊れる
   - UITK の画面を撮れるのは ScreenCapture だけで、uloop screenshot の rendering モードには写らない（PoC での実測）。録画（Unity Recorder の GameView 入力）に写るかはまだ実測していない

---

## 1. 検証資産の全体量

| 資産 | 量 | 実行環境 | CI | 根拠 |
|---|---|---|---|---|
| vitest | 147 ファイル・約 865 件 | node（DOM 無し。一部は react-test-renderer を使う） | `run_test.yml` の `webui-test` ジョブで実行 | `moorestech_web/webui/vitest.config.ts`、`.github/workflows/run_test.yml:27-54` |
| Playwright | 53 ファイル・約 213 件 | 本番ビルドと mock-host（WebSocket の偽ホスト）を相手に、直列・ワーカー 1 本で実行 | **CI では型検査（tsc）だけで、実行はローカルのみ** | `e2e/playwright.config.ts`、`run_test.yml:53` |
| mock-host とテスト補助 | 約 3,300 行（`e2e/mock-host`・`support`・`fixtures`） | Playwright から使う | － | `e2e/mock-host/*.ts`、`e2e/support/actions.ts` |
| C# の WebUi／WebUiHost テスト | 62 ファイル・285 件 | Unity EditMode | 実行（client 系シャード） | `moorestech_client/Assets/Scripts/Client.Tests/WebUi/`、`.../WebUiHost/` |
| Web に依存する EditModeInPlayingTest | 35 ファイル中 7 ファイル（ユーティリティ 2 本を含む） | EditMode から PlayMode へ遷移して実行 | 実行 | `Client.Tests/EditModeInPlayingTest/` |
| その他の Web を参照する C# テスト | 約 12 ファイル（WebUiInputExclusivity・WebUiHost の起動順など） | EditMode | 実行 | `Client.Tests/Player/TextInputMovementLockTest.cs` ほか |
| プレイテスト DSL の Web 操作部 | 5 ファイル・約 670 行 | PlayMode（録画あり） | － | `Client.Playtest/WebUi/*.cs`、`Operations/PlaytestWebUiOps.cs`、`Operations/Ui/PlaytestBuildMenuOps.cs` |
| 録画シナリオ | 90 本中 27 本が Web 操作を使用（呼び出し約 143 か所、testid 約 45 種類） | PlayMode | － | `.agents/skills/unity-playmode-recorded-playtest/scenarios/**` |
| webui 側のテスト用 ID | `data-testid` 156 か所（94 ファイル）、Playwright の `getByTestId` 518 回 | － | － | `moorestech_web/webui/src/**` |
| チュートリアルのアンカー | 静的 ID 19 個・動的 prefix 6 種、付与 25 か所（19 コンポーネント） | － | － | `src/shared/tutorialAnchor/anchorIds.ts` |
| マスタ中のアンカー ID | v8 に 9 か所、旧 mod_3 に 1 か所 | － | － | 8 章 |

補足: Playwright の書き方の内訳は次のとおり。座標を取る `boundingBox` 89 回、CSS を読む `toHaveCSS` と `getComputedStyle` 計 106 回、`data-*` 属性の確認 28 回、送信アクションの照合 `payloadsOf` 92 回、UI 状態の切り替え `setUiState` 178 回。テストは「UI 状態を与える → 操作する → 送信されたアクションと表示を照合する」という形がほとんどである。

---

## 2. Web テストの機能別集計

分類はパスとファイル名の規則で機械的に付けたため、誤差がある。分類スクリプトとファイル単位の結果は調査時の一時ファイルで、リポジトリには入れていない。

種別の定義:
- **P 画面フロー**: Playwright のテスト。実ブラウザで操作し、送信アクション・表示・レイアウトを照合する
- **C コンポーネント**: react-test-renderer で部品の挙動を見る vitest
- **L ロジック**: 純粋関数やストアの vitest
- **I 多言語**: 辞書の取得・反映
- **S 静的規約**: CSS や TSX のソースを grep する設計規約テスト
- **W 通信契約**: bridge・transport・zod スキーマ・mock-host

| 機能 | P | C | L | I | S | W | 計 |
|---|---|---|---|---|---|---|---|
| 通信・契約（bridge） | 3(1) | - | - | - | - | 167(26) | 170 |
| ブロック UI（機械・ベルト・歯車・油井ほか） | 42(12) | 27(8) | 45(7) | 3(2) | 16(1) | - | 133 |
| インベントリ・装備 | 20(3) | 16(2) | 57(7) | - | - | - | 93 |
| 共通部品（スロット・ツールチップほか） | 2(1) | 48(12) | 20(5) | 3(1) | 4(2) | - | 77 |
| 研究ツリー | 18(3) | 21(5) | 37(4) | - | - | - | 76 |
| ビルドメニュー | 19(2) | 22(4) | 30(3) | - | - | - | 71 |
| ポーズ・バグ報告・設定 | 7(3) | 43(9) | - | 11(3) | - | - | 61 |
| チュートリアル | 7(2) | 28(4) | 4(1) | - | 21(1) | - | 60 |
| i18n | 2(1) | - | - | 37(5) | 19(3) | - | 58 |
| HUD（モード・進捗・ピン・キーヒント） | 25(5) | 4(1) | 12(2) | 6(1) | 5(1) | - | 52 |
| レシピ・クラフト | 18(4) | 10(4) | 19(3) | 1(1) | - | - | 48 |
| 通知・トースト | 6(4) | 3(1) | 25(3) | - | 12(2) | - | 46 |
| 画面遷移・入力層 | 12(3) | - | 32(2) | - | - | - | 44 |
| 列車（時刻表ほか） | 6(3) | 6(2) | 14(2) | - | - | - | 26 |
| ホットバー | 6(1) | 7(1) | 3(1) | - | - | - | 16 |
| アプリ骨格・デザイン規約 | - | - | - | - | 14(3) | - | 14 |
| チャレンジ | 11(2) | - | - | - | - | - | 11 |
| モーダル | 3(1) | - | 8(1) | - | - | - | 11 |
| スキット | 6(2) | - | 5(1) | - | - | - | 11 |
| **計** | **213** | **235** | **311** | **61** | **91** | **167** | **1,078** |

表の値は「ケース数（ファイル数）」。

### 2.1 種別ごとの読み替え可否

残す件数はテスト名から人手で見積もった値（推定）。

| 種別 | 件数 | 受入条件として読み替えられるもの | 捨てるもの | UITK での行き先 |
|---|---|---|---|---|
| P 画面フロー | 213 | 約 205 件。うち約 52 件はタイトルからみてレイアウトや見た目の主張（位置・幅・層の順・クリップ・解像度）で、レイアウトの照合か目視確認に回す | 約 8 件。接続と再接続 3（`regression/connection.spec.ts`）、`research/tmp-icon-click.spec.ts` 1、テスト補助 expectCraftGrip 自身のテスト 2、辞書の再取得 1、Mantine 固有の見た目 1 | 画面ごとの受入表と、UITestFixture を使うビューテスト |
| C コンポーネント | 235 | 約 200 件 | WebSocket の非同期 echo や楽観更新など、hook の内部に依存するもの（例: `EquipmentPanel/index.test.ts` の「楽観topicと古いechoが来ても…」） | UITestFixture のビューテスト |
| L ロジック | 311 | 約 290 件。手元のスロット計画・長押しクラフト・ビルドメニューの分類とスクロール追従・研究ロジック・ビューポート計算・ツールチップ位置・通知ストア・画面のルーティング表など | 約 10 件。DOM 要素の判定（`activeLayer.test.ts` の isTextInputElement）やブラウザ既定動作の抑止 | C# の純ロジックに移植し、NUnit で検証（パネル不要） |
| I 多言語 | 61 | 約 25 件（言語の切り替えが全画面に反映される、表示用の文字列にリテラルを書かない、など） | 約 36 件（HTTP での辞書取得・再試行・世代管理・React hook） | C# のローカライズ（Client.Localization）のテストと、UXML の静的検査 |
| S 静的規約 | 91 | 約 35 件の意図（色と周期のトークンを正本は 1 か所にする、機能側に色の値を直書きしない、層の順序） | 約 56 件（Mantine の禁止、CSS Modules のハッシュ、TS の import 構造） | USS／UXML を読む静的テスト |
| W 通信契約 | 167 | 0 件 | すべて | CEF の撤去と一緒に削除する |

---

## 3. 受入条件へ読み替える具体例

どれも「Web で何を検証していたか」を「UITK 版の受入条件」と「検証する層」に置き換えたもの。UITK 側の操作は、UI Test Framework の `PanelSimulator` の API で書いている（5 章）。

| # | 由来（ファイル: テスト名） | UITK 版の受入条件 | 検証層 | 注意 |
|---|---|---|---|---|
| 1 | `e2e/tests/hotbar/hotbar.spec.ts`: 「ビルドメニューエントリを空き枠へドラッグするとhotbar.assign{slot,id}を送る」「枠外へドラッグするとhotbar.clear{slot}を送る」、`hotbarDnd.test.ts`・`dragThreshold.test.ts` | ビルドメニューのエントリを 5px を超えて動かしてホットバーの枠 1 へ落とすと、`AssignHotbar(1, entryId)` が 1 回だけ発行され、`SelectBuildMenu` は発行されない。枠の外へ落とすと `ClearHotbar(slot)` が発行される | ビューテスト（`simulate.DragAndDrop`） | **ビルドメニューとホットバーの両方が UITK にならないと成立しない**（6.4 節） |
| 2 | `e2e/tests/inventory/inventory.spec.ts`: 「grab 中の右ドラッグで通過スロットへ1個ずつ配置する」、`playerSlotPlan.test.ts` 13 件 | アイテムを掴んだまま右ボタンを押してスロット 3→4 を通過すると、`Move(grab→main3, 1)` と `Move(grab→main4, 1)` がこの順で発行される。空手で右クリックすると `Split` が発行される | 判定の計画（どのスロット操作にするか）は L0、経路は L1 | `MouseMove(from,to)` は途中のスロットへの PointerEnter を通らない可能性がある（推測）。Playwright と同じくスロットごとに移動させる |
| 3 | `e2e/tests/recipe/recipe.spec.ts`: 「アイテム選択でレシピ表示、長押しで素材が尽きるまで連続クラフトする」「押下後にボタンから外れるとクラフトが止まり経過時間がリセットされる」、`holdCraftLogic.test.ts` 9 件 | クラフトボタンを押したまま `craftTime` だけ時間を進めると `Craft` が 1 回発行され、続けて押し続けると素材が尽きるまで繰り返す。ポインタがボタンの外へ出ると止まり、進捗が 0 に戻る。1 回の時間進行で発火するのは最大 1 回 | L0 と、L1（`simulate.FrameUpdateMs`） | 長押しのタイマーをパネルのスケジューラか差し込んだ時計で駆動しないと、EditMode では時間を進められない（5.3 節） |
| 4 | `e2e/tests/inventory/equipment.spec.ts`: 「小さいホイール入力を累積し閾値を越えた時だけ切り替える」「装備枠のクリックは選択ではなくアイテム移動になる」「持ち物画面の一覧スクロールでは装備が持ち替わらない」 | 閾値に満たないホイール入力を N 回送っても、累積が閾値を超えたときだけ `SelectEquipment(next)` が発行される。装備枠のクリックでは `Move` が発行される。持ち物画面の ScrollView の中でホイールを回しても装備は変わらない | L1（`simulate.ScrollWheel`） | ホイールを UI とゲームのどちらが受け取るかの排他は、UITK ではピッキングとイベント伝播の設計次第で変わる（6.3 節） |
| 5 | `e2e/tests/regression/buildMenu.spec.ts`: 「閉じて開き直すと検索・スクロール・詳細stickyが復元される」、`buildMenuSessionState.test.ts` | 検索欄に入力し、スクロールし、エントリにホバーしてから UI 状態を GameScreen にして BuildMenu に戻すと、検索文字列・`scrollOffset`・詳細サイドバーの内容が元のまま残る | L1 | セッション状態はビューの外（C# のセッションストア）に置く。ビューを開くたびに作り直す設計でも成り立たせるため |
| 6 | `e2e/tests/uiState.spec.ts`: 「許可表で拒否されたui_state.requestはtransition_not_allowedを返し画面を維持する」、`uiScreenRouting.test.ts` 15 件 | 許可表に無い遷移要求は拒否され、画面は変わらない。UI 状態ごとに、どの画面・常駐 HUD・背景の暗幕を出すかは表のとおりに決まる | L0（C# の UIState 方針とルーティング表） | Web の `screenForUiState` の表は、そのまま C# の表になる |
| 7 | `e2e/tests/recipe/itemListScroll.spec.ts`: 「アンカーがスクロールで隠れ始めたらラベルを引っ込め、完全に隠れたら枠も消す」、`system/tutorialHighlightClip.spec.ts` | アイテム一覧をスクロールしてアンカーの一部がクリップされるとハイライトのラベルが消え、完全に隠れると枠も消える。枠の見えている矩形は、アンカーの矩形と、クリップしている祖先との交差に一致する | L1（レイアウトをもとに判定する） | 2026-08-20 の裁定「実 UI の経路 × 正解を計算する判定役（オラクル）で 1 本」の UITK 版になる（7.5 節） |
| 8 | `e2e/tests/regression/inputLayer.spec.ts`: 「ボタンにフォーカスしてSpaceを押してもclickせずキーハンドラにも届かない」「Tabはブラウザのフォーカスを動かさない」、`.decisions/2026-10-01-Web UI全体で文字入力欄以外のSpace既定動作を封じる.md` | ゲーム画面のボタンにフォーカスがあっても、Space や Enter でボタンが押されず、Tab でフォーカスが移らない。文字入力欄の中だけは Space で空白が入る | L1（`simulate.KeyPress(KeyCode.Space)`・`TabKeyPress`） | **Web 固有の仕様に見えて、UITK では別の形で必要になる。** UITK は Space と Enter をフォーカス中のボタンへの NavigationSubmitEvent に変換し、Tab をフォーカス移動に使う（推測。要確認） |
| 9 | `e2e/tests/notification/animation.spec.ts`: 「通知は左からのスライドとフェードで入場し生存尺の終端で退場する」、`notificationStore.test.ts` | 通知は入場から生存時間が過ぎるまで表示され、終端で退場アニメーションを経て木から外れる。生存時間の正本はストアにある | L0（ストア）と、L1（`FrameUpdateMs` で時間を区切り、`resolvedStyle` の opacity と translate を見る） | `experimental.animation` がシミュレーション時間で進むかは未確認（推測） |
| 10 | `e2e/tests/hotbar/hotbar.spec.ts`: 「ホットバー9枠が常時表示され、fixtureのselectedSlotがハイライトされる」 | 枠 `hotbar-slot-0..8` が 9 個あり、選択中の枠だけに選択状態の USS クラスが付く | L1 | Web の `data-selected` などの属性 28 か所は、USS のクラスに置き換える |

**受入表の作り方の提案:** Playwright のテスト名 213 件と、vitest の `describe`／`it` の名前は、ほぼすべて日本語の挙動文になっている。これを機械的に抜き出し、「前提（モデルの状態）・操作・期待（発行コマンドと表示）・由来（ファイル: テスト名）」の 4 列で、画面ごとの受入表を作るのがよい。

前例がある。uGUI から Web へ移行したときにも、Phase D で「画面ごとの操作パリティ受け入れ表」と「PlayMode 全画面遷移スモーク」を最終検証にしていた（`docs/webui/plans/phase-d-cutover.md:33-37`）。

---

## 4. Unity 側の C# テストの扱い

### 4.1 `Client.Tests/WebUi`・`WebUiHost`（62 ファイル・285 件）

| 種別 | 件数 | 代表 | 扱い |
|---|---|---|---|
| W 通信路の契約（topic のフィクスチャ一致・JSON・ポート・Vite・静的配信・エンドポイント・topic の配信と発行） | 131（34 ファイル） | `WireContractTest`、`WireContractC2Test`、`GraphicsQualityContractTest`(25)、`SprintInputModeContractTest`(8)、`Ports/*`、`Static/WebUiArtifactValidatorTest` | ほぼ捨てる。例外として、設定系（GraphicsQuality と Sprint の計 33 件）は「プリセットを適用し、1 項目を変えたらカスタムになる」など設定ロジックの仕様を含む。この部分（推定 10〜15 件）は設定サービスのテストへ移す |
| D ビューモデルの構築（DTO を組み立てる処理） | 90（15 ファイル） | `InventoryAreaMapperTest`(28)、`TrainTimetableStopParserTest`(13)、`BlockDetail/*DtoBuilderTest`(17)、`BuildMenuEntryDtoFactoryTest`、`ResearchNodeDtoFactoryTest` | 残す。出力が wire 用の DTO から UITK のビューモデルに変わるだけで、何を照合しているかは同じ |
| A アクション（コマンド） | 36（6 ファイル） | `CollectActionTest`(17)、`CraftActionTest`、`MachineRecipeSelectActionTest`、`SplitGrabEquipmentActionTest`、`BuildMenuActionsTest` | 残す。JObject の解析をやめ、型付きのコマンド呼び出しに替える |
| S 状態ストア | 28（5 ファイル） | `TutorialPresentationStateStoreTest`(11)、`SkitPresentationStateStoreTest`(7)、`WorldPinStateStoreTest`、`WorldPinScreenProjectionTest` | そのまま残す。ストアは `Client.Game` にあり、描画系に依存しない |

### 4.2 Web に依存する EditModeInPlayingTest

| ファイル | 何に依存しているか | UITK 版での扱い |
|---|---|---|
| `BlockInventory/ElectricToGearOutputModeActionTest.cs`、`MachineRecipeSelectRoundTripTest.cs` | `WebUiActionInvoker` が hub から登録済みのハンドラを引いて実行する（`WebUiActionInvoker.cs:14-24`） | 呼び出し口を UITK のコマンドに差し替える。さらに、ゲームの PanelSettings に `RuntimePanelSimulator` をつなげば、実際の UITK 要素を押す結合テストにできる（推測） |
| `Boot/EventLanguageGateCursorTest.cs` | hub の topic を確認する（`StartGateTopicAssert`） | 言語選択ゲートの UITK ビューが表示されているかの確認に替える |
| `Playtest/PlaytestReportAndProgressTest.cs` と `Util/BugReportSubmitUtil.cs` | `bug_report.submit` のハンドラ | 報告フォームのコマンドに差し替える |
| `RemoteExec/RemoteExecEndpointTest.cs` | **WebUiHost の `KestrelServer` を直接起動している** | 遠隔実行の待ち受けを Kestrel から切り離す作業が先に要る（9 章） |
| `BlockInventory/WebUiActionInvoker.cs` | 補助クラス | 削除する |

### 4.3 そのほかに Web を参照している C# テスト

- `Player/TextInputMovementLockTest.cs`・`Input/PlayableInteractInputTest.cs`・`UIState/DragDelete/Service/DeleteInputFixture.cs` は `WebUiInputExclusivity` を使う。今は Web が「ポインタが UI の上にあるか」「文字入力欄にフォーカスがあるか」を WebSocket で通知している（`Client.Input/WebUiInputExclusivity.cs`）。UITK 版ではこれを C# 内で判定する。ポインタはパネルのピッキングで、文字入力はフォーカス中の要素が TextField かで判定し、同じ ReactiveProperty へ流す。テストは値の出どころを差し替えるだけで残せる
- `Playtest/PlaytestBootUnityCallbackBindingTest.cs:52-62` は「WebUi の起動後に、実際のポートで遠隔実行を有効にする」という呼び出し順を検査している。遠隔実行の待ち受けをどうするかに合わせて書き直す
- `UnitTest/Tutorial/*` の約 26 件は描画系に依存しないので、そのまま残る。`KeyControlUiStateVocabularyTest` も含む

---

## 5. UITK を EditMode（PlayMode 無し）で検証する設計案

### 5.1 使える基盤: UI Test Framework 1.0.0（組み込み済み）

- 場所: `/Applications/Unity/Hub/Editor/6000.3.8f1/Unity.app/Contents/Resources/PackageManager/BuiltInPackages/com.unity.ui.test-framework`（`package.json` は `"unity": "6000.3"`、CHANGELOG は 2025-07-09 の 1.0.0）
- **manifest（`moorestech_client/Packages/manifest.json`）にはまだ入っていない。** 導入には `com.unity.ui.test-framework` の追加と、`Client.Tests.asmdef` から `Unity.UI.TestFramework.Runtime` と `Unity.UI.TestFramework.Editor` への参照が要る
- テスト用の土台（fixture）は 3 種ある。PlayMode が不要なのは `UITestFixture` だけ（EditMode では Editor パネルを使う）
  - `UITestFixture`: EditMode ならウィンドウ無しの Editor パネルを用意する。描画はしないが、レイアウトとイベントは処理する。PlayMode なら空の UIDocument を用意する
  - `EditorWindowUITestFixture`: 実際の EditorWindow を作る
  - `RuntimeUITestFixture`: PlayMode で、UIDocument を含むシーンを読み込む
- 主な API（`Runtime/PanelSimulator.cs`）:
  - 入力: `Click`、`DoubleClick`、`MouseDown`、`MouseUp`、`MouseMove`、`DragAndDrop`（ボタンと修飾キーを指定できる）、`ScrollWheel`、`KeyDown`、`KeyUp`、`KeyPress`、`TabKeyPress`、`ReturnKeyPress`、`TypingText`、`ExecuteCommand`
  - 時間: `FrameUpdate`（既定では 1 回で 200ms 進む）、`FrameUpdateMs`、`timePerSimulatedFrameMs`
  - そのほか: 右クリックメニューやポップアップのシミュレータ（`MenuSimulator`・`PopupMenuSimulator`）
- 設定できる項目: `panelSize`（既定 500×500）、`themeStyleSheet`、`debugMode`（失敗した UI を残して見られる）。USS を文字列から作る `StylesApplicator.CreateStyleSheetFromString` もある

### 5.2 検証の層

| 層 | 中身 | 道具 | 由来（移すもの） | PlayMode |
|---|---|---|---|---|
| L0 純ロジック | スロット計画・長押しクラフト・ビルドメニューの分類・ビューポート計算・ルーティング表・通知ストア・ビューモデルの構築・コマンド | NUnit | vitest の L（311）、C# の D／A／S（154） | 不要 |
| L1 ビュー | UXML・USS・Presenter を偽モデルと記録用コマンドでつなぎ、操作をシミュレートして、発行コマンド・クラス・テキスト・レイアウトを照合する | `UITestFixture` と `PanelSimulator` | Playwright の P（約 205）、vitest の C（約 200） | 不要 |
| L2 静的規約 | USS と UXML をテキストとして読む（色トークンの正本は 1 か所、色の値の直書きを禁止、`:nth-child` を禁止、UXML の表示文字列にリテラルを書かない、UI Builder 専用の状態を持たない、アンカーと name の重複を禁止） | NUnit でファイルを読む | vitest の S（意図の約 35 件）、`noJsxVisibleLiteral` | 不要 |
| L3 結合 | 実際にゲームを起動し、UI 状態の遷移・パネルの表示・コマンドのサーバー到達を確かめる | EditModeInPlayingTest（必要なら `RuntimePanelSimulator`） | 既存の 4.2 節 | 必要（少数に絞る） |
| L4 録画プレイテスト | 実際の入力経路と見た目 | プレイテスト DSL | 既存の 27 シナリオ | 必要 |

### 5.3 ビューをテストしやすくする作法（L1 を成り立たせる条件）

- **ビューは MonoBehaviour に依存させない。** `XxxView` は VisualElement を受け取って組み立て、`XxxPresenter` が「モデル（`IObservable`・`IReadOnlyReactiveProperty`）を購読して描く」ことと「操作を型付きのコマンドインターフェースへ渡す」ことを受け持つ。UIDocument や MonoBehaviour は、本番で Presenter に依存を渡す薄い層だけにする。そうすれば、テストでは `rootVisualElement` に同じビューを組める
- **mock-host を置き換える。**
  - 画面ごとに `FakeXxxModel`（ReactiveProperty で組んだフィクスチャ）と `RecordingXxxCommands`（発行されたコマンドの記録）を用意する
  - Playwright の `setUiState` にあたるものは、偽の UI 状態の出どころにする
  - `payloadsOf` にあたるものは、型付きの記録リストにする
- **時間はパネルのスケジューラ（`schedule.Execute().Every()`）か、差し込んだ時計で駆動する。** MonoBehaviour の `Update` と `Time.deltaTime` で駆動すると、`FrameUpdateMs` で時間を進められない。長押しクラフト・通知の生存時間・ドラッグの閾値に効く。サーバーの「時間は GameUpdater のティック加算だけ」という規約は、サーバー側のロジックだけが対象なので、UI 側は対象外と読める（推測。規約の文言の解釈なので確認が要る）
- **パネルの寸法とテーマを本番に揃える。** `panelSize` を本番の参照解像度（例: 1920×1080）にし、`themeStyleSheet` に本番の暗色テーマ USS（PoC で 146 行）を渡す。文字の幅は FontAsset で決まるので、テーマで本番の SDF FontAsset を当てないと、レイアウトの照合値が本番とずれる（推測）
- **testid と同じ文字列を `name` に付ける**（6.3 節）。Playwright の `getByTestId("hotbar-slot-3")` は `rootVisualElement.Q("hotbar-slot-3")` にほぼそのまま書き換えられる

### 5.4 テストの骨格（例 1 を書いた場合のイメージ）

```csharp
// UI Test Framework の Editor パネル上でホットバーの割当 DnD を検証する例
public class HotbarAssignDragTest : UITestFixture
{
    [Test]
    public void ビルドメニューのエントリを空き枠へドラッグすると割当だけを発行する()
    {
        panelSize = new Vector2(1920, 1080);
        var commands = new RecordingHotbarCommands();
        var model = FakeHudModel.Create(BuildMenuFixture.IronChest, HotbarFixture.Empty);
        HudScreenMount.Mount(rootVisualElement, model, commands); // UXML+USS+Presenter
        simulate.FrameUpdate();

        var source = rootVisualElement.Q($"build-menu-entry-block-{BuildMenuFixture.IronChestGuid}");
        var target = rootVisualElement.Q("hotbar-slot-1");
        simulate.DragAndDrop(source.worldBound.center, target.worldBound.center);
        simulate.FrameUpdate();

        Assert.That(commands.Assigned, Is.EqualTo(new[] { (1, BuildMenuFixture.IronChestGuid) }));
        Assert.That(commands.BuildMenuSelected, Is.Empty);
    }
}
```

### 5.5 制約と罠（すべて推測。第一弾で実測する）

| 項目 | 内容 |
|---|---|
| 描画しない | Editor パネルは描画しない。そのため、Linear 色空間で半透明が明るく出る問題（PoC の実測）や、文字のにじみは L1 では捕まえられない。見た目は L4 の録画か ScreenCapture で確認する |
| `MouseMove(from,to)` | 途中の要素に PointerEnter が届かない可能性がある。通過したスロットを判定するテスト（例 2）では、1 スロットずつ移動させる |
| スクロールと座標の追従 | ScrollView のスクロールは content の translate で動くため、子要素の GeometryChangedEvent は発火しない可能性がある。アンカーの追従やツールチップの位置は、毎フレームの監視かスクロールイベントの購読で作る |
| `experimental.animation` と transition | シミュレーション時間で進むかは未確認 |
| batchmode の CI | 描画しない Editor パネルなら `-batchmode` の game-ci でも動く見込み。`docs/development/async-and-ci-pitfalls.md` の前例どおり、CI だけで落ちるケースは先に 1 本流して確かめる |
| 実行コスト | EditMode のテストは PlayMode へ遷移しないので、テストごとのドメインリロードは無い。USS と UXML の変更はアセットのインポートだけで、コンパイルは要らない。律速になるのはテストランナーの起動と、C# 変更時のコンパイル |
| CI の差分 | Playwright は今 CI で実行されていない。L1 を CI の EditMode シャードに載せれば、検証面はむしろ広がる。`run_test.yml` の `webui-test` ジョブと `unity-test-shard-filter.sh` の `needs_webui` は CEF 撤去後に消す |

---

## 6. プレイテスト DSL を UITK の要素へ付け替える

### 6.1 現状の経路

```
シナリオ: ClickWebUi("hotbar-slot-0")
 → PlaytestDomQuery.Query: hub.Publish("playtest.dom_query", {requestId, testid})      Client.Playtest/WebUi/PlaytestDomQuery.cs:53-84
 → React: document.querySelector([data-testid=...])、getBoundingClientRect、elementFromPoint   webui/src/bridge/playtest/domQueryResponder.ts:95,120
 → action "playtest.dom_query_result" で返答 → 2 回続けて同じ矩形なら安定とみなす   Operations/PlaytestWebUiOps.cs:115-140
 → CefScreenMapper（リフレクションで CefUnityBrowserSample の private フィールドを読み、RawImage の座標を逆変換）  WebUi/CefScreenMapper.cs
 → SemanticInput（InputSystem に QueueStateEvent で注入）→ CefInputForwarder が CEF へ転送   WebUi/CefInputForwarder.cs
```

起動時に `PlaytestRunner.cs:49-50` で `RegisterHandler` と `StartForwarding` を呼んでいる。

### 6.2 UITK 版の経路（案）

```
シナリオ: ClickUi("hotbar-slot-0")
 → UitkLocator: ゲームのパネル群（UIDocument.rootVisualElement）から Q(name) で探す。見つかった数が 0 なら再試行、2 以上なら失敗
 → 可視の判定: 自分と祖先の resolvedStyle の display・visibility・opacity、worldBound の面積、パネルの範囲内か
 → 被覆の判定: panel.Pick(中心) が対象自身かその子孫か                         elementFromPoint の代わり
 → パネル座標を画面座標へ変換（PanelSettings のスケールと Y 軸の反転）
 → SemanticInput で注入 → InputSystemUIInputModule → UITK のパネルが受け取る
```

- MainGame.unity には InputSystemUIInputModule がある（`Scenes/Game/MainGame.unity:8428`。GUID `01614664…` から判断）。そのため **CefInputForwarder・PlaytestDomQuery・CefScreenMapper・webui の domQueryResponder は不要になる見込み（推測）**。WebSocket の往復と「2 回続けて同じ矩形」の待ちも、同じプロセス内で同期的に読むだけになる
- パネル座標から画面座標への変換は公開 API に `ScreenToPanel` しか無いため、逆方向は自作になる（推測）。最初の probe で、クリック位置が要素の中心に当たるかを実測する

### 6.3 Web の概念と UITK の対応

| Web | UITK | 備考 |
|---|---|---|
| `data-testid`（94 ファイル・156 か所） | `name` に同じ文字列を付ける | 綴りを保てば、シナリオの 143 か所は文字列を変えずに済む。`name` の一意性は UITK が強制しないため、DSL が重複を検出して失敗させ、L2 で UXML の重複を検査する |
| `getBoundingClientRect` | `worldBound`（パネル座標） | `FrameUpdate` かレイアウトの完了後に読む |
| `elementFromPoint` | `panel.Pick(Vector2)` | ピッキングは overflow のクリップを考慮する見込み（推測） |
| `devicePixelRatio` | PanelSettings のスケールモードと参照解像度 | |
| `textContent` | 子孫の TextElement の `text` を連結する | `WaitWebUiTextContains` の置き換え |
| `data-selected` などの属性（28 回照合） | USS のクラス（例: `hotbar-slot--selected`） | |
| ARIA ロールとアクセシブル名（`getByRole` 67 回・`getByText` 30 回） | 型（`Q<Button>`）と表示文字列 | UITK にはロールが無い |
| ホイール | `ScrollWheel`（L1）、`SemanticInput.MouseWheel`（L4） | |
| `WebUiInputExclusivity.IsPointerOverWebUi`（Web から通知） | C# 内でピッキングして判定する | `PlaytestUiOps.cs:138` が待ちに使っている |

### 6.4 移行中に両方を解決できるようにする必要はあるか

- **画面ごとに順に移す（strangler）なら必要。** 一度に全画面を切り替える（big-bang）なら不要。
- 理由: シナリオ 27 本は、ビルドメニュー・ポーズメニュー・設定・バグ報告・列車の時刻表・言語選択・インベントリの整理をまたいで使う。移行の途中でも master のシナリオが通る必要がある。
- 設計案:
  1. `ClickWebUi`・`HoverWebUi`・`UntilWebUiElement`・`WaitWebUiTextContains`・`ScrollDownUntilWebUiElementIsClickable` の中身を、描画系に依存しない `IUiElementLocator` に差し替える。実装は Web 版と UITK 版の 2 本にし、UITK を先に探してから Web を探す。**同じ ID が両方で見つかったら、二重描画の不具合として理由をログに出して失敗させる**（黙って先勝ちにしない）
  2. 名前は `ClickUi` などへ一度に置換する。後方互換は考えなくてよい方針（AGENTS.md）なので、旧名は残さない。シナリオ 27 本とスキル文書 4 本（`references/write-scenario.md`・`input-injection.md`・`place-blocks-via-ui.md`・`run-scenario.md`）も同じ PR で直す
  3. CEF を撤去するときに Web 版の実装を消す
- **描画系をまたぐドラッグ＆ドロップは成立しない。** CEF のテクスチャと UITK のパネルは別々のポインタ捕捉を持つため（推測）。DnD でつながる画面群は、同じ段階で移す必要がある。
  - ビルドメニューのエントリ → ホットバー（`hotbar.assign`）
  - 持ち物 ↔ 装備 ↔ ブロックのインベントリ（ブロック UI）（掴みのオーバーレイが共通）
  - ホットバーの枠 → 枠外（`hotbar.clear`）
  - したがって「インベントリだけ」「ホットバーだけ」を第一弾にすると、その画面の P テストの一部（例 1、`blockInventoryGestures.spec.ts` など）が移行中の受入条件として書けない

### 6.5 録画と見た目の確認

- 録画は Unity Recorder の `GameViewInputSettings` を使う（`Recording/PlaytestRecorder.cs:33`）。CEF は uGUI の RawImage に描かれるので、今は録画に写っている。UITK のオーバーレイパネルが GameView の録画に写るかは **未実測**。ScreenCapture 系と同じ最終フレームを撮るなら写る見込み（推測）。第一弾の最初に 1 本録画して確かめる
- PoC で実測した制約: `uloop screenshot` の rendering モードには UITK が写らず、`ScreenCapture.CaptureScreenshot` なら写る。DSL の `PlaytestScreenshot`（`Operations/PlaytestScreenshot.cs:21`）は ScreenCapture を使っているので問題ない。議論記録にある「見た目は uloop screenshot で agent が自己確認する」という作法は、**rendering モードでは成立しない**。window モードか ScreenCapture を指定する必要がある
- 操作オーバーレイ（`Overlay/PlaytestOverlayView.cs`）は IMGUI なので、UITK 化の影響を受けない

---

## 7. チュートリアルのアンカー解決を付け替える

### 7.1 現状の仕組み

1. マスタの `highLightAnchorId`、`fromAnchorId`、`toAnchorId` を、Unity は変換せずに使う。例外は `itemViewHighLight` だけで、アイテム GUID から itemId を引き、`recipe.item-<itemId>` を作る（`Client.Game/InGame/Tutorial/UIHighlight/TutorialAnchorIdMapper.cs`）
2. 表示内容は `TutorialPresentationStateStore`（`Client.Game/InGame/Tutorial/Presentation/`。描画系に依存しない）が、チャレンジごとのセッションと要素（outline・dragGuide・keyControl）として持つ
3. WebUiHost がこれを topic `tutorial.presentation` で配信する
4. Web は `data-tutorial-anchor` を空白区切りのトークンとして一致させて探す。見つかった数が 0 なら missing、2 以上なら duplicate、ほかに非表示・面積 0・画面外を判定する（`resolveAnchor.ts`）。矩形は rAF でまとめて追従し、DOM から外れたときだけ同期で消す（`anchorRegistry.ts`）。祖先のクリップは CSS の包含ブロックの規則で交差させる（`ancestorClip.ts`、ADR 0024）
5. 解決すると `tutorial.anchor_ack` を返す。C# 側はセッションとリビジョンを検証するだけ（`Client.WebUiHost/Game/Actions/TutorialAnchorAckAction.cs`）

アンカーの語彙は `anchorIds.ts` が単一の正本である。静的 ID が 19 個、動的 prefix が 6 種（`research.node-`・`recipe.item-`・`build-menu.entry-`・`challenge.node-`・`inventory.item-`・`equipment.slot-`）ある。C# 側にあるのは `recipe.item-` の prefix だけで、`Client.Tests/WebUi/WireFixtures/tutorial_anchor_ids.json` と突き合わせている（`TutorialAnchorContractTest`、1 件）。

アンカーを付けている場所は 19 ファイル・25 か所。ResearchNodeCard、CommonHud、PlacementModeHud、DeleteModeWarningBands、TrainRidingHud、BlockInventoryPanel、ProgressBar、ItemListPanel、RecipeContent、HotbarPanel、PauseMenu 系 3 ファイル（6 か所）、InventoryPanel、EquipmentPanel（3 か所）、BuildMenuSlot、CurrentChallengeHud、ChallengeNodeCard、ChallengePanel（2 か所）。1 つの要素に複数の ID を付ける例もある（`equipment.slot-<i>` と `equipment.selected-slot`）。

### 7.2 UITK でアンカーを付ける方法の比較

| 方法 | 複数 ID | 検索 | 寿命の追跡 | 評価 |
|---|---|---|---|---|
| `name` | 不可（1 つだけ） | `Q(name)` | － | testid に使うので、アンカーとは分ける。anchor-convention.md の「testid とアンカーは別の契約」を守る |
| USS クラス（`AddToClassList`） | 可 | `Query(className: id)` | Attach と Detach のイベント | **推奨。** UI Toolkit Debugger で見え、AI でも人でも確かめやすい。`.` を含む ID は USS のセレクタとして解釈されないので、見た目と結びつかずに済む（むしろ好都合） |
| `userData` | 不可（オブジェクト 1 つ） | 木を総なめする | － | ListView のバインドなどと衝突しうるので使わない |
| 専用のレジストリ（`Dictionary<string, List<VisualElement>>`） | 可 | O(1) | `AttachToPanelEvent`／`DetachFromPanelEvent` で登録と解除 | クラス方式と組み合わせて、追従と重複診断に使う |

推奨する構成:
1. `anchorIds.ts` を C# の定数クラス（静的 ID と動的 prefix の生成関数）へ移し、これを唯一の正本にする。`TutorialAnchorIdMapper` もここへまとめ、`tutorial_anchor_ids.json` のフィクスチャと突き合わせのテストは消す（語彙が C# の 1 か所になるので）
2. アンカーは `TutorialAnchor.Mark(VisualElement, params string[] ids)` の 1 か所で付ける。TS の `tutorialAnchor()` ヘルパーと同じ役割で、空白を含む ID は弾く。静的なアンカーは UXML に書かず、C# でバインドするときに付ける。語彙を C# の定数だけに置くため
3. 解決役（リゾルバ）は、パネルのルートからクラスで探し、0 件なら missing、2 件以上なら duplicate とする。可視性は 6.2 節と同じ判定を使う。DetachFromPanelEvent で同期的に消す（Web の onMutation と同じ挙動。`tutorial.spec.ts`「outline never outlives its anchor」の受入条件）
4. 位置の追従は GeometryChangedEvent に加え、スクロールによる移動を拾うために毎フレーム `worldBound` を監視する（5.5 節）
5. 祖先のクリップ: UITK では `overflow: hidden` が absolute 配置の子孫も含めてクリップする見込み（推測）。そうなら、CSS のような包含ブロックからの脱出規則は要らず、「overflow を隠す祖先の `worldBound` との交差」で足りる。ただし、公開 API で祖先の overflow を読めるか（`resolvedStyle` に overflow が無い可能性）は要確認。読めなければ、クリップする容器に印のクラスを付ける
6. **仮想化 ListView の行に付けたアンカーは、画面外では存在しない。** Web では「画面外だが存在する」（outside-viewport）だった。研究ツリーのノードやビルドメニューのエントリを仮想化するなら、チュートリアルが指すアンカーの missing と outside の区別が変わる。行の Bind と Unbind でクラスを付け替えることと、missing 時の扱いの両方が設計点になる

### 7.3 裁定との整合

- `.decisions/2026-08-19-チュートリアルUI指定はWebアンカーIDを直書きし変換も検証も持たない.md` は、ID の形（`<画面>.<要素>`）を描画系から独立に定めている。**ID の文字列を変えなければ、裁定は UITK でもそのまま成り立つ。** 変換表は持たず、誤記は素通しにする
- ただし「アンカー」が「Web アンカー」でなくなるので、次の名前と文書を直す。どれもセーブ形式とは無関係
  - スキーマ `VanillaSchema/challenges.yml:280-337` の既定値の文字列 `web anchor id`
  - `docs/webui/anchor-convention.md` の置き場所
- `.decisions/2026-08-19-uiObjectIdの正本はCore.Masterに置く.md` は上書き済みで、uiObjectId という語彙はもう存在しない。移行で触る必要はない

### 7.4 移行中の二重解決

- ストアは描画系に依存しないので、同じ表示内容を Web（topic 経由）と UITK（直接購読）の両方へ流せる。それぞれが自分の木の中でアンカーを探し、見つかったものだけを描けば、両方の描画系で解決できる。共通のリゾルバは要らない
- 条件:
  - 1 つの画面は、どちらか一方の描画系だけで描くこと（UIState のルーティングで排他にする）
  - **描画系をまたぐ `uiDragGuide` は描けない。** 現在のマスタでは 0 件なので実害は無い。移行中は、未移行の画面と移行済みの画面をまたぐ dragGuide を新設しない
- 診断: 同じアンカーが両方の描画系で同時に解決したら二重描画の不具合なので、理由をログに出す。AGENTS.md の「無音の縮退は禁止」に従うもの。マスタの誤記を素通しにする裁定とは別の話である

### 7.5 2026-08-20 のテスト方針を UITK に移す

`.decisions/2026-08-20-チュートリアルハイライトのマスク方式とテスト方針.md` は「実 UI の経路 × ブラウザを判定役（オラクル）にして 1 本」と定めている。UITK 版では次のようにする。

- 実際の研究ツリーのビューを UITestFixture に組み、`ScrollWheel` でノードを境界へ動かす
- 「ハイライトの見えている矩形」が「アンカーの `worldBound` と、クリップする祖先との交差」に一致することを照合する
- 判定役（オラクル）はレイアウトで計算するので、期待値を手で書かずに済む。この方針はそのまま移せる

---

## 8. マスタデータに書かれた Web アンカー ID

moorestech_master `0d129c8` の全ファイルを `AnchorId|uiObjectId|testId|tutorialAnchor` で検索した結果。

| ファイル | 項目 | 件数 | 値 |
|---|---|---|---|
| `server_v8/mods/moorestechAlphaMod_8/master/challenges.json` | `highLightAnchorId`（uiHighLight） | 9 | `challenge.current-hud`（1）、`recipe.craft-button`（1）、`research.node-<guid>`（7: `858bcb10…`・`b47c5e3c…`・`bc5e7786…`・`0d76f2e5…`・`48f75a7e…`・`3bca3b97…`・`50372888…`） |
| 同上 | `fromAnchorId`／`toAnchorId`（uiDragGuide） | 0 | ADR 0022 にあった石器の装備誘導は、現在のマスタには無い |
| 同上 | `itemViewHighLight`（アイテム GUID。Unity が `recipe.item-<itemId>` に変換） | 4 | アンカーの文字列は書かれていない |
| 同上 | `keyControl`（`uiState` に UIStateEnum の名前と `keyName`） | 13 | アンカーではない。UIState の語彙に依存する（`KeyControlUiStateVocabularyTest` が守っている） |
| `server/mods/moorestechAlphaMod_3/master/challenges.json`（旧世代の mod） | `highLightAnchorId` | 1 | `recipe.craft-button` |
| `server_v4`〜`server_v7`、その他の JSON | － | 0 | － |

まとめ:
- マスタに直書きされた Web アンカー ID は計 10 件で、実質的に使われている v8 では 9 件
- `data-testid` の文字列はマスタに 1 件も無い。シナリオに直書きされたカテゴリ GUID（`build-menu-category-d1000000-…`）は、マスタのカテゴリ GUID であり Web 固有ではない
- **ID の文字列を UITK 側で保てば、マスタの変更は要らない**

---

## 9. 計画への提言（grill と plan の材料）

1. **仕様の正本（未裁定の論点）**
   - Playwright のテスト名 213 件と、vitest の L と C の describe 名を機械的に抜き出し、画面ごとの受入表（4 列）を作る
   - 移した画面の受入表が L0 と L1 で緑になったら、その画面の React 機能フォルダ・vitest・Playwright を一緒に消す
   - W（vitest 167 件・C# 131 件）は WebUiHost を撤去するときにまとめて消す
2. **完了の定義（未裁定の論点）に足すべきもの**
   - 遠隔実行の入口を、Web UI の Kestrel から切り離す。現状は `Client.WebUiHost/Boot/WebUiEndpoints.cs:97-101`、根拠は ADR 0072「入口は既存の Web UI サーバー（Kestrel）に1本足す」。テストは `RemoteExecEndpointTest` と `PlaytestBootUnityCallbackBindingTest`
   - CI の `webui-test` ジョブと `needs_webui` を撤去する
   - DSL の Web 実装（約 670 行）と `domQueryResponder` を撤去する
   - スキル文書 4 本を更新する
3. **第一弾の画面（未裁定の論点）**: 議論記録では「インベントリかホットバー」が挙がっている。しかし、テストのつながり（6.4 節）から見ると、単独では受入条件の一部が書けない
   - 案 A: 持ち物画面一式（持ち物・装備・レシピビューア・掴みのオーバーレイ）。テストは P 38 件・C 26 件・L 76 件で、インベントリ・装備とレシピ・クラフトの行の合計
   - 案 B: ホットバーとビルドメニュー
   - 共通基盤の部品（ツールチップ・DnD・暗色テーマ・アイコンの SpriteAsset）は、どちらでも最初に作る
4. **第一弾の最初に実測する項目（この調査ではすべて未実測）**
   1. UI Test Framework を manifest に入れ、CI の batchmode でテストが 1 本通るか
   2. 本番テーマと FontAsset を当てた Editor パネルのレイアウトが、PlayMode の実画面と一致するか
   3. `experimental.animation` と transition が `FrameUpdateMs` で進むか
   4. InputSystem に注入した入力が InputSystemUIInputModule 経由で UITK に届き、パネル座標から画面座標への変換が正しいか（DSL の probe）
   5. Unity Recorder の GameView 録画に UITK が写るか
   6. Space、Enter、Tab が NavigationSubmit とフォーカス移動に変換されるのを止められるか

---

## 付録: 主な根拠ファイル

- 裁定と議論:
  - `.decisions/2026-10-18-CEFを撤去しUI-Toolkitへ全面移行する.md`
  - `docs/webui/ui-toolkit-migration-discussion-2026-10-18.md`
  - `.decisions/2026-08-19-チュートリアルUI指定はWebアンカーIDを直書きし変換も検証も持たない.md`
  - `.decisions/2026-08-19-uiObjectIdの正本はCore.Masterに置く.md`（上書き済み）
  - `.decisions/2026-08-20-アンカー語彙にインベントリ所持スロットと装備スロットを足す.md`
  - `.decisions/2026-08-20-チュートリアルハイライトのマスク方式とテスト方針.md`
  - `.decisions/2026-10-01-Web UI全体で文字入力欄以外のSpace既定動作を封じる.md`
  - `docs/adr/0022-tutorial-label-keycontrol-anchor-vocabulary.md`、`docs/adr/0024-tutorial-highlight-ancestor-clip-mask.md`、`docs/adr/0052-ugui-removal-scope-and-exceptions.md`、`docs/adr/0072-remote-exec-in-distributed-builds.md`
  - `docs/webui/anchor-convention.md`、`docs/webui/MIGRATION.md`、`docs/webui/plans/phase-d-cutover.md`
- Web:
  - `moorestech_web/webui/vitest.config.ts`、`e2e/playwright.config.ts`、`e2e/support/actions.ts`
  - `src/shared/tutorialAnchor/{anchorIds,resolveAnchor,anchorRegistry,ancestorClip}.ts`
  - `src/bridge/playtest/domQueryResponder.ts`、`src/bridge/transport/protocol.ts`（topic 29 個）
- Unity:
  - `Client.Playtest/{PlaytestRunner.cs,PlaytestDriver.cs,WebUi/*,Operations/PlaytestWebUiOps.cs,Operations/Ui/PlaytestBuildMenuOps.cs,Recording/PlaytestRecorder.cs,Operations/PlaytestScreenshot.cs}`
  - `Client.Tests/WebUi/**`、`Client.Tests/WebUiHost/**`、`Client.Tests/EditModeInPlayingTest/**`
  - `Client.Game/InGame/Tutorial/{UIHighlight,Presentation}/*`、`Client.Input/WebUiInputExclusivity.cs`
  - `Client.WebUiHost/Game/Actions/*`（アクション 44 種）、`Client.WebUiHost/Boot/WebUiEndpoints.cs`
  - `Client.Tests/Tests.asmdef`、`Packages/manifest.json`、`Scenes/Game/MainGame.unity:8428`
- スキル: `.agents/skills/unity-playmode-recorded-playtest/{references/*.md,scenarios/**}`
- マスタ: `moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/challenges.json`、`server/mods/moorestechAlphaMod_3/master/challenges.json`、`VanillaSchema/challenges.yml:180-345`
- PoC: `docs/uitk-migration/poc/hard-cases/index.html`（2026-10-22）
- UI Test Framework: `/Applications/Unity/Hub/Editor/6000.3.8f1/Unity.app/Contents/Resources/PackageManager/BuiltInPackages/com.unity.ui.test-framework/{package.json,Documentation~,Runtime/PanelSimulator.cs}`
