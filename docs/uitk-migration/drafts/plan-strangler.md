# UI Toolkit 移行計画案: リスク先行・画面単位の段階置換（strangler）

- 作成: 2026-10-22。読むだけで作った草案で、repo・bd・git には何も書いていない。
- 位置づけ: grill（moores-grill-with-docs）に掛ける前の計画案。ここに書いた「推奨」は裁定ではない。裁定が要るものは §6 に集めた。
- 前提として読んだもの:
  - 裁定 `.decisions/2026-10-18-CEFを撤去しUI-Toolkitへ全面移行する.md`
  - 議論の記録 `docs/webui/ui-toolkit-migration-discussion-2026-10-18.md`
  - `docs/adr/0052-ugui-removal-scope-and-exceptions.md`、`docs/adr/0006`・`0013`〜`0021`・`0072`、`.decisions/2026-09-05-*`（uGUI 撤去の一連）、`.decisions/` の webui・UI 系
  - 難所 PoC の実測（`docs/uitk-migration/poc/hard-cases/index.html`、2026-10-22）
  - 調査 5 本（`docs/uitk-migration/research/understand-{screens,host,input,tests,shipping}.md`。以下 screens・host・input・tests・shipping と略す）
- 表記: パスは repo 相対。`web/` は `moorestech_web/webui/src/`、`Scripts/` は `moorestech_client/Assets/Scripts/`。「推測」と書いたものは実測していない。規模の数字はすべて推測（Web 側の行数は screens §1・§2 の実測値）。

---

## 0. 要約

1. CEF と UI Toolkit（以下 UITK）を画面単位で共存させる。どちらが描くかは C# の**所有表**（画面と面ごとに Web か UITK のどちらか一方）で決め、移した面の Web 実装は同じ PR で消す。実行時に切り替えるフラグは作らない。
2. 段階は **7 つ（P0〜P6）**。どの段階の終わりでも master は出荷でき、配布は既存の工程（staging → Windows 検証機 → 手動で playtest 反映）を通る。
3. 移す順は「共存の物理を実測 → 入力の無い HUD → 建築（D&D・ツールチップ・IME・入力の取り合い）→ ポーズとモーダル（文字入力・CEF 障害時の命綱）→ 持ち物（掴み・ブロック UI・研究）→ 残り（Web を空にする）→ 撤去」。難所を P1〜P2 で踏む。
4. 移す単位は UIState 1 つではなく「同時に出て、掴みや D&D でつながる面の集まり」（束）。持ち物の束は大きいので、所有表を「画面 × ブロック種別の族」で引き、出荷できる小段階 5 つに割る。
5. 撤去を塞ぐ 3 点を P0 で先に片付ける。遠隔実行（ADR 0072）を Kestrel から外すこと、`Client.WebUiHost` の中にある状態を DI へ移すこと、OneJS を外すこと。プレイテスト DSL の付け替えは P1 で行う。
6. 新規の UITK コードは本体で約 2 万行、テストで約 1 万行の見込み（推測。Web の本体は約 15.4k 行）。

### 段階一覧

| 段階 | 目的 | 主な対象 | 終了時に出荷できるか | 規模（推測） |
|---|---|---|---|---|
| P0 地ならし | 見た目を変えずに、撤去を塞ぐものを先に外す | OneJS、遠隔実行の待受、Web ホスト内の状態の移設、台帳・ゲート | 可（見た目は不変） | 30〜45 ファイル、新規 0.8〜1.5k 行＋移設 |
| P1 基盤と共存プローブ | 共存の物理を実測し、基盤を作り、入力の無い HUD を出す | 層・ルータ・入力・テーマ・フォント・i18n・アンカー・DSL、HUD 10 種 | 可（HUD だけ UITK） | 70〜100 ファイル、5〜7k 行 |
| P2 建築の束 | D&D・ホバーツールチップ・IME 検索・入力の取り合いを踏む | ビルドメニュー、ホットバー | 可 | 35〜50 ファイル、2.5〜3.5k 行 |
| P3 ポーズとモーダル | 文字入力・全画面モーダル・ESC を潰し、CEF 障害時の命綱を作る | ポーズ、設定、バグ報告、モーダル、トースト、言語ゲート | 可 | 25〜35 ファイル、1.5〜2.5k 行 |
| P4 持ち物の束 | 掴みでつながる面を揃え、Web の掴み系を消す | 4a 持ち物画面 → 4b〜4d ブロック UI → 4e 研究 | 小段階ごとに可 | 100〜150 ファイル、8〜11k 行 |
| P5 残りと Web の空化 | Web の描画を 0 にして 1 サイクル出荷する | チャレンジ、スキット、通知、チュートリアル枠の残り | 可（CEF は起動するが描かない） | 30〜40 ファイル、2〜2.5k 行 |
| P6 撤去 | CEF・Kestrel・Web ホスト・Web 資産・共存機構を消す | §5 の完了の定義 | PR ごとに可 | 削除が中心 |

---

## 1. 方針

### 1.1 方針の要約

- 正本（`Client.Game`・`Client.Skit`・`Client.Input`・`Client.Localization`・サーバー）には手を入れない。Web へ JSON で配っていた層を「型付きの購読元（スナップショットと変化通知）」と「JSON アダプタ」に分け、UITK のビューは前者を直接購読する（host §5.1）。操作も「型付きコマンド」と「JSON アダプタ」に分け、両系が同じ前提条件の検証を通る（host §3）。
- 共存中に入力を裁く場所は 2 か所に集める。ポインタは `UiPointerHitTest`（UITK の Pick、Web の pointerOverUi、uGUI の EventSystem の論理和）、文字入力は入力排他（UITK でフォーカス中の TextField と Web の textFocused の論理和）（host §7.3、input §3.7）。
- 画面ごとの所有者はちょうど 1 つとし、C# のテストで固定する。Web へは `ui_state.current` に「委譲」を足して伝え、Web 側のルーティングは委譲された画面では何も描かない（§1.4 R2）。
- 束を割らない。描画系をまたぐ D&D・掴み・分割ドラッグは成立しないので、つながる面は同じ段階で移す（screens §7.1、tests §6.4、input §3.7）。系をまたぐドラッグの橋は作らない。

### 1.2 この方針を選ぶ理由

- **配布環境の差を早く浴びられる。** 裁定の理由は「数万環境で UI が壊れない」ことにある。UITK の描画・入力・フォントを P1 から毎週の playtest に載せれば、環境差の不具合を規模の小さいうちに拾える。一括切替だと、約 2 万行が配布環境に一度も触れないまま最後に出る。
- **いつ止めても出荷できる。** 段階の終わりごとに、所有表のテスト・録画シナリオ・Windows 検証機の通し検証を通す。配布は staging から手動で反映する工程なので（`.decisions/2026-09-24-Steamの自動アップロード先はplaytest-stagingとしplaytest反映はWebUIで手動実行する.md`）、検証に落ちた段階はテスターへ届かない。
- **CEF が壊れた PC での非対称性が良い方へ働く。** Web ホストが起動に失敗すると UI がゼロになる（shipping §6.2、`ugui-retirement-plan.md:23-24`）。UITK へ移した面はその PC でも生き残る。P3 でポーズ・セーブ・終了・バグ報告を移せば、CEF が壊れたテスターからも報告が届く。
- **難所を早く踏める。** P1 で描画順・入力経路・録画・IME を実測し、P2 で D&D・ホバーツールチップ・検索欄 IME・ホットバーと Web 画面の同居（入力の取り合い）を踏む。持ち物の束（最大）に入る時点で、部品と共存の規則は実戦で確かめ済みになる。

### 1.3 捨てるもの

| 捨てるもの | 理由 |
|---|---|
| 一括切替（移行ブランチに溜めて最後に入れる） | 出荷できない期間が長く、配布環境の検証が最後に集中する（§6 D1） |
| 実行時の切替フラグ（設定・DI の bool） | 前例の `SkitPresentationMode.WebUiEnabled` は常に true のまま残骸になった（host §7.5 R1）。所有表は設定ではなく静的なコード表として持ち、P6 で表ごと消す |
| 系をまたぐ D&D・掴みの橋 | DOM のドラッグを UITK の要素へ落とせない（host §7.2）。橋を作るより束ごと移すほうが安い |
| 画素の一致（パリティ） | Linear 合成と小さい文字は一致しない（PoC）。前回は画素パリティが足かせになり打ち切った（`.decisions/2026-08-17-uGUI移植を終了しWeb-UIは独自調整へ移る.md`）。一致させるのは機能・トークン（色・寸法・層）・受入条件 |
| 移行中の Web への新機能追加 | 二重作業になる。Web はバグ修正だけにする（§6 D2） |
| WS の規約一式（revision・snapshot・restoring・再接続・heartbeat・zod） | 同じプロセスで同期的に購読するので要らない。zod の不変条件は C# の型（sealed 継承の判別 union）へ移す（host §5.1） |
| 通信契約のテスト（vitest 167 件・C# 131 件） | JSON アダプタと一緒に消える（tests §2.1、§4.1） |
| OneJS | 裁定で不採用。参照 0 件なのに配布物へ quickjs と 2.2MB が入る（shipping §5） |

### 1.4 共存の規則

| # | 規則 | 守り方 |
|---|---|---|
| R1 | 同じ画面状態で、同じ位置に両系の操作できる要素を置かない | 所有表で画面と面ごとに所有者を 1 つに決める。CEF は RawImage 全面で入力を受け続け、領域単位で止める API が無いため（host §1、`CefUnityBrowserSample.cs:553-590`） |
| R2 | 所有表は「(画面, 面) → Web か UITK」の静的なコード表で、どの組でも所有者はちょうど 1 つ | C# の EditMode テストで固定する。Web へは `ui_state.current` に委譲を足して伝え、Web の `screenForUiState` は委譲画面を「パネル無し・常駐 HUD 無し」の画面へ写す（`web/shared/uiState/uiScreenRouting.ts`） |
| R3 | 面を移す PR で、Web 側の該当コンポーネント・document 全域のキー処理・テストを同じ PR で消す | ゲートテストで、移した面の Web ファイルが残っていないことを見る |
| R4 | 入力の裁きは `UiPointerHitTest` と入力排他の 2 か所だけで行う。個々の画面で入力判定を持たない | 共存中はどちらも UITK と Web の論理和にする（input §3.7） |
| R5 | 正本・状態クラスは Hub と無関係に生成する | P0 で移設する（host §5.2） |
| R6 | 同じアンカー ID が両系で同時に解決したら、二重描画の不具合として理由をログに出す | 黙って先勝ちにしない（tests §7.4。AGENTS.md の無音の縮退の禁止） |
| R7 | DSL が同じ testid を両系で見つけたら失敗させる | tests §6.4 |

### 1.5 束（移す単位）の決め方

screens §7.1・host §7.2・input §3.7 の束を、実行時のつながりで見直した。コードの import だけのつながり（blockInventory が recipe の RecipeRow を使う等）は束にしない。部品として UITK 版を作れば済むため。

| 束 | 中身 | つながりの根拠 | 移す段階 |
|---|---|---|---|
| 建築 | BuildMenuPanel、HotbarPanel | ホットバーの割当はビルドメニューからの D&D だけ（`web/features/hotbar/hotbarDnd.ts`） | P2 |
| 掴み | InventoryPanel、EquipmentPanel、GrabOverlay、BlockInventoryPanel | 掴み絵・Shift 配分・分割ドラッグ・右ドラッグ配りがパネルをまたぐ（host §7.2、`web/app/App.tsx:97-131`） | P4（下記の割り方） |
| 同時表示だが掴みでつながらない | RecipeViewer・ItemListPanel（playerInventory）、ResearchTreePanel（researchTree） | 掴んだアイテムを落とす先にならない | 画面単位で掴みの束と一緒に移す（4a・4e） |
| ポーズ | PauseMenuPanel、設定、トースト | import と同一画面（screens §7.1 束 D） | P3 |
| チュートリアル | 枠・ドラッグガイド・ワールドピン | アンカーを持つ面ごとに描く系が決まる。表示内容のストアは描画系に依存しない（tests §7.4） | 両系で自分の木のアンカーを描く（P1 に UITK 側を作る）。Web 側は P5 で消す |

**掴みの束の割り方（P4）。** 所有表のキーを「画面 × ブロック種別の族」にし、画面のインスタンス単位で丸ごとどちらかが描く。

- 移行していない族の subInventory は、Web が InventoryPanel・GrabOverlay・EquipmentPanel ごと描く。UITK 側にも同じ面を作るので、P4 の間は InventoryPanel が両系に 1 つずつある（一時的な二重実装。Web 版は凍結してバグ修正だけにする）。
- 装備 HUD は画面によって所有者が変わる。GameScreen・playerInventory・buildMenu・challengeList・pauseMenu では UITK、未移行の subInventory と researchTree では Web が描く（`SCREEN_TRAITS` の alwaysOnHud は playerInventory・subInventory・buildMenu・challengeList・pauseMenu・trainHud で true。`uiScreenRouting.ts:18-31`）。
- これで系をまたぐ掴みは起きない。代わりに、P4 の途中で止めると InventoryPanel の二重実装が負債として残る（§6 D14）。

---

## 2. 段階

各段階に共通する完了条件（以下「共通条件」）:

1. 移した面の受入表（§4.1）の行が、L0/L1 テスト（§4.2）で緑になっている。CI の EditMode シャードで回る。
2. 所有表のテストが緑。移した面の所有者は UITK だけで、Web の該当ファイルは存在しない（R2・R3）。
3. 録画シナリオ（全数）が緑。移した面に触るシナリオは、DSL の UITK ロケータで解決している（ログで確認）。
4. Unity の Error ログが 0。
5. その段階を含む配布ビルドで、Windows 検証機の通し検証と Mac の Steam 確認が通る（moores-playtest-release）。実機が要る確認はこの工程に組み込み、チェックボックスで後回しにしない（shipping §6.2 の「実機検証の 3 か月滞留」の対策）。

### P0 地ならし（見た目は不変）

**目的。** 挙動を変えずに先に済ませられるもの、後回しにすると全段階を塞ぐものを片付ける。

**作業。**

| # | 作業 | 根拠 |
|---|---|---|
| 0-1 | `com.singtaa.onejs` を manifest と lock から外す。参照 0 件の `com.annulusgames.debug-ui` も同じ PR で外してよい | shipping §5、input §5.1 |
| 0-2 | 遠隔実行の待受を Kestrel から切り離す（方式は §6 D4）。`RemoteExecEndpoint` を `HttpContext` 非依存にし、`RemoteExecActivation.ActivateIfRequested(webUiStarted, KestrelPort)` の Web 依存を外す。`RemoteExecEndpointTest` と `PlaytestBootUnityCallbackBindingTest.cs:52-62` を書き直す。`scripts/playtest/remote-exec.sh` は `access.json` 経由のまま使えるようにする | host §6.3、tests §4.2、`Client.Starter/InitializeScenePipeline.cs:73`、`Client.WebUiHost/Boot/WebUiEndpoints.cs:97-103` |
| 0-3 | `Client.WebUiHost` の中にある状態と取得処理を、Hub と無関係な DI 登録へ移す（抽出 PR）。対象: `ResearchTopic` の `_nodeStates`、`ChallengeTopicState`、`BlockNetworkInfoCache`、`BlockInventoryTopic.TrackBlock` の監視判断、`OpenTrainTimetableTracker`・`TrainTimetableFetcher`、`NotificationTopic` の連番と移行告知、`WebUiModalService`（static をやめる）、`BlueprintNameInputWebBridge` の状態側、`EventLanguageGate`。JSON アダプタは移設先を読むだけにする。挙動は変えない | host §5.2、`Host/Game/WebUiGameBinder.cs:48-55`。前例は ADR 0052 の「抽出 PR と削除 PR の 2 本」 |
| 0-4 | `UIStateControl.OnStateChanged`（`event Action<UIStateEnum>`）を UniRx へ直す | `UIStateControl.cs:16`。AGENTS.md「イベント発火に Action を使わない」 |
| 0-5 | 台帳を作る。`docs/webui/MIGRATION.md` を UITK 移行の入口文書へ置き換え、TODO・処遇台帳の 3 点セットにする。棚卸しは 3 軸（`web/features/*`、`App.tsx` のマウント点と Portal、`WebUiGameBinder` の topic と action の登録）で突き合わせ、状態の外にある面（常駐 HUD、Ctrl+U、背景スキット、カットシーン中の退避、カーソル追従）を別表にする。所有表の初版（全部 Web）もここで置く | shipping §6.1・§6.2（前回は状態機械だけで洗い出して背景スキットを見落とした） |
| 0-6 | 受入表を機械的に抜き出す。Playwright のテスト名 213 件と vitest の describe/it 名を、画面ごとに「前提・操作・期待・由来」の 4 列へ落とす | tests §3 |
| 0-7 | ゲートを置く。`web/features` への新規ファイル追加を禁止する（凍結。§6 D2）。所有表のテストの枠を作る。`WebUiGateAuditTest`（新規 uGUI 禁止）は残す | `.decisions/2026-09-08-uGUI追加禁止はトークン検査と分類粒度の両方で守る.md` |
| 0-8 | bd に移行の epic を切り、段階を子にする | AGENTS.md（Beads） |

**完了条件（観測できる形）。**

- 共通条件 1〜5。
- 配布ビルドから `quickjs_unity.dll`（Windows の `Plugins/x86_64`）が消え、それ以外の差分が無い。
- Windows 検証機の配布ビルドで、`--remoteExec` 付きなら `remote-exec.sh` が `Succeeded` を返し、付けなければ待受が開かない（ADR 0072 の完了条件を新しい待受で再確認）。
- Web ホストの起動を失敗させた構成でも、0-3 の状態クラスが DI に存在することを EditMode テストで確かめる。
- 台帳の 3 軸の全行が分類済みで、未分類ならテストが落ちる。

**出荷。** 可。テスターに見える差は無い。

**リスクと潰し方。**

| リスク | 潰し方 |
|---|---|
| 移設で topic の配信タイミングが変わる | 既存の C# テスト（ビューモデル構築 90・アクション 36・状態ストア 28）と全録画シナリオで回帰を見る |
| 遠隔実行の載せ替えで、検証機の運用（以降の全段階の検証路）が止まる | 旧経路と新経路の切替を 1 PR で行い、検証機で通してから master へ入れる。`HttpListener` を選ぶなら Mono での挙動を P0 の中で先に試す |
| 凍結中に Web の画面へ機能追加の要求が来る | その画面の移行を前倒しする（§6 D2） |

**規模とエージェント並列度。** 30〜45 ファイル。新規 0.8〜1.5k 行と、移設 1.5〜2.5k 行。並列は 3 本（遠隔実行 ／ OneJS・台帳・ゲート・受入表 ／ 状態の移設）。`WebUiGameBinder.cs` は移設の 1 本だけが触る。

### P1 基盤・共存プローブ・入力の無い HUD

**目的。** 共存の物理（描画順・入力経路・録画・IME）を最初に実測して以降の設計を確定する。全画面が乗る基盤を作る。入力を持たない HUD を本番に出し、配布環境で UITK の描画・拡縮・フォント・i18n・アンカーを確かめる。HUD に入力が無いので、ここで出る不具合の原因は描画側に絞れる。

**作業 1: プローブ。** worktree 1 本・Editor 1 本で行い、結果を台帳に記録する。

| # | 実測すること | 決まること |
|---|---|---|
| a | CEF の Canvas（`MainGameUI.prefab`。Canvas は自前なので sortingOrder を変えられる）と UITK パネルの前後を入れ替えられるか | UITK を CEF の上に置けるか。P2 以降の掴み絵・トースト・モーダルの層 |
| b | Recorder（`GameViewInputSettings`）、`PlaytestScreenshot`、バグ報告の `ScreenFrameReader`（`CaptureScreenshotIntoRenderTexture`）に UITK が写るか | 録画検証とテスターのバグ報告動画が成り立つか。**写らなければここで止めて撮影経路を直す** |
| c | `QueueStateEvent` で注入した入力が `InputSystemUIInputModule` 経由で UITK に届くか。パネル座標から画面座標への逆変換 | DSL の UITK ロケータ |
| d | `EventSystem.IsPointerOverGameObject()` が UITK を拾うか。`panel.Pick` で同じフレームに判定できるか | `UiPointerHitTest` の中身（input §3.3 案 A） |
| e | `DefaultInputActions` の Navigate（WASD・矢印）、Submit（Space・Enter）、Tab のフォーカス移動を止められるか | UI 用のアクション資産。`.decisions/2026-10-01-メニュー中の視点は回さず移動だけ解放する.md`・`…Space既定動作を封じる.md`・`…クラフトボタンはSpaceに反応させずジャンプ専用にする.md` を守れるか |
| f | TextField 上の ESC と `UIStateControl.Update` の同じフレーム内の順序 | ESC ラッチの要否 |
| g | UI Test Framework（`com.unity.ui.test-framework` 1.0.0、組み込み）を manifest に入れ、CI の batchmode の EditMode で 1 本通るか。`experimental.animation` と transition が `FrameUpdateMs` で進むか。Editor パネルのレイアウトが PlayMode と一致するか | L1 テストの成否（tests §5.5） |
| h | IME。CEF が `imeCompositionMode=On` を常時にしている状態で、UITK の TextField が変換・確定・取消できるか。Mac は Editor で、Windows は配布ビルドに遠隔実行で TextField を出して、どちらも人が打つ | P2・P3 の入力欄（ユーザーの手作業。§6 末尾） |
| i | 1280×720 の実画素で 11〜12px の和文がどう見えるか。主要な半透明トークン 4 つ（`--surface-navy`、`--tooltip-face`、`--slot-ghost-opacity`、`--screen-vignette-edge`）を Linear 合成で決め直す | §6 D6・D7 |

**作業 2: 基盤。**（各項目の設計方針は §3）

- UI ルート: MainGame の UIDocument（prefab への配置は `uloop execute-dynamic-code`）、PanelSettings（基準 1280×720・Expand）、層 enum と層コンテナ（`PickingMode.Ignore` を既定）、stage 族・viewport 族・背面 viewport 族の 3 つの置き場。
- 画面ルータと所有表: `SCREEN_TRAITS` の C# 版、所有表、Web への委譲の伝え方（R2）、所有がちょうど 1 つであることのテスト。
- 入力: `UiPointerHitTest` の Pick 化（共存中は論理和）、`WebUiInputExclusivity` を `UiInputExclusivity` へ改名して汎用化、ESC ラッチ、UI 用のアクション資産、入力欄以外を `focusable=false` にする規約、ホイールを通す印のクラス。
- テーマ・フォント: トークンの USS（Web の約 238 トークンのうち使う分から。α は UITK 用の列）、ランタイム既定テーマの暗色化（PoC の `PocControls.uss` 146 行が起点）、TextCore の FontAsset（JP・EN・KR）、静的検査（L2）。
- i18n: 名前付き補間（§6 D5）、言語切替で再解決するラベル部品、数値書式の InvariantCulture 固定、UXML の生文字の禁止、kbd の差し込み（`LocalizedShortcutHint` 相当）。
- アンカー: `web/shared/tutorialAnchor/anchorIds.ts` を C# の定数へ移して唯一の正本にする。`TutorialAnchor.Mark`（USS クラスとレジストリ）、UITK 側の枠描画（祖先クリップとの交差、脈動）、R6 の診断。
- モーション基盤（§6 D9）、GamePanel（まず hud variant。4 辺フェードは頂点カラーのメッシュ）、カーソルツールチップの層（ADR 0019 の書式、画面端での反転、カーソルロック中は画面中央に出す規則）。
- DSL: 描画系に依存しない `IUiElementLocator`（UITK を先に探し、次に Web。R7）。`ClickWebUi` などを `ClickUi` などへ一度に改名し、シナリオ 27 本とスキル文書 4 本（`write-scenario.md`・`input-injection.md`・`place-blocks-via-ui.md`・`run-scenario.md`）も同じ PR で直す（tests §6.4）。
- テストの土台: `UITestFixture` に本番のテーマ・フォント・パネル寸法を当てる型、`FakeXxxModel` と `RecordingXxxCommands` の型（tests §5.3）。
- スキル: `ref-uitk-design` の初版。`ref-webui-design` の原則（§1〜§7・§9）を継ぎ、§0（mock-host・Vite・Playwright）と §10（目視 QA）を UITK の手順へ置き換える（shipping §2.5）。

**作業 3: 入力の無い HUD を移す。** Crosshair、KeyHintHud、KeyControlHintHud、ProgressBar、TrainRidingHud、CurrentChallengeHud、PlacementModeHud、DeleteModeWarningBands と DeleteBoxHeightHud、CursorTooltip、WorldPinOverlay。どれもクリックを受けないので `PickingMode.Ignore` で置ける。Web 側の該当コンポーネントとテストは同じ PR で消す（R3）。

- CurrentChallengeHud のアンカー `challenge.current-hud` はマスタが参照している（`moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/challenges.json`）。そのため P1 で UITK 側の枠描画が本番で動く。チュートリアルの束の難所を、ここで先に踏む。
- WorldPinOverlay は、毎フレーム C# で射影して WS で送っていた遅れが消える（input §1.5）。

**完了条件。**

- 共通条件 1〜5。
- プローブ a〜i の結果が台帳にあり、P2 以降の前提（UITK を CEF の上に置くか等）が決まっている。
- 移した HUD のうち 1 本以上が録画に写っている（フレーム番号の透かし付き）。バグ報告の動画にも UITK が写っている。
- マスタの `challenge.current-hud` のハイライトが UITK で出る録画がある。
- 名前付き補間のキーと数値書式が、de 列（ドイツ語）でも崩れない EditMode テストがある。
- IME の手動確認（Mac・Windows）の結果が台帳にある。

**出荷。** 可。テスターには「HUD だけ UITK、パネルは Web」の混在が届く（§6 D1）。

**リスクと潰し方。**

| リスク | 潰し方 |
|---|---|
| 描画順を制御できない | Canvas の sortingOrder を変えて試す。それでも駄目でも、P1 の HUD は Web のパネルと重ならない位置にあるので P1 は成り立つ。P2 以降の掴み絵・トースト・モーダルの層を設計し直す |
| 録画・バグ報告に UITK が写らない | P1 の停止条件にする。写らないまま進むと、以降の録画検証とテスターの報告動画が全部欠ける |
| viewport 族の二重基準（位置は実画面、寸法は stage 拡縮）が PanelSettings 1 枚で表せない | パネルのルート寸法から viewport 族のオフセットを C# で出す（screens §9-7） |
| 小さい文字が細い・半透明が薄い | トークンの UITK 列で吸収する。画素の一致は追わない |
| 基盤を作り込みすぎて P1 が長引く | P1 で使う部品と、P2 で必ず使う入力の土台（Pick・入力排他）だけにする。D&D・HoverTooltip・TextField の様式は P2 で作る |
| IME が CEF と取り合う | P1 で実測する。取り合うなら、TextField のフォーカス中だけ `imeCompositionMode` を明示的に制御する（推測） |

**規模とエージェント並列度。** 基盤 50〜70 ファイル（C# 3.5〜5k 行、USS 0.8〜1.2k 行）、HUD 20〜30 ファイル（1〜1.5k 行）、DSL の改名 31 ファイル、L0/L1 テスト 80〜120 件。プローブは直列で 1 本。基盤は、共有ファイル（層 enum・所有表・MainGame の配線）を 1 セッションが持ち、ほかに「テーマ・フォント・GamePanel」「i18n・アンカー・モーション」「DSL・テストの土台」を並べる。Mac mini の Editor 上限（2〜3 本）があるので、同時に動くのは 3 本まで。HUD の移行は 2〜3 本。

### P2 建築の束（ビルドメニュー＋ホットバー）

**目的。** 最初の操作できる画面で、D&D・ホバーツールチップ・IME 付きの検索欄・スクロール・入力の取り合いを踏む。ホットバーは常駐 HUD なので、Web のインベントリ画面やポーズ画面と同時に出る。共存の入力規則（R1・R4）が本番で試される。

**対象。** BuildMenuPanel 一式（カテゴリのサイドバー、検索欄、1 本のスクロールに並ぶカテゴリ群、ホバーで固定される詳細サイドバー、BP の右クリック削除、不足時のツールチップ）、HotbarPanel（9 枠、選択、D&D の割当・入替・解除）。

**作業。**

- 部品: press ジェスチャ（閾値 5px、tap・drag・abort、`CapturePointer`、捕捉中に下の要素へ届く PointerEnter の抑止）、D&D の層（ドラッグのゴースト、`panel.Pick` で落ち先を判定）、HoverTooltip（祖先がスクロールしたら引っ込める）、SlotFrame・ItemSlot・SlotGrid・PlacementTargetFace、縦の ModeSwitch、IconButton（× は Painter2D のパス）、スクロール領域（§8.10 の様式、smooth scroll、スクロールスパイ）、検索用の TextField（§8.9 の様式）。
- ロジックの移植: `buildMenu/logic`（分類・検索・不足）、`hotbarDnd.ts`、`placementTarget`、`materialTooltipText`、`useBuildMenuCategoryScroll.ts`（305 行）の計算部分、セッション状態（検索語・スクロール位置・ホバー。2026-08-05 の裁定どおり C# のモデルへ）。
- データ層: `build_menu.entries` と `local_player.hotbar` を型付きの購読元へ分ける。`build_menu.select`・`blueprint.delete`・`hotbar.*` を型付きコマンドへ分ける。
- ホイール: ホットバーにホイールを通す印を付ける。装備 HUD は P4 まで Web に残るので、ホットバーと床を共有するトークン（`--equipment-bottom: var(--hotbar-bottom)`。ADR 0013）が両系で同じ値になることをテストで見る。
- Web 側: BuildMenuPanel・HotbarPanel・関連テストを消し、buildMenu 画面を委譲にする。
- アンカー: `build-menu.entry-*` とホットバー系を UITK で付ける。

**完了条件。**

- 共通条件 1〜5。受入表の由来は buildMenu（P 19・C 22・L 30）、hotbar（P 6・C 7・L 3）（tests §2）。例:
  - エントリを 5px を超えて動かしてホットバーの枠 1 へ落とすと、`AssignHotbar(1, id)` が 1 回だけ発行され、`SelectBuildMenu` は発行されない。枠の外へ落とすと `ClearHotbar(slot)` が発行される（tests §3 例 1）
  - 閉じて開き直すと、検索語・スクロール位置・詳細サイドバーが元に戻る（tests §3 例 5）
- 共存の受入:
  - Web のインベントリ画面やポーズ画面を開いたまま UITK のホットバーをクリックしても、Web 側で action が発行されない（action ログが 0 件）
  - UITK の検索欄に入力している間はゲームのキーが止まり、Web の document 全域のキー処理も動かない
  - 検索欄で ESC を押すと、フォーカスが外れるだけで画面は閉じない（現行の見え方を保つ）
- ビルドメニューからホットバーへ D&D して設置する録画シナリオ（`place-blocks-via-ui` 系）が UITK で緑。
- 検索欄の日本語 IME の手動確認（Mac・Windows）。

**出荷。** 可。

**リスクと潰し方。**

| リスク | 潰し方 |
|---|---|
| Web が UITK 上のクリックを拾って二重に発火する | R1。ホットバーの位置に Web の要素が無いことを所有表で保証し、共存の受入テストで action ログ 0 件を見る |
| 捕捉中のドラッグにホバーツールチップが出る | PoC の罠。press ジェスチャ側で最初から抑止する |
| スクロールで子の `GeometryChangedEvent` が来ず、スクロールスパイやアンカーが追従しない | `scrollOffset` を監視して判定する（tests §5.5） |
| Web の掴み絵が UITK のホットバーの下に潜る | 見た目だけの問題。P4 で解消する。許容したことを台帳に書く |
| 装備 HUD（Web）とホットバー（UITK）の床がずれる | 床トークンの一致テスト。1280×720 と横長の 2 解像度で録画を撮る |

**規模とエージェント並列度。** 35〜50 ファイル、2.5〜3.5k 行（部品を含む）、テスト 90〜130 件。D&D・ジェスチャ・スロットの部品を 1 本が先に持ち、その後ビルドメニューとホットバーを 2 本で並べる。ロジックの移植はもう 1 本で並行できる。同時は 3 本まで。

### P3 ポーズ・設定・バグ報告・モーダル・トースト・言語ゲート

**目的。** 複数行の入力欄・モーダルの入力欄・全画面モーダルの層・フォーカスの閉じ込め・ESC の 1 段戻りを潰す。あわせて、CEF が起動に失敗した PC でもポーズ・セーブ・終了・バグ報告ができる「命綱」を UITK に作る。

**対象。** PauseMenuPanel（トップとサブページ）、設定 3 種（画質・言語・ダッシュ方式）、BugReportForm、UITK のトースト層（Web 側の、ブリッジの失敗を出すトーストは P5 まで Web に残る）、ModalHost、`BlueprintNameInputWebBridge` の置き換え、EventLanguageGate。

**作業。**

- 部品: モーダル層（幕、フォーカスの閉じ込め、ESC）、全画面ゲートの層、複数行の TextField、PanelActionButton、横の ModeSwitch。
- 言語ゲートは DI より前に出す（`Client.Starter/EventMode/EventModeStartGate.cs`）。UITK のパネルを Starter の寿命で持つ設計を、最初に決める。辞書を通さない固定の文言（`DictionaryIndependentText`）は C# の定数へ移す。
- 入れ子ポーズ（スキット中・列車中。`INestedPauseScreenState`、ADR 0035）でも UITK のポーズメニューが出るようにする。
- 言語を切り替えたら UITK のラベルを再解決する。Web の画面は従来どおり Web 側で再描画する。
- 操作音は、ビューからサウンドのサービスを直接呼ぶ（`.decisions/2026-08-26-Web操作音はC#へsound.playを送って鳴らす.md` の経路が変わる）。
- Mantine の既定の見た目のままの部品（Button・TextInput・Notification）は、ホワイトリストの語彙で様式化する（§6 D11）。

**完了条件。**

- 共通条件 1〜5。受入表の由来は pauseMenu（vitest 29）、modal（8）、eventLanguageGate（9）、settings（15）と、`system/pauseMenu.spec.ts`・`feedbackReportLabels.spec.ts`・`pauseMenuSettingsLayout.spec.ts`。
- **命綱の受入**: Web ホストが起動に失敗する構成（作り方は P3 で決める）で起動しても、ESC でポーズ → セーブ → 終了、およびバグ報告の送信ができる。これを録画で確かめる。
- バグ報告の本文と BP 名入力の、日本語 IME の手動確認（Mac・Windows）。
- 入れ子ポーズの 2 経路（スキット中・列車中）の録画。

**出荷。** 可。

**リスクと潰し方。**

| リスク | 潰し方 |
|---|---|
| フォーカスの閉じ込めで、WASD の移動が止まらない／止まりすぎる | `UiInputExclusivity` の受入テスト。`.decisions/2026-10-01-文字入力欄にフォーカス中は移動を止めたままにする.md` と `…メニュー中の移動停止はESCメニューだけにする.md` を受入表へ |
| ESC の 1 段戻り（`PauseMenuStateService.HandleCloseKey`）が二重に処理される | ESC は C# の UIState に一本化する。Web の ESC 経路は委譲画面では動かない（R3） |
| DI より前の UITK 表示が、ゲーム本体のパネルと寿命を取り合う | 言語ゲート用の UIDocument を Starter が所有し、本体のパネルとは分ける（推測。P3 の最初に決める） |
| UITK を CEF の上に置けなかった場合、全画面ゲートが Web の上に出ない | P1 プローブ a の結果に従う。置けないなら、ゲート表示中は Web を空の画面へ委譲する |

**規模とエージェント並列度。** 25〜35 ファイル、1.5〜2.5k 行、テスト 60〜90 件。並列は 3 本（ポーズ・設定・バグ報告 ／ モーダル・トースト・BP 名入力 ／ 言語ゲートと DI 前の表示）。

### P4 持ち物の束（掴み・ブロック UI・研究）

**目的。** 掴みでつながる面を UITK に揃え、Web の InventoryPanel・GrabOverlay・EquipmentPanel・BlockInventoryPanel を消す。最大の段階なので、出荷できる小段階 5 つに割る（§1.5）。

| 小段階 | 中身 | 主な難所 | 規模（推測） |
|---|---|---|---|
| 4a 持ち物画面 | InventoryPanel、EquipmentPanel（HUD）、GrabOverlay、RecipeViewer、ItemListPanel | `shared/itemMove` の計画の移植（左・右・ダブルクリック・Shift 配分）、分割ドラッグ、右ドラッグ配り、整理ボタン、長押しクラフト（パネルのスケジューラで駆動）、Enter でのクラフト、ESC での選択解除、ドラッグスクロール、コンテナクエリ（`GeometryChangedEvent` で C# 計算）、レシピ枠の装飾（Painter2D）、装備 HUD のホイール循環、アンカー `recipe.craft-button`・`recipe.item-<id>`（マスタが参照） | 30〜40 ファイル、2.5〜3.5k 行 |
| 4b ブロック UI の汎用族 | SectionStackView（チェスト・機械・発電機・採掘機・ポンプ・パイプ・液体） | 機械のレシピ選択とインベントリの往復（`.decisions/2026-08-30-機械UIはタブを廃しレシピ選択とインベントリをSatisfactory方式で往復する.md`）、ゴーストスロット、モジュールスロット、進捗矢印、RecipeRow の共用（`.decisions/2026-08-20-レシピ行は独自gridを維持しSlotGridの例外とする.md`） | 20〜30 ファイル、1.5〜2.5k 行 |
| 4c 特殊ブロック族 | FilterSplitter、ElectricToGear、GearToElectric、ElectricPole、Gear・Shaft・GearBeltConveyor、ベルト流量、電力網・歯車網 | BarSeries（Painter2D）、←→ キーでの窓切替、網情報の 1 秒ポーリング（P0 で移設済み） | 20〜30 ファイル、1.5〜2k 行 |
| 4d 列車族 | 列車インベントリ、駅名、プラットフォーム、時刻表 | 駅名の IME、時刻表の楽観更新（ack と配信の両方を待つ仕様を保つ）、兄弟セレクタの置き換え（C# でクラスを付ける）。ここで Web の blockInventory と subInventory 用の InventoryPanel を消す | 12〜20 ファイル、1〜1.5k 行 |
| 4e 研究 | ResearchTreePanel、ノード 4 状態、詳細ペイン、研究ボタン | TreeView（パン・ズーム 0.4〜2.5、慣性 τ=325ms、Painter2D の線、ビューポートの保存、初期フォーカス）、アンカー `research.node-<guid>`（マスタ 7 件）、2026-08-20 の判定役（オラクル）方式のハイライトのテスト。ここで Web の InventoryPanel・GrabOverlay・EquipmentPanel がすべて消える | 20〜30 ファイル、1.5〜2.5k 行 |

**完了条件（小段階ごと）。**

- 共通条件 1〜5。受入表の由来は、インベントリ・装備（P 20・C 16・L 57）、レシピ（P 18・C 10・L 19）、ブロック UI（P 42・C 27・L 45）、列車（P 6・C 6・L 14）、研究（P 18・C 21・L 37）（tests §2）。
- 録画: 掴み・分割ドラッグ・機械へのアイテム投入・時刻表の編集・研究の完了のシナリオ。
- 台帳: ブロック種別 × 族の全行が「UITK 済／Web（どの小段階で移すか）」のどちらかに分類されている。未分類ならテストが落ちる。
- 4e の終了時: Web 側で掴みを扱うコードが 0。`screenAllowsGrab` を参照する Web のコードも 0。

**出荷。** 小段階ごとに可。

**リスクと潰し方。**

| リスク | 潰し方 |
|---|---|
| 小段階の間に、両系の InventoryPanel の挙動が食い違う | Web 版は凍結してバグ修正だけにする。仕様は受入表を正とする。4a から 4d までの期間を短く保つ |
| 系をまたぐ掴み | 未移行の族の subInventory では UITK の面を出さない（装備 HUD も Web）。所有表のテストで固定する |
| ブロック UI（11 種以上）の網羅漏れ | 3 軸の台帳で、ブロック種別ごとに行を持つ |
| スロットが多い画面の描画負荷 | スロットは border の入れ子で描き、Painter2D はパネル枠と図形だけに使う（推測）。持ち物画面と研究ツリーで、要素数と UITK の CPU 時間（`UIElementsUpdatePanels` と `UIElementsRepaintPanels`）を測る |
| 仮想化した一覧の行に付けたアンカーは、画面外では存在しない | 研究ツリーは仮想化しない（transform で動かす）。仮想化する一覧は Bind 時にアンカーを付け替え、missing の扱いを受入表に書く（tests §7.2-6） |

**規模とエージェント並列度。** 合計 100〜150 ファイル、8〜11k 行、テスト 300〜400 件。4a は 3 本（持ち物と掴み ／ レシピビューアと一覧 ／ ロジックの移植）。4b〜4d は族ごとに 1 本で、Editor の上限から同時 3 本まで。4e は TreeView を先に作り、その後 2 本で並べる。

### P5 残りの画面と横断層（Web を空にする）

**目的。** Web が描くものを 0 にする。CEF は「起動するが何も描かない」状態で 1 サイクル出荷し、見落とし（状態の外にあるオーバーレイなど）を、撤去の前に表へ出す。

**対象。**

- ChallengePanel（TreeView を再利用、Mantine の素の見た目を様式化）
- スキット: 会話窓、タイプライタ、選択肢、ツールバー、暗転、背景スキット。あわせて旧 UITK の `Client.Skit/UI/SkitUI.cs` と `SkitPresentationMode` を撤去する（bd `moorestech-lnsf.8`）
- 通知: 背面 viewport 族、入場と退場、集約、生存尺 7000ms を単一の正にすること、saveMigration の告知
- Web 側の ToastHost・TutorialOverlay・DragGuide（`uiDragGuide` の UITK 版。マスタでは 0 件だが機構は残す）
- Ctrl+U とカットシーン中の全体非表示
- 再接続オーバーレイ・辞書エラー・`AppErrorBoundary` が担っていた「UI の例外に誰が気づくか」の代わり。UITK のビューの例外を LogError とバグ報告の manifest へ出す

**作業。** `web/features` を全部消し、`App.tsx` を空のシェルにする。所有表はすべて UITK になる。`WebUiGameBinder` の topic と action の登録はまだ残る（何も購読しない）。

**完了条件。**

- 共通条件 1〜5。受入表の由来は、チャレンジ（P 11）、スキット（P 6・L 5）、通知（P 6・L 25・S 12）、チュートリアル（P 7・C 28・L 4・S 21）。
- `moorestech_web/webui/src/features` が空。
- 全録画シナリオが緑で、DSL の Web ロケータが一度も要素を返さない（ログで確認）。
- 3 軸の台帳の全行が「UITK 済」か「除外（理由つき）」。背景スキットは受入表に明記する。
- 1 サイクルの playtest 配布で、UI の欠落の報告が無い。

**出荷。** 可。CEF は同梱され起動もするが、何も描かない。

**リスクと潰し方。**

| リスク | 潰し方 |
|---|---|
| 状態の外にあるオーバーレイの見落とし | 3 軸の棚卸しと状態外の表。CEF を空にしたまま 1 サイクル出すこと自体が、最後の網になる |
| タイプライタで行の折り返しが途中で変わる | 残りの文字を透明にして配置を固定する（推測。shipping §3.1） |
| 通知の集約で退場の予定が二重になる | 退場の予約を取り消して張り直す API をモーション基盤に持たせる。L1 で時間を進めて確かめる |

**規模とエージェント並列度。** 30〜40 ファイル、2〜2.5k 行。Web 側の削除は大量。並列は 3 本（チャレンジとチュートリアル ／ スキット ／ 通知・トースト・全体非表示）。

### P6 撤去

**目的。** CEF・Kestrel・Web ホスト・Web 資産と、共存のための仕組みを消し、§5 の完了の定義を満たす。

**PR の分け方**（順序の理由は §5.1）。

| PR | 中身 |
|---|---|
| 6a 起動配線と共存機構 | Starter の `WebUiStartup`・`EventModeStartGate` の Hub 依存・Finalizer の `Bind`。所有表と委譲、DSL の Web ロケータ、`UiPointerHitTest` の Web の項、`UiInputExclusivity` の Web 入力 |
| 6b Web ホストと Web 資産 | `Client.WebUiHost`、C# の通信契約テスト、`WebUiActionInvoker`、`Client.Playtest/WebUi/*`（`CefInputForwarder`・`CefScreenMapper`・`PlaytestDomQuery`）、`moorestech_web/`、CI の `webui-test` ジョブと `needs_webui`、pnpm の step、`WebUiProductionArtifactBuilder` |
| 6c CEF と配布工程 | `jp.juha.cefunity`、`MainGameUI.prefab` の CEF Canvas（`uloop execute-dynamic-code` で外す）、`CefRuntimeBundler`、`CefPackageLfsValidator`、`scripts/setup-cef.*`、CI の CefUnity 事前取得、`release-artifact.sh` の CEF helper 検査とその契約テスト、README の「CEF raw input 確認」。`CefLfsPointer` は中立名へ改名し、`EnvironmentScrubOnEditorLoad` は `Client.ExternalProcess` 側へ移す |
| 6d NuGet | AspNetCore 系。§6 D4 で HttpListener を選んだなら全部外す。先に `Microsoft.Extensions.*` を ModelContextProtocol などが使っていないか確かめる |
| 6e 文書・スキル・改名 | `WebPinId`（13 か所）、`LocalizationKeys.Ui.Blueprint.WebModal*`、`VanillaSchema/challenges.yml` の既定文字列 `web anchor id`、`ref-webui-design` の後継の完成、moores-code-review の `core-ts_tsx-*` 12 本の撤去、`docs/webui` の archive 化、ADR の改訂（§7） |

**完了条件。** §5.2 の完了の定義。6c は配布物が変わるので、Windows 検証機・Mac の Steam・`test-release-playtest-mac.sh` を必ず通してから playtest へ反映する。

**出荷。** PR ごとに可。

**リスクと潰し方。**

| リスク | 潰し方 |
|---|---|
| CI の Windows ビルド（コンテナ内の UPM と LFS）の前提が変わる | 6c の PR で CI を一度回し、ビルドログで CEF の 237 ファイルが消えていることを確かめる |
| `activeInputHandler` を InputSystem だけにすると IME が壊れる | 完了の定義に入れない。別に判断する（input §6-3） |
| Mac の arm64 固定を外すと ffmpeg が壊れる | 外さない。ffmpeg も arm64 しか無い（shipping §1.3） |

**規模とエージェント並列度。** 削除が中心（`Client.WebUiHost` 約 10.5k 行、`moorestech_web` はテスト込みで約 24.5k 行、DSL の Web 部 約 670 行、Web を参照するテスト 65 ファイル）。新規は改名と移設だけ。PR は直列のチェーンで、1〜2 本。

---

## 3. 共通基盤の設計方針

### 3.1 データ購読層

- **Topic を 2 つに分ける。** 各 `XxxTopic` を、①型付きのスナップショット DTO と変化通知（`IObservable<Unit>`）を持つ購読元と、②それを JSON にする Web 用のアダプタに分ける。DTO はもう public なので（例: `InventoryTopic.cs:110-123`）、分割は機械的にできる。①は Web ホストと無関係な場所（`Client.Game` か新しい asmdef）に置き、P6 で②だけ消す。
- **フレーム末に畳む処理（PostLateUpdate）は残す。** 中間状態を描かないため。一方、WS の帯域のための間引き（`ui.progress` の 100ms、歯車の `IntervalFrame(4)`）は UITK では外し、毎フレーム直接読む（host §5.1）。`BlockInventoryTopic` のコメント（4tick）と実装（`IntervalFrame(4)`）が食い違っているので、移植するときに意図を確かめる（host §9）。
- **操作は型付きコマンドにする。** `IActionHandler` の JObject 解析と前提条件の検証（state の一致・解放判定・遷移許可表）を分け、検証をコマンド側へ寄せる。結果はエラーコードの文字列をやめて enum で返す。トーストを出すかどうかは、良性の失敗（Web の `BENIGN_ERRORS`）を enum で分けて決める。
- **単一消費者は描画者を 1 つにする。** モーダル、BP 名入力、スキットの入力（host §7.1）。所有表に「横断層の描画者」も行として持つ。
- サーバーの可変状態の同期（「イベントパケット＋初期データ＋購読」の 3 点セット）は変えない。UITK 化はクライアントの内側だけの話。

### 3.2 ビューの構成と部品

- **View と Presenter に分ける**（tests §5.3）。`XxxView` は VisualElement を組むだけで、MonoBehaviour に依存しない。`XxxPresenter` はモデルを購読して描き、操作を型付きコマンドへ渡す。UIDocument と MonoBehaviour は、本番で Presenter に依存を渡す薄い層だけにする。こうすればテストで `rootVisualElement` に同じビューを組める。
- **置き場。** UITK のビューは新しい asmdef（名前は plan で決める。例 `Client.UI`）に置き、`Client.Game` のモデルへ依存する。逆向きの依存は作らない。1 ディレクトリ 10 ファイルまで・1 ファイル 200 行以下（AGENTS.md）に合わせ、機能ごとにサブディレクトリを切る。
- **部品は必要になった段階で作る。** 例外は P1 の基盤だけ。

| 部品 | 作る段階 | 中身 |
|---|---|---|
| 層マネージャ、画面ルータ、所有表 | P1 | 層 enum の順に兄弟コンテナを並べる（USS に z-index が無いため） |
| GamePanel（4 variant） | P1（hud）、P2（default）、P3（craft） | `generateVisualContent` の頂点カラーメッシュで、縦グラデ・4 辺フェード・内周線・右下グリップ・下向き三角を描く（mask・::before・clip-path の代わり。shipping §2.3） |
| カーソルツールチップ | P1 | `tooltipPosition.ts` の移植、OFFSET 12、画面端で反転 |
| スロット系、SlotGrid | P2 | 3 層のベベルは border の入れ子、選択時のシアン、個数の縁取り（`-unity-text-outline`）、Sprite を直接表示 |
| press ジェスチャ、D&D 層、HoverTooltip | P2 | §3.5 |
| ボタン類、ModeSwitch、IconButton | P2〜P3 | `:hover`・`:active`・`:disabled`、`:focus-visible` は `:focus` で代える |
| スクロール領域、TextField | P2 | §8.10 と §8.9 の様式、smooth scroll |
| モーダル層、全画面ゲート | P3 | 幕、フォーカスの閉じ込め |
| GaugeBar、ProgressArrow、BarSeries、FadeRule、斜線帯 | 必要な段階で | Painter2D |
| TreeView | P4e | パンとズーム、慣性、Painter2D の線 |

### 3.3 テーマ USS

- **原則はそのまま継ぐ。** ホワイトリスト、GamePanel だけが面、スロットは共有部品だけ、装飾は 6 語彙、色はトークンだけ（shipping §2.2）。新スキル `ref-uitk-design` に写す。`components.md` の §8 系（23 節）は、画面を移すたびに UITK 版へ書き直す。台帳に「節 → Web 版のまま／UITK 版」の対応を持つ。
- **トークンは USS を正本にする**（§6 D10）。`calc()` で導いていた約 70 か所はリテラルの結果を書き、導出式をコメントに残し、L2 テストで導出と一致するかを確かめる。C# が値を要るとき（Painter2D や配置計算）は `customStyle` と `CustomStyleProperty` で読む。`rem`・`em` は px へ換算する（1rem＝16px）。
- **半透明はトークン表に「Web の値」と「UITK の値」を別の列で持つ。** Web の値は流用しない（PoC。Linear 合成で薄く出る）。決め方は §6 D6。
- **データ属性（`[data-selected]` など 40 か所）は USS のクラスにする。** C# で `EnableInClassList` する。`:first-child`・`:last-child`・隣接兄弟は無いので、C# でクラスを付ける。
- **ランタイムの既定テーマを暗色にする USS** は PoC の 146 行が起点。スライダーの入力部の高さが 0 になる癖への対処も含める。

### 3.4 アニメーション

- **入場・退場・ホバーは USS の transition を基本にする。** 要件の大半はこれで足りる。`height: auto` は補間できないので、現在の高さを px で固定してから動かす。並べ替えの FLIP は `GeometryChangedEvent` の旧矩形と新矩形で書く（PoC の罠 1〜3）。
- **ループ・キーフレーム・ばねは 1 系統だけ入れる**（§6 D9。推奨は自作）。対象は 3 系統だけ（通知の出入り、チュートリアルの脈動、ドラッグガイドの往復。shipping §3.1）。持たせる API は 4 つ。時計の注入、頭から回し直す、予約した退場の取り消し、完了の通知。
- **時間源は `timeScale` に依存しない実時間とする**（ポーズ中も通知は進む）。サーバーの「ティックで測る」規則は UI の演出には掛からない（shipping §3.2）。ここは agent の既定で決めて台帳に書く。
- **尺と曲線はトークンに置く。** ADR 0016 の「生存尺を単一の正として一方向に流す」を継ぐ。テストで尺を 0 に落とす抜け道は作らない（ホワイトリスト §6）。

### 3.5 ツールチップ・D&D・モーダルの層

- **パネルは 1 枚にする。** 層はルート直下の兄弟コンテナで表す。順は次のとおり。

  backdrop → 背面（通知）→ stage → viewport の HUD → stage 内のオーバーレイ → 掴み → モーダル → ツールチップ → トースト → チュートリアル → 全画面ゲート

  Pick とフォーカスが 1 枚のパネルの中で閉じるので、判定が 1 か所で済む（input §3.1）。`zLayerTokens.test.ts` の不変条件は C# のテストへ移す。
- **層コンテナと backdrop は `PickingMode.Ignore` を既定にする。** 忘れると「パネルの外での右短押し」（ADR 0046）とワールドのクリックが死ぬ（input §3.1）。層コンテナを生成する処理の既定にし、テストで固定する。
- **D&D は自作する**（PoC 約 140 行）。press ジェスチャ（閾値 5px）→ `CapturePointer` → ゴーストを掴みの層へ → 落ち先は `panel.Pick` の結果の祖先から、落ち先として登録された要素を探す。捕捉中は、下の要素への PointerEnter でホバー系が反応しないよう抑止する。
- **ツールチップは 2 種類。** HoverTooltip は要素ごと（祖先がスクロールしたら引っ込める）。CursorTooltip は `MouseCursorTooltipState` を購読する。位置は同じフレームのポインタから決め、カーソルロック中は画面中央にする（ロック中の UITK のポインタは画面外になるため。input §3.2・§3.5）。
- **掴み絵**は掴みの層に置き、同じフレームの `PointerMoveEvent` かマウス位置で動かす（現状は CEF の mousemove 頼みで、ワープ後に追従しない経路があった。ADR 0020・0074）。

### 3.6 入力とフォーカス

| 項目 | 方針 | 根拠 |
|---|---|---|
| UI 用のアクション資産 | moorestech 専用の資産を作る。Point・Click・RightClick・MiddleClick・ScrollWheel は残し、Navigate は空、Cancel は使わない | `DefaultInputActions` は Navigate に WASD を含み、メニュー中も WASD で歩く仕様とぶつかる（input §3.2） |
| フォーカス | 入力欄以外は `focusable=false`。Space・Enter でボタンが押されず、Tab でフォーカスが動かないことを受入表に入れる | `.decisions/2026-10-01-Web UI全体で文字入力欄以外のSpace既定動作を封じる.md`、tests §3 例 8 |
| ポインタ | `UiPointerHitTest` の中身を「ロック中は false、そうでなければ UITK の Pick ‖ Web（共存中だけ）‖ EventSystem（デバッグの uGUI）」にする。呼び出し側（23 ファイル 28 か所）は変えない | input §3.3 案 A |
| 文字入力 | ルートで `FocusInEvent`・`FocusOutEvent` を受け、フォーカス中の要素が TextField かを `UiInputExclusivity` の ReactiveProperty へ同期で書く。WS の受信スレッドから来ないので、lock と `ObserveOnMainThread` は要らない | input §3.4 |
| ESC | 「このフレームの ESC は UI が消費した」というラッチを `InputKey` の抑止判定に足す。入力欄での ESC はフォーカスを外すだけにする | 今は WS の遅れで偶然防げているだけ（`docs/webui/design/input-focus-exclusivity.md`） |
| ホイール | ホイールを通す印のクラスを Pick の結果の祖先で見る。装備の循環は C# の 1 か所へ写す（UI の上なら止める、印のある HUD の上は通す、建築ツールが占有していたら止める） | input §3.3、`web/features/inventory/EquipmentPanel/index.tsx:55-80` |
| キー処理の置き場 | 画面のキー処理（ESC・←→・Enter）は C# の UIState か画面の Presenter に集める。Web の document 全域の handler は、画面を移す PR で消す | host §7.3 |
| ゲームパッド | UI のパッド操作は作らない（正式対応外。ADR 0082 sprint） | input §3.2 |

### 3.7 ローカライズ

- **C# の `Localize` へ一本化する。** 辞書の合成、解決順（対象言語 → english → source → `[!key]`）、型付きキーはもうある（input §4.1）。HTTP の辞書取得・再試行・409 は消える。
- **足すのは 3 つ。** 名前付き補間（§6 D5）、言語切替で再解決するラベル部品（`TextMeshProLocalize` の UITK 版）、表示用の数値書式（InvariantCulture を明示。de 列があるので `ToString()` の `,` が実害になる）。
- **DTO は Guid とキーだけを運び、ビューが解決する。** ADR 0006 の原則は保ち、決定 5 の「Web 側で解決」を「ビュー側で解決」へ改める（§7）。
- **静的検査。** UXML の `text=` 属性に生の文字列を書くことを禁止する（`noJsxVisibleLiteral.test.ts` の置き換え）。全画面が辞書を通ることは `allScreensI18n` 相当で見る。

### 3.8 フォント

- UITK 用の TextCore FontAsset を Editor で作ってコミットする。和文は NotoSansJP-Medium、欧文は NotoSans-Regular、韓国語は NotoSansKR-Medium。既存の `Dependencies/Font/*SDF*.asset` は TMP 用の別の型なので、そのままでは使えない（input §4.3）。`FontDefinition.FromFont` はビットマップ描画で、拡大すると崩れるので使わない（PoC）。
- フォールバックの順はロケールごとのルートクラスで切り替える（Web の `data-locale` の代わり）。合成の太字は使わない。
- 韓国語は Web では OS のフォントへ落ちていた（推測）。UITK は自動では落ちないので、NotoSansKR を明示的にフォールバックへ入れる。
- 最小の文字寸法は §6 D7。
- 文中アイコンの SpriteAsset は内部 API に頼る（PoC）。構築と描画を確かめるテストを置き、Unity の版上げで最初に壊れる箇所として台帳に書く。

### 3.9 AI 向けの作法（議論の記録の未裁定論点への案）

- UXML と USS はテキストで持つ。UI Builder にしか無い状態を作らない。インラインの style は、計算で決まる幾何（位置・寸法）にだけ使う。
- `name` には Web の `data-testid` と同じ文字列を付ける（DSL とテストの照会キー）。アンカーは USS クラスで付ける（`name` とは別の契約。`docs/webui/anchor-convention.md` の「testid とアンカーは別」を継ぐ）。
- 見た目の自己確認は ScreenCapture 系か window モードの撮影で行う。**uloop screenshot の rendering モードには UITK が写らない**（PoC）。撮影にはフレーム番号か時刻の透かしを入れ、古いフレームを見ないようにする（shipping §6.2）。
- USS・UXML の変更はドメインリロードを起こさない（アセットのインポートだけ）。ビュー側の C# は薄く保つ。
- 規約違反は L2 の静的テストで止める。対象は、色の直書き、`:nth-child`、UXML の生文字、`name` の重複、層コンテナの picking、Web の feature への新規ファイル。

---

## 4. 検証の作り方

### 4.1 受入表（仕様の正本）

- P0 で、Playwright のテスト名 213 件と vitest の describe/it 名を機械的に抜き出す（ほぼすべて日本語の挙動文）。画面ごとに「前提（モデルの状態）・操作・期待（発行コマンドと表示）・由来（ファイル: テスト名）」の 4 列にする（tests §3）。
- 読み替えの目安（tests §2.1）:

| 種別 | 件数 | 残す | 行き先 |
|---|---:|---:|---|
| 画面フロー（P） | 213 | 約 205 | 受入表と L1 ビューテスト。うち約 52 件はレイアウトの主張なので、L1 のレイアウト照合か録画での目視へ |
| コンポーネント（C） | 235 | 約 200 | L1 ビューテスト |
| ロジック（L） | 311 | 約 290 | L0（C# の純ロジックと NUnit） |
| 多言語（I） | 61 | 約 25 | `Client.Localization` のテストと UXML の静的検査 |
| 静的規約（S） | 91 | 約 35（意図） | L2（USS・UXML を読む静的テスト） |
| 通信契約（W） | 167 | 0 | P6 で JSON アダプタと一緒に消す |

- **移した面の受入表の行が L0・L1 で緑になった PR で、その面の React の機能フォルダ・vitest・Playwright を消す。** 受入表の行と Unity 側のテスト名を 1 対 1 で結ぶ（台帳に由来の列を残す）。
- 前例: uGUI から Web へ移ったときも、「画面ごとの操作パリティ受け入れ表」と「全画面遷移のスモーク」を最終検証にしていた（`docs/webui/plans/phase-d-cutover.md:33-37`）。

### 4.2 テストの層

| 層 | 中身 | 道具 | PlayMode |
|---|---|---|---|
| L0 純ロジック | スロット計画、長押しクラフト、ビルドメニューの分類とスクロール追従、ビューポート計算、ルーティング表、所有表、通知ストア、ビューモデルの構築、コマンド | NUnit | 不要 |
| L1 ビュー | UXML・USS・Presenter を偽モデルと記録用コマンドでつなぎ、操作をシミュレートして、発行コマンド・クラス・テキスト・レイアウトを照合する | `UITestFixture` と `PanelSimulator` | 不要 |
| L2 静的規約 | USS・UXML をテキストとして読む（§3.9 の規約） | NUnit | 不要 |
| L3 結合 | ゲームを起動し、UI 状態の遷移・パネルの表示・コマンドのサーバー到達を見る | EditModeInPlayingTest。数は絞る | 必要 |
| L4 録画プレイテスト | 実際の入力経路と見た目 | プレイテスト DSL | 必要 |

Playwright は今 CI で走っていない（型検査だけ。`run_test.yml:53`）。L1 を CI の EditMode シャードに載せるので、移行後のほうが CI での検証面は広くなる。

### 4.3 EditMode の UI テスト（L1）

- **土台。** UI Test Framework 1.0.0（Unity 6000.3.8f1 に組み込み）を manifest に足し、`Client.Tests.asmdef` から `Unity.UI.TestFramework.Runtime` と `.Editor` を参照する。最初の 1 本を CI の batchmode で通すのは P1 のプローブ g。
- **本番に揃える。** `panelSize` は本番の基準（1280×720 か 1920×1080）、`themeStyleSheet` は本番の暗色テーマ、フォントは本番の FontAsset。文字の幅で決まるレイアウトがずれないようにするため。
- **mock-host を置き換える。** 画面ごとに `FakeXxxModel`（ReactiveProperty で組んだフィクスチャ）と `RecordingXxxCommands`（発行の記録）を用意する。Playwright の `setUiState` は偽の UI 状態の出どころに、`payloadsOf` は型付きの記録リストになる。
- **時間はパネルのスケジューラか、注入した時計で駆動する。** MonoBehaviour の `Update` と `Time.deltaTime` で動かすと、`FrameUpdateMs` で時間を進められない。長押しクラフト、通知の生存尺、ドラッグの閾値に効く。
- **描かれない部分は L4 で見る。** Editor パネルは描画しないので、半透明の濃さや文字のにじみは L4 の録画か ScreenCapture で確かめる。
- **罠**（tests §5.5。P1 で実測）: `MouseMove(from, to)` は途中の要素に PointerEnter を届けない可能性がある。通過したスロットを判定するテストは、1 スロットずつ動かす。

### 4.4 録画プレイテスト（L4）

- **DSL の付け替え（P1）。** `IUiElementLocator` を UITK 版と Web 版の 2 本で実装し、UITK を先に探す。UITK 版の流れは次のとおり（tests §6.2）。
  1. ゲームのパネルから `Q(name)` で探す
  2. 自分と祖先の `resolvedStyle`（display・visibility・opacity）と `worldBound` の面積で、可視かを判定する
  3. `panel.Pick(中心)` が対象かその子孫かで、覆われていないかを判定する
  4. パネル座標を画面座標へ変換し、SemanticInput で注入する
- **消えるもの。** WS の往復、「2 回続けて同じ矩形なら安定」の待ち、`CefInputForwarder`、`CefScreenMapper`、`PlaytestDomQuery`、`PlaytestUiOps.cs:134-138` の pointerOverUi の待ち合わせ。
- **改名は P1 で一度に行う。** シナリオ 27 本（約 143 か所）とスキル文書 4 本。後方互換の旧名は残さない（AGENTS.md）。
- **段階ごとの録画。** 移した面に触るシナリオを最低 1 本増やすか書き換える。1 本は、UITK が写っていることを透かし付きの画で確かめる。

### 4.5 アンカーの付け替え

- **ID の文字列は変えない。** マスタに直書きされているのは v8 で 9 件だけ（`challenge.current-hud` 1、`recipe.craft-button` 1、`research.node-<guid>` 7）。これに `itemViewHighLight` から作る `recipe.item-<itemId>` が加わる（tests §8）。ID を保てば、マスタも `.decisions/2026-08-19-チュートリアルUI指定はWebアンカーIDを直書きし変換も検証も持たない.md` もそのまま成り立つ。
- **語彙の正本は C# の定数クラス 1 か所**（静的 ID 19 個と動的 prefix 6 種を `anchorIds.ts` から移す）。`TutorialAnchorIdMapper` もここへまとめ、`tutorial_anchor_ids.json` のフィクスチャと突き合わせのテストは消す。
- **付け方。** `TutorialAnchor.Mark(VisualElement, params string[] ids)` の 1 か所で USS クラスとして付け、`AttachToPanelEvent`・`DetachFromPanelEvent` でレジストリに登録・解除する。静的なアンカーも UXML には書かず、C# でバインドするときに付ける。
- **解決。** 0 件なら missing、2 件以上なら duplicate、ほかに可視・面積・画面外を判定する。`DetachFromPanelEvent` で同期的に枠を消す（`tutorial.spec.ts` の「outline never outlives its anchor」）。位置は `GeometryChangedEvent` に加え、スクロールの移動を拾うため毎フレーム `worldBound` を見る。
- **共存中は両系がそれぞれ自分の木で探す**（tests §7.4）。両系で同時に解決したら R6 のエラーログ。系をまたぐ `uiDragGuide` は描けないので、移行中は未移行の面と移行済みの面をまたぐ dragGuide をマスタに足さない（現在 0 件）。
- **祖先クリップ。** 2026-08-20 のテスト方針（実 UI の経路と判定役で 1 本）を UITK で書き直す。見えている枠の矩形が、アンカーの `worldBound` とクリップする祖先との交差に一致することを照合する。祖先の overflow を公開 API で読めなければ、クリップする容器に印のクラスを付ける（tests §7.2-5）。

### 4.6 実機・IME・配布での確認

- IME（日本語の変換・確定・取消）は自動操作では確かめられない。P1（プローブ）、P2（検索欄）、P3（バグ報告・BP 名）、P4d（駅名）で、Mac と Windows を人が確かめる（§6 末尾のユーザー作業）。Windows は検証機の配布ビルドを使う。
- 各段階の配布ビルドは、moores-playtest-release の通し検証（Windows 検証機、Mac mini の Steam）を通してから playtest へ反映する。Mac の確認項目「Tab でインベントリが開く」は移行後も残る。

### 4.7 決定論ゲート

- **所有表**: どの組でも所有者はちょうど 1 つ（R2）。
- **凍結**: `web/features` への新規ファイルの追加を禁止する。
- **移した面の Web 実装の不在**: R3。
- **新規 uGUI の禁止**: `WebUiGateAuditTest` を残し、P6 で名前を変えて対象を付け替える。
- **台帳**: 3 軸の全行が分類済み。

前回、ゲートが分類を 1 本に畳まれて無効化した（`.decisions/2026-09-08-…`）。その反省から、「分類の粒度」と「禁止トークンの検査」の両方で守る。

---

## 5. 撤去の順序と完了の定義

### 5.1 撤去の順序

| 時期 | 外すもの | 順序の理由 |
|---|---|---|
| P0 | OneJS、`com.annulusgames.debug-ui` | どこからも参照されていない |
| P0 | 遠隔実行の Kestrel 依存 | Kestrel を消す前提条件。検証機の運用を壊さないよう、ほかの変更が無いうちに済ませる |
| P0 | Web ホスト内の状態（移設） | `Client.WebUiHost` を消す前提条件 |
| 各段階 | 移した面の React・vitest・Playwright・Web の document 全域のキー処理 | R3 |
| P5 | `web/features` の残り全部、Web の横断層 | Web が描くものを 0 にして 1 サイクル見る |
| P6a | Starter の起動配線 → 所有表・委譲・DSL の Web ロケータ・入力の論理和の Web の項 | 起動しなければ、残りは参照されないコードになる |
| P6b | `Client.WebUiHost` → C# の W テスト → `Client.Playtest/WebUi` → `moorestech_web`・CI の webui | 依存の上流から消す（host §6.3） |
| P6c | `jp.juha.cefunity` と prefab の CEF Canvas → bundler・LFS 検査・setup-cef・CI の CEF 取得・release の CEF 検査 | 配布物が変わる PR を 1 本にまとめ、検証機で 1 回確かめる |
| P6d | AspNetCore 系の NuGet | 遠隔実行の待受の決定（§6 D4）に従う |
| P6e | 文書・スキル・改名・ADR の改訂 | 実体が消えてから書く |

### 5.2 完了の定義（案）

議論の記録の案（CEF 削除、Kestrel ホスト削除、`Client.WebUiHost` と `moorestech_web` の撤去、`CefRuntimeBundler` の撤去）に、調査で見つかった抜けを足した。

1. `jp.juha.cefunity` が manifest に無く、`MainGameUI.prefab` に CEF の Canvas と RawImage が無い。
2. Kestrel が無い。遠隔実行は別の待受で動き、Windows 検証機で ADR 0072 の 3 条件（有効時に結果が返る、Harmony で差し込める、オプション無しでは開かない）を満たす。D4 で (b) を選んだ場合だけ、「遠隔実行専用の最小 Kestrel が残る」と読み替える。
3. `Client.WebUiHost`、`moorestech_web/`、`Client.Playtest/WebUi/*`、`WebUiActionInvoker`、C# の通信契約テストが無い。
4. `CefRuntimeBundler`、`CefPackageLfsValidator`、`scripts/setup-cef.*`、CI の CefUnity 事前取得と pnpm の step、`webui-test` ジョブと `needs_webui`、`release-artifact.sh` の CEF helper 検査とその契約テスト、README の CEF 確認が無い。
5. 共用部品が中立の場所にある。`CefLfsPointer` は改名し、`EnvironmentScrubOnEditorLoad` は `Client.ExternalProcess` 側へ移す（shipping §1.2）。
6. 共存の仕組みが無い。所有表、委譲、DSL の Web ロケータ、`UiPointerHitTest` と入力排他の Web の項。
7. Web という名前の残りを改名した。`WebUiInputExclusivity`、`WebPinId`、`SkitPresentationMode.WebUiEnabled`、`LocalizationKeys.Ui.Blueprint.WebModal*`、スキーマの `web anchor id`。
8. スキルと文書を置き換えた。`ref-webui-design` の後継、moores-code-review の ts/tsx レビュアーの撤去、`docs/webui` の archive 化、ADR の改訂（§7）。
9. ゲーム内の画面 UI は UITK だけになっている。uGUI は ADR 0052 の例外 2〜4（タイトルとロード、HP バー、デバッグ UI）だけが残る（§6 D12）。
10. CEF を含まない配布ビルドが、Windows 検証機の通し検証と Mac の Steam 確認を通り、playtest へ反映されている。
11. bd の CEF 関連の open 課題 12 件（shipping §1.2）が、閉じるか受入表へ転記されている。

完了の定義に入れないもの（別に判断する）: `com.unity.ugui` の撤去、`activeInputHandler` を InputSystem だけにすること、Mac の Universal 化、Linux クライアントの再開、タイトルとロードの UITK 化。

---

## 6. ユーザーの裁定が要る論点

「推奨」は agent の案。上の 5 つ（D1〜D3、D15、D16）は議論の記録が未裁定として挙げた論点。

| # | 論点 | 選択肢 | 推奨 | 推奨の理由 | 既存の裁定・ADR との関係 |
|---|---|---|---|---|---|
| D1 | 移行中の出荷形 | (a) strangler で master に入れ、毎週の playtest に混在した UI を出す (b) 移行ブランチに溜めて一括で入れる (c) master に入れるが、配布ビルドだけ Web に固定するフラグを持つ | (a) | 配布環境の差を早く浴び、段階ごとに止められる。(b) は約 2 万行が配布環境に触れないまま最後に出る。(c) は切替フラグが残骸になり（`SkitPresentationMode` の前例）、配布物で検証されない。帰結: テスターには混在した見た目が届き、CEF 由来の不具合は P6 まで続く（P3 の命綱で緩和する） | 議論の記録の agent 案と同じ |
| D2 | 移行中の新機能の行き先 | (a) Web を凍結する。新しい UI 機能は P1 の基盤ができた後に UITK でだけ作る。未移行の画面に機能が要るときは、その画面の移行を前倒しする (b) Web で作り続け、後で移す (c) 移行中は UI 機能を止める | (a) | (b) は二重作業になり、移行中の Web に負債が積もる。(c) はゲーム開発が止まる。帰結: P1 が終わるまで新しい UI 機能は待つ | `.decisions/2026-08-17-…独自調整へ移る.md`（Web 専任）は 10-18 の裁定で覆っている |
| D3 | 第一弾の画面 | (a) 入力の無い HUD（P1）→ 建築の束（P2） (b) 持ち物の束を最初に移す (c) ホットバーだけ移し、CEF から UITK へのドラッグの橋を作る (d) 議論の記録の「インベントリかホットバー」を単独で移す | (a) | 束を割らない。P1 で共存の物理を入力無しで確かめ、P2 で D&D・ツールチップ・IME・入力の取り合いを全部踏める。(b) は最大の束に未熟な基盤で挑むことになり長引く。(c) は捨てる橋を作る。(d) は束を割り、受入条件の一部が書けない（tests §6.4） | 議論の記録の「第一弾はインベントリかホットバー」から外れる（未裁定の案なので矛盾ではない） |
| D4 | 遠隔実行の待受 | (a) `HttpListener` へ載せ替え、AspNetCore 系の NuGet（`packages.config` 74 件中 47 件の大半）を外す (b) 遠隔実行専用の最小の Kestrel として残す (c) 専用の TCP 待受を作る | (a)。P0 で Mono の挙動を試し、駄目なら (b) | NuGet と Kestrel の保守面積が大きく減る。帰結 (b): Kestrel と NuGet が遠隔実行のためだけに残る | **ADR 0072 と矛盾する**（「入口は既存の Web UI サーバー（Kestrel）に 1 本足す」）。ADR 0072 の改訂が要る。トークン・起動オプション・結果の 7 種は変えない |
| D5 | 文言の差し込み記法 | (a) 名前付き `{name}` に統一し、生成するキーのメソッドに名前付きの引数を出す (b) 位置指定 `{pN}` に統一する（名前付きの 44 キーを書き換える） (c) C# で両方を解釈する | (a) | 翻訳者が文脈を読め、引数名をコンパイル時に照合できる。(c) は混在が負債として残る。波及: `{pN}` の 190 か所と C# の呼び出し側を一括で直す（AGENTS.md「変更の波及を恐れない」） | ADR 0006 と矛盾しない（補間記法は決めていない） |
| D6 | 見た目の正本と半透明 | (a) 機能とトークンを一致させる。α は UITK の実機で決め直し、トークン表に Web 列と UITK 列を持つ（出発点は α の近似補正） (b) Web の画面写真と画素を一致させる (c) パネルを sRGB の RenderTexture に描いて Gamma で合成する（PoC が要る） | (a) | Linear 合成と小さい文字はどうせ一致しない。画素パリティは前回足かせになった。(c) は合成がずれるという報告があり、工数が読めない | `.decisions/2026-08-17-uGUI移植を終了し…` の画素パリティ打ち切りの方針を継ぐ |
| D7 | 最小の文字寸法 | (a) P1 で 1280×720 の実画素を見て、12px 以下を 13px へ上げる (b) 据え置く (c) SDF の太さと縁を調整する | (a)（P1 の実測を見て確定） | 11〜12px の和文は Chrome より細い（PoC）。個数のバッジや流体量など、読めないと困る所に効く | なし |
| D8 | 画像アセット禁止の扱い | (a) 継ぐ。グラデとフェードは頂点カラーのメッシュ、アイコン（スキットの 6 種・×・ピン・矢印）は Painter2D のパスをコードで書く (b) SVG の VectorImage アセットを許す (c) テクスチャを焼くことも許す | (a) | テキストで差分が読め、アセットの取り込み工程が要らない。アイコンは 10 種ほど | `ref-webui-design` §6（装飾の画像アセット化の禁止）を継ぐ |
| D9 | アニメーション | (a) 薄い自作の層（100〜150 行、時計を注入） (b) LitMotion（MIT、UITK の Bind 25 種、手動ディスパッチャ） (c) PrimeTween | (a) | 要件は 3 系統だけで、EditMode で決定的に検証できる。外部の版上げに振り回されない。DOTween がすでにあるので、トゥイーンの系統をさらに増やさない。要件が増えたら (b) へ。(c) は再配布の制限があり、手動で進める API も確認できなかった | 裁定 10-18「導入または自作」の範囲内 |
| D10 | トークンの正本 | (a) USS を正本にし、C# は `customStyle` で読む (b) C# の定数から USS を生成する | (a) | AI がテキストを直接読み書きでき、生成物の同期が要らない | なし |
| D11 | Mantine の素の見た目のままの画面（トースト、チャレンジ、列車 HUD、ポーズのボタン） | (a) 移行のときにホワイトリストの語彙で様式化する (b) 見た目を再現する | (a) | Mantine の既定は再現の正本にならない。負債 `moorestech-cgm` も解消できる。トーストは、残す用途（バグ報告の結果・コマンドの失敗）を決めてから作る | ADR 0014 の「他の Button は今回触らない」を、移行の範囲で解く |
| D12 | タイトルとロード画面 | (a) 今回の完了の定義に含めない。メインメニューの作り変えのときに UITK で作る (b) 今回の移行に含める | (a) | 範囲を広げない。ただし uGUI のまま残した理由（CEF はゲーム内でしか起動できない）は消えるので、作り変えのタスクに「UITK で作る」と書く。帰結: uGUI と TMP は残る | `.decisions/2026-09-05-メインメニューとロード画面はuGUI現状維持.md` と矛盾しない（「UI TK 化はあとから考える」） |
| D13 | Mac の Universal 化と Linux クライアント | (a) 完了の後に別に判断する (b) 今回に含める | (a) | ffmpeg も arm64 しか無いので、CEF を消しても自動では解けない（shipping §1.3） | ADR 0071 は根拠を CEF としているが、実態は ffmpeg にも縛られている |
| D14 | 持ち物の束の粒度 | (a) 所有表を「画面 × ブロック種別の族」で引き、5 つの小段階で出荷する（Web 版の InventoryPanel は 4d まで残す） (b) 掴みの束を 1 本の PR（長命ブランチ）でまとめて入れる | (a) | いつ止めても出荷できる。帰結: 4a〜4d の途中で止めると、InventoryPanel の二重実装が負債として残る。(b) は、出荷できない期間が最大の段階で長くなる | なし |
| D15 | 完了の定義 | §5.2 の 11 項目 | §5.2 | 議論の記録の案は遠隔実行（ADR 0072）とぶつかり、共用部品・DSL・共存機構の撤去が抜けている | 議論の記録の「Kestrel ホスト削除」は D4 次第で修正が要る |
| D16 | 仕様の正本と AI 向けの作法 | (a) 受入表（§4.1）を正本にし、面を移す PR で Web のテストを消す。作法は §3.9 (b) Web のテストを移行が終わるまで保つ | (a) | 面を消した後の Web のテストは走らない。受入表と Unity 側のテスト名を 1 対 1 で結べば、仕様は消えない | 議論の記録の「見た目は uloop screenshot で自己確認」は、PoC の実測（rendering モードには UITK が写らない）と矛盾する。ScreenCapture 系か window モードへ改める |

**agent の既定で進め、台帳に記録する細部**（大きく外れたら裁定に上げる）: UI の演出の時間源は実時間、`name` は testid と同じ文字列、アンカーは USS クラス、パネルは 1 枚、UITK は CEF の上（P1 の実測で可能なら）、PanelSettings は 1280×720 の Expand、入力欄の ESC はフォーカスを外すだけ、入力欄以外は `focusable=false`、UI のパッド操作は作らない、旧 UITK の SkitUI は撤去して作り直す（input §5.2）。

**ユーザーにしてもらう作業**: 日本語 IME の手動確認（P1・P2・P3・P4d の入力欄。Mac と Windows 検証機）。各段階の配布ビルドを playtest へ反映する操作（既存の手順のまま）。

---

## 7. 既存の裁定・ADR との関係

| 裁定・ADR | この計画での扱い | 矛盾するか |
|---|---|---|
| ADR 0072（遠隔実行の入口は Web UI の Kestrel） | P0 で待受を切り離す | **矛盾する**。改訂が要る（D4） |
| ADR 0006 決定 5（マスタ由来の表示名は Web の辞書で解決） | 「ビュー側（UITK）で解決」へ改める。DTO が Guid だけを運ぶ原則は保つ | **改訂が要る**（表現の更新） |
| `.decisions/2026-08-19-獲得通知の集約はWebUI側で行う.md` | 集約は C# の通知のビューモデルが持つ。「集約は表示側の状態」という理由は保つ | **置き場所の書き換えが要る** |
| ADR 0019・`.decisions/2026-08-19-カーソルツールチップの書式はWeb側が持つ.md` | 書式は UITK 側のトークンが持つ（wire に寸法を載せない原則は保つ） | 表現の更新 |
| ADR 0013・0017（stage 族と viewport 族、z の層トークン） | 原則は保つ。層は z-index の代わりに層 enum と兄弟順で表す | 実装手段の更新 |
| ADR 0016（通知の出入り、`getAnimations` での e2e の同期） | 演出と生存尺の単一の正は保つ。検証は L1 と時計の注入へ | 実装手段の更新 |
| ADR 0021（テキスト選択は入力欄だけ） | UITK の Label は既定で選択できないので、既定の挙動が裁定どおりになる | P6 で「置き換え済み」と記録 |
| ADR 0052 例外 1（CEF の描画面） | P6 で消える。agent 前提 2 のゲートは名前を変えて残す | 例外が 1 つ減る（矛盾ではない） |
| ADR 0052 例外 2〜4、`.decisions/2026-09-05-メインメニューとロード画面はuGUI現状維持.md` | 今回は触らない（D12） | 矛盾しない |
| `.decisions/2026-08-18-チュートリアル提示はWebUI経路に統一しD&Dは矢印ループで示す.md` | 「UI の描画系の経路に統一」と読み替える。共存中は両系が自分の木のアンカーを描き、P5 で UITK だけになる | 表現の更新 |
| `.decisions/2026-08-19-チュートリアルUI指定はWebアンカーIDを直書きし変換も検証も持たない.md` | ID の文字列を保つので、そのまま成り立つ。スキーマの既定文字列 `web anchor id` だけ直す | 矛盾しない |
| `.decisions/2026-08-26-Web操作音はC#へsound.playを送って鳴らす.md` | ビューから直接鳴らす | 経路の更新 |
| `.decisions/2026-08-23-操作ヒントの正はC#のUIStateに置きtopicで配る.md` | C# が正本のまま。topic の代わりに購読になる | 矛盾しない |
| `.decisions/2026-10-01-Web UI全体で文字入力欄以外のSpace既定動作を封じる.md` ほか 10-01 の入力系の裁定 | UITK でも受入表として同じ挙動を守る（§3.6） | 矛盾しない |
| `.decisions/2026-09-13-CEF raw input応急処置は入れず…` | P6 で前提ごと消える | 消滅 |
| `.decisions/2026-09-09-WebSocketHubをレジストリの単一所有者にする.md` | P6 で Hub ごと消える | 消滅 |
| `.decisions/2026-09-05-uGUI撤去は抽出PRと削除PRの2本に分ける.md` | 同じ型を使う（P0 の抽出、P6 の削除） | 継承 |
| 議論の記録「見た目は uloop screenshot で自己確認」 | ScreenCapture 系か window モードへ改める | **矛盾する**（PoC の実測による） |
| 議論の記録「完了の定義に Kestrel 削除」 | D4 の結果に合わせる | **矛盾しうる**（ADR 0072） |
| AGENTS.md「イベント発火に Action を使わない」 | P0 で `UIStateControl.OnStateChanged` を直す | 既存の違反を解消する |

---

## 8. 推測・未実測の一覧（P1 のプローブで潰す）

| 項目 | 確かめる段階 |
|---|---|
| uGUI の Overlay Canvas（CEF）と UITK パネルの描画順を sortingOrder で決められるか | P1-a |
| Recorder・PlaytestScreenshot・バグ報告の動画に UITK が写るか | P1-b |
| InputSystem の注入が UITK に届くか、パネル座標から画面座標への変換 | P1-c |
| `IsPointerOverGameObject` が UITK を拾うか | P1-d |
| Navigate・Submit・Tab の無効化 | P1-e |
| TextField 上の ESC の処理順 | P1-f |
| UI Test Framework が CI の batchmode で動くか、アニメがシミュレーション時間で進むか、Editor パネルと PlayMode のレイアウトの一致 | P1-g |
| CEF の常時 IME と UITK の TextField が取り合うか、日本語 IME | P1-h（人の手作業） |
| viewport 族の二重基準を PanelSettings 1 枚で表せるか（Expand の挙動） | P1 の基盤 |
| GamePanel の 4 辺フェードを頂点カラーのメッシュで描けるか | P1 の基盤 |
| 祖先の overflow を公開 API で読めるか | P1 のアンカー |
| `HttpListener` の Mono での挙動 | P0-2 |
| `Microsoft.Extensions.*` を AspNetCore 以外が使っているか | P6d の前 |
| スロットの多い画面の UITK の CPU 時間 | P2・P4 |
| タイプライタの折り返しを、残りを透明にすることで固定できるか | P5 |
| 各段階の行数・ファイル数（本書の規模はすべて推測） | 各段階の plan |
