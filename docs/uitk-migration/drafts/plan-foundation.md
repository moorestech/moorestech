# UI Toolkit 全面移行 計画案 — 基盤先行・一括移植

作成 2026-10-22。計画の案で、裁定ではない。repo・bd・git には何も書いていない（読むだけ）。

- **前提として読んだもの**
  - 裁定 `.decisions/2026-10-18-CEFを撤去しUI-Toolkitへ全面移行する.md`、議論 `docs/webui/ui-toolkit-migration-discussion-2026-10-18.md`
  - 関連裁定: ADR 0006・0013〜0021・0052・0071・0072、`.decisions/2026-09-05-*`（uGUI 撤去群）、2026-08-19（アンカー直書き・通知集約）、2026-09-08（uGUI ゲート）、2026-10-01（Space 封じ）、2026-10-18（液体 UI は WebUI に足す）、`docs/webui/MIGRATION.md`
  - PoC 報告 `docs/uitk-migration/poc/hard-cases/index.html`（2026-10-22）
  - 調査 5 本: `understand-screens.md`・`understand-host.md`・`understand-input.md`・`understand-tests.md`・`understand-shipping.md`。以下では「screens §x」「host §x」のように略す
- **記法**
  - パスは repo 相対で書く。`Host/` は `moorestech_client/Assets/Scripts/Client.WebUiHost/`、`web/` は `moorestech_web/webui/src/` を指す
  - 「推測」と書いた所は実測していない。**規模・期間の数字はすべて推測**

---

## 0. 結論

1. **共存は画面単位でなく「UI スタック丸ごとの切替」にする。**
   - 起動オプションで Web か UITK かを選ぶ。切替日までの既定は Web
   - UITK の画面は、既定 OFF（暗転）のまま master へ順次マージする
   - テスターに混在した UI は一度も届かない
   - 画面単位の strangler で要る橋はどれも作らない（入力の裁き、重なり順、系をまたぐ D&D、アンカー矩形、DSL の二重ロケータ）
2. **段階は 6 つ。**
   - P0 準備（裁定・実測・台帳・受入表・凍結）
   - P1 地ならし（描画系に依存しない C# の抽出と、純ロジックの移植。挙動は変えない）
   - P2 UITK 基盤と実証画面
   - P3 残り全画面の並列移植（3 波）
   - P4 既定の切替と実機確認
   - P5 撤去
3. **並列度の律速は 2 つ。** Editor の本数（Mac mini で同時 3 本が目安）と、ユーザーのマージ。
   - 共有ファイルは integrator 1 人が直列で持つ
   - P2 の最後に全画面の「殻」を置き、P3 の各エージェントが共有ファイルに触れずに済む形にする
4. **P0 で裁定が要る論点は 6 つ（§6.1）。** 共存の形、遠隔実行の待受、Web 凍結と新機能の行き先、i18n の差し込み記法、完了の定義、範囲。
   - 遠隔実行の待受は ADR 0072 と矛盾するので、改訂が要る

---

## 1. 方針

### 1.1 要約

- **移植の前に、UITK 側の基盤を作り込む。** 中身は次のとおり。
  - デザイントークン USS、暗色テーマ、フォント
  - 層マネージャと画面ルータ
  - 入力とフォーカス、ローカライズの結線
  - 共通部品、横断層（ツールチップ・D&D・モーダル・トースト・通知）
  - アニメーション基盤、EditMode の UI テスト基盤
  - データ購読層、プレイテスト DSL の UITK 版
- **基盤は実証画面（パイロット）を作りながら固める。** パイロットは HUD 群・ホットバー・インベントリ一式（装備・掴み・ツールチップ）・通知・BP 名入力モーダル。最難所（掴み、D&D、ツールチップ、IME、アニメ、層、アンカー）をパイロットで全部踏む。
- **その上で残りの 13 単位を一気に移す。** 3 波に分け、同時 5〜6 エージェントで進める。
- **切り替えは 3 段で進める。**
  1. 全画面がそろったら既定を UITK に切り替える
  2. その後 1 プレイテスト周期だけ Web を退避路として残す
  3. 周期が明けたら Web 一式を撤去する

### 1.2 この方針を選ぶ理由

1. **共存のコストは、共存の期間と粒度に比例する。** 画面単位で共存させると、次をすべて作る必要がある。
   - 入力の二重配送の裁き。cef-unity は RawImage 全面で入力を CEF へ送り、領域単位で止める API が無い（host §7.3、input §3.7）
   - CEF Canvas と UITK パネルの重なり順（未実測。host §7.4）
   - 束 A〜D の拘束。描画系をまたぐ D&D と掴みは成立しない（screens §7.1）
   - アンカー矩形の橋（screens §7.1 C）
   - DSL の二重ロケータ（tests §6.4）
   - Gamma と Linear の見た目の混在をテスターへ出すこと（shipping §1.5）

   スタックを丸ごと切り替えれば、どれも作らずに済む。
2. **多数の AI エージェントを並列で回すには、画面を独立した作業単位にする必要がある。**
   - 共通部品 23 項目のうち、トークン・層・i18n・アンカーは全機能か十数機能が使う。GamePanel は 8 機能、ボタン類は 10 機能が使う（screens §6）
   - 画面ごとに部品を作らせると、同じものが別々の形で何度も作られる
   - 基盤と部品カタログを先に作り、規約テストで縛れば、画面の移植は「殻の中を埋める」並列作業になる
3. **Web 版は切替日まで、動く参照実装として残る。** 各エージェントは TS/CSS と vitest/Playwright を仕様として読める。比較用の画面も Web モードで撮れる。
4. **master へ暗転のまま入れられる。** 長命ブランチが要らないので、並行セッションとの衝突が小さい。テスターの体験は切替日まで変わらない。

### 1.3 捨てるもの（この方針の代償）

| 捨てるもの | 内容と緩和策 |
|---|---|
| 画面ごとの段階的なテスター公開と、そのフィードバック | UITK がテスターへ届くのは P4 になる |
| 早い段階の目に見える進捗 | P1〜P3 の成果はテスターから見えない |
| 一括切替のリスクを避けること | 緩和策は 4 つ: パイロットでの早期統合、UITK モードで録画シナリオ全本、起動オプションでの実機事前確認、切替後 1 周期の退避路 |
| 移行中の Web 側の改善 | P0 以降、Web は新機能を凍結する。P3 中に Web へバグ修正が入った場合は、UITK 側にも入れる二重作業になる |
| 画素単位のパリティ | 一致させるのは機能・トークン・層だけ。前例として、2026-08-17 にパリティ目標が足かせになり打ち切った（`.decisions/2026-08-17-uGUI移植を終了しWeb-UIは独自調整へ移る.md`） |
| 束単位 strangler のための橋 | アンカー矩形の受け渡し、系をまたぐ掴み、二重ロケータは作らない |

### 1.4 範囲外（既存の裁定のまま。§6.1 D6 で確認する）

- **MainMenu とローディング**: uGUI のまま維持する（`.decisions/2026-09-05-メインメニューとロード画面はuGUI現状維持.md`、ADR 0052 例外 2）。
  - uGUI に残した理由（CEF の起動位置）は消える
  - それでも作り変えは別タスク（`.decisions/2026-09-13-配布ビルドのメインメニューは作り変え開発者向け操作は別途検討する.md`）なので、UITK 化もそこで行う
- **mapObject の HP バーとデバッグ UI**: uGUI のまま（ADR 0052 例外 3・4）。`com.unity.ugui` も残す。
- **Editor 拡張、プレイテストの操作オーバーレイ**: どちらも IMGUI なので対象外。
- **Mac の Universal 化と Linux クライアントの再開**: 撤去後に別途裁定する。ffmpeg が arm64 のみなので、CEF を外しても arm64 固定は解けない（shipping §1.3）。
- **Mod から UI を足す仕組み**: 現状どこにも無い（shipping §4）。Mod 向けの UI 契約として実在するのは、アンカー ID・アイコン・CSV の 3 つだけで、これらは保つ。

---

## 2. 段階

### 2.0 全体表

| 段階 | 目的 | 出荷できるか | 同時エージェント（うち Editor） | 規模（推測） | 期間の目安（推測） |
|---|---|---|---|---|---|
| P0 準備 | 裁定・実測・台帳・受入表・凍結 | できる（挙動不変） | 4〜5（3）＋人手の IME 確認 | 文書 15 前後、スクリプト約 300 行、使い捨てスパイク約 1.5k 行（master に入れない） | 1 週 |
| P1 地ならし | 描画系に依存しない C# の抽出と、純ロジックの移植 | できる（UI は Web のまま） | 6〜8（3） | 変更 180〜250 ファイル。移動 6〜8k 行、新規 5〜6k 行、テスト 3〜4k 行 | 2 週 |
| P2 基盤 | UITK 基盤、実証画面、殻 | できる（UITK は起動オプション時だけ） | 3〜4（3）＋integrator | 新規 120〜170 ファイル。C# 7〜9k 行、USS 1.2〜1.8k 行、テスト 2〜3k 行 | 2〜3 週 |
| P3 一括移植 | 残り全画面 | できる（同上） | 5〜6（3〜4）＋integrator | 新規 250〜350 ファイル。C# 9〜12k 行、USS 1.5〜2.5k 行、L1 テスト約 400 件 | 2〜3 週 |
| P4 切替 | 既定を UITK にし、実機で確認し、1 周期観察する | UITK を既定にして出荷 | 1〜2＋人手 | 修正 PR 数本 | 1〜2 週（プレイテスト周期） |
| P5 撤去 | CEF・Web・橋渡し・切替を消す | できる（PR ごと） | 2〜3（直列寄り） | 削除約 45〜50k 行、変更 30〜50 ファイル | 1 週 |

- 合計の目安は 9〜12 週（推測）。
- PR は全体で 45〜50 本の見込み（推測）。moorestech は PR 作成までで止めるので、ユーザーのマージが律速になりうる（§2.7）。

### 2.1 P0 準備（裁定・実測・台帳・凍結）

**目的**: 計画の前提を実測で固め、裁定を取る。並列作業の土台として、台帳・受入表・凍結ゲートを作る。

**作業項目**

| ID | 作業 | 中身 |
|---|---|---|
| P0-1 | 裁定と入口文書 | §6.1 の 6 論点を moores-grill-with-docs で裁定し、ADR と `.decisions/` に残す。旧 `docs/webui/MIGRATION.md` の型（方針・完了の定義・段階と依存・並行運用ルール）で、入口文書 `docs/uitk/MIGRATION.md` と `TODO.md`、`disposition.md` を作る |
| P0-2 | 実測スパイク S1〜S13 | 下表。使い捨ての隔離 worktree で行い、結果は報告ファイルに残す |
| P0-3 | 台帳（3 軸） | 棚卸しの軸は 3 本: (a) `web/features/*` の 20 機能と `shared/`、(b) `web/app/App.tsx` のマウント点・Portal 層・状態の外のオーバーレイ（常駐 HUD、Ctrl+U、BG スキット、カットシーン退避、カーソル追従、ワールドピン）、(c) C# の topic 28・action 44・アンカー（静的 19、動的 prefix 6）・シナリオが使う testid 約 45。全項目を「済／吸収先／除外（理由）」に分類し、未分類は欠陥として扱う。軸を 3 本にするのは、状態機械だけで洗い出してオーバーレイを見落とした失敗の対策（shipping §6.2） |
| P0-4 | 受入表の機械抽出 | Playwright のテスト名 213 件と、vitest の describe/it 名を抜き出す。画面ごとに「前提・操作・期待・由来」の 4 列の表を作る（tests §3、§4.1） |
| P0-5 | 参照キャプチャ | Web 版の全画面と主要状態を撮る（Playwright の mock-host と PlayMode）。通知・脈動・ドラッグガイドは短い動画で残す。置き場は repo の外（`../moorestech_logs` か share）にし、台帳からリンクする。P5 で Web が消えた後も「意図した見た目」を辿れるようにするため（2026-08-17 の「参照すべき実物がもう無い」の再発防止） |
| P0-6 | 凍結 | 凍結ゲート（`web/` への新規ファイル追加を落とす決定論テスト。許可リスト付き）と運用ルールを入れる。進行中の Web PR は完了させてから凍結する。`git log --since=2026-10-15 -- moorestech_web` は 44 コミットあり、画質設定などが進行中 |
| P0-7 | 掃除 PR | OneJS を外す（manifest と lock から 1 行ずつ。参照 0 件。shipping §5）。`com.annulusgames.debug-ui`（参照 0 件）も同じ PR で外してよい |
| P0-8 | bd | epic と、段階・単位ごとの子 issue を起こす。派生は `--parent` で系譜を残す |

**実測スパイク**（どれも未実測。各スパイクは 1 日で打ち切り、「不可」も結果として記録する）

| # | 確かめること | 判定に使う観測 | 不可だったときの手 |
|---|---|---|---|
| S1 | UI Test Framework（`com.unity.ui.test-framework` 1.0.0。組み込み済みだが manifest には未記載）を入れ、`UITestFixture` のテスト 1 本がローカルと CI（game-ci の batchmode）の両方で緑になるか | CI の Test Results | L1 はローカルの Editor レーンだけで回す。P3 の並列度は Editor の本数に縛られる |
| S2 | InputSystem への注入（`SemanticInput` の QueueStateEvent）が `InputSystemUIInputModule` を経て UITK に届くか。パネル座標と画面座標の往復変換。`IsPointerOverGameObject` が UITK を拾うか | クリック位置と、受け取った要素のログ | DSL が UITK のイベントを直接合成する（推測） |
| S3 | Unity Recorder（`GameViewInputSettings`。`Client.Playtest/Recording/PlaytestRecorder.cs:33`）とバグ報告の動画（`ScreenFrameReader.cs:24`）に UITK が写るか | 録画 1 本、バグ報告 1 本 | 録画を ScreenCapture の連番へ切り替える |
| S4 | 専用の UI アクション資産で、WASD・矢印の Navigate、Space・Enter の Submit、Tab のフォーカス移動を止められるか（2026-10-01 の Space 裁定の再現） | 注入後のフォーカスと、クリックの発火 | `focusable=false` を徹底し、root で NavigationSubmitEvent を打ち消す |
| S5 | TextField の上で ESC を押したとき、EventSystem と `UIStateControl` のどちらが先に走るか | 同じフレームのログの順序 | ESC ラッチを置く場所を決める |
| S6 | PanelSettings（Scale With Screen Size、1280×720、Expand）の倍率が `min(w/1280, h/720)` と一致するか。16:9・16:10・21:9・720p・4K で見る | `resolvedStyle` と `Screen` の値 | 倍率を C# で計算して scale を当てる |
| S7 | Linear 色空間での半透明を 3 案（実機で決め直す、sRGB の RT で合成する、α を補正する）で比べる。10〜13px の文字を 720p・1080p・4K で比べる | Web と UITK の ScreenCapture を並べる | §6.2 の D9・D10 の材料にする |
| S8 | transition、`experimental.animation`、schedule が UI Test Framework の `FrameUpdateMs` で進むか | L1 で opacity と translate の値 | 自作 UiMotion の時計を差し込んで、決定的に回す |
| S9 | **日本語 IME**（Windows 検証機と Mac）。単行・複数行の TextField で変換・確定・取消・Esc を試し、フォーカスを往復した後に入力が戻るかも見る | 人の目視と動画 | **赤信号**。Unity の版・設定を見直すか、composition 表示を自作する作業を P2 に足す |
| S10 | TextCore の SDF FontAsset を Editor で作ってコミットできるか。NotoSansKR へのフォールバック | 日本語と韓国語の表示 | PoC と同じく実行時に生成する |
| S11 | SVG の path 文字列を Painter2D に流してアイコンを描けるか（画像アセット禁止を保つ場合） | skit のアイコン 6 種と × | VectorImage を許すか、D8 で裁定する |
| S12 | 純ロジックを Unity の外で `dotnet test` できるか。engine に依存しない asmdef と csproj を対応させる（前例: `mooresmaster/mooresmaster.Tests`） | 1 モジュールで緑 | Editor のレーンだけで回す（任意） |
| S13 | UITK モードで CEF を起動しないで済むか。CEF の子オブジェクトを既定で非アクティブにしたとき、cef-unity-server が spawn されないか | プロセス一覧 | 配線する位置を Awake より前へ移す |

**完了条件（観測できる形）**

- §6.1 の裁定が ADR と `.decisions/` にある。
- スパイク 13 本の報告がそろっている。各報告は「可／条件付き／不可」に、証拠（ログの行・スクショ・数値）を添える。
- 台帳の未分類が 0（スクリプトで数える）。
- 受入表が 20 機能分あり、行数が抽出元（Playwright のテスト名と、対象にした vitest の件数）と一致する。
- 凍結ゲートが master で緑。わざと新規ファイルを足すと赤になる（ゲート自体を検査するテスト）。
- OneJS 撤去後のビルドで、`Plugins/x86_64` に `quickjs_unity.dll` が無い。

**出荷**: できる（挙動は変わらない）。

**リスクと潰し方**

| リスク | 潰し方 |
|---|---|
| S9 で IME が使えない | 移行全体の前提が崩れるので、ユーザーへすぐ上げる |
| S1 で CI が通らない | L1 はローカルだけで回す。P3 の並列度を下げた計画に組み直す |
| S3 で録画に写らない | 録画の方式を変える |
| スパイクが長引く | 1 本 1 日で打ち切る |

**規模・並列**: スパイクは同時 3 本（Editor の予算）。台帳と受入表は 2〜3 エージェントで回し、Editor は要らない。IME は人手で確かめる。

### 2.2 P1 地ならし（描画系に依存しない C# の抽出と移植。挙動は変えない）

**目的**: UITK が乗る C# の土台を、Web を壊さずに先に作る。Hub が無くても UI の状態が生きている状態にする。ADR 0052 の「抽出 PR と削除 PR」の型のうち、抽出側にあたる。

**作業単位**（ファイルが重ならない単位。同時 6〜8）

| 単位 | 中身 |
|---|---|
| P1-a 遠隔実行の分離 | `/api/remote-exec` を `Host/Boot/WebUiEndpoints.cs:97-101` から外し、D2 の裁定どおりの待受へ移す。推奨は `Client.RemoteExec` 専用の最小 Kestrel。起動位置は `InitializeScenePipeline.cs:68-73` のまま。`RemoteExecEndpointTest` と `PlaytestBootUnityCallbackBindingTest` を直す |
| P1-b 研究・チャレンジ | `ResearchTopic._nodeStates` と `ChallengeTopicState` を `Client.Game` の DI 登録へ移す。topic を「ViewModel＋変化通知」と「JSON アダプタ」に分け、`research.complete` を型付きコマンドにする |
| P1-c ブロック詳細 | `BlockNetworkInfoCache`、`BlockInventoryTopic.TrackBlock`、`OpenTrainTimetableTracker`・`TrainTimetableFetcher`、DtoBuilder 群、`block_inventory.*`・`machine_recipe`・`filter_splitter`・`electric_to_gear`・`train_*` の各 action |
| P1-d インベントリ・建築 | inventory、hotbar、build_menu、placement_mode、crafting.*、recipe_viewer の topic と action |
| P1-e UI 状態と単発のもの | ui_state（`UIStateControl.OnStateChanged` を UniRx 化する。今は `event Action` で、AGENTS.md に反する。`UIStateControl.cs:16`）、モーダル（`WebUiModalService` の static Instance を解消）、`BlueprintNameInputWebBridge`、通知の連番と移行告知、出展モードの言語ゲート（DI より前の起動ゲートとして `Client.Starter` 側へ）、pause_menu、settings、localization、skit、tutorial、HUD 系の小さな topic |
| P1-f 画面ルーティング表 | `web/shared/uiState/uiScreenRouting.ts` の SCREEN_TRAITS を C# の純データへ写し、vitest の 9＋15 件を NUnit に移す |
| P1-g〜n 純ロジックの移植（8 単位） | Web にしか無い約 4,000 行の純関数を、vitest を仕様にして C# へ 1 対 1 で移す（L0 約 290 件。host §5.3）。単位は 8 つ: ①itemMove・splitDrag・slotActions の束縛による絞り込み・hotbarDnd の判定、②craftLogic・holdCraftLogic・recipeSlotLayout、③researchLogic・treeView のビューポートと慣性の式、④buildMenu の分類・検索・不足判定・スクロールスパイの計算、⑤detailLogic・beltThroughput・machineSlotGhosts・machineRecipeSelection・timetableEdit・filterSplitter、⑥notificationMessages（約 70 件の文言表）と集約、⑦tooltipPosition・slotAmountFormat・materialTooltipText・placementTarget・ownedCounts・equipmentLogic、⑧言語選択の状態機械と anchorIds の定数 |
| P1-o ローカライズ | D4 の裁定どおりに差し込み記法を統一する（推奨は名前付き＋型付き書式メソッドの生成）。数値書式（InvariantCulture）、辞書が届く前の文言を C# の固定文言へ移すことも含む |
| P1-p 共用部品と入力 | `CefLfsPointer` を中立名へ改名、`EnvironmentScrubOnEditorLoad` を `Client.ExternalProcess` へ移設、`WebUiInputExclusivity` を `UiInputExclusivity` へ改名（値の出どころを複数から合成する形）、ESC ラッチの口だけ用意する |

- DI 登録は単位ごとの Installer ファイルの中に閉じる。composition root への 1 行の追加だけを integrator が束ねる。

**完了条件**

- Web 側の検証がすべて緑で、挙動は変わっていない。対象は vitest、ローカルの Playwright、C# の WebUi 系テスト、代表の録画シナリオ数本。
- Hub が null（`WebUiGameBinder.Bind` を通らない）でも、研究・チャレンジ・ブロック詳細・時刻表・モーダル・通知の状態が DI から取れる。EditMode テストで確かめる（今は `Host/Game/WebUiGameBinder.cs:48-55` で丸ごとスキップされる）。
- 遠隔実行が `Client.WebUiHost` を起動しなくても動く（テストで確かめる）。
- 移植した L0 が NUnit で緑。台帳に「vitest のファイル → C# のテスト」の対応と件数を記録する。

**出荷**: できる。

**リスクと潰し方**

| リスク | 潰し方 |
|---|---|
| 抽出で挙動が変わる | 抽出 PR は「移動と配線」だけにし、ロジックの変更を混ぜない |
| 並行開発との衝突（液体 UI などが `BlockInventoryTopic` を触っている） | 凍結を入れたうえで、topic 群ごとに短い作業窓を bd で告知する |
| フレーム末に畳む処理の意味が変わる | 既存の Topic テストを残して守る |
| 移植したロジックが P2・P3 まで使われない | 台帳で追跡し、凍結中に Web 側で起きた修正を同期する |

**規模・並列**: 単位は 16、同時 6〜8。S12 が成立すれば、純ロジックの移植を Editor の本数から切り離せる。

### 2.3 P2 UITK 基盤と実証画面（パイロット）

**目的**: 全画面が乗る基盤を完成させ、パイロット画面で使えることを証明する。P3 のエージェントが共有ファイルに触れずに済むよう、殻まで作る。

**作業単位**（着手の順序: a → b・c・f を並行 → d・e → g → h）

| 単位 | 中身 | 所有 |
|---|---|---|
| P2-a スタック切替・パネル・層・ルータ | `UiStackSelection`（§3.1）、`GameUiRoot`（MainGameUI.prefab に事前配置。uloop 経由）、PanelSettings、層 enum と層コンテナ、ルータ（P1-f の表を使う）、Ctrl+U とカットシーン中の全非表示、UITK モードで Kestrel・WS・CEF を起動しない配線 | integrator |
| P2-b テーマ | Tokens.uss（約 238 個）、Theme.uss（ランタイムの既定コントロールを暗色化）、Typography.uss、SDF FontAsset とフォールバック、ロケール別の root クラス | 1 |
| P2-c 入力 | UI アクション資産、`UiPointerHitTest` の Pick 化、ホイールの素通し、テキストフォーカスの追跡、ESC ラッチ、ゲーム層のキー処理の枠 | 1 |
| P2-d 横断層とアニメ | ツールチップ（Hover と Cursor）、D&D と掴み、モーダル、トースト、通知、全画面ゲート、チュートリアル層の器、UiMotion | 1 |
| P2-e 部品カタログ | §3.3 の部品。GamePanel とスロット系を先に作り、その後 2 人に分けてよい | 1〜2 |
| P2-f テスト基盤と DSL | UI Test Framework の導入、UiTestHost、Fake モデルと Recording コマンドの雛形、L2 の規約テスト、アンカーの登録簿・リゾルバ・カバレッジテスト。DSL は `IUiElementLocator` を入れ、`ClickWebUi` 系を `ClickUi` 系へ改名する。シナリオ 27 本とスキル文書 4 本も同じ PR で直す | 1 |
| P2-g パイロット画面 | HUD 群（commonHud、keyHint、progress、modeHud の斜線帯、trainHud、チャレンジの目標 HUD）、ホットバー（表示・選択・ドロップ先の登録）、インベントリ一式（InventoryPanel、EquipmentPanel のホイール、GrabOverlay、ツールチップ、分割ドラッグ）、通知、BP 名入力モーダル | 2 |
| P2-h 殻と規約 | 殻を全画面ぶん作る（空の View・Presenter・UXML・USS、ルータへの登録、テストクラスの雛形、受入表へのリンク）。新スキル `ref-uitk-design`、UITK 版の部品カタログ、moores-code-review の UITK 観点も作る | integrator |

**完了条件**

- UITK モードで起動し、パイロット画面が録画プレイテストで動く。例: 「インベントリを開き、アイテムを掴んで別スロットへ置くと、ツールチップが出る」「BP 名入力モーダルを開いて閉じる」。日本語入力は人手で確かめる。
- パイロットの受入表の全行が、L0・L1・L4・目視のいずれかで緑。
- L2 の規約テストが緑。
- 1280×720、1920×1080、3840×2160、21:9 の ScreenCapture を証跡として残す。
- UITK モードでは、Kestrel も cef-unity-server も起動しない（プロセス一覧とログで確認）。遠隔実行は使える。
- Web モード（既定）では、既存のテストがすべて緑。
- 全画面の殻がコンパイルされ、ルータに登録されている（テストで確認）。
- HUD とインベントリを表示したときの UITK の CPU 時間（UIElementsUpdate＋Repaint）を記録する。目安は 1ms 以下（推測。PoC では CSS 表現 12 項目で 0.27ms）。

**出荷**: できる。既定は Web で、UITK は起動オプションのときだけ動く。切替が配布物に入るのは期限付きの例外（D1）。

**リスクと潰し方**

| リスク | 潰し方 |
|---|---|
| 基盤の抽象が P3 で合わない | パイロットで最難所を全部踏む。P3 中の基盤 API の変更は integrator がまとめて直す |
| 見た目がばらつく | 部品カタログ・`ref-uitk-design`・L2 で縛る |
| 基盤どうしの結合が強い | 同時 3〜4 に絞り、共有ファイルは integrator の直列所有にする |

**規模・並列**: 単位は 8、同時 3〜4。

### 2.4 P3 一括移植（残り全画面を並列）

**目的**: 残りの全画面を殻の中へ移し、UITK モードで全機能がそろった状態にする。

**作業単位**

| 単位 | 中身（難所） | 難易度 |
|---|---|---|
| U1 buildMenu | 3 カラム、検索の TextField（IME）、スクロールスパイ、smooth scroll、ホットバーへの D&D の元、BP の右クリック削除、セッション中の状態保持 | 高 |
| U2 recipe | ItemListPanel（ドラッグでスクロール）、RecipeViewer、長押しクラフト、コンテナクエリ相当の寸法計算、四隅のブラケット、グラデのクラフトボタン | 高 |
| U3a blockInventory の中核 | パネル、レジストリ、SectionStackView、チェスト、Esc と ×、機械（レシピ選択とインベントリの往復、ゴースト、モジュール、毎分の生産数） | 高 |
| U3b ブロック: 電力と歯車 | Generator（GaugeBar）、GearToElectric、ElectricToGear、Gear、Electric/GearNetwork、BeltThroughput（BarSeries と ←→） | 中 |
| U3c ブロック: 列車 | 駅名（IME）、Item/FluidPlatform、列車インベントリのタブ、時刻表（楽観更新） | 中 |
| U3d ブロック: その他 | FilterSplitter、Miner、Pump、ElectricPole、液体の表示（2026-10-18 裁定のパイプ・タンク・液体 PF・貨車の液体） | 中 |
| U4 research | TreeView 上のノード 4 状態、詳細ペイン、アンカー `research.node-*`（マスタに 7 件） | 中 |
| U5 challenge | カテゴリ列と TreeView のノード（目標 HUD は P2 で作る） | 中 |
| U6 skit | 会話窓のマスクフェード、タイプライター、選択肢の板、ツールバーとアイコン、暗転、背景スキット、旧 UITK の SkitUI の撤去 | 中 |
| U7 pause＋settings＋toast | トップ・サブページ、バグ報告（textarea の IME、配送状況）、設定 3 種、トースト | 中 |
| U8 eventLanguageGate | 全画面のゲート、DI より前の起動、辞書を通さない文言 | 低 |
| U9 tutorial | ハイライト枠（祖先クリップ、脈動、ラベルの反転）、ドラッグガイド、ワールドピン、キー操作ヒント、ack に代わる開発者ログ | 高 |
| U10 統合 | 同時に出る組を確かめる（playerInventory は Inventory・RecipeViewer・ItemList、subInventory は Inventory・BlockInventory、researchTree は Research・Inventory）。状態の外のオーバーレイ、全シナリオ、遅れて出てきた共有部品の昇格もここで扱う | integrator |

**波**

| 波 | 単位 | 依存 |
|---|---|---|
| 波 1 | U1、U2、U3a、U9 | 長いものから先に着手する。U9 の層と登録簿は P2 で用意済み |
| 波 2 | U3b、U3c、U3d、U4、U7 | U3b〜d は U3a に着手依存する |
| 波 3 | U5、U6、U8、U10 | — |

- U9 のマスタアンカー検証は U4 が完了ゲート依存になる（`research.node-*` が U4 で現れるため）。
- 波 2 は 5 単位ある。Editor 3〜4 本に対して 1〜2 単位は、Editor の要らない作業（L0、受入表の精査、UXML の下書き）から始める。

**完了条件**

- 20 機能の受入表の全行が、緑（L0・L1・L3・L4・目視の証跡）か「捨てる（理由）」になっている。未分類は 0。
- UITK モードで録画シナリオ全本が緑。全 90 本で、Web を操作する 27 本を含む。
- アンカーがそろっている。
  - 静的 19 個と動的 prefix 6 種が、UITK のどこかで登録される（カバレッジテスト）
  - マスタの 9 件（`challenge.current-hud`、`recipe.craft-button`、`research.node-*` 7 件）が実ゲームで解決する
- 状態の外のオーバーレイ一覧を、UITK モードで確認済み。
- 凍結期間に Web へ入ったバグ修正が、UITK 側にも反映済み（台帳で確認）。

**出荷**: できる（既定は Web のまま）。

**リスクと潰し方**

| リスク | 潰し方 |
|---|---|
| 見た目のばらつき | 波ごとに ScreenCapture の一覧を外部監査（codex-audit）にかけ、ユーザーに 1 回目視してもらう |
| 部品の不足 | 単位の中で作る。2 機能目で需要が出たら、integrator が波の境で shared へ昇格させる |
| U3 の衝突 | U3a でレジストリとセクションの枠を確定させてから、U3b〜d に入る |
| IME（駅名・検索・バグ報告） | 単位の完了条件に、実機の動画を入れる |
| 二重実装の漏れ | 凍結ゲートと台帳で防ぐ |
| スキットの文字送りで、行の折り返しが途中で変わる | 残りの文字を透明にして配置を固定する（推測） |

**規模・並列**: 13 単位、同時 5〜6。エージェント日数で 25〜32 日の見込み（推測）。

### 2.5 P4 切替（既定を UITK に・実機・1 周期の観察）

**目的**: UITK を既定にして配布し、実機と実際のプレイで確かめる。

**作業項目**

| ID | 作業 | 中身 |
|---|---|---|
| P4-1 | パリティの判定 | 台帳の全行、録画シナリオ全本、アンカー、状態の外のオーバーレイ、性能（全画面を開いたときの UI の CPU 時間と GC）を判定する |
| P4-2 | 起動オプションでの実機事前確認 | Windows 検証機（`scripts/playtest/verify-on-windows.sh`）と Mac mini の Steam で、UITK モードを確かめる。項目: IME（4 欄）、720p・1080p・4K、DPI、Steam オーバーレイ、言語 4 種（数値の書式とフォールバックを含む）、Ctrl+U、カットシーン、スキット、出展モード |
| P4-3 | 既定を切り替える PR | 既定を UITK にし、Web は `--ui=web` を退避路にする。moores-playtest-release で配布し、テスターへ告知する |
| P4-4 | 1 周期の観察 | バグ報告のバンドルを triage し、修正する |

**完了条件**

- プレイテスト 1 周期で、UI が原因で進行を阻む不具合が 0 か、全件修正済み。
- 実機チェックリストの全項目に証跡がある（スクショと動画を share に置く）。
- 配布ビルドで遠隔実行が動く。

**出荷**: UITK を既定にして出荷する。

**リスクと潰し方**

| リスク | 潰し方 |
|---|---|
| 実環境での破綻 | P4-2 で、起動オプションを使って先に確かめる |
| 退避路が長引く | 1 周期で P5 に入る規則にする（延ばす場合はユーザーの裁定を取る） |

**規模・並列**: コードは修正 PR が数本。並列は 1〜2 と人手。

### 2.6 P5 撤去

**目的**: CEF・Web・橋渡し・共存スイッチを消し、完了の定義（§5.2）を満たす。順序は §5.1。

**PR**（P5-1 は直列。P5-2 と P5-3 は一部を並行してよい）

| PR | 中身 |
|---|---|
| P5-1 Web 経路と切替を消す | 次をまとめて消す: `UiStackSelection`、`Client.Starter` の Web 配線、`Client.WebUiHost`、`Client.Playtest/WebUi` と DSL の Web 実装、`Client.Tests` の Web 系 65 ファイル、MainGameUI.prefab の CEF 面（uloop 経由）。ゲート監査は付け替えて残す |
| P5-2 CEF とビルド・配布 | 次を消す: `jp.juha.cefunity`、`CefRuntimeBundler`、`CefPackageLfsValidator`、`setup-cef.*`、`build.yml` の CEF と pnpm、`release-artifact.sh` の CEF helper 検査と契約テスト、README の CEF raw input 確認、`WebUiProductionArtifactBuilder`、`StreamingAssets/WebUi`、`.gitignore:128-130`。NuGet は D2 の裁定どおりに整理する。Win と Mac のビルドをこの PR の中で回す |
| P5-3 Web 一式と文書・名前 | 次を消すか直す: `moorestech_web/`、CI の `webui-test` と `needs_webui`、docs（`docs/webui` は archive へ）、スキル（`ref-webui-design` は廃止、moores-code-review の ts/tsx 観点 12 本は撤去、DSL のスキル文書は更新）、ADR の改訂記録、Web という名前の改名（`WebPinId` 13 箇所、`LocalizationKeys.Ui.Blueprint.WebModal*` など） |

**完了条件**: §5.2 の 17 項目。

**出荷**: PR ごとにできる。

**リスクと潰し方**

| リスク | 潰し方 |
|---|---|
| 隠れた依存 | P1 で共用部品を移設しておく。`Microsoft.Extensions.*` を他のパッケージ（ModelContextProtocol など）が共有していないかを、P5-2 の前に依存グラフで確かめる（未確認。host §9） |

**規模・並列**: 削除は約 45〜50k 行（推測）。内訳は moorestech_web が約 28k、`Client.WebUiHost` が 10.5k、テストが 6〜8k、DSL が 0.7k。並列は 2〜3。

### 2.7 並列実行の設計（全段階に共通）

**作業の切り方**

1. **1 単位 = 1 PR = 受入表 1 枚。** 単位はファイルの所有が重ならないように切る。新しく作るディレクトリの中で閉じるのが基本（前例: `docs/webui/MIGRATION.md` §4 の並列実行マップ）。
2. **共有ファイルは integrator が直列で所有する。** 対象は次のとおり。
   - 層 enum、画面ルータの表、composition root（スタック切替と DI）
   - Tokens.uss・Theme.uss、AnchorIds、DSL のロケータ本体
   - `Localization/localization.csv`、台帳、prefab とシーン（uloop で 1 本ずつ）
3. **殻を先に置く。** P2 の最後に全画面の殻を作っておけば、P3 は殻の中身を書くだけになる。共有ファイルの差分は「波の境で integrator が束ねる」ものだけになる。
4. **部品の昇格。** 1 機能だけが使う部品は `Screens/<機能>/` に置く。2 機能目で使われたら、integrator が波の境で `Components/` へ移す。
5. **挙動を変えるのは grill を通したものだけ。** P3 の移植は受入表どおりのパリティが基本。外れるもの（Mantine の素の見た目のままの画面を様式化する、トーストの扱いなど）は、moores-grill-with-docs を通した D15・D16 の裁定に従う。
6. **段階の詳細計画。** 各段階の着手前に moores-writing-plans で詳細計画を切る。P3 の単位は受入表を要件にして、短い計画で足りる。

**Editor の予算と役割**

| 項目 | 予算・中身 |
|---|---|
| Mac mini（32GB・10 コア） | Editor は同時 3 本が目安（`CLAUDE.local.macmini.md`）。pr-apply の常設スロットも別にメモリを使う |
| macbook-pro | 起きているときだけ、1 本を足せる |
| CI | S1 が成立すれば、client shard に L1 を載せて追加のレーンにする（`run_test.yml` は EditMode で 9 shard） |
| 指揮 | 波を起動し、完了を確かめ、台帳を更新する |
| integrator | 共有ファイルの所有、部品の昇格、衝突の解消を担う |
| 単位エージェント | 実装（opus）。worktree は `moores-wt new` で切り、Editor を起動する前に `moores-wt status` で本数を確かめる。4 本目になるなら Editor の要らない作業を先に進める |
| レビュー | moores-code-review → PR → pr-independent-review の順に回す |
| 外部監査 | 波ごとに画面の写真を codex-audit にかける |

**依頼文に必ず入れること**

- 前景で実行し、バックグラウンドのポーリングで待たない。
- 区切りごとにコミットする。
- 受入表のパスと、触ってよいディレクトリの列挙。共有ファイルへの書き込みは禁止。
- `ref-uitk-design` と部品カタログを読む。
- 完了報告は、報告ファイルと 3 行の要約で返す。

**完了の確かめ方**

- 報告をすぐには信じない。`git log`、報告ファイル、台帳の更新が実在するかで確かめる（ユーザーの運用規則）。
- 無ければ ListAgents で生存を確かめる。生きていれば再派遣しない。

**PR とマージ**

- moorestech は PR 作成で止まり、マージはユーザーが行う。1 波で 4〜6 本の PR がマージ待ちに並ぶ。
- 次の波は、前の波が master に反映されていることを前提にする単位だけを待たせる（着手依存と完了ゲート依存を分ける）。
- 暗転で入れるので、1 単位 1 PR を master へ出しても、テスターへの影響は無い。

---

## 3. 共通基盤の設計方針

### 3.1 全体構成とスタック切替

- **アセンブリ**
  - `Client.Ui` を新設し、ビュー・Presenter・基盤を置く。参照先は `Client.Game`（ViewModel・コマンド・状態の正本）、`Client.Input`、`Client.Localization`
  - `Client.Game` は `Client.Ui` を参照しない（今の WebUiHost と同じ依存の向き）
  - 名前に技術名（Uitk）を入れない。Web という名前の改名を繰り返さないため
  - DI は既存の VContainer 1.10、イベントは UniRx を使う
- **ディレクトリ**: `Client.Ui/Foundation/{Stack,Layers,Routing,Input,Motion,Tooltip,DragDrop,Modal,Anchor,Text,Theme}`、`Client.Ui/Components/<部品>`、`Client.Ui/Screens/<機能>`。1 ディレクトリ 10 ファイルまで（AGENTS.md）。
- **UXML/USS**
  - 機能の横に置き、ロードは 1 か所の `UiAssetCatalog` を通す。Resources か Addressables かは P2-a で決める（推測。PoC は Resources を使った）
  - 起動時に全部ロードし、欠けていたら起動の段階で止める（AGENTS.md「必須の値は最も早い段階で止める」）
  - L2 で参照パスが実在するかを検査する
- **実行時の形**
  - MainGameUI.prefab に、事前配置した `GameUiRoot`（UIDocument と PanelSettings を持つ）を置く。シングルトンの規約どおり Awake で `_instance` を設定する
  - パネルは 1 枚にする（Pick とフォーカスが 1 パネルの中で閉じるため。input §3.1）
  - PanelSettings は Scale With Screen Size・1280×720・Expand（S6）。既存の `SkitPanelSettings.asset`（1920×1080）は流用しない
  - root 直下に、中央寄せの 1280×720 の stage と、viewport 族の層を置く（ADR 0013/0017 の三分法）。通知は今の Web でも自前で `--ui-scale` を掛けている（`web/features/notification/style.module.css:14`）ので、単一パネルの拡縮で同じ意味になる
- **スタック切替 `UiStackSelection`**
  - 起動時に 1 回だけ決まる（Web か Uitk）
  - 参照してよいのは composition root の 3 か所だけ: `InitializeScenePipeline` の Web ホスト起動、`MainGameInitializationFinalizer` の Bind、`GameUiRoot` の有効化。この 3 か所は L2 テストで固定する。uGUI 退役で恒久 true のゲート参照 26 箇所が死に分岐として残った失敗の対策（shipping §6.2）
  - 選び方は起動オプション `--ui=web|uitk` と、Editor 用の環境変数。ADR 0072 の `--remoteExec` と同じく、全ビルドに入れてオプションで切り替える形
  - P5 で切替ごと消す
  - UITK モードでは Kestrel・WS・CEF を起動しない。CEF の子は既定で非アクティブにし、Web スタックだけが有効化する（prefab は uloop 経由。S13）

### 3.2 データ購読層

- **正本には手を入れない**（host §7.5 R2）。
  - P1 で、topic を「ViewModel＋変化通知」と「JSON アダプタ」に分ける。UITK の Presenter は前者を、Web は後者を使う
  - サーバー同期の新設は無い。正本はすでに「イベントパケット＋初期データ＋購読」でクライアントへ届いている
- **形**
  - `XxxViewModelPublisher`（`Client.Game`）が `IReadOnlyReactiveProperty<XxxViewModel>` を公開する
  - 正本の UniRx を購読し、PostLateUpdate でフレーム末に 1 回へ畳む（INFRA-7 の流用。中間状態を描かない）
  - WS の帯域のためのサンプリング（`ui.progress` の 100ms、歯車の `IntervalFrame(4)`）は、JSON アダプタ側へ移す。UITK では毎フレーム読んでよい（host §5.1）
- **ViewModel**
  - 不変の sealed class にする。判別 union は抽象基底と sealed 派生で表す
  - zod の不変条件はこの型へ移す（FluidSlot、変換機の詳細、Tooltip、Hotbar の 9 枠、PlacementMode）
- **コマンド**
  - action ごとに型付きメソッドにする（例 `IInventoryCommands.MoveItem(SlotRef from, SlotRef to, int count)`）。結果は `CommandResult`（Accepted か Rejected と理由コード）
  - 前提条件の検証（state の一致、解放判定、遷移許可表）はコマンド側に置き、両スタックで同じ検証を通す
  - 拒否の理由は開発者ログへ出す（AGENTS.md の fail-closed）。良性の拒否（web `BENIGN_ERRORS`）は、理由コードの性質としてコマンド側に持つ
- **Presenter の規約**
  - ViewModel を購読して View へ push し、View の操作をコマンドへ渡す
  - `Update()` で毎フレーム同値判定をしない
  - 画面は起動時に 1 回だけマウントし、表示と非表示はルータが切り替える。非表示の間は描画を止め、表示するときに最新値で 1 回描く
- **単一消費者**（モーダル、BP 名入力、スキットの入力、チュートリアル）: スタック切替で描画者が 1 つに決まるので、二重描画は起きない（host §7.1 の懸念が消える）。

### 3.3 部品

| 部品 | 実装の方針 | 使う機能 | 段階 |
|---|---|---|---|
| GamePanel（default / craft / skit / hud） | 面のグラデと 4 辺のフェードは、`generateVisualContent` の頂点カラーの 9 パッチで描く（推測。P2 で確認）。inset の 2 層は border の入れ子、右下のグリップと下向きの三角は Painter2D、外側の影は背面の板に filter blur | 8 | P2 |
| SlotFrame・ItemSlot・アイコン・個数・ゴースト・不足表示 | 3 層のベベル、選択時のシアン、`-unity-text-outline`、Sprite を直接表示 | 7 | P2 |
| SlotGrid | flex-wrap と固定のセル幅で N 列。末尾の余白は C# でクラスを付ける | 4 | P2 |
| ボタン類 | PanelAction、Icon、主要アクションの青グラデ、確認の赤、Mantine Button の代わり。`:hover`・`:active`・`:disabled` | 10 | P2 |
| ModeSwitch（横・縦） | `:focus` で代用する（`:focus-visible` は無い） | 4 | P2 |
| スクロール領域 | Scroller を §8.10 の様式で塗る。チュートリアル用の逃げの余白、smooth scroll、ドラッグでのスクロール | 5 | P2 |
| テキスト入力（単行・複数行） | 暗色の様式。フォーカスで入力の排他を切り替える。IME | 4 | P2 |
| 図形: GaugeBar・ProgressArrow・FadeRule・斜線帯 | Painter2D かメッシュ | 5 / 3 / 1 | P2 |
| アイコン（skit の 6 種・×・ピン・矢印） | SVG の path を Painter2D へ流す（S11）。VectorImage にするかは D8 | 4 | P2 |
| TreeView | パンとズーム（0.4〜2.5）、慣性 τ=325ms、Painter2D の線、ビューポートの保存、初期フォーカス（PoC で実証済み） | 2 | P2（research と challenge が P3 で同時に使うため） |
| FluidSlot 系 | 色の付いた背面フィル | 2 | P2 |
| RecipeRow | blockInventory と recipe が共有する（screens §3.4） | 2 | P2 |
| LocalizedLabel | キーと引数を保持し、言語が変わったら解決し直す | 16 | P2 |
| 文中アイコン（実行時の SpriteAsset） | PoC の `RuntimeSpriteAssetBuilder`（内部 API）に、構築と描画のテストを付ける | 必要時 | P2 |
| BarSeries・四隅のブラケット・選択肢の板 | Painter2D | 各 1 | P3（単位の中で作る） |

- 部品は `ref-uitk-design` のホワイトリストにあるものだけを使う。新しい語彙は「様式を先に決め、実装を後にする」（ADR 0014/0015 の型）。

### 3.4 テーマ USS

- **ファイル**
  - `Tokens.uss`: tokens.css 192、skit 38、slot 8 の約 238 個をすべて `:root` へ移す
  - `Theme.uss`: ランタイム既定コントロールの暗色化。初版は PoC の `PocControls.uss`（146 行）
  - `Typography.uss`
  - 部品ごとの USS
- **変換規則**（shipping §2.2〜2.3）

  | CSS の側 | USS での扱い |
  |---|---|
  | rem・em | px にする（1rem=16px） |
  | `calc()` 70 箇所 | 計算済みのリテラルにし、導出式はコメントに残す |
  | `rgb(from …)`・color-mix 5 箇所 | 事前に計算する |
  | data 属性 40 箇所 | `--modifier` クラスと `EnableInClassList` |
  | z-index 44 箇所 | 層 enum と兄弟順 |
  | gap 77 箇所 | margin |
  | grid | flex |
  | `:first-child` など | C# でクラスを付ける |

- **半透明（67 箇所）は Web の値を流用しない。** トークン表に「Web の値」「UITK の値」「決め方（S7・D9）」の 3 列を持つ。
- **正本は USS に置く**（D11 の推奨）。C# が値を要るとき（Painter2D、配置の計算）は `CustomStyleProperty` で解決後の値を読む。導出値が合っているかは、式を持つ L2 テストで検算する。
- **L2 で落とす規約**
  - 色は Tokens.uss 以外では `var()` だけ
  - 構造擬似クラスと z-index は禁止
  - UXML の表示文字列にリテラルを書かない
  - `name` の重複は禁止
  - 画像の参照は禁止（D8 次第）

### 3.5 アニメーション

- **要件は 3 系統だけ**（shipping §3.1）: 通知の出入り、チュートリアルの脈動、ドラッグガイドの往復。ほかは毎フレームの値の反映（長押しの進捗、慣性、smooth scroll）。
- **入退場は USS transition** で書く。
  - 追加直後にクラスを外すと補間されないので、`GeometryChangedEvent` を契機にする
  - `height: auto` は補間できないので、px に固定してから動かす（PoC の罠）
- **薄い自作 `UiMotion`**（100〜150 行の見込み。D12）が受け持つもの:
  - ループとキーフレーム
  - 頭からの再生し直し（通知の集約で寿命を再始動する。`web/features/notification/NotificationHost.tsx:68`）
  - 完了イベント
- **時計は差し込み式にする。** 本番はパネルの scheduler（実時間）、テストは `FrameUpdateMs` で進める（S8）。
- **尺と曲線はトークンに置く。** ADR 0016 の「生存尺を単一の正として流す」と、ホワイトリスト §6 の「テストのために尺を 0 にする抜け道を作らない」は継ぐ。
- **UI 演出は実時間（unscaled）で動かす。** AGENTS.md の GameUpdater の規則は、サーバーのゲームロジックが対象なので UI には掛からない（解釈。推測を含む）。

### 3.6 横断層（ツールチップ・D&D・モーダル・トースト・通知・ゲート・チュートリアル）

- **層の並び**
  - root 直下に、層 enum の固定順で兄弟として並べる
  - 順序の正本は `web/app/tokens.css` の `--z-*` と `zLayerTokens.test.ts`。これを C# の enum と不変条件テストへ写す
  - 大筋は「背景 → 通知（stage の裏）→ stage（中に画面・overlay・掴みの副層）→ viewport の常駐 HUD → モーダル → 暗転 → ツールチップ → トースト → チュートリアル → 全画面ゲート」。細部の順は写すときに確かめる（推測）
  - 再接続オーバーレイ（2000）は不要になる
- **層コンテナと背景は、すべて PickingMode.Ignore にする。** 忘れると「パネル外の右短押し」とワールドのクリックが死ぬ（input §3.1）。L1 で固定する。
- **ツールチップ**
  - HoverTooltip は、祖先がスクロールしたら引っ込める
  - CursorTooltip は `MouseCursorTooltipState` を購読し、画面端でクランプする（OFFSET 12）。カーソルがロック中は画面中央を基準にする
  - 書式は ADR 0019 の値（18px、padding 6/10、max-width 320）をトークンで持つ
  - ドラッグ中は、PointerEnter による表示を抑える（PoC の罠）
- **D&D と掴み**
  - press ジェスチャ（5px の閾値、tap・drag・abort、CapturePointer）を 1 つの状態機械にする
  - 落ち先は `panel.Pick` で判定する。`IDropTarget` の登録とペイロードの型（アイテム、ビルドメニューのエントリ）は P2 で確定する。U1 は元の側を作るだけで済む
  - 掴んだ絵（GrabFollower）は Grab 層に 1 つだけ置く
- **モーダル**: 同時に 1 件（`WebUiModalService` の後継）。幕を張り、フォーカスを中に閉じ込める。Esc は UIState を経由し、入力欄には自動でフォーカスする。尺は 0。
- **通知**: 背面の viewport 族に置く。集約と生存は通知の ViewModel が持つ（`.decisions/2026-08-19-獲得通知の集約はWebUI側で行う.md` の「表示側に置く」をそのまま継ぐ）。
- **トースト**: 残す範囲は D16 で決める。
- **全画面ゲートとチュートリアル**: 器は P2 で作り、中身は P3（U8・U9）で入れる。

### 3.7 入力とフォーカス

- **UI 用の InputActionAsset を新設する。**
  - 残すのは Point、Click、RightClick、MiddleClick、ScrollWheel だけ。Navigate は空にし、Submit と Cancel は使わない
  - MainGame の `DefaultInputActions`（`Assets/Scenes/Game/MainGame.unity:8435`。Navigate に WASD を含む）を差し替える（シーンは uloop 経由）
- **テキスト入力欄以外は `focusable=false` にする。** 2026-10-01 の Space 裁定の UITK 版で、Tab・Space・Enter の既定動作も封じる（S4）。
- **`UiPointerHitTest.IsPointerOverAnyUi()` は中身だけ差し替える**（呼び出し側は 23 ファイル 28 箇所で、そのまま）。
  - カーソルがロックされていなければ、`RuntimePanelUtils.ScreenToPanel` から `panel.Pick` で調べ、層コンテナ以外に当たれば true にする
  - 値の出どころは「Web（WS）または UITK（Pick）」の論理和にする。UITK モードでは WS が来ないので、スタックを参照せずに済む
  - デバッグ用 uGUI のための EventSystem 判定は残す（ADR 0052 の agent 前提 1）
- **ホイール**
  - 装備の循環（`web/features/inventory/EquipmentPanel/index.tsx:55-80`）を C# の 1 か所へ移す
  - 条件は「UI の上なら止める。ただし `wheel-passthrough` クラスの祖先は通す」「建築ツールが占有中なら止める」「装備が 1 枠以下なら何もしない」
- **テキストのフォーカス**
  - root で FocusIn/Out を受け、`UiInputExclusivity` へ同期で書く
  - ESC は「このフレームは UI が消費した」ラッチを、InputKey の抑止判定に足す（S5）
- **キー処理を 1 か所へ集める。** Web に散っていた次の処理は、C# の UIState か画面の Presenter に置く（input §1.4）。
  - Esc: BlockInventoryKeyHandler、RecipeSelectionKeyHandler、入力欄の blur
  - ←→: ベルト流量の窓切替
  - Enter: クラフト、スキット送り
- **カーソル追従**: `PointerMoveEvent` から同じフレームで位置を置く。ADR 0020 のワープ前提は要らなくなる。
- **対象外と据え置き**
  - ゲームパッドは対象外（ADR 0082）
  - `activeInputHandler: 2`（Both。`ProjectSettings.asset:1025`）は撤去まで維持する。InputSystem だけにできるかは IME 次第（推測）

### 3.8 ローカライズ

- **C# の `Localize` に一本化する。** 辞書の合成、解決順、型付きキーはすでにある（input §4）。HTTP での配信、世代管理、「辞書が無い」状態は消える。
- **差し込み記法は D4 で決める。** 今は `{p0}` が 190 箇所、名前付きが 44 キーあり、混在している。推奨は名前付きへの統一で、加えて CSV から型付きの書式メソッドを生成する（推測: `ContentKeyCodeEmitter` 系を拡張して作れる）。
- **数値の書式**: InvariantCulture を明示する表示用ヘルパーを置く。Web の `toFixed`（12 箇所）は常に `.` を使う。de 列があるので、カルチャ依存のままだと実害が出る。
- **LocalizedLabel**: `Localize.OnLanguageChanged` で解決し直す。ViewModel は Guid とキーを運び、ビューが解決する（ADR 0006 の原則。決定 5 の「Web 側」を「ビュー側」へ改訂する）。
- **静的検査**: UXML の `text` 属性にリテラルを書かない、全画面の文言がキーを通っている。それぞれ `noJsxVisibleLiteral` と `allScreensI18n` の後継。
- **スキット**: 今どおり C# が解決した文字列を push する（ADR 0006 決定 11）。

### 3.9 フォント

- **TextCore の SDF FontAsset を Editor で作ってコミットする**（S10）。既存の `Assets/Dependencies/Font/*SDF*.asset` は TMP_FontAsset で、型が違う。
- **フォールバックの連鎖**: NotoSans-Regular → NotoSansJP-Medium → NotoSansKR-Medium。Web はハングルのフォントを持たず、OS のフォントへ落ちていた（推測）。
- **ロケールで優先を切り替える**: CJK では JP を先頭にする。切替は root のクラスで行う。
- **合成太字は使わない**: `-unity-font-style: bold` を書かない。
- **最小の文字寸法**は、S7 の結果で決める（D10）。
- **アイコン上の個数の縁取り**は `-unity-text-outline` で描く。SDF の縁は内側にも食い込むので、目視で調整する。

### 3.10 AI 向けの作法（議論の未裁定点への案）

- UXML/USS はテキストで持つ。UI Builder にしか無い状態を作らない。
- View・Presenter・Mount の 3 層に分ける。
  - View は VisualElement を組むだけで、MonoBehaviour に依存しない
  - Presenter は購読と push、コマンドの呼び出しを持つ
  - Mount は `GameUiRoot` へ載せる薄い層
  - テストでは `rootVisualElement` に同じ View を組める
- 見た目の調整は USS を中心に回す。USS/UXML の変更はコンパイルもドメインリロードも起こさない。
- 名前の付け方を分ける。
  - testid は `name` に同じ綴りで付ける
  - アンカーは USS クラスで付ける（`name` とは別の契約。tests §7.2）
  - 状態はクラス（`--selected` など）で表す
- 画面の確認は ScreenCapture 系で行う（uloop screenshot の rendering モードには UITK が写らない。PoC）。撮影にはフレーム番号か時刻の透かしを入れ、古いフレームを見ないようにする（shipping §6.2）。
- AGENTS.md の規約を守る: 200 行以下、1 ディレクトリ 10 ファイル、`#region Internal`、partial と `Func<>` は禁止、イベントは UniRx。
- `ref-uitk-design` と部品カタログを P2 で作る。`ref-webui-design` の §1〜§9 の原則を写し、§0・§10 の Web 用の手順は置き換える。全エージェントの依頼文に、これらを読む指示を入れる。

---

## 4. 検証の作り方

### 4.1 受入表（仕様の正本）

- **置き場と列**
  - `docs/uitk/acceptance/<機能>.md` に置く
  - 列は ID、前提（モデルの状態）、操作、期待（発行コマンドと表示）、由来（ファイル: テスト名）、検証層、UITK のテスト名、状態（未・緑・捨てる＋理由）
- **作り方**
  - P0-4 のスクリプトで機械的に抜き出し、単位のエージェントが精査する
  - Playwright と vitest のテスト名は、ほぼ日本語の挙動文になっている（tests §3）
  - 通信契約のテスト（vitest 167 件、C# 131 件）は、最初から「捨てる」に分類する
- **どの行も終わり方を決める。** 終わり方は 3 つで、テスト、目視（証跡のパス）、捨てる（理由）のどれか。未分類は欠陥（disposition の型）。
- **前例**: uGUI→Web の移行でも、Phase D で「画面ごとの操作パリティ受け入れ表」を最終検証にしていた（`docs/webui/plans/phase-d-cutover.md:33-37`）。

### 4.2 Web のテストの行き先

| Web 側 | 件数 | 行き先 | 残る見込み |
|---|---|---|---|
| L ロジック | 311 | L0 NUnit（P1 で移植） | 約 290 |
| C コンポーネント | 235 | L1 ビューテスト | 約 200 |
| P 画面フロー | 213 | L1。位置・幅・層などの見た目の主張（約 52）は、レイアウトの照合か目視へ | 約 205 |
| I 多言語 | 61 | L0 と L2 | 約 25 |
| S 静的規約 | 91 | L2（意図だけ） | 約 35 |
| W 通信契約 | 167 | 捨てる | 0 |
| C# WebUi 系 | 285 | ViewModel の構築 90 とコマンド 36 は形を変えて残し、状態ストア 28 はそのまま残す。wire 131 は捨てる（設定系の 10〜15 件は、設定サービスのテストへ移す） | 154 |

- **Web 側のテストを消すのは P5。** 切替日までは Web が出荷物なので、守りとして残す。tests §9 は画面ごとに消す案だが、スタック切替ではそうしない。

### 4.3 EditMode UI テスト（L1）

- **土台**: UI Test Framework 1.0.0 の `UITestFixture` と `PanelSimulator` を使い、PlayMode は使わない（tests §5）。
- **本番と同じ条件で組む。** `UiTestHost` が次を当てる。
  - `panelSize` は 1280×720（参照解像度）
  - 本番の Tokens・Theme・SDF FontAsset。当てないと、文字幅が違ってレイアウトの照合値がずれる（推測）
- **mock-host の代わり**: 画面ごとに `FakeXxxModel`（ReactiveProperty で組むフィクスチャ）と `RecordingXxxCommands`（発行の記録）を用意する。
- **時間**: scheduler か、差し込んだ時計で進める。`MonoBehaviour.Update` で駆動すると時間を進められない。
- **罠**（どれも推測。P2 で確かめる）
  - `MouseMove(from,to)` は途中の要素の PointerEnter を通らない可能性がある。分割ドラッグは 1 スロットずつ動かす
  - ScrollView の中の子は、スクロールしても GeometryChangedEvent が発火しない可能性がある
  - L1 は描画しないので、Linear の色や文字のにじみは L4 か目視で見る
- **CI**: S1 が通れば client shard に載せる。今 Playwright は CI で実行されていないので、L1 を CI に載せると検証面はむしろ広がる。

### 4.4 録画プレイテストと DSL（L4）

- **API の改名（P2-f）**
  - `ClickWebUi`・`HoverWebUi`・`UntilWebUiElement`・`WaitWebUiTextContains`・`ScrollDownUntilWebUiElementIsClickable` を、`ClickUi` 系へ一度に改名する
  - シナリオ 27 本とスキル文書 4 本も同じ PR で直す。旧名は残さない（AGENTS.md: 後方互換は考えない）
- **ロケータ**: `IUiElementLocator` を Web 版と UITK 版の 2 本で持ち、実行中のスタックで選ぶ。UITK 版の手順は次のとおり。
  1. ゲームのパネルを `Q(name)` で探す。0 件なら再試行し、2 件以上なら理由付きで失敗させる
  2. 可視かどうかを判定する
  3. `panel.Pick(中心)` が対象自身かその子孫かを確かめる
  4. パネル座標を画面座標へ変換する
  5. `SemanticInput` で注入する
- **消えるもの**: UITK では、`CefInputForwarder`・`CefScreenMapper`・`PlaytestDomQuery` と、`PlaytestUiOps.cs:134-138` の pointerOverUi 待ちが要らなくなる。
- **回し方**
  - 最初に、S3 で録画に写るかを 1 本で確かめる
  - 単位のエージェントは、自分の画面に触るシナリオを UITK モードで回す
  - P3 の U10 と P4 で、全 90 本を UITK モードで回す

### 4.5 アンカーの付け替え

- **語彙は変えない。**
  - マスタに直書きされた ID は v8 で 9 件（tests §8）。綴りを保てばマスタの変更は要らない
  - `.decisions/2026-08-19-チュートリアルUI指定はWebアンカーIDを直書きし変換も検証も持たない.md` とは矛盾しない
  - 直すのはスキーマ既定値の文字列 `web anchor id`（`VanillaSchema/challenges.yml:280-337`）と、`anchor-convention` の置き場だけ
- **正本**: `web/shared/tutorialAnchor/anchorIds.ts` を、C# の `AnchorIds` 定数（静的 ID と、動的 prefix の生成関数）へ移す。`TutorialAnchorIdMapper` もここへまとめる。語彙が C# の 1 か所になるので、フィクスチャの突き合わせテストは消す。
- **付け方**: `TutorialAnchor.Mark(VisualElement, params string[] ids)` の 1 か所で、USS クラスとして付ける。1 つの要素に複数の ID を付けられる。空白を含む ID は弾く。
- **登録簿**: AttachToPanel・DetachFromPanel で登録と解除をする。Detach は同期で消すので、枠がアンカーより長生きしない。
- **リゾルバ**
  - 0 件なら missing、2 件以上なら duplicate、ほかに非表示と画面外を判定し、開発者ログへ出す（無音の縮退を禁止。マスタ読込時の検証は足さない）
  - 位置は GeometryChangedEvent に加え、スクロールによる移動を拾うため毎フレーム `worldBound` を監視する
- **祖先のクリップ**
  - overflow を隠している祖先の `worldBound` との交差で求める（ADR 0024 の UITK 版）
  - 祖先の overflow を公開 API で読めない場合は、クリップする容器に印のクラスを付ける（推測）
  - 判定役は、2026-08-20 の「実 UI の経路×オラクルで 1 本」をそのまま移す
- **仮想化**: 仮想化した ListView の行にあるアンカーは、画面外では存在しない。研究ノードとビルドメニューのエントリは仮想化しない（数が小さい。推測）。
- **カバレッジテスト**: 全静的 ID と全 prefix が、どこかの画面の L1 マウントで登録されることを確かめる。

### 4.6 見た目の確認

- 画素のパリティは目標にしない。P0-5 の参照キャプチャと、トークン表（色・寸法・層）と並べて比べる。
- 撮影には ScreenCapture 系を使い、透かしを入れる（§3.10）。解像度は 720p・1080p・4K・21:9 で撮る。
- 波ごとに外部監査（codex-audit。画像付き）にかけ、ユーザーには短い目視を 1 回だけ頼む。

### 4.7 決定論ゲート（最初の PR から入れる）

| # | ゲート | 中身 |
|---|---|---|
| G1 | Web 凍結 | `web/` への新規ファイル追加を落とす。変更 PR には台帳の追記を求める（moores-code-review の決定論チェックで見る） |
| G2 | uGUI 追加禁止 | `WebUiGateClassification` と `WebUiGateAuditTest` を、UI 一般のゲートへ付け替える（2026-09-08 の、トークン検査と分類粒度の併用を保つ） |
| G3 | USS/UXML の規約 | §3.4 の規約 |
| G4 | 台帳の完全性 | 機能、topic、action、アンカー、testid が全部分類されている |
| G5 | ルーティング表 | 全 UIState と特性の組み合わせを網羅している。P4 の前には、全画面に UITK の実装があること |
| G6 | スタック切替の閉じ込め | `UiStackSelection` を参照できるのは 3 か所だけ |
| G7 | 撤去後 | `WebUi`・`Cef`・`webui` のトークンが、許可リスト外で 0 |

---

## 5. 撤去の順序と完了の定義

### 5.1 撤去の順序

先に済ませておくもの（P1）:

- 遠隔実行を Kestrel から外す（P1-a）
- `Client.WebUiHost` の中にある状態を、DI へ移す（P1-b〜e）
- 共用部品を移設する。`CefLfsPointer` の改名、`EnvironmentScrubOnEditorLoad` の移設（P1-p）
- DSL の改名と、UITK 版のロケータ（P2-f）

これらを前提に、P5 の撤去は次の順で進める。

1. `UiStackSelection`、`Client.Starter` の Web 配線（`WebUiStartup`、`InitializeScenePipeline:73` のポート引き渡し、Finalizer の Bind、`EventModeStartGate` の Hub 依存）
2. `Client.WebUiHost` 全体（Boot、Cef、Common、Static、Vite、Editor、Assets、Game の JSON アダプタ）
3. `Client.Playtest/WebUi`、`PlaytestWebUiOps` の Web 実装、`Client.Tests` の Web 系
4. MainGameUI.prefab の CEF Canvas、RawImage、`CefUnityBrowserSample`、`WebUiCefNavigator`（uloop 経由）
5. `jp.juha.cefunity`、`CefRuntimeBundler`、`CefPackageLfsValidator`、`setup-cef.*`、CI の CEF 事前取得、`release-artifact.sh` の CEF 検査と契約テスト、README の確認項目
6. NuGet（D2 の裁定どおり）
7. `moorestech_web/`、CI の `webui-test` と `needs_webui`、`WebUiProductionArtifactBuilder`、`StreamingAssets/WebUi`
8. docs とスキル、ADR の改訂記録
9. Web という名前の改名
10. `activeInputHandler` の見直し（任意。IME 次第）

### 5.2 完了の定義（観測できる形）

| # | 条件 | 観測のしかた |
|---|---|---|
| 1 | UI のスタック切替が無い | `UiStackSelection` と `--ui` の参照が 0（grep） |
| 2 | `Client.WebUiHost` が無い | asmdef とディレクトリが無い。`Client.WebUiHost` の参照が 0 |
| 3 | CEF が無い | manifest と lock に `jp.juha.cefunity` が無い。bundler、validator、setup が無い。配布ビルドの Windows `Plugins/x86_64` と Mac `PlugIns/cef-unity-server.app` に CEF のファイルが 0（`release-artifact.sh` で検査。今は Win 237 ファイル・約 368MB、Mac 252 ファイル・約 298MB） |
| 4 | Web 一式が無い | `moorestech_web/` が無い。CI に `webui-test`、`needs_webui`、pnpm の step が無い |
| 5 | 遠隔実行が動く | 配布ビルドで ADR 0072 の完了条件 3 点を、Windows 検証機で再確認する |
| 6 | NuGet が裁定どおり | Kestrel を最小ホストで残すなら、AspNetCore を残したことを台帳に記録する。HttpListener なら AspNetCore 系が 0 |
| 7 | 受入表 | 全 20 機能で未分類が 0 |
| 8 | 録画 | 全 90 本が緑。Web という名前の DSL API が 0 |
| 9 | アンカー | 静的 19 と prefix 6 が登録される。マスタの 9 件が実ゲームで解決する |
| 10 | 実機 | Windows と Mac のチェックリスト（P4-2）に証跡がある |
| 11 | プレイテスト | UITK 既定で 1 周期、UI が原因で進行を阻む不具合が 0 |
| 12 | 名前 | G7 が緑。`WebPinId` などを改名済み |
| 13 | 共用部品 | `CefLfsPointer` の改名と、`EnvironmentScrubOnEditorLoad` の移設が済んでいる |
| 14 | 文書とスキル | `docs/webui` は archive、入口は `docs/uitk`。`ref-webui-design` を廃止して `ref-uitk-design` に置き換え、moores-code-review の ts/tsx 観点 12 本を撤去、DSL のスキル文書を更新 |
| 15 | ADR | 0006、0013/0016/0017、0019、0020、0021、0024、0052、0071、0072 の改訂か廃止の記録がある（§7） |
| 16 | スキットの残骸 | 旧 UITK の SkitUI と `SkitPresentationMode.WebUiEnabled` を撤去し、bd `moorestech-lnsf.8` を閉じた |
| 17 | uGUI ゲート | G2 で、新規のスクリーンスペース uGUI 禁止が引き続き効く |

---

## 6. ユーザーの裁定が要る論点

### 6.1 P0 で要る（決まらないと P0・P1 が始められない）

**D1 共存の形（移行中の出荷形）**

| 選択肢 | 中身 |
|---|---|
| **A（推奨）** | UI スタックを丸ごと切り替える。起動オプションで選び、既定は Web。切替日に一括で UITK へ |
| B | 束（A〜D）単位の strangler |
| C | 移行ブランチに溜め、一括でマージする |

- **A を推す理由**
  - 共存の橋（入力の裁き、重なり順、系をまたぐ D&D、アンカー矩形、二重ロケータ）が要らない
  - 並列の単位が束に縛られない
  - master へ暗転で入れられ、テスターに混在した UI を出さない
- **既存方針との関係**
  - 議論記録の agent 案（画面単位の strangler）と、host §7.5 R1「実行時の切替フラグは作らない」とは逆の案。どちらも裁定ではない。切替を 3 か所に閉じ（G6）、P5 で消すことで、R1 の懸念（残骸になること）を潰す
  - AGENTS.md「デバッグ/テスト専用 public をプロダクションに残さない」とは、P2〜P4 の間だけ配布物に切替が入る点で緊張がある。期限付きの例外として扱い、DoD #1 で消す

**D2 遠隔実行の待受**

| 選択肢 | 中身 |
|---|---|
| **(a)（推奨。P1 で）** | Kestrel を `Client.RemoteExec` 専用の最小ホストとして移す。NuGet は残る |
| (b) | `HttpListener` に載せ替え、AspNetCore 系を外す（Mono での挙動は推測） |
| (c) | 遠隔実行をやめる |

- **(a) を推す理由**
  - 既存のテストと `scripts/playtest/remote-exec.sh` が無改変で済む
  - UITK モードでも遠隔実行が要る
- (b) は撤去後の別タスクにする。
- **既存裁定との矛盾**: ADR 0072 の「入口は既存の Web UI サーバー（Kestrel）に 1 本足す」と、その棄却案「専用の待ち受けを新設」に反する。どの案を採っても 0072 の改訂が要る。

**D3 Web の凍結と、新機能の行き先**

- **推奨**
  - P0 の凍結ゲートが入った後、Web には新機能を入れず、バグ修正だけにする（台帳に記録する）
  - 進行中の PR は完了させる
  - P2 の開始以降、新しい UI は UITK にだけ作る。テスターに見えるのは P4 以降になる
  - P2 より前に急ぐ新機能は Web で作り、移植対象を 1 つ増やす。前例は液体 UI（`.decisions/2026-10-18-パイプタンク液体PFのUIはWebUIに液体名と量容量の表示だけ足す.md`）
- **推す理由**: Web 並行開発との衝突と二重実装を、期間で区切れる。
- **矛盾**: 無い（2026-08-17 の「Web 専任」は、2026-10-18 で覆り済み）。

**D4 i18n の差し込み記法**

| 選択肢 | 中身 |
|---|---|
| (a) | C# に名前付き補間を足し、混在のままにする |
| (b) | 名前付きの 44 キーを `{pN}` へ直す |
| **(c)（推奨）** | `{pN}` の 190 箇所を名前付きへ統一し、型付きの書式メソッドを生成する |

- **(c) を推す理由**
  - 記法が 1 つになる
  - 翻訳者に文脈が見える
  - 引数の漏れがコンパイルエラーになる
  - AGENTS.md「変更の波及を恐れない」に沿う
- **注意**: mod の CSV に `{pN}` があれば、同時に直す（推測: マスタ由来のキーはプレースホルダを持たない）。
- **矛盾**: 無い。ADR 0006 の改訂と一緒に記録する。

**D5 完了の定義**

- §5.2 の 17 項目で承認を求める。
- 議論記録にあった 4 項目（CEF・Kestrel・WebUiHost と moorestech_web・CefRuntimeBundler）に、遠隔実行・共用部品・DSL・ゲート・名前・ADR を足したもの。
- **注意**: 「Kestrel の削除」は、D2 (a) を採ると「Web 用 Kestrel の削除」に読み替わる。

**D6 範囲**

- **推奨**
  - MainMenu とローディングは含めない（作り変えのタスクで UITK 化する）
  - HP バーとデバッグ UI は uGUI のまま、`com.unity.ugui` は残す
  - Mac の Universal 化と Linux は、撤去後に別途裁定する
- **矛盾**: 無い（ADR 0052 の例外 2〜4 を維持する）。ただし例外 2 の理由（CEF の起動位置）が消えたことは記録する。

### 6.2 P2 の着手までに要る

| # | 論点 | 選択肢 | 推奨と理由 |
|---|---|---|---|
| D7 | パイロット画面 | 議論記録の「インベントリかホットバー」、HUD だけ、などから選ぶ | HUD 群＋ホットバー＋インベントリ一式（装備・掴み・ツールチップ）＋通知＋BP 名入力モーダル。基盤の最難所を全部踏める。スタック切替なら束に縛られないので、両方を含められる |
| D8 | 画像アセット禁止（ref-webui-design） | (a) 維持（メッシュ・Painter2D・SVG path の実行時変換）、(b) アイコンだけ VectorImage を許す、(c) テクスチャも許す | (a)（S11 が成立したら）。差分がテキストで読め、AI が直せ、色をトークンで変えられる |
| D9 | 半透明の決め直し | 実機での目視、sRGB の RT で合成、α の補正 | 既定案は実機の目視で UITK の値を決め直し、トークン表に 2 列を持つ。S7 の結果で最終判断する |
| D10 | 最小の文字寸法 | 12px 以下を 13px へ上げる、据え置く | S7 で 720p の見え方を並べてから決める。アイテム個数のバッジは縁取りがあるので別扱い |
| D11 | トークンの正本 | (a) USS を正本にし C# が読む、(b) C# を正本にし USS を生成する | (a)。見た目の変更が USS だけで済み、ドメインリロードも起きない。見る場所が 1 か所になる |
| D12 | アニメーション基盤 | (a) 自作の UiMotion、(b) LitMotion（MIT・手動ディスパッチャあり）、(c) DOTween で代用 | (a)。要件は 3 系統だけで、時計をテストの時間に合わせやすく、外部の版上げにも振り回されない。(b) でもよい |
| D13 | 純ロジックの、Editor の要らないレーン（S12） | 採る、採らない | S12 が成立すれば採る。AGENTS.md のコンパイル必須は、PR 前の uloop compile で守る |
| D14 | PR の粒度とマージ | 1 単位 1 PR を master へ、波ごとの統合ブランチ | 1 単位 1 PR（暗転なので安全）。ユーザーのマージが律速になるので、波ごとにまとめて判断してもらう |

### 6.3 P3・P4 までに要る

| # | 論点 | 推奨と理由 |
|---|---|---|
| D15 | Mantine の素の見た目のままの画面（toast、challenge、trainHud） | 移植のついでに、既存の語彙（GamePanel の hud 面など）へ寄せる。新しい語彙は作らない。見た目が変わるので grill に通す |
| D16 | トーストの残し方 | WS の障害に由来するトーストは消える。残すのはバグ報告の結果と、良性でないコマンドの拒否。様式を整えて負債 `moorestech-cgm` を閉じる |
| D17 | 切替後の退避路 | 1 プレイテスト周期だけ `--ui=web` を残し、周期が明けたら P5 に入る。実環境で破綻したときの戻り道として持つ。期限は延ばさない |
| D18 | 旧 UITK の SkitUI（bd `lnsf.8` の確認） | U6 で撤去し、新しい UITK のビューを唯一の経路にする。lnsf.8 の「フォールバックを持たないか」の確認は、これで実質的に解消する |

---

## 7. 既存の裁定・ADR との関係

| 既存 | 計画での扱い | 種別 |
|---|---|---|
| ADR 0072（入口は既存の Web UI サーバー。棄却案「専用の待ち受けを新設」） | D2 | **矛盾**（改訂が要る） |
| ADR 0006 決定 5（Web 側で解決） | ビュー側で解決する。言語切替は再バインドで行う | 改訂 |
| ADR 0013・0017（族、z、`--ui-scale`） | 族の概念は継承する。実装は PanelSettings と層 enum へ移す | 改訂（実装の節） |
| ADR 0016（通知の出入り） | 意味は継承し、手段は transition と UiMotion に変える | 改訂（実装の節） |
| ADR 0019（ツールチップの書式は Web が所有） | 所有者を UI のトークンへ移す | 改訂 |
| ADR 0020（ロック時にカーソルをワープ） | UI 側でワープを前提にする必要が無くなる | 改訂 |
| ADR 0021（テキスト選択は入力欄だけ） | UITK の既定の挙動で満たされる | 廃止の記録 |
| ADR 0024 と 2026-08-20（祖先のクリップ、テスト方針） | overflow を隠す祖先との交差で求める。オラクルの方針はそのまま | 改訂 |
| ADR 0052 | 例外 1 が消え、例外 2 の理由も消える。ゲートは付け替える。agent 前提 1（EventSystem 判定）は維持する | 改訂 |
| ADR 0071（Mac は arm64 のみ） | 理由を ffmpeg だけにする | 改訂 |
| 2026-08-19 アンカーの直書き | 語彙を保つ（矛盾は無い）。スキーマ既定値の文字列と、規約文書の置き場だけを直す | 文言 |
| 2026-08-19 獲得通知の集約は WebUI 側 | 置き場所を「通知の表示モデル」へ移す。「表示側に置く」原則は継承する | 文言 |
| 2026-08-26 Web 操作音は sound.play で | 直接呼び出しに変える。音を Unity に一本化する原則は継承する | 文言 |
| 2026-10-01 Space の封じ | focusable と UI アクション資産で再現する（S4） | 継承 |
| 2026-09-08 uGUI ゲート | G2 で付け替える | 継承 |
| 2026-09-05 状態の主権は Unity、抽出 PR と削除 PR | 型として流用する（P1 が抽出、P5 が削除） | 継承 |
| 2026-09-05 メインメニューとロード画面 | 範囲外のまま（D6） | 継承 |
| AGENTS.md デバッグ専用 public の禁止 | スタック切替を期限付きの例外にする（D1、DoD #1） | 緊張あり |
| AGENTS.md イベントに Action を使わない | `UIStateControl.OnStateChanged` を UniRx に直す（P1-e） | 是正 |
| AGENTS.md コンパイル必須 | D13 を採っても、PR 前の uloop compile は維持する | 継承 |
| 議論記録の未裁定 5 点 | 出荷形は D1、仕様の正本は §4.1、完了の定義は §5.2 と D5、AI 向けの作法は §3.10、第一弾の画面は D7 | — |
