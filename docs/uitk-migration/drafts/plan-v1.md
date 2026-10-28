# UI Toolkit 全面移行 統合計画

- 作成 2026-10-23。3 つの草案（strangler・foundation・player）を採点し、1 本にまとめた計画案。**裁定ではない。** repo・bd・git には何も書いていない（読むだけ）。
- 数の確認: master HEAD `e8e611f1c0` で grep などを使って数え直した。本文で「実測」と書いた数がそれにあたる。規模・期間の数字はすべて推測。
- 入力
  - 裁定 `.decisions/2026-10-18-CEFを撤去しUI-Toolkitへ全面移行する.md`
  - 議論 `docs/webui/ui-toolkit-migration-discussion-2026-10-18.md`
  - PoC 報告 `docs/uitk-migration/poc/hard-cases/index.html`（コードはブランチ `poc/uitk-hard-cases` の `de6c68621c`。master には無い）
  - 調査 5 本 `docs/uitk-migration/research/understand-{screens,host,input,tests,shipping}.md`（以下 screens・host・input・tests・shipping と略す）
  - 草案 3 本 `docs/uitk-migration/drafts/plan-{strangler,foundation,player}.md`
- 表記
  - パスは repo 相対。`Scripts/` は `moorestech_client/Assets/Scripts/`、`web/` は `moorestech_web/webui/src/` を指す
  - 「推測」と書いた所は実測していない

---

## 0. 要約

**判定: 束単位の strangler を軸にする。** 束の中身は master の中で暗転のまま並列に作り、そろったら所有表の 1 行で切り替えて毎週の playtest に出す。実行時の切替フラグは配布物に入れない。第一弾は「命綱」（ポーズ・設定・バグ報告）にする。

**何を移すか**

- CEF（cef-unity）と Web UI 一式を、C#・UXML・USS の UI Toolkit（以下 UITK）に置き換える
- Web UI 一式の中身
  - React の本番コード約 15.6k 行（実測。features 8,482・shared 4,002・app 922・bridge 2,203）
  - `Client.WebUiHost` 140 ファイル・10,565 行（実測）
  - Kestrel・WebSocket
  - 配布物の CEF（Windows 237 ファイル・約 368MB、Mac 252 ファイル・約 298MB。shipping §1.1）
- 移す単位は「束」。束は、同時に表示され、掴みや D&D でつながる面の集まり
- 共存中は、どの画面状態でも描く系をちょうど 1 つにする（所有表）

**どの順で移すか、テスターに何が届くか**

| 段階 | 中身 | テスターに届くもの |
|---|---|---|
| 0 地ならしと実測 | 遠隔実行を Kestrel から外す、状態を DI へ移す、OneJS の撤去、DSL の付け替え、台帳・受入表・参照キャプチャ・凍結、実測 15 本 | 見た目は変わらない。配布物から quickjs が消える |
| 1 基盤と命綱 | UITK 基盤＋ポーズ・設定・バグ報告・トースト＋CEF 停止の検知 | CEF が死んでも Esc でセーブ・設定・バグ報告ができる |
| 2 常駐表示 | 入力の無い HUD、通知、ツールチップ、ワールドピン、チュートリアル枠、スキット | ゲーム画面とストーリーで、CEF が操作できるものを描かない |
| 3 建築の束 | ビルドメニュー、ホットバー、BP 名入力モーダル | ビルドメニューから選んで建てられる |
| 4 持ち物の束 | インベントリ・装備・掴み・レシピ・ブロック UI 全族・研究（束ごと一度に切り替える） | 生産ループ全体が CEF に依存しない |
| 5 残りと Web の空化 | チャレンジ一覧、出展モードの言語ゲート、Web の横断層の残り。1 周期観察する | CEF は起動するが何も描かない |
| 6 撤去 | CEF・Web ホスト・Web 資産・共存機構・Web という名前の残り | 配布物から CEF が消える |

**いつ何が消えるか**

各段階は「切替 PR → 1 周期 → 削除 PR」の順に進む（§3.3 R3）。

| 段階 | 消えるもの |
|---|---|
| 0 | `com.singtaa.onejs`、`com.annulusgames.debug-ui`、遠隔実行の Kestrel 依存 |
| 1 | Web の `features/pauseMenu`・`features/settings` とテスト、ポーズ系の Web の ESC 経路 |
| 2 | Web の `features/commonHud`・`progress`・`keyHint`・`modeHud`・`trainHud`・`notification`・`skit`、CursorTooltip、WorldPinOverlay、KeyControlHintHud。旧 UITK の `SkitUI` と `SkitPresentationMode` |
| 3 | Web の `features/buildMenu`・`hotbar`・`modal`、`BlueprintNameInputWebBridge` の Web 送り |
| 4 | Web の `features/inventory`・`recipe`・`blockInventory`・`research`、`shared/itemMove` などの掴み系 |
| 5 | `web/features` の残りすべて（challenge・eventLanguageGate・tutorial・toast） |
| 6 | 次の 6 群をすべて消す |
| | ・`jp.juha.cefunity`、MainGameUI の CEF Canvas、`CefRuntimeBundler` など CI・配布の CEF 工程 |
| | ・`Client.WebUiHost`、`moorestech_web/` |
| | ・Kestrel と AspNetCore 系 NuGet（D6 で HttpListener を選んだ場合） |
| | ・DSL の Web 実装 |
| | ・所有表・委譲・WebUiHealth |
| | ・`WebPinId` などの Web という名前 |

**規模・期間・判断（推測）**

- 期間は合計 12〜16 週（段階 2 と 3 の暗転作業は重なる）。律速は Editor の本数（Mac mini で同時 3 本が目安）と、ユーザーのマージの 2 つ
- PR は 55〜70 本
- UITK の新規コードは本体約 2 万行・テスト約 1 万行。削除は約 45〜50k 行
- 裁定が要る論点は 20 件（§8）。本筋の D1〜D9 は段階 0 に入る前に決めておく（D10〜D20 は推奨の既定で進めてよい）

---

## 1. 段階一覧

| 段階 | 目的 | 主な対象 | 完了の物差し（CEF 無し起動ラダー §4.4） | 出荷 | 規模（推測） | 期間（推測） |
|---|---|---|---|---|---|---|
| 0 地ならしと実測 | 見た目を変えずに、撤去を塞ぐものを外し、共存の物理を実測し、並列作業の土台を置く | 遠隔実行、状態の移設、OneJS、DSL、台帳・受入表・参照キャプチャ、凍結、実測 S1〜S15 | Web ホストを起動しなくても、状態クラスと遠隔実行が生きている | 可（見た目不変） | 40〜60 ファイル。新規 1〜2k 行、移設 2〜3k 行、シナリオ 41 本 | 1.5〜2 週 |
| 1 基盤と命綱 | 全画面が乗る基盤を作り、Esc 以下を CEF から外す | 層・所有表・入力・テーマ・フォント・i18n・アンカー・L1・DSL の UITK 版・殻。ポーズ・設定・バグ報告・トースト・WebUiHealth | `uitk-lifeline` | 可 | 基盤 60〜90 ファイル（C# 4〜5.5k 行）、画面 25〜35 ファイル（1.5〜2.5k 行） | 2.5〜3 週 |
| 2 常駐表示 | 画面を開いていない時間を CEF から外す | HUD 10 種、通知、カーソルツールチップ、ワールドピン、チュートリアル枠、スキット | `uitk-opening-hud` | 可 | 70〜90 ファイル、3.5〜4.5k 行 | 1.5〜2 週 |
| 3 建築の束 | D&D・ホバーツールチップ・IME 付き検索・入力の取り合いを本番で踏む | ビルドメニュー、ホットバー、モーダル | `uitk-build-bp` | 可 | 40〜55 ファイル、3〜4k 行 | 1〜1.5 週（段階 2 と並走で暗転作業） |
| 4 持ち物の束 | 掴みでつながる面を一度に UITK へ切り替える | インベントリ、装備、掴み、レシピ、ブロック UI 全族（列車を含む）、研究 | `uitk-production-loop`、`uitk-research-train` | 可（束の切替時） | 100〜150 ファイル、8〜11k 行、テスト 300〜400 件 | 3〜4 週 |
| 5 残りと Web の空化 | Web が描くものを 0 にして 1 周期出す | チャレンジ、言語ゲート、Web の横断層 | `uitk-challenge-gate` | 可（CEF は描かない） | 25〜40 ファイル、1.5〜2.5k 行 | 1 週＋1 周期 |
| 6 撤去 | 完了の定義（§5.3）を満たす | CEF・Web・共存機構・名前 | ラダー 6 本が CEF の存在しないビルドで通る | PR ごとに可 | 削除が中心。PR 5 本 | 1〜1.5 週 |

---

## 2. 各段階の詳細

### 2.0 全段階の共通条件

各段階の終わりに、次をすべて満たす。

1. 移した面の受入表（§4.1）の全行が、L0〜L4 のどれかで緑か、「捨てる（理由）」に分類されている。L0 と L1 は CI の EditMode シャードで回る（S13 が不成立ならローカルの Editor レーンで回す）。
2. 所有表のテスト（G5）が緑。
3. 録画シナリオが全本緑。移した面に触るシナリオは、DSL の UITK ロケータで解決している（ログで確かめる）。対象外は壊れている 8 本（bd `moorestech-wwxy`）だけ。
4. それまでの段のラダーのシナリオが、Web ホストを起動しない設定で緑（§4.4）。
5. Unity の Error ログが 0。
6. moores-playtest-release の工程で、Windows 検証機の通し検証と Mac mini の Steam 確認が通る。
   - 工程の順は staging → 検証機 → 手動で playtest へ反映（`.decisions/2026-09-24-Steamの自動アップロード先はplaytest-stagingとしplaytest反映はWebUIで手動実行する.md`）
   - 実機の確認はこの工程に組み込み、チェックボックスで後回しにしない（前回は実機検証が 3 か月滞留した。shipping §6.2）
7. 切り替えた束の UITK の CPU 時間（`UIElementsUpdatePanels` と `UIElementsRepaintPanels` の合計）を台帳に記録する。

### 2.1 段階 0 地ならしと実測（見た目は不変）

**目的**

- 挙動を変えずに、後回しにすると全段階を塞ぐものを片付ける
- 共存の物理を実測して、段階 1 以降の前提を確定する
- 並列作業の土台を置く: 台帳・受入表・参照キャプチャ・凍結・bd

**作業**

| # | 作業 | 中身と根拠 |
|---|---|---|
| 0-1 | 使っていないパッケージを外す | `com.singtaa.onejs`（manifest 13 行目）と `com.annulusgames.debug-ui`（3 行目）を manifest と lock から外す。どちらも参照は 0 件。OneJS は配布物に quickjs と Resources 2.2MB を入れている（shipping §5） |
| 0-2 | 実測 S1〜S15（§4.8） | 使い捨ての隔離 worktree で行う。PoC ブランチ `poc/uitk-hard-cases` を土台にしてよい。1 本 1 日で打ち切り、「不可」も結果として記録する。master へ入れるのは結論と、UI Test Framework の manifest 追加だけ |
| 0-3 | 遠隔実行を Kestrel から切り離す（D6） | 下の「0-3 の詳細」 |
| 0-4 | 状態を Hub の外へ移す（抽出 PR。挙動は変えない） | 下の「0-4 の詳細」 |
| 0-5 | `UIStateControl.OnStateChanged` を UniRx にする | 今は `event Action<UIStateEnum>`（`Scripts/Client.Game/InGame/UI/UIState/UIStateControl.cs:16`）。AGENTS.md「イベント発火に Action を使わない」に反している |
| 0-6 | 差し込み記法を統一する（D8） | 下の「0-6 の詳細」 |
| 0-7 | DSL を描画系に依存しない形にする（この段は Web 実装だけ） | 下の「0-7 の詳細」 |
| 0-8 | 台帳・受入表・参照キャプチャ | 下の「0-8 の詳細」 |
| 0-9 | 凍結とゲート（D7） | G1（`web/features` への新規ファイル追加の禁止。許可リスト付き）、G2（新規 uGUI の禁止。既存の `WebUiGateAuditTest`）、G4（台帳の完全性）を入れる。進行中の Web の PR は完了させてから凍結する。10-15 以降の `moorestech_web` へのコミットは 44 本あり、画質設定などが進行中（実測） |
| 0-10 | Web ホストを起動しない Editor 限定スイッチ | `#if UNITY_EDITOR` で囲み、ファイル末尾に置く。Hub が null の経路（起動失敗と同じ形）を PlayMode で再現する。CEF 無し起動ラダー（§4.4）の土台 |
| 0-11 | bd | epic を立て、段階と単位を子にする。派生は `--parent` で系譜を残す |

**0-3 の詳細（遠隔実行）**

- `/api/remote-exec` は Web UI の Kestrel に相乗りしている（`Scripts/Client.WebUiHost/Boot/WebUiEndpoints.cs:97-101`、ADR 0072）
- 今は Web ホストの起動に失敗すると遠隔実行も開かない（`WebUiStartup.cs` の警告文「遠隔実行も開きません」）。検証機で CEF の不具合を調べる手段まで失う
- やること
  - `RemoteExecEndpoint` を `HttpContext` に依存しない形にする
  - 起動の Web 依存（`InitializeScenePipeline.cs:68-73` の `KestrelPort` 受け渡し）を外す
  - `RemoteExecEndpointTest` と `PlaytestBootUnityCallbackBindingTest` を直す
  - `scripts/playtest/remote-exec.sh` は `access.json` 経由のまま使えるようにする
- ADR 0072 の改訂を同じ PR に入れる

**0-4 の詳細（状態の移設）**

- 対象
  - `ResearchTopic._nodeStates`、`ChallengeTopicState`、`BlockNetworkInfoCache`
  - `BlockInventoryTopic.TrackBlock` の監視判断
  - `OpenTrainTimetableTracker`・`TrainTimetableFetcher`
  - `NotificationTopic` の連番と移行告知
  - `WebUiModalService`（static をやめ、`Client.Game` のインターフェースにする）
  - `BlueprintNameInputWebBridge` の状態部分
  - `EventLanguageGate`
- 移す先は `Client.Game` の DI（MainGameStarter の LifetimeScope）
- なぜ要るか: 今はこれらが `WebUiGameBinder.Bind()` の中でしか作られない。Hub が null だと丸ごと作られない（`Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs:48-55`）
- 移した topic は「型付きの購読元」と「JSON アダプタ」に分ける（§3.2）
- 前例は ADR 0052 の「抽出 PR と削除 PR の 2 本」

**0-6 の詳細（差し込み記法）**

- `Localization/localization.csv`（422 キー）のうち、位置指定 `{pN}` を使う 32 キー（全言語列で 190 か所）を名前付きにする（実測）
- 名前付きはすでに 44 キー（370 か所）ある（実測）
- `LocalizationTextInterpolator` を名前付きだけにする
- C# の呼び出し元を同じ PR で直す。例: `TooltipLine.cs`・`ConnectServer.cs`・`RemoteConnectionDenyReason.cs`・`LoadingProgressLog.cs`
- mod 同梱の CSV に差し込みは 0 件なので、mod への波及は無い（実測: `moorestech_master/server_v8/mods/moorestechAlphaMod_8/localization/localization.csv`）

**0-7 の詳細（DSL）**

- `IUiElementLocator` を入れる。Find（可視・矩形・文字列・状態フラグ）を返す
- `ClickWebUi` 系を `ClickUi` 系へ一度に改名する
- 対象は Web UI に実質依存する 41 本（実測）
  - DSL の Web 操作を使う 32 本・151 か所
  - `PlaytestDomQuery.Query` を直接呼ぶ 19 本・27 か所
  - `WebUiInputExclusivity` を参照する 3 本
- 直接呼び出しはロケータの照会 API に置き換える。`data-selected` などの属性は「状態フラグ」として抽象化する（Web は属性、UITK はクラス）
- スキル文書 4 本も同じ PR で直す: `write-scenario.md`・`input-injection.md`・`place-blocks-via-ui.md`・`run-scenario.md`
- 旧名は残さない

**0-8 の詳細（台帳・受入表・参照キャプチャ）**

- `docs/uitk/` に入口文書 `MIGRATION.md`、`TODO.md`、`disposition.md`、`acceptance/<機能>.md` を作る
- 旧 `docs/webui/MIGRATION.md` は archive への案内にする
- 棚卸しは 3 軸で突き合わせる
  1. `web/features` の 20 機能と `shared`
  2. `web/app/App.tsx` のマウント点と Portal
  3. C# の topic 29・action 44・アンカー（静的 19、動的 prefix 6）・シナリオが使う testid 53 種（topic・action・testid は実測）
- 状態機械の外にあるもの（常駐 HUD・Ctrl+U・背景スキット・カットシーン中の退避・カーソル追従）は別表にする。前回は状態機械だけで洗い出して背景スキットを見落とした（shipping §6.2）
- 所有表の初版（全部 Web）もここで置く
- 受入表は、Playwright のテスト名 213 件と vitest の describe・it 名を、スクリプトで機械的に抜き出して作る
- 参照キャプチャ
  - Web 版の全画面と主要な状態を撮る（Playwright の mock-host と PlayMode）
  - 通知・脈動・ドラッグガイドは短い動画で残す
  - 置き場は repo の外（`../moorestech_logs` か share）。台帳からリンクする
  - 束を消した後も「意図した見た目」を辿れるようにするため

**完了条件（観測できる形）**

- Windows のビルドログで、`Plugins/x86_64` に `quickjs_unity.dll` が無い。それ以外の差分が無い。
- Windows 検証機の配布ビルドで、次の 3 つを確かめる。
  - `--remoteExec` 付きで起動すると、`remote-exec.sh` が `Succeeded` を返す
  - 付けなければ待受が開かない
  - Web ホストの起動を失敗させても、遠隔実行は開く
- Editor 限定スイッチで Web ホストを起動しない PlayMode でも、研究・チャレンジ・ブロック詳細・時刻表・モーダル・通知の状態が DI から取れる。EditModeInPlayingTest で確かめる。
- 台帳の未分類が 0（スクリプトで数える）。受入表が 20 機能分あり、行数が抽出元と一致する。
- 改名したシナリオの合否が、改名前と同じ。
- 実測 S1〜S15 の報告が証拠付きでそろっている。
  - 証拠はログの行・スクショ・数値
  - S1・S2・S9・S13・S15 が成立しない場合は、段階 1 に入らず裁定に上げる
- 凍結ゲートが、わざと足した新規ファイルで赤になる（ゲート自体のテスト）。

**出荷**: 可。テスターから見える差は無い。

**リスクと潰し方**

| リスク | 潰し方 |
|---|---|
| 移設で topic の配信タイミングが変わる | 抽出 PR は「移動と配線」だけにする。既存の C# テスト（ビューモデル 90・アクション 36・状態ストア 28）と全録画シナリオで回帰を見る |
| 遠隔実行の載せ替えで、検証機の運用（以降の全段階の検証路）が止まる | 旧経路と新経路の切替を 1 PR にまとめ、検証機で通してから master へ入れる。待受の方式は S14 の結果で決める |
| DSL の改名でシナリオが壊れる | 機械的な置換に限る。照会 API への置き換えは 1 本ずつ合否を突き合わせる |

**規模と並列（推測）**

- 4〜5 本で並列（Editor 3）。分け方は次の 4 群
  - 遠隔実行
  - 状態の移設（`WebUiGameBinder.cs` はこの 1 本だけが触る）
  - DSL
  - OneJS・台帳・受入表・凍結（Editor 不要）
- 実測は別に Editor を 1〜2 本使う。IME は人手で確かめる（Windows 検証機と Mac）

### 2.2 段階 1 基盤と命綱

**目的**

- 全画面が乗る基盤を作る
- ポーズ一式を UITK に移し、CEF が死んでも Web ホストが起動しなくても、Esc でセーブ・セーブして終了・設定・バグ報告ができる状態を毎週の playtest に出す
- CEF が止まったことを、テスターと開発者の両方に届ける

**命綱を第一弾にする理由**（D2）

- テスターの実害が「UI がまるごと死ぬ・報告もできない」に集まっている（実測。Discord ミラー）
  - 6 時間放置で cef-unity のプロセスが消え、Tab・Esc・T・R と施設の UI が一切開かなくなった（2026-10-02 https://discord.com/channels/888315641709330433/1555576468828987532 ）
  - helper のメモリが伸び続け、約 14,600MB 使っていたところで UI がフリーズした。Esc の連打だけで 51MB → 170MB に伸びる（https://discord.com/channels/888315641709330433/1554924554697187389 ）
- 束 D（ポーズ・設定・トースト）は掴みも D&D も持たず、単独で切り出せる（screens §7.1）
- ポーズのアンカー（`pause.*`）は、マスタからの参照が v8 で 0 件。マスタが参照するのは 9 件だけ（実測）
- IME・ESC・フォーカス・ローカライズ・フォント・モーダル層の難所を、掴み抜きで最初に通せる

**対象**

- PauseMenuPanel（トップ、サブページ、切断の表示、入れ子のポーズ〈スキット中 skitPause・乗車中 trainPause。ADR 0035〉）
- 設定 3 種（画質のプリセットと個別設定、言語、ダッシュ方式）
- バグ報告フォーム（種別・本文・送信・配送状況の 1 行・下書き）
- トースト層（バグ報告の結果と、良性でないコマンド拒否の分）。通信失敗のトーストは、Web が残る間 Web に残る
- system-notice 層と WebUiHealth（§3.11）

**作業（基盤。設計は §3）**

- UI ルート
  - `GameUiRoot`（UIDocument 1 枚）を MainGameUI.prefab に事前配置する（`uloop execute-dynamic-code` で行う）
  - PanelSettings は Scale With Screen Size、基準 1280×720、Expand
  - 層 enum と層コンテナ（`PickingMode.Ignore` を既定にする）
- ルータと所有表: SCREEN_TRAITS の C# 版、所有表、Web への委譲（R1・R2）、G5 のテスト、Editor 限定の上書き（R9）
- 入力
  - `UiPointerHitTest` に Pick を論理和で足す。呼び出し側の 23 ファイル・28 か所は変えない（実測）
  - `WebUiInputExclusivity` を `UiInputExclusivity` へ改名して汎用化する
  - ESC ラッチ
  - UI 専用のアクション資産。MainGame の `DefaultInputActions`（`Assets/Scenes/Game/MainGame.unity:8435`）を uloop 経由で差し替える
  - 入力欄以外は `focusable=false`
- テーマとトークン
  - `Tokens.uss` に全トークン約 238 個を機械的に移す
  - UITK 列の α は、この段で使う分を実機で決める（D10）
  - `Theme.uss` は PoC の `PocControls.uss`（146 行、PoC ブランチ）を初版にする
- フォント: TextCore の SDF FontAsset（JP・EN・KR）とフォールバック
- ローカライズ: `LocalizedLabel`、InvariantCulture の数値書式、UXML の生文字の禁止
- アンカー: `AnchorIds` を C# の唯一の正本にする（`web/shared/tutorialAnchor/anchorIds.ts` から移す）。`TutorialAnchor.Mark` と登録簿も作る。枠の描画は段階 2
- テストの土台: `UiTestHost`（本番のテーマ・フォント・1280×720 を当てる）、`FakeXxxModel` と `RecordingXxxCommands` の雛形、L2 の規約テスト
- DSL: `IUiElementLocator` の UITK 版（§4.4）
- スキルとレビュー: `ref-uitk-design` の初版、moores-code-review の UITK 観点の初版
- 殻: 段の終わりに integrator が全画面ぶん作る
  - 中身は空の View・Presenter・UXML・USS、ルータへの登録（所有者は Web）、テストクラスの雛形、受入表へのリンク
  - 以降の束を、共有ファイルに触れずに並列で埋められるようにするため

**作業（画面）**

- `PauseMenuState` と `PauseMenuStateService` は C# のまま使う。作るのはビューと Presenter だけ
- `pause_menu.*` と `bug_report.submit` のハンドラを、型付きコマンドと JSON アダプタに分ける（host §3）
- ポーズ系の状態では、Web は暗幕も操作できる要素も描かない（R1・R10）
- 常駐 HUD（ホットバー・装備 HUD）は、Web が表示だけで描き続ける
- 操作音は、ビューからサウンドのサービスを直接呼ぶ
- 切替 PR の 1 周期後の削除 PR で、Web の `features/pauseMenu`・`features/settings`・テスト・`App.tsx` のマウントを消す

**完了条件（観測できる形）**

- 共通条件 1〜7 を満たす。
- ラダー 1 `uitk-lifeline` が緑。
  - Web ホストを起動しない設定で、「Esc → 設定で言語を切り替える → 日本語の本文でバグ報告を送る → セーブして終了」を通す
  - 報告バンドルの manifest に `webUiHealth=Dead` が入っている
- Windows 検証機の配布ビルドで、ゲーム中に自分が起動した CEF の PID を落とす（人手で 1 回）。次の 3 つを確かめ、結果を台帳へ書く。
  - 知らせが出る
  - 凍った絵が消える
  - Esc でセーブと報告ができる
- 所有表のテストで、ポーズ系の状態の暗幕と操作できる要素を Web が描いていない。
- 委譲中の Web が action を発行しない（S15 の受入。action ログが 0 件）。
- IME の手動確認（Windows と Mac）の記録がある。対象はバグ報告の本文（複数行）。変換・確定・取消と、変換中の ESC で画面が閉じないことを見る。
- 入れ子ポーズの 2 経路（スキット中・列車中）の録画がある。
- 全画面の殻がコンパイルされ、ルータに登録されている（テスト）。

**出荷**: 可。テスターから見える変化は次の 3 つ。パッチノートに「UI 移行中」と書く。

- ポーズ画面の見た目（半透明の濃さ・字の太さ）
- ポーズ中、Web の HUD が UITK の幕の下に入る
- CEF が止まったときの知らせ

**リスクと潰し方**

| リスク | 潰し方 |
|---|---|
| ESC が二重に処理される | ESC は C# の UIState に一本化する。ポーズ系の Web の ESC 経路は、委譲中は動かない。ESC ラッチを L1 で固定する |
| 入れ子ポーズで、UITK のポーズが Web のスキット窓の下に隠れる | S1 で前後を確定し、録画で確かめる。段階 2 でスキットも UITK になる |
| 言語切替で、片方の系だけが再描画される | 同じ録画で両系を確かめる |
| 基盤を作り込みすぎて段階 1 が長引く | 部品は「その段で使うもの」だけを作る（§3.4）。例外は殻と、全画面が使う基盤（層・入力・テーマ・i18n・アンカーの登録簿） |

**規模と並列（推測）**

- 基盤 60〜90 ファイル（C# 4〜5.5k 行、USS 0.8〜1.2k 行）、画面 25〜35 ファイル（1.5〜2.5k 行）、テスト 120〜180 件
- 共有ファイル（層 enum・所有表・composition root・Tokens.uss・AnchorIds・DSL のロケータ本体）は integrator 1 人が直列で持つ
- ほかに 3 本を並べる
  - テーマ・フォント・GamePanel
  - 入力・DSL・テストの土台
  - ポーズ・設定・バグ報告・WebUiHealth

### 2.3 段階 2 常駐表示（画面を開いていない時間）

**目的**

- テスターが一番長く見ている「ゲーム画面」と「ストーリー」を CEF から外す
- 完了時、GameScreen・PlaceBlock・DeleteBar・Story・TrainHUDScreen の各状態で、Web が描くのは「表示だけのホットバーと装備 HUD」だけになる

**対象**

- 入力の無い HUD
  - Crosshair、KeyHintHud、KeyControlHintHud、ProgressBar（採掘ゲージ）、TrainRidingHud
  - CurrentChallengeHud（アンカー `challenge.current-hud`。マスタが参照）
  - PlacementModeHud、DeleteModeWarningBands と DeleteBoxHeightHud
- 通知（獲得通知の集約と出入りの演出）
- CursorTooltip（topic `ui.tooltip`）とワールドピン
- UITK 側のチュートリアル枠（UITK のアンカーだけを照らす。Web の TutorialOverlay は段階 5 まで残る）
- スキット（会話窓・タイプライタ・選択肢・ツールバー・暗転・背景スキット）
- UITK 側の Ctrl+U とカットシーン中の全体非表示
- 対象外: ホットバーと装備 HUD。束（B と A）に縛られるため、段階 3・4 まで Web に残る。player 案の「状態ごとに描画者を分ける」はとらず、束を守る

**作業**

- 基盤
  - UiMotion（§3.6）
  - ツールチップ層。オフセットは既存の `OFFSET` を参照する（現在 12）
  - アンカーの枠: 祖先クリップとの交差、脈動、ラベルの反転、R6 の診断
  - Painter2D の図形: FadeRule・GaugeBar・削除モードの斜線帯
  - GamePanel の hud と skit の面。装飾を含めて完成形で作り、後回しにしない
- データ
  - 通知の集約（`web/features/notification/notificationStore.ts`）を C# の通知モデルへ移す
  - 生存尺は既存の `NOTIFICATION_DISPLAY_MS` を参照する（現在 7000ms）。入場 160ms・退場 200ms（ADR 0016）
  - ワールドピンは同じフレームで投影し、WS を通さない
- スキット
  - 旧 UITK の `Scripts/Client.Skit/UI/SkitUI.cs` と `SkitPresentationMode` を撤去する（bd `moorestech-lnsf.8`）
  - スキットの入力は単一消費者なので、描画者を 1 つにする
- 通知の重なり（ADR 0017 は「全画面 UI の裏」）
  - UITK は CEF の上に描くので、Web の全画面パネルが出ている間は UITK の通知層を隠す
  - S1 で背面パネルが安く作れると分かれば、背面パネルに置いて ADR 0017 の形を保つ
  - どちらにするかは agent の既定で決め、台帳に書く
- ホイール: ホットバー（Web）の上のホイールは、今どおり Web の装備切替へ通す。C# 側の `UiPointerHitTest` は `wheel-passthrough` を尊重する

**完了条件**

- 共通条件 1〜7 を満たす。
- ラダー 2 `uitk-opening-hud` が緑。
  - Web ホストを起動しない設定で、次の順に通す
  - オープニングのスキットを選択肢まで進める → 歩いて採掘する（ゲージが出る）
  - 数字キーで選んで設置する（ホットバーの表示は段階 3 から）
  - 獲得通知と、目標 HUD を指すチュートリアル枠が出る
- マスタの `challenge.current-hud` のハイライトが UITK で出る録画がある。
- ADR 0024 の判定役テスト（祖先のクリップ）の UITK 版が 1 本ある。`.decisions/2026-08-20-チュートリアルハイライトのマスク方式とテスト方針.md` の型に従う。
- R6 のテスト: 同じアンカーが両系で解決したら、エラーログが出る。
- ゲーム画面での CEF の 0F-wait（`_enableLog=1` の `block_avg`）を、段階 2 の前後で比べて台帳に書く。下がらなくても完了は妨げない（`docs/webui/cef-unity-frame-rate-followup-2026-07-29.md` §2.2。効果は推測）。

**出荷**: 可。

**リスクと潰し方**

| リスク | 潰し方 |
|---|---|
| チュートリアルの二重描画・取りこぼし | 両系がそれぞれ自分の木で探す（共通のリゾルバは作らない）。系をまたぐ dragGuide は新設しない（マスタは 0 件） |
| スキットの入力が二重に送られる | Web のスキットの入力経路は委譲中は動かない。削除 PR で消す |
| タイプライタで、行の折り返しが途中で変わる | 残りの文字を透明にして配置を固定する（推測。shipping §3.1） |
| 通知を隠すことで、全画面 UI を開いている間に通知を見落とす | ADR 0017 でも全画面 UI の裏なので、実害は小さい（推測）。段階 5 で元の形に戻す |

**規模と並列（推測）**

- 70〜90 ファイル、C# 3.5〜4.5k 行、UXML/USS 約 1k 行
- 並列は 4 本（Editor 3）
  - HUD とツールチップ
  - チュートリアル・アンカー・ワールドピン
  - スキット
  - 通知と UiMotion
- 同じ期間に、Editor の要らないレーンで次を始める
  - 段階 3 の純ロジックの移植
  - 段階 4 の純ロジックの移植（`shared/itemMove` など。L0）

### 2.4 段階 3 建築の束（ビルドメニュー・ホットバー・モーダル）

**目的**

- 最初の操作の多い画面で、D&D・ホバーツールチップ・IME 付きの検索欄・スクロールスパイ・入力の取り合いを本番で踏む
- ホットバーは常駐 HUD なので、Web のインベントリ画面と同時に出る。共存の入力規則（R1・R4）が本番で試される

**対象**

- BuildMenuPanel 一式
  - カテゴリのサイドバー、検索欄（IME）
  - 1 本のスクロールに並ぶカテゴリ群（スクロールスパイ・smooth scroll）
  - ホバーで固定される詳細サイドバー
  - BP の右クリック削除、不足時のツールチップ
  - セッション中の状態の保持（2026-08-05 の裁定群）
- HotbarPanel（9 枠、選択、D&D の割当・入替・解除）
- モーダル層と BP 名入力
  - `WebUiModalService` の後継。単一消費者なので、描画者を UITK に一本化する
  - モーダルを使うのは BP 名入力だけ（実測）

**作業**

- 束の中身は段階 2 の間に暗転で作り始め、そろったら所有表を切り替える
- 部品
  - press ジェスチャ。閾値は既存の `usePressGesture` を参照する（現在 5px）
  - D&D 層（`panel.Pick` と落とし先の印のクラス）
  - HoverTooltip（祖先がスクロールしたら引っ込める）
  - SlotFrame・ItemSlot・SlotGrid・PlacementTargetFace、縦の ModeSwitch、IconButton
  - スクロール領域（components.md §8.10 の様式）、TextField（§8.9）
- ロジックの移植
  - 対象: `buildMenu/logic`、`hotbarDnd.ts`、`placementTarget`、`materialTooltipText`
  - `useBuildMenuCategoryScroll.ts`（305 行）の計算部分は、先に L1 で固定してからビューを作る
- データ
  - `build_menu.entries` と `local_player.hotbar` を型付きの購読元にする
  - `build_menu.select`・`blueprint.delete`・`hotbar.*` を型付きコマンドにする
- 床の一致: 装備 HUD は段階 4 まで Web に残る。ホットバーと床を共有するトークン（`--equipment-bottom: var(--hotbar-bottom)`。ADR 0013）が、両系で同じ値になることをテストで見る
- アンカー: `build-menu.entry-*` とホットバー系を UITK で付ける

**完了条件**

- 共通条件 1〜7 を満たす。
- 受入表
  - 由来は buildMenu（P 19・C 22・L 30）、hotbar（P 6・C 7・L 3）、modal（vitest 8）（tests §2）
  - 例: 「エントリを 5px を超えて動かし、ホットバーの枠 1 に落とすと、`AssignHotbar(1, id)` が 1 回だけ発行され、`SelectBuildMenu` は発行されない」
- ラダー 3 `uitk-build-bp` が緑。内容は「B → 検索 → エントリをホットバーへドラッグ → 選んで設置 → BP をコピーして名前を付ける」。
- 共存の受入
  - Web のインベントリ画面を開いたまま UITK のホットバーをクリックしても、Web の action が 0 件
  - UITK の検索欄に入力している間は、ゲームのキーも Web の document 全域のキー処理も動かない
  - 検索欄の ESC は、フォーカスを外すだけで画面は閉じない
- IME の手動確認（検索欄・BP 名。Windows と Mac）。

**出荷**: 可。

**リスクと潰し方**

| リスク | 潰し方 |
|---|---|
| Web が UITK 上のクリックを拾って二重に発火する | R1 と S15。共存の受入テストで、action ログが 0 件であることを見る |
| 捕捉中のドラッグにホバーツールチップが出る | PoC の罠。press ジェスチャ側で最初から抑止する |
| スクロールで子の `GeometryChangedEvent` が来ず、スクロールスパイやアンカーが追従しない | `scrollOffset` を監視して判定する |
| 装備 HUD（Web）とホットバー（UITK）の床がずれる | 床トークンの一致テスト。1280×720 と横長の 2 解像度で録画を撮る |

**規模と並列（推測）**

- 40〜55 ファイル、3〜4k 行、テスト 100〜140 件
- 部品（D&D・ジェスチャ・スロット）を 1 本が先に持つ。その後、ビルドメニューとホットバー・モーダルの 2 本で並べる

### 2.5 段階 4 持ち物の束（束ごと一度に切り替える）

**目的**

- 掴みでつながる面を、UITK にまとめて切り替える
- 研究画面も InventoryPanel を同時に出し、掴みを許す画面なので、同じ束に入れる（`uiScreenRouting.ts` の SCREEN_TRAITS で researchTree は `grab: true`、`web/app/App.tsx:95,100` で InventoryPanel と ResearchTreePanel が同時にマウントされる。実測）
- 列車族も subInventory で InventoryPanel を使うので、同じ束に入れる

**対象**

- InventoryPanel、EquipmentPanel
  - HUD とホイールの循環。循環は C# の 1 か所へ移す（`web/features/inventory/EquipmentPanel/index.tsx:55-80`）
- GrabOverlay、スロットの HoverTooltip
- RecipeViewer と ItemListPanel
- BlockInventoryPanel の全族
  - 汎用族: SectionStackView（チェスト・機械・発電機・採掘機・ポンプ・パイプと液体）
  - 特殊族: FilterSplitter、ElectricToGear、GearToElectric、ElectricPole、Gear・Shaft・GearBeltConveyor、ベルトの流量、電力網、歯車網
  - 列車族: 列車インベントリ、駅名、プラットフォーム、時刻表
- ResearchTreePanel（TreeView・ノード 4 状態・詳細ペイン・研究ボタン）
- インベントリ内の DragGuide
- アンカー: `recipe.craft-button`、`recipe.item-<itemId>`、`research.node-<guid>`（マスタは 7 件）

**作り方（暗転のまま波で並列に作る）**

| 波 | 中身 | 本数 |
|---|---|---|
| 4-α 中核 | `shared/itemMove` の計画の移植（左・右・ダブルクリック・Shift 配分）、掴み層、分割ドラッグ、右ドラッグ配り、GamePanel の default と craft、SectionStackView とレジストリ、RecipeRow（`.decisions/2026-08-20-レシピ行は独自gridを維持しSlotGridの例外とする.md`） | 1〜2 本が直列で先に作る |
| 4-β 並列 | 次の 5 群に分ける。(1) インベントリ・装備・レシピ・一覧（長押しクラフトはパネルのスケジューラで動かす。コンテナクエリは `GeometryChangedEvent` で計算する）、(2) 機械と汎用族（`.decisions/2026-08-30-機械UIはタブを廃しレシピ選択とインベントリをSatisfactory方式で往復する.md`）、(3) 特殊族（BarSeries、←→ での窓の切替）、(4) 列車族（駅名の IME、時刻表の楽観更新）、(5) 研究（TreeView: パンとズーム 0.4〜2.5、慣性 τ=325ms、Painter2D の線） | 4〜5 本（Editor 3） |
| 4-γ 統合 | 同時に出る組（playerInventory、subInventory、researchTree）を確かめる。Editor 限定の上書きで全録画シナリオを回す。所有表の切替 PR を出す | integrator |

- 切替後の流れ: staging → 検証機・Mac → playtest。1 周期後に削除 PR を出す

**完了条件**

- 共通条件 1〜7 を満たす。
- 受入表の由来（tests §2）
  - インベントリ・装備（P 20・C 16・L 57）
  - レシピ（P 18・C 10・L 19）
  - ブロック UI（P 42・C 27・L 45）
  - 列車（P 6・C 6・L 14）
  - 研究（P 18・C 21・L 37）
- ラダーの 2 本が緑。
  - `uitk-production-loop`: Tab → 長押しクラフト → 石窯に燃料と素材を入れる（掴み・Shift・右ドラッグ）→ 取り出す → 整理
  - `uitk-research-train`: 研究を 1 つ完了 → 駅名を日本語で付ける → 時刻表を組んで自動運転を入れる
- 機械 UI を開いたまま中身が更新される録画を、UITK で通す（`.decisions/2026-10-18-液体UIは開いたままRefresh更新をunityプレイ録画テストで検証する.md`）。
- 台帳のブロック種別の全行が分類済み。マスタのアンカー（`recipe.craft-button` と研究ノード 7 件）が実ゲームで解決する。
- 削除 PR の後、Web 側で掴みを扱うコードが 0。`screenAllowsGrab` の参照も 0。
- インベントリと研究ツリーを開いたときの UITK の CPU 時間を記録する。目安は 1ms 以下（推測。PoC では CSS 表現 12 項目で 0.27ms）。

**出荷**: 切替のときに可。暗転中も master は出荷できる。

**リスクと潰し方**

| リスク | 潰し方 |
|---|---|
| 最大の束を一度に切り替えるので、退行の幅が大きい | 次の 3 つで抑える。(1) 切替の前に、Editor 限定の上書きで全録画シナリオと受入表を回す。(2) staging から検証機を通してから playtest に出す。(3) 切替 PR は 1 行なので、戻すのも 1 行（削除 PR は 1 周期後） |
| ブロック UI（11 種以上）の網羅漏れ | 台帳でブロック種別ごとに行を持つ |
| 族の間で衝突する | 4-α でレジストリとセクションの枠を確定させてから 4-β に入る |
| 仮想化した一覧の行のアンカーが、画面外では存在しない | 研究ツリーは仮想化しない（transform で動かす）。仮想化する一覧は Bind 時にアンカーを付け替え、missing の扱いを受入表に書く |
| `MouseMove(from, to)` が途中のスロットに PointerEnter を出さない（L1） | 1 スロットずつ動かす（tests §5.5） |
| 暗転の期間中、テスターはインベントリ系の CEF の不具合（helper のメモリの伸び）を被り続ける | D9 の帰結として受け入れる。段階 1 の命綱と WebUiHealth で、止まっても報告できるようにしてある |

**規模と並列（推測）**

- 100〜150 ファイル、8〜11k 行、テスト 300〜400 件
- エージェント日数で 15〜20 日

### 2.6 段階 5 残りと Web の空化

**目的**

- Web が描くものを 0 にする
- CEF を「起動するが何も描かない」状態で 1 周期出し、見落としを撤去の前に表へ出す。見落としの例は、状態の外にあるオーバーレイ

**対象**

- ChallengePanel（研究の TreeView を再利用。カテゴリのボタン列）
- EventLanguageGate（出展モード）
  - DI より前に出す全画面ゲート
  - `GameUiRoot` を起動の早い段で使えるようにするか、Starter が所有するゲート用のパネルを分けるかは、段階 5 の最初に決める（推測）
  - 辞書を通さない文言（`DictionaryIndependentText`・`preDictionaryText.ts`）は C# の定数へ移す
- Web の横断層の残り: TutorialOverlay、ToastHost、DragGuide、再接続オーバーレイ、辞書エラー
- `AppErrorBoundary` の代わり
  - これまで担っていたのは「UI の例外に誰が気づくか」
  - UITK のビューの例外は、LogError とバグ報告の manifest に出す
- Mantine の素の見た目のままの画面の様式化（D14。チャレンジなど。ポーズのボタンとトーストは段階 1 で済ませる）
- 通知を ADR 0017 の形（stage の背面）へ戻す

**完了条件**

- 共通条件 1〜7 を満たす。
- ラダー 5 `uitk-challenge-gate` が緑。内容は「チャレンジ一覧を開く → 出展モードの言語ゲートを通る」。
- `moorestech_web/webui/src/features` が空で、`App.tsx` は空のシェル。
- 全録画シナリオが緑。DSL の Web ロケータが一度も要素を返さない（ログで確かめる）。
- 台帳の全行が「UITK 済」か「除外（理由つき）」。背景スキットは受入表に明記する。
- 1 周期の playtest で、UI の欠落の報告が無い。
- 検証機で CEF を起動したまま 1 時間遊び、helper のメモリを記録する。伸びないはず（推測）。

**出荷**: 可。CEF は同梱され起動もするが、何も描かない。

**規模と並列（推測）**

- 25〜40 ファイル、1.5〜2.5k 行。Web 側の削除は大量
- 並列は 3 本（チャレンジ／言語ゲート／横断層と例外の経路）

### 2.7 段階 6 撤去

**目的**: CEF・Kestrel・Web ホスト・Web 資産・共存の仕組みを消し、完了の定義（§5.3）を満たす。

**PR**（直列のチェーン。順序の理由は §5.2）

| PR | 中身 |
|---|---|
| 6a 起動配線と共存機構 | 下の「6a の詳細」 |
| 6b Web ホストと Web 資産 | 下の「6b の詳細」 |
| 6c CEF と配布工程 | 下の「6c の詳細」 |
| 6d NuGet | 下の「6d の詳細」 |
| 6e 文書・スキル・改名・ADR | 下の「6e の詳細」 |

**6a の詳細（起動配線と共存機構）**

- 起動配線
  - `Client.Starter` の `WebUiStartup`
  - `InitializeScenePipeline.cs:73` の `KestrelPort`
  - `MainGameInitializationFinalizer` の `Bind`
  - `EventModeStartGate` の Hub 依存
- 共存機構
  - 所有表・委譲・Editor 限定の上書き
  - DSL の Web ロケータ
  - `UiPointerHitTest` と `UiInputExclusivity` の Web の項
  - WebUiHealth と manifest の該当フィールド
  - 通知を隠す処理
  - Web ホストを起動しない Editor スイッチ

**6b の詳細（Web ホストと Web 資産）**

- `Client.WebUiHost`（asmdef ごと）
- C# の通信契約テスト（131 件）、`WebUiActionInvoker`
- `Client.Playtest/WebUi/*`（`CefInputForwarder`・`CefScreenMapper`・`PlaytestDomQuery`）
- `Client.Tests` のうち WebUiHost を参照する 67 ファイル（実測）
- `moorestech_web/`
- CI の `webui-test` ジョブ、`needs_webui`、pnpm の step
- `WebUiProductionArtifactBuilder`、`StreamingAssets/WebUi` とその `.gitignore`

**6c の詳細（CEF と配布工程）**

- `jp.juha.cefunity`（manifest 45 行目）
- MainGameUI.prefab の CEF 一式を `uloop execute-dynamic-code` で外す
  - CEF の Canvas（RenderMode は Overlay、sortingOrder は 0。実測）
  - RawImage、`CefUnityBrowserSample`、`WebUiCefNavigator`
- `CefRuntimeBundler` と `BuildPipeline` からの呼び出し
- `CefPackageLfsValidator`、`scripts/setup-cef.*`、CI の CefUnity 事前取得
- `scripts/playtest/lib/release-artifact.sh` の CEF helper 検査と、その契約テスト（`scripts/playtest/tests/test-release-playtest-mac.sh`）
- `scripts/playtest/README.md` の CEF raw input の確認
- 共用部品は消さずに整理する
  - `CefLfsPointer` は中立な名前へ改名する（ffmpeg と Harmony の同梱検査が使う）
  - `EnvironmentScrubOnEditorLoad` は `Client.ExternalProcess` 側へ移す
- この PR の中で、Windows と Mac のビルドを回す

**6d の詳細（NuGet）**

- 消すもの: AspNetCore 系（D6 で HttpListener を選んだ場合）と、使われなくなった `Microsoft.Extensions.*`
- 依存グラフを確かめてから消す
- `packages.config` は 74 件で、そのうち 47 件が `Microsoft.AspNetCore.*` か `Microsoft.Extensions.*`（実測）
- `ModelContextProtocol 0.2.0-preview.3` も載っているが、client と server の scripts に `using` が無い（実測）。未使用なら同じ PR で外す（推測）

**6e の詳細（文書・スキル・改名・ADR）**

- 改名
  - `WebPinId`（13 か所。実測）
  - `LocalizationKeys.Ui.Blueprint.WebModal*`
  - `VanillaSchema/challenges.yml` の `web anchor id`（3 か所。実測）
  - `WebUiGateAuditTest`。名前を変え、対象を付け替えて残す
- スキル
  - `ref-webui-design` を廃止し、`ref-uitk-design` を完成させる
  - moores-code-review の ts/tsx 観点 12 本を撤去する
- 文書
  - `docs/webui` を archive にする
  - `docs/development/macmini/unity-editor.md` の CEF の節を消す
- ADR の改訂（§8.3）

**完了条件**

- §5.3。
- 6c は配布物が変わるので、次の 3 つを必ず通してから playtest へ反映する。
  - Windows 検証機
  - Mac の Steam
  - `test-release-playtest-mac.sh`

**出荷**: PR ごとに可。

**規模（推測）**

- 削除が中心で、PR は 5 本。並列は 1〜2 本
- 削除の内訳: `moorestech_web` 約 28k 行（テスト込み）、`Client.WebUiHost` 10.5k 行、テスト 6〜8k 行、DSL の Web 部約 0.7k 行

---

## 3. 共通基盤

### 3.1 置き場と構成

- **asmdef**
  - `Client.Ui` を新設する。名前に技術名（Uitk）を入れない（Web という名前の改名を繰り返さないため）
  - 参照先: `Client.Game`（モデル・コマンド・状態の正本）、`Client.Input`、`Client.Localization`
  - `Client.Game` は `Client.Ui` を参照しない。モーダルの要求などはインターフェース越しにする
  - DI は既存の VContainer、イベントは UniRx を使う
- **ディレクトリ**
  - 基盤: `Client.Ui/Foundation/{Root,Layers,Routing,Input,Motion,Tooltip,DragDrop,Modal,Anchor,Text,Theme,Health}`
  - 部品: `Client.Ui/Components/<部品>`
  - 画面: `Client.Ui/Screens/<機能>/`
  - AGENTS.md の上限（1 ディレクトリ 10 ファイル、1 ファイル 200 行）に合わせる
- **3 層に分ける**
  - View: VisualElement を組むだけ。MonoBehaviour に依存しない
  - Presenter: モデルを購読して View へ push し、操作を型付きコマンドへ渡す
  - Mount: `GameUiRoot` に載せる薄い層
  - テストでは `UITestFixture` の `rootVisualElement` に同じ View を組める（tests §5.3）
- **アセットのロード**
  - UXML と USS は機能の横に置き、1 か所の `UiAssetCatalog` を通してロードする
  - 起動時に全部ロードし、欠けていたら起動の段階で止める（AGENTS.md「必須の値は最も早い段階で止める」）
  - L2 で参照パスの実在を検査する
- **実行時の形**
  - `GameUiRoot` は MainGameUI.prefab に事前配置する。シングルトンの規約どおり、Awake で `_instance` を設定する
  - パネルは 1 枚。Pick とフォーカスが 1 枚の中で閉じる
  - 既存の `SkitPanelSettings.asset`（1920×1080）は流用しない

### 3.2 データ購読層

- **正本には手を入れない**: `Client.Game`・`Client.Skit`・`Client.Input`・`Client.Localization`・サーバー（host §7.5 R2）。サーバー状態の同期（イベントパケット＋初期データ＋購読）も変えない。UITK 化はクライアントの内側だけの話。
- **topic を 2 つに分ける**
  - ①型付きのスナップショットと変化通知（`IReadOnlyReactiveProperty<XxxViewModel>`）を、`Client.Game` に置く
  - ②それを JSON にするアダプタ。Web が使う
  - 分けるのは、その段で使う topic だけ。段階 6 で②を消す
- **フレーム末の畳み込みと間引き**
  - フレーム末に 1 回へ畳む処理（PostLateUpdate）は残す。中間状態を描かないため
  - WS の帯域のための間引きはアダプタ側へ移し、UITK では毎フレーム読む。対象は `ui.progress` の 100ms と歯車の `IntervalFrame(4)`
  - `BlockInventoryTopic` は、コメント（4tick）と実装（`IntervalFrame(4)`）が食い違っている。移植するときに意図を確かめる
- **ViewModel**
  - 不変の sealed class にする。判別 union は、抽象基底と sealed 派生で表す
  - zod の不変条件をこの型へ移す。対象は FluidSlot、変換機の詳細、Tooltip、Hotbar の 9 枠、PlacementMode
- **コマンド**
  - action ごとに型付きメソッドにし、`CommandResult` を返す（Accepted か Rejected と理由の enum）
  - 前提条件の検証（state の一致・解放判定・遷移許可表）はコマンド側に置き、共存中も両系が同じ検証を通る
  - 拒否の理由は開発者ログへ出す（AGENTS.md の fail-closed）
  - 良性の拒否（Web の `BENIGN_ERRORS`）は理由の性質として enum に持ち、トーストを出すかどうかをそれで決める
- **Presenter の規約**
  - `Update()` で毎フレームの同値判定をしない
  - 画面は起動時に 1 回だけマウントし、表示・非表示はルータが切り替える
  - 非表示の間は描画を止め、表示するときに最新値で 1 回描く

### 3.3 層・所有表・共存の規則

**層の並び**

- ルート直下に、層 enum の固定順で兄弟コンテナを並べる（USS に z-index は無い）
- 順序の正本は `web/app/tokens.css` の `--z-*` と `web/app/zLayerTokens.test.ts`。C# の enum と不変条件テストへ写す
- 大筋の順は次のとおり（細部は写すときに確かめる）
  1. backdrop
  2. 背面（通知）
  3. stage（画面・stage 内のオーバーレイ）
  4. viewport の HUD
  5. 掴み
  6. モーダル
  7. ツールチップ
  8. トースト
  9. チュートリアル
  10. system-notice
  11. 全画面ゲート
- 層コンテナと backdrop は `PickingMode.Ignore` を既定にし、L1 で固定する。忘れると「パネル外の右短押し」（ADR 0046）とワールドのクリックが効かなくなる（input §3.1）
- stage 族・viewport 族・背面 viewport 族の三分法（ADR 0013・0017）はそのまま表す

**共存の規則**

| # | 規則 | 守り方 |
|---|---|---|
| R1 | 同じ画面状態で、同じ位置に両系の操作できる要素を置かない。主パネルが UITK の状態では、Web は表示だけにし、入力を受けない | 委譲中の Web は document 全体を `pointer-events: none` にし、キー処理を止める（S15 で効くかを確かめる）。CEF は RawImage 全面で入力を受け続け、領域単位で止める API が無いため（host §1） |
| R2 | 所有表は「(画面状態[, subState, ブロック族], 面) → Web か UITK」の静的なコード表。どの組も所有者はちょうど 1 つ | G5 のテスト。Web へは `ui_state.current` に委譲の印を足して伝え、Web の `screenForUiState` は委譲画面で主パネルを描かない |
| R3 | 面を移すときは「切替 PR → 1 周期 → 削除 PR」の 2 本にする。削除 PR で、Web の該当コンポーネント・document 全域のキー処理・テストを全部消す | G6。前例は ADR 0052 の抽出 PR・削除 PR の型。削除 PR は、切替が playtest で 1 周期通ったら出す。延ばすならユーザーの裁定を取る |
| R4 | 入力の裁きは `UiPointerHitTest` と `UiInputExclusivity` の 2 か所だけで行う | 共存中はどちらも UITK と Web の論理和にする（input §3.7） |
| R5 | 状態クラスは Hub と無関係に作る | 段階 0 の 0-4 |
| R6 | 同じアンカー ID が両系で同時に解決したら、二重描画として理由をログに出す | 黙って先勝ちにしない（AGENTS.md の無音の縮退の禁止） |
| R7 | DSL が同じ testid を両系で見つけたら、失敗させる | tests §6.4 |
| R8 | 掴み・D&D でつながる面は同じ系にする（束を割らない）。系をまたぐ橋は作らない | G5 |
| R9 | 暗転中の束の所有者は Web のまま。Editor 限定の上書きでだけ UITK にして検証する。配布物に切替フラグを入れない | 上書きの API は `#if UNITY_EDITOR` の中でファイル末尾に置く。プレイヤービルドに存在しないので、誤って参照するとビルドが落ちる（G7） |
| R10 | 暗幕は、その状態の主パネルを描く系が描く | G5 |

### 3.4 部品と作る段階

部品は、その段で使うものだけを作る。例外は、全画面が使う基盤と殻（段階 1）。
各部品は、作る段で装飾を含めた完成形にする。磨き込みの段は設けない（後回しの負債を作らないため）。

| 段階 | 部品 |
|---|---|
| 1 | 層マネージャ、ルータ、所有表、GamePanel（default）、PanelActionButton、横の ModeSwitch、IconButton（× は Painter2D）、スクロール領域、TextField（単行・複数行）、トースト、system-notice、LocalizedLabel、KeyCap |
| 2 | UiMotion、CursorTooltip、FadeRule、GaugeBar、斜線帯、GamePanel（hud・skit）、通知の行、チュートリアルの枠とラベル、ワールドピン、スキットの選択肢の板とツールバーのアイコン |
| 3 | press ジェスチャ、D&D 層、HoverTooltip、SlotFrame・ItemSlot・SlotGrid・PlacementTargetFace、縦の ModeSwitch、スクロールスパイ、モーダル層 |
| 4 | 掴み層、分割ドラッグ、GamePanel（craft）、RecipeRow、ProgressArrow、BarSeries、FluidSlot、SectionStackView、TreeView、四隅のブラケット、コンテナクエリの代わり |
| 5 | 全画面ゲート |

**GamePanel の描き方**

- 面のグラデと 4 辺のフェード: `generateVisualContent` の頂点カラーのメッシュで描く
- inset の 2 層: border の入れ子にする
- 右下のグリップと下向きの三角: Painter2D で描く
- PoC で実証した部分と、未実証の部分がある（推測を含む）

**USS に無いものの置き換え**（screens §5）

| CSS の側 | UITK での扱い |
|---|---|
| grid | flex-wrap と固定幅 |
| gap | margin |
| `::before` | 子要素 |
| `:first-child` など | C# でクラスを付ける |
| z-index | 層 |
| コンテナクエリ | `GeometryChangedEvent` |

### 3.5 テーマ USS とトークン

- **ファイル**
  - `Tokens.uss`: `tokens.css` 192 個、`skitTokens.css` 38 個、`slotTokens.css` 8 個、合わせて約 238 個を `:root` へ移す
  - `Theme.uss`: ランタイムの既定コントロールを暗色にする。スライダーの入力部の高さが 0 になる癖への対処も含める
  - `Typography.uss` と、部品ごとの USS
- **トークンの正本は USS**（D13）。C# が値を要るとき（Painter2D・配置の計算）は、`CustomStyleProperty` で解決後の値を読む。トークンを C# の定数に二重に持たない。
- **CSS からの変換規則**

  | CSS の側 | USS での扱い |
  |---|---|
  | rem・em | px にする（1rem=16px） |
  | `calc()` 70 か所 | 計算済みのリテラルにする。導出式はコメントに残し、L2 で一致を検算する |
  | `color-mix`・`rgb(from …)` | 事前に計算する |
  | data 属性 40 か所 | `--modifier` クラスと `EnableInClassList` |
  | z-index 44 か所 | 層 enum |
  | `:nth-child` | C# でクラスを付ける |

- **半透明（67 か所）は Web の値を流用しない**（PoC の実測: 同じ指定が UITK では `#664f29`、Chrome では `#3e311a`）
  - トークン表に「Web の値」「UITK の値」「決め方」の 3 列を持つ
  - UITK の値は、その部品を作る段で実機で決める（D10）
- **L2 で落とす規約**
  - 色は `Tokens.uss` の外では `var()` だけ
  - 構造擬似クラスと z-index は禁止
  - UXML の `text=` にリテラルを書かない
  - `name` の重複は禁止
  - ビットマップの参照は禁止（D12）
  - 合成の太字（`-unity-font-style: bold`）は禁止

### 3.6 アニメーション

- **入退場とホバーは USS の transition** で書く。
  - 追加した直後にクラスを外すと補間されないので、`GeometryChangedEvent` を契機にする
  - `height: auto` は補間できないので、px に固定してから動かす
  - 並べ替えの FLIP は、`GeometryChangedEvent` の新旧の矩形で書く。`schedule.Execute` はレイアウトより先に走るため（PoC の罠）
- **ループ・キーフレーム・smooth scroll・慣性は、自作の `UiMotion` で書く**（100〜150 行の見込み。D11）
  - 対象の 3 系統: 通知の出入り、チュートリアルの脈動、ドラッグガイドの往復
  - 持たせる API: 時計の注入、頭からの再生し直し（通知の集約で寿命を張り直す）、予約した退場の取り消し、完了の通知
  - 要素がパネルから外れたら止まる
- **時間源は実時間**（パネルのスケジューラ、timeScale に依らない。D18）。テストでは `FrameUpdateMs` で進める（S8）。
- **尺と曲線はトークンに置き、既存の値を参照する**
  - 通知の生存は `NOTIFICATION_DISPLAY_MS`（現在 7000ms）、入場 160ms・退場 200ms（ADR 0016）
  - 脈動は 1200ms（ADR 0039）
- ref-webui-design §6 を継ぐ。テストのために尺を 0 にする抜け道は作らない。時計を進めるのは、尺を 0 にすることではない。

### 3.7 ツールチップ・D&D・掴み・モーダル

- **CursorTooltip**
  - `MouseCursorTooltipState` を購読し、同じフレームのポインタから位置を決める
  - 画面端では反転させる
  - カーソルがロックされている間は、画面中央を基準にする（ロック中の UITK のポインタは画面外になるため。input §3.2）
  - 書式は ADR 0019 の値（18px、padding 6/10、max-width 320）をトークンで持つ
- **HoverTooltip**: 祖先がスクロールしたら引っ込める。ドラッグの捕捉中は出さない（PoC の罠: 捕捉中も下の要素に PointerEnter が届く）。
- **press ジェスチャ**
  - tap・drag・abort の 1 つの状態機械にし、`CapturePointer` を使う
  - D&D・パン・ドラッグスクロール・長押しで共通にする
- **D&D**
  - 自作する（PoC で約 140 行）
  - 落とし先は、`panel.Pick` の結果の祖先から、印のクラスを持つ要素を探して決める
  - ゴーストは掴み層に出す
- **掴み絵**は掴み層に 1 つだけ置き、同じフレームの `PointerMoveEvent` で動かす。今は CEF の mousemove 頼みで、カーソルのワープ後に追従しない経路があった（ADR 0020・0074）。
- **モーダル**
  - 同時に 1 件だけ出す。幕を張り、フォーカスを中に閉じ込める
  - ESC は UIState が持ち、入力欄には自動でフォーカスする
- **右クリックメニュー**が要れば、`GenericDropdownMenu` を暗色のテーマで使う（PoC）。

### 3.8 入力とフォーカス

| 項目 | 方針 | 根拠 |
|---|---|---|
| UI 用のアクション資産 | moorestech 専用の資産を作る。Point・Click・RightClick・MiddleClick・ScrollWheel は残し、Navigate は空、Submit と Cancel は使わない | `DefaultInputActions` は Navigate に WASD を含み、メニュー中も WASD で歩く仕様（`.decisions/2026-10-01-メニュー中の視点は回さず移動だけ解放する.md`）とぶつかる |
| フォーカス | 入力欄以外は `focusable=false`。Space・Enter でボタンが押されず、Tab でフォーカスが動かないことを L1 で固定する | `.decisions/2026-10-01-Web UI全体で文字入力欄以外のSpace既定動作を封じる.md`、`…クラフトボタンはSpaceに反応させずジャンプ専用にする.md` |
| ポインタ | `UiPointerHitTest.IsPointerOverAnyUi` を次の形にする。ロック中は false。そうでなければ「UITK の Pick が層コンテナ以外を返す」か「Web の pointerOverUi（共存中だけ）」か「EventSystem（デバッグ用の uGUI）」 | input §3.3 の案 A。呼び出し側の 23 ファイル・28 か所は変えない（実測）。EventSystem 判定は ADR 0052 の agent 前提 1 |
| ホイール | ホイールを通す印を USS のクラス（`wheel-passthrough`）にし、Pick の結果の祖先で判定する | 装備の循環は段階 4 で C# の 1 か所へ移す（UI の上なら止める、印のある HUD の上は通す、建築ツールが占有中なら止める、1 枠以下なら何もしない） |
| 文字入力 | ルートで `FocusInEvent`・`FocusOutEvent` を TrickleDown で受ける。フォーカス中の要素が TextField かを、`UiInputExclusivity` の ReactiveProperty へ同期で書く | 同じスレッド・同じフレームで書くので、lock も `ObserveOnMainThread` も要らない。`.decisions/2026-10-01-文字入力欄にフォーカス中は移動を止めたままにする.md` を受入表に入れる |
| ESC | 「このフレームの ESC は UI が使った」というラッチを、`InputKey` の抑止判定に足す。入力欄での ESC はフォーカスを外すだけにする | 今は WS の遅れで偶然防げているだけ（`docs/webui/design/input-focus-exclusivity.md`） |
| キー処理の置き場 | 画面のキー処理（ESC・←→・Enter でのクラフトとスキット送り）は、C# の UIState か画面の Presenter に集める | Web の document 全域のキー処理は削除 PR で消す（host §7.3） |
| IME | CEF が常時 `imeCompositionMode=On` にしている。共存中にこれが UITK の TextField と取り合うかは S9 で確かめる | input §1。CEF が消えればゲーム中の常時 ON も消える |
| ゲームパッド | UI のパッド操作は作らない | ADR 0082（sprint-toggle）の 7 項で「ゲームパッドは正式対応外」 |
| `activeInputHandler` | Both（2）のまま。InputSystem だけにするかは、完了の後に別に判断する | IME の実装次第（推測） |

### 3.9 ローカライズ

- **C# の `Localize` に一本化する。** 次のものはすでにある（input §4.1）。HTTP の辞書取得・再試行・409 は消える。
  - 辞書の合成
  - 解決順（対象言語 → english → source → `[!key]`）
  - 型付きキー
- **足すもの**
  - 名前付き補間（段階 0 の 0-6）と、CSV から作る型付きの書式メソッド（推測: `ContentKeyCodeEmitter` 系を拡張して作れる）
  - `LocalizedLabel`: キーと引数を持ち、`Localize.OnLanguageChanged` で解決し直す
  - 表示用の数値書式ヘルパー: InvariantCulture を明示する。de 列があるので、`ToString()` の「,」が実害になる。Web の `toFixed`（12 か所）は常に「.」
  - kbd の差し込み: KeyCap 部品と文字列の分割で作る（`LocalizedShortcutHint` 相当）
- **DTO は Guid とキーだけを運び、ビューが解決する。** ADR 0006 の原則を保ち、決定 5 の「Web 側で解決」を「ビュー側で解決」へ改める（§8.3）。スキットは今どおり、C# が解決した文字列を push する。
- **静的検査**
  - UXML の `text=` に生の文字を書かない（`noJsxVisibleLiteral` の後継）
  - 全画面の文言がキーを通る（`allScreensI18n` の後継）

### 3.10 フォント

- **TextCore の SDF FontAsset を Editor で作ってコミットする**（S10）
  - 既存の `Assets/Dependencies/Font/*SDF*.asset` は TMP_FontAsset で、型が違う
  - `FontDefinition.FromFont` はビットマップで描くので、拡大すると崩れる（PoC）
- **フォールバック**
  - NotoSans-Regular → NotoSansJP-Medium → NotoSansKR-Medium の順にする
  - ロケールごとに、ルートのクラスで順を切り替える（`:root[data-locale]` の代わり）
  - 韓国語は、Web では OS のフォントへ落ちていた（推測）。UITK は自動では落ちないので、明示する
- **最小の文字寸法**（D10）: 11〜12px の和文は Chrome より細い（PoC）。S7 の実測を見て、12px 以下を 13px へ上げる。アイコン上の個数は `-unity-text-outline` で縁取るので、別に扱う。
- **文中アイコンの SpriteAsset** は内部 API に頼る（PoC）。構築と描画のテストを置く。Unity の版上げで最初に壊れる箇所として台帳に書く。

### 3.11 CEF 停止の検知（WebUiHealth）

AGENTS.md「失敗の扱い」に従い、「誰がどこで気づくか」を先に決める。

**今の問題**

- Web ホストの起動に失敗すると LogWarning だけを出して続行する（`Scripts/Client.Starter/Initialization/Boot/WebUiStartup.cs`。コメント「UIはWeb一本なので起動失敗時は非表示で続ける」）
- 配布物では開発者に届かない。報告の口（Web のポーズ）も一緒に死ぬ

**状態の持ち方**

- `WebUiHealth` が Alive か Dead を持つ
  - 実装は `Client.WebUiHost` に置く。インターフェースは `Client.Game` に置く（バグ報告から参照できる向きにするため）
  - 段階 6 で両方消す
- Dead にする条件
  - Web ホストの起動に失敗した
  - WS の接続が無い時間が閾値を超えた
  - ping が閾値を超えて途絶えた
- 閾値は、Web 側の無応答切断を参照する（`webSocketClient.ts` の 15000ms、ping の間隔 5000ms。実測）
- ホストは今 pong を返すだけで、最後に受けた時刻を持っていない（`WebSocketMessageDispatcher.cs:54-55`）ので、それを足す

**気づく経路**

1. テスター: UITK の system-notice 層に「一部の画面が応答していません。Esc でセーブとバグ報告ができます」と出す。
2. 開発者: バグ報告の manifest に、`webUiHealth` と「最後の ping からの秒数」を必ず入れる。ADR 0057 の受け口から Mac mini に届く。bug-report-auto-fix の読み手も同じ PR で対応する。
3. LogError も出す。

**Dead のときの処置**

- CEF の RawImage を隠す。CEF が固まると最後のフレームが画面に残る可能性があるため（推測。Discord 2026-10-03「インベントリ開いたままだったので閉じれなくなってました」）
- CEF の自動再起動は作らない（撤去する対象への投資になるため。D19）

**原因を CEF と決めつけない**

- UI の全停止がすべて CEF 由来とは限らない。2026-10-14 の「破壊モードで橋脚にカーソル合わせるだけで…UI操作がたぶんできなくなる」は、cef-unity が 2 種とも生きていた（実測。Discord ミラー）
- WebUiHealth は CEF と WS の生存だけを見る。manifest には生の値を入れる

### 3.12 AI 向けの作法（D5）

- UXML と USS はテキストで持ち、UI Builder にしか無い状態を作らない。インラインの style は、計算で決まる幾何（位置・寸法）にだけ使う。
- 見た目の調整は USS を中心に回す。USS と UXML の変更は、コンパイルもドメインリロードも起こさない。ビュー側の C# は薄く保ち、判断は Presenter とモデルに置く。
- 名前の付け方
  - testid は `name` に同じ綴りで付ける（シナリオの 151 か所を変えずに済む）
  - アンカーは USS クラスで付ける（`name` とは別の契約。`docs/webui/anchor-convention.md` の「testid とアンカーは別」を継ぐ）
  - 状態はクラスで表す（`--selected` など）
- 見た目の自己確認は、ScreenCapture 系か window モードの撮影で行う。
  - **`uloop screenshot` の rendering モードには UITK が写らない**（PoC の実測）。議論の記録の「見た目は uloop screenshot で自己確認」はこれで改める
  - 撮影にはフレーム番号か時刻の透かしを入れ、古いフレームを見ないようにする
- 規約違反は L2 で止める。moores-code-review に UITK の観点を足す（PickingMode の既定、所有表、トークンの直書き、`Func<>`・partial の禁止）。
- `ref-uitk-design`（段階 1 で初版）
  - `ref-webui-design` の原則を写す: ホワイトリスト、面は GamePanel だけ、スロットは共有部品だけ、装飾は 6 語彙、色はトークンだけ、族と層
  - §0 と §10（mock-host・Vite・Playwright・目視 QA）は、EditMode での組み立てと ScreenCapture に置き換える
- AGENTS.md の規約を守る: 200 行以下、1 ディレクトリ 10 ファイル、`#region Internal`、partial と `Func<>` の禁止、イベントは UniRx、日英 2 行のコメント。

### 3.13 並列実行の設計

**作業の切り方**

1. **1 単位 = 1 PR = 受入表 1 枚。** 単位は、ファイルの所有が重ならないように切る。新しく作るディレクトリの中で閉じるのが基本。
2. **共有ファイルは integrator が直列で所有する。** 対象は次のとおり。
   - 層 enum、ルータと所有表、composition root（DI の Installer の登録）
   - `Tokens.uss`・`Theme.uss`、`AnchorIds`、DSL のロケータ本体
   - `Localization/localization.csv`、台帳
   - prefab とシーン（uloop で 1 本ずつ）
3. **殻を先に置く**（段階 1 の終わり）。以降の単位は殻の中を埋めるだけで済む。
4. **部品の昇格。** 1 機能だけが使う部品は `Screens/<機能>/` に置く。2 機能目で使われたら、integrator が波の境で `Components/` へ移す。
5. **純ロジックの移植は別レーンにする。** vitest を仕様にして、C# の NUnit へ 1 対 1 で移す（約 4,000 行、L0 約 290 件）。S12 が成立すれば Editor が要らない。AGENTS.md のコンパイル必須は、PR の前の `uloop compile` で守る。
6. **挙動を変えるのは grill を通したものだけ。** 移植は受入表どおりのパリティが基本。外れるもの（Mantine の素の見た目の様式化など）は、moores-grill-with-docs を通した裁定に従う。各段階の着手前に、moores-writing-plans で詳細計画を切る。

**Editor の予算と役割**

| 項目 | 中身 |
|---|---|
| Mac mini | Editor は同時 3 本が目安（`CLAUDE.local.macmini.md`）。pr-apply の常設スロットも、別にメモリを使う |
| macbook-pro | 起きているときだけ、1 本を足せる |
| CI | S13 が成立すれば、client shard に L1 を載せる |
| 指揮 | 波を起動し、完了を確かめ、台帳を更新する |
| integrator | 共有ファイルの所有、部品の昇格、所有表の切替 PR |
| 単位エージェント | 実装を担う（opus）。worktree は `moores-wt new` で切り、Editor を起動する前に `moores-wt status` で本数を確かめる。4 本目になるなら、Editor の要らない作業（L0・受入表の精査・UXML の下書き）を先に進める |

**依頼文に必ず入れること**

- 前景で実行し、バックグラウンドのポーリングで待たない
- 区切りごとにコミットする
- 受入表のパスと、触ってよいディレクトリの列挙。共有ファイルへの書き込みは禁止
- `ref-uitk-design` と部品カタログを読む
- 完了報告は、報告ファイルと 3 行の要約で返す

**完了の確かめ方**

- 報告をすぐには信じない。`git log`、報告ファイル、台帳の更新が実在するかで確かめる
- 無ければ ListAgents で生存を確かめる。生きていれば再派遣しない

**レビューとマージ**

- 順序: moores-code-review → PR → pr-independent-review
- moorestech は PR の作成で止まり、マージはユーザーが行う。1 波で 4〜6 本の PR がマージ待ちに並ぶ（D20）
- 暗転の PR はテスターに影響しないので、波ごとにまとめて判断してもらえる

---

## 4. 検証

### 4.1 受入表（仕様の正本。D3）

- **置き場と列**
  - 置き場: `docs/uitk/acceptance/<機能>.md`
  - 列: ID、前提（モデルの状態）、操作、期待（発行コマンドと表示）、由来（ファイル: テスト名）、検証層、UITK のテスト名、状態（未・緑・捨てる＋理由）
- **作り方**
  - 段階 0 のスクリプトで機械的に抜き出し、単位のエージェントが精査する
  - Playwright と vitest のテスト名は、ほぼすべて日本語の挙動文になっている（tests §3）
- **どの行も終わり方を決める。** 終わり方は 3 つ: テスト、目視（証跡のパス）、捨てる（理由）。未分類は欠陥とする（`docs/webui/disposition.md` の型）。
- **Web のテストを消す時期**: 面の削除 PR。その前に、受入表の行と Unity 側のテスト名を 1 対 1 で結ぶ。
- 前例: uGUI から Web へ移ったときも、「画面ごとの操作パリティ受け入れ表」を最終検証にしていた（`docs/webui/plans/phase-d-cutover.md:33-37`）。

**Web のテストの行き先**（tests §2.1。件数は正規表現で数えた値で、議論の記録のランナー実測〈vitest 809・Playwright 211〉とは少しずれる）

| 種別 | 件数 | 残す見込み | 行き先 |
|---|---:|---:|---|
| ロジック（L） | 311 | 約 290 | L0（C# の純ロジックと NUnit） |
| コンポーネント（C） | 235 | 約 200 | L1 |
| 画面フロー（P） | 213 | 約 205 | L1。うちレイアウトの主張約 52 件は、L1 のレイアウト照合か L4 の目視 |
| 多言語（I） | 61 | 約 25 | L0 と L2。HTTP の辞書取得まわりは捨てる |
| 静的規約（S） | 91 | 約 35 | L2（意図だけ） |
| 通信契約（W） | 167 | 0 | 捨てる |
| C# の WebUi 系 | 285 | 154 | ビューモデル 90 とコマンド 36 は形を変えて残し、状態ストア 28 はそのまま残す。wire の 131 は捨てる（設定系の 10〜15 件は、設定サービスのテストへ移す） |

### 4.2 テストの層

| 層 | 中身 | 道具 | PlayMode |
|---|---|---|---|
| L0 純ロジック | スロットの計画、長押しクラフト、ビルドメニューの分類とスクロール追従、ビューポートの計算、ルーティング表、所有表、通知のストア、ビューモデル、コマンド | NUnit（S12 が成立すれば `dotnet test` も） | 不要 |
| L1 ビュー | UXML・USS・Presenter を偽モデルと記録用コマンドでつなぎ、操作をシミュレートして、発行コマンド・クラス・テキスト・レイアウトを照合する | UI Test Framework の `UITestFixture` と `PanelSimulator` | 不要 |
| L2 静的規約 | USS と UXML をテキストとして読む（§3.5、§3.12） | NUnit | 不要 |
| L3 結合 | ゲームを起動し、UI 状態の遷移、パネルの表示、コマンドのサーバー到達を確かめる。CEF 無しでの起動もここで見る | EditModeInPlayingTest（数は絞る） | 要 |
| L4 録画 | 実際の入力経路と見た目 | プレイテスト DSL | 要 |

Playwright は今 CI で走っていない（型検査だけ。`run_test.yml`）。L1 を CI に載せるので、移行後のほうが CI での検証面は広くなる。

### 4.3 EditMode の UI テスト（L1）

- **導入**
  - UI Test Framework 1.0.0（`com.unity.ui.test-framework`）は Unity 6000.3.8f1 に組み込まれている（実測）。ただし manifest には無い（実測）
  - 段階 0 で manifest に足し、`Client.Tests.asmdef` から `Unity.UI.TestFramework.Runtime` と `.Editor` を参照する
- **本番に揃える**: `panelSize` は 1280×720、`themeStyleSheet` は本番の暗色テーマ、フォントは本番の FontAsset。文字の幅で決まるレイアウトがずれないようにするため。
- **mock-host の代わり**
  - 画面ごとに `FakeXxxModel`（ReactiveProperty で組んだフィクスチャ）と `RecordingXxxCommands`（発行の記録）を用意する
  - Playwright の `setUiState` は偽モデルに、`payloadsOf` は記録リストになる
- **時間**: パネルのスケジューラか、注入した時計で動かす。`MonoBehaviour.Update` と `Time.deltaTime` で動かすと、`FrameUpdateMs` で時間を進められない。
- **描かれない部分**: L1 は描画しないので、半透明の濃さや文字のにじみは L4 か ScreenCapture で見る。
- **罠**（S13 と段階 1 で確かめる）
  - `MouseMove(from, to)` は、途中の要素に PointerEnter を届けない可能性がある
  - ScrollView の子は、スクロールしても `GeometryChangedEvent` が発火しない可能性がある

### 4.4 録画プレイテスト（L4）と CEF 無し起動ラダー

**DSL**

- `IUiElementLocator` を Web 版と UITK 版の 2 本で持つ
- UITK を先に探す。同じ testid が両系で見つかったら、失敗させる（R7）
- UITK 版の手順
  1. ゲームのパネルから `Q(name)` で探す。0 件なら再試行し、2 件以上なら理由付きで失敗させる
  2. 自分と祖先の `resolvedStyle`（display・visibility・opacity）と、`worldBound` の面積で可視かを判定する
  3. `panel.Pick(中心)` が対象自身かその子孫かで、覆われていないかを判定する
  4. パネル座標を画面座標へ変換し、SemanticInput で注入する
- UITK の画面では次のものが要らなくなる
  - WS の往復と「2 回続けて同じ矩形なら安定」の待ち
  - `CefInputForwarder`・`CefScreenMapper`・`PlaytestDomQuery`
  - `PlaytestUiOps.cs:134-138` の pointerOverUi の待ち合わせ

**CEF 無し起動ラダー**（各段階の物差し）

- Editor 限定のスイッチ（段階 0 の 0-10）で、Web ホストを起動せずに PlayMode を始める
- 段ごとにシナリオを足し、前の段のシナリオも毎回流す（退行を検知するため）
- 段階 6 の後は、同じシナリオが CEF の存在しないビルドでそのまま通ること

| 段階 | シナリオ（仮称） | 内容 |
|---|---|---|
| 1 | `uitk-lifeline` | Esc → 設定で言語を切り替える → 日本語でバグ報告 → セーブして終了 |
| 2 | `uitk-opening-hud` | オープニングのスキット → 採掘 → 数字キーで設置 → 通知とチュートリアル枠 |
| 3 | `uitk-build-bp` | ビルドメニュー → ホットバーへドラッグ → 設置 → BP の名前付け |
| 4 | `uitk-production-loop`、`uitk-research-train` | 手クラフト → 石窯に入れて取り出す → 整理。研究 → 駅名 → 時刻表と自動運転 |
| 5 | `uitk-challenge-gate` | チャレンジ一覧 → 出展モードの言語ゲート |

- ゲームの途中で CEF が死ぬ場合は、Windows 検証機で人手で 1 回確かめる（段階 1）
- Mac mini では、並走しているほかのセッションの CEF を巻き込むので、プロセス名で落とさない。自分の Editor を親に持つ PID だけを落とす

### 4.5 アンカーの付け替え

- **ID の文字列は変えない**
  - マスタに直書きされた ID は v8 で 9 件（実測: `challenge.current-hud` 1、`recipe.craft-button` 1、`research.node-<guid>` 7）
  - これに、`itemViewHighLight` から作る `recipe.item-<itemId>` が加わる
  - ID を保てば、マスタも `.decisions/2026-08-19-チュートリアルUI指定はWebアンカーIDを直書きし変換も検証も持たない.md` もそのまま成り立つ
- **語彙の正本は C# の `AnchorIds` 1 か所**（静的 19 個と動的 prefix 6 種）。`TutorialAnchorIdMapper` もここへまとめる。共存中は、Web の `anchorIds.ts` と突き合わせるテストで同期を守り、段階 6 で消す。
- **付け方**
  - `TutorialAnchor.Mark(VisualElement, params string[] ids)` の 1 か所で、USS クラスとして付ける
  - `AttachToPanelEvent`・`DetachFromPanelEvent` で、登録簿に出し入れする
  - Detach は同期で消すので、枠がアンカーより長生きしない
- **解決**
  - 判定: 0 件なら missing、2 件以上なら duplicate。ほかに非表示と画面外も判定し、開発者ログへ出す
  - 位置: `GeometryChangedEvent` に加え、スクロールによる移動を拾うため、毎フレーム `worldBound` を見る
- **祖先のクリップ**（ADR 0024）
  - overflow を隠す祖先の `worldBound` との交差で求める
  - 祖先の overflow を公開 API で読めなければ、クリップする容器に印のクラスを付ける（推測）
- **カバレッジテスト**: 全静的 ID と全 prefix が、どこかの画面の L1 マウントで登録されることを確かめる。

### 4.6 見た目の確かめ方

- 画素のパリティは目標にしない（前回、parity-check 47/47 が足かせになって打ち切った。`.decisions/2026-08-17-uGUI移植を終了しWeb-UIは独自調整へ移る.md`）
- 一致させるもの: 機能、トークン（色・寸法・層）、受入条件
- 比べ方: 段階 0 の参照キャプチャと、UITK の ScreenCapture（透かし入り）を並べる
- 撮る解像度: 1280×720、1920×1080、3840×2160、21:9
- 波ごとに画面の写真を外部監査（codex-audit、画像付き）にかける。ユーザーには、段階ごとに短い目視を 1 回だけ頼む

### 4.7 決定論ゲート

| # | ゲート | 中身 | 入れる段階 |
|---|---|---|---|
| G1 | Web 凍結 | `web/features` への新規ファイル追加を落とす（許可リスト付き）。変更 PR には台帳の追記を求める | 0 |
| G2 | uGUI 追加の禁止 | `WebUiGateAuditTest` を残す。段階 6 で名前を変え、対象を付け替える（`.decisions/2026-09-08-uGUI追加禁止はトークン検査と分類粒度の両方で守る.md` の、両方で守る形を保つ） | 既存 |
| G3 | USS・UXML の規約 | §3.5 と §3.12 | 1 |
| G4 | 台帳の完全性 | 機能・topic・action・アンカー・testid・ブロック種別が、すべて分類済み | 0 |
| G5 | 所有表 | 次の 4 つを見る。(1) どの組も所有者がちょうど 1 つ。(2) 掴み・D&D でつながる面が同じ系。(3) 単一消費者（モーダル・スキットの入力・BP 名入力）の描画者が 1 つ。(4) 暗幕は主パネルの系 | 1 |
| G6 | 移した面の Web 実装の不在 | 削除 PR の後、該当する Web のファイルが 0 | 1 |
| G7 | 上書きの閉じ込め | 所有表の上書き API は `#if UNITY_EDITOR` の中だけ（プレイヤービルドが通ることで担保する）。参照してよい箇所は許可リストで持つ | 1 |
| G8 | 撤去後 | `WebUi`・`Cef`・`webui` のトークンが、許可リストの外で 0 | 6 |

前回、ゲートが分類を 1 本に畳まれて無効化した（2026-09-08 の裁定）。その反省から、分類の粒度と禁止トークンの検査の両方で守る。

### 4.8 実測（段階 0）

すべて未実測。1 本 1 日で打ち切り、証拠付きで台帳に書く。

| # | 確かめること | 不可だったときの手 |
|---|---|---|
| S1 | UITK パネルと CEF の Canvas の前後を、PanelSettings の sortingOrder で決められるか（CEF の Canvas は Overlay・sortingOrder 0。実測）。背面パネルを足せるか | CEF の Canvas の sortingOrder を動かす。それでも駄目なら、委譲中に Web を空にする範囲を広げる。**段階 1 の前提** |
| S2 | Unity Recorder（`PlaytestRecorder.cs:33` の GameViewInputSettings）、`PlaytestScreenshot`、バグ報告の `ScreenFrameReader`（`CaptureScreenshotIntoRenderTexture`）に、UITK が写るか | 撮影経路を ScreenCapture の連番へ直すまで、段階 1 に入らない（**赤信号**） |
| S3 | `QueueStateEvent` で注入した入力が、`InputSystemUIInputModule` を経て UITK に届くか。パネル座標と画面座標の往復。`IsPointerOverGameObject` が UITK を拾うか | DSL が UITK のイベントを直接合成する（推測） |
| S4 | 専用の UI アクション資産で、WASD・矢印の Navigate、Space・Enter の Submit、Tab のフォーカス移動を止められるか | ルートで NavigationSubmitEvent を打ち消す |
| S5 | TextField の上で ESC を押したとき、EventSystem と `UIStateControl.Update` のどちらが先に処理するか | ESC ラッチを置く場所を決める |
| S6 | PanelSettings の Expand が、Web の `min(w/1280, h/720)` と同じ倍率になるか。16:9・16:10・21:9・720p・4K で見る | 倍率を C# で計算して scale を当てる |
| S7 | Linear 色空間での半透明を 3 案（実機で決め直す・α の近似補正・sRGB の RT で合成）で比べる。10〜13px の文字を 720p・1080p・4K で比べる | D10 の材料にする |
| S8 | transition・`experimental.animation`・schedule が、UI Test Framework の `FrameUpdateMs` で進むか | UiMotion の時計を差し込んで、決定的に回す |
| S9 | **日本語 IME**（Windows 検証機の配布ビルドと Mac）。単行・複数行で、変換・確定・取消・ESC を試す。フォーカスを往復した後に入力が戻るか。CEF の常時 `imeCompositionMode=On` と取り合うか | **赤信号**。D17 |
| S10 | TextCore の SDF FontAsset を Editor で作ってコミットできるか。NotoSansKR へのフォールバック | PoC と同じく実行時に生成する |
| S11 | SVG の path 文字列を Painter2D に流して、アイコンを描けるか | D12 の (b)（アイコンだけ VectorImage） |
| S12 | 純ロジックを Unity の外で `dotnet test` できるか（前例: `mooresmaster.Tests`） | Editor のレーンだけで回す |
| S13 | UI Test Framework の `UITestFixture` が、CI（game-ci の batchmode）の EditMode で 1 本通るか | L1 はローカルの Editor レーンだけで回す。並列度を下げた計画に組み直す |
| S14 | `HttpListener` が、配布ビルド（Mono。managed stripping は Standalone で 3＝High。実測）で遠隔実行の待受に使えるか。Windows 検証機で確かめる | D6 を (b)（遠隔実行専用の最小 Kestrel）へ変える |
| S15 | 委譲中の Web を `pointer-events: none` にしたとき、CEF 経由の pointerOverUi が false になり、Web が action を出さないか | 委譲中の状態では、Web の HUD も描かない（ポーズ中に HUD が消える） |

### 4.9 実機・IME・配布での確かめ

- IME は自動操作では確かめられない。人が Mac と Windows 検証機の配布ビルドで確かめる。
  - 段階 0: S9
  - 段階 1: バグ報告
  - 段階 3: 検索欄と BP 名
  - 段階 4: 駅名
- 各段階の配布ビルドは、moores-playtest-release の通し検証を通してから playtest へ反映する。Mac の確認項目「Tab でインベントリが開く」は移行後も残る。
- 実機で見る項目
  - 解像度と UI スケール（720p・1080p・4K・DPI）
  - Steam オーバーレイ
  - 言語 4 種（数値の書式とフォールバックを含む）
  - Ctrl+U、カットシーン、出展モード

---

## 5. 撤去と完了の定義

### 5.1 段階ごとに消えるもの

| 時期 | 消すもの | 順序の理由 |
|---|---|---|
| 段階 0 | OneJS、`com.annulusgames.debug-ui` | どこからも参照されていない |
| 段階 0 | 遠隔実行の Kestrel 依存 | Kestrel を消す前提条件。検証機の運用を壊さないよう、ほかの変更が無いうちに済ませる |
| 段階 0 | Web ホストの中の状態（移設） | `Client.WebUiHost` を消す前提条件 |
| 段階 1〜5 の削除 PR | 移した面の React・vitest・Playwright・Web の document 全域のキー処理 | R3 |
| 段階 2 | 旧 UITK の `SkitUI` と `SkitPresentationMode.WebUiEnabled` | 恒久 true の切替の残骸（host §7.5 R1） |
| 段階 5 | `web/features` の残りすべてと Web の横断層 | Web が描くものを 0 にして 1 周期見る |
| 段階 6 | §2.7 の 6a〜6e | 下の 5.2 |

### 5.2 段階 6 の順序

依存の向き（host §6.2）に沿って、上流から消す。

1. 6a 起動配線と共存機構: 起動しなくなれば、残りは参照されないコードになる。
2. 6b `Client.WebUiHost` → C# の通信契約テスト → `Client.Playtest/WebUi` → `moorestech_web` と CI の webui。
3. 6c `jp.juha.cefunity` と prefab の CEF Canvas → bundler・LFS 検査・setup-cef・CI の CEF 取得・release の CEF 検査。配布物が変わる PR を 1 本にまとめ、検証機で 1 回確かめる。
4. 6d NuGet（D6 に従う）。
5. 6e 文書・スキル・改名・ADR。実体が消えてから書く。

### 5.3 完了の定義（観測できる形。D4）

議論の記録の案（CEF の削除、Kestrel ホストの削除、`Client.WebUiHost` と `moorestech_web` の撤去、`CefRuntimeBundler` の撤去）に、調査で見つかった抜けを足した。

| # | 条件 | 観測のしかた |
|---|---|---|
| 1 | CEF が無い | 次の 3 つを確かめる。(1) manifest と lock に `jp.juha.cefunity` が無い。(2) MainGameUI.prefab に CEF の Canvas・RawImage が無い。(3) 配布ビルドの Windows `Plugins/x86_64` と Mac `PlugIns/cef-unity-server.app` に、CEF のファイルが 0（`release-artifact.sh` で検査） |
| 2 | Web 用の Kestrel が無い。遠隔実行は別の待受で動く | 次の 2 つを確かめる。(1) Windows 検証機で、ADR 0072 の完了条件 3 点（有効時に結果が返る、Harmony で差し込める、オプション無しでは開かない）を再確認する。(2) D6 で HttpListener を選んだなら AspNetCore 系が 0。最小 Kestrel を選んだなら、それを残したことを台帳に記録する |
| 3 | Web ホストと Web 資産が無い | 次のものが無い。`Client.WebUiHost`（asmdef ごと）、`moorestech_web/`、`Client.Playtest/WebUi/*`、`WebUiActionInvoker`、C# の通信契約テスト。`Client.WebUiHost` の参照も 0 |
| 4 | 配布工程の CEF が無い | 次のものが無い。`CefRuntimeBundler`、`CefPackageLfsValidator`、`scripts/setup-cef.*`、CI の CefUnity 事前取得と pnpm の step、`webui-test` と `needs_webui`、`release-artifact.sh` の CEF helper 検査とその契約テスト、README の CEF の確認 |
| 5 | 共用部品が中立な場所にある | `CefLfsPointer` を改名し、`EnvironmentScrubOnEditorLoad` を `Client.ExternalProcess` へ移した |
| 6 | 共存の仕組みが無い | 次のものが無い。所有表、委譲、Editor 限定の上書き、DSL の Web ロケータ、`UiPointerHitTest` と入力排他の Web の項、WebUiHealth、通知を隠す処理。前例（恒久 true にしたゲート参照 26 か所が死に分岐で残った。shipping §6.2）の再発を防ぐ |
| 7 | Web という名前が無い | G8 が緑。`WebPinId`・`WebModal*`・`web anchor id`・`WebUiInputExclusivity` を改名済み |
| 8 | 受入表 | 20 機能で未分類が 0。L0 と L1 が CI で緑 |
| 9 | 録画 | 全 90 本が緑（壊れている 8 本は直すか削除して bd `moorestech-wwxy` を閉じる）。DSL に Web という名前の API が 0 |
| 10 | ラダー | CEF 無し起動ラダーの 6 本が、CEF の存在しないビルドで通る |
| 11 | アンカー | 静的 19 個と prefix 6 種が登録される。マスタの 9 件が実ゲームで解決する |
| 12 | 実機 | Windows 検証機の通し検証と Mac の Steam 確認を通り、CEF を含まない配布ビルドが playtest に反映されている |
| 13 | プレイテスト | 段階 5（Web 空）と段階 6 の後、それぞれ 1 周期で、UI が原因で進行を阻む不具合が 0 か全件修正済み |
| 14 | 文書とスキル | `docs/webui` は archive、入口は `docs/uitk`。`ref-webui-design` を廃止して `ref-uitk-design` に置き換えた。moores-code-review の ts/tsx 観点 12 本を撤去した。DSL のスキル文書を更新した |
| 15 | ADR と裁定 | §8.3 の改訂か廃止の記録がある |
| 16 | uGUI の扱い | G2 で新規のスクリーンスペース uGUI の禁止が引き続き効く。uGUI は ADR 0052 の例外 2〜4（タイトルとロード、HP バー、デバッグ UI）だけが残る |
| 17 | bd | CEF 関連の open 課題 12 件（shipping §1.2）、`moorestech-lnsf.8`、`moorestech-cgm` が、閉じるか受入表へ転記されている |

### 5.4 完了の定義に入れないもの（別に判断する）

- `com.unity.ugui` の撤去（ADR 0052 の例外 2〜4 が残る）
- `activeInputHandler` を InputSystem だけにすること
- Mac の Universal 化と Linux クライアントの再開（D16）
- タイトルとロード画面の UITK 化（D15）

---

## 6. リスク

| # | リスク | 兆候・検知 | 潰し方 | 赤信号か |
|---|---|---|---|---|
| 1 | 日本語 IME が UITK の TextField で成立しない | S9 | ユーザーへすぐ上げる。D17 で、自前の composition 処理か、文字入力のある画面だけ Web に残すかを決める | **赤** |
| 2 | 録画・バグ報告の動画に UITK が写らない | S2 | 撮影経路を直すまで段階 1 に入らない。写らないまま進むと、録画での検証とテスターの報告動画が全部欠ける | **赤** |
| 3 | UITK と CEF の描画順を決められない | S1 | CEF の Canvas の sortingOrder を動かす。それでも駄目なら、委譲中に Web を空にする | 段階 1 の前提 |
| 4 | 委譲中に Web の入力を止められない | S15、共存の受入（action ログ 0 件） | 委譲中の状態では、Web の HUD も描かない | 段階 1 の前提 |
| 5 | UI Test Framework が CI の batchmode で動かない | S13 | L1 をローカルの Editor レーンで回す。並列度を下げた日程に組み直す | 中 |
| 6 | HttpListener が、Mono・High stripping の配布ビルドで動かない | S14 | 遠隔実行専用の最小 Kestrel に切り替える（D6 の (b)） | 中 |
| 7 | 持ち物の束の切替で大きな退行が出る | 切替後の staging と検証機。playtest の報告 | 次の 3 つで抑える。(1) Editor 上書きで全録画を回す。(2) staging → 検証機 → playtest の順に出す。(3) 戻すのは切替 PR の 1 行（削除 PR は 1 周期後） | 中 |
| 8 | Web の凍結中に機能の要求が来る。進行中の Web の PR と衝突する | G1 が赤になる。bd | 進行中の PR（10-15 以降の 44 コミット。画質設定など）は完了させてから凍結する。凍結後の新機能は、その束の移行を前倒しする | 低 |
| 9 | 並行セッションとの衝突（液体 UI が `BlockInventoryTopic` を触る等） | マージ時の衝突 | integrator が共有ファイルを直列で持つ。topic 群ごとに短い作業窓を bd で告知する | 中 |
| 10 | Editor の本数とユーザーのマージが律速になる | マージ待ちの PR 数 | Editor の要らない作業（L0・受入表・UXML の下書き）を並べる。マージは波ごとにまとめて判断してもらう（D20） | 中 |
| 11 | 半透明が薄く、小さい文字が細い | S7、目視 | トークンに UITK 列を持ち、作る段で実機で決める。12px 以下は 13px へ上げる（D10） | 低 |
| 12 | CEF 停止を誤検知する・見逃す | バグ報告の manifest の `webUiHealth` と、最後の ping からの秒数 | CEF と WS の生存だけを見て、原因を決めつけない（10-14 の UI 停止は CEF が生きていた） | 低 |
| 13 | 共存の仕組みが残骸になる | G8、完了の定義の 6 | 完了の定義に入れる。段階 6a で一括で消す | 低 |
| 14 | スロットの多い画面や研究ツリーの性能 | UITK の CPU 時間の記録（共通条件 7） | スロットは border の入れ子で描き、Painter2D は枠と図形だけに使う。足りなければ ListView（PoC で 1 万行・行要素 28 個）。研究ツリーのノード数を先に数える | 低 |
| 15 | SpriteAsset が内部 API に依存している | Unity の版上げでテストが落ちる | 構築と描画のテストを置き、台帳に書く | 低 |
| 16 | 仮想化した一覧のアンカーが消える | アンカーの missing ログ | 研究とビルドメニューは仮想化しない。仮想化するものは Bind で付け替える | 低 |
| 17 | 撤去時に CI とリリース検査が同時に赤になる | 6c の CI | 6c を 1 PR にまとめ、Windows と Mac のビルドをその PR で回す | 低 |
| 18 | Mod の契約（アンカー ID・アイコン・CSV）を壊す | マスタの 9 件の解決、CSV の読込 | ID の文字列は変えない。mod の CSV に差し込みは 0 件（実測）。アイコンの経路は変えない | 低 |
| 19 | 共存中に UITK の通知が Web の全画面 UI の上に出る（ADR 0017 との一時的なずれ） | 録画 | Web の全画面状態の間は通知層を隠すか、背面パネルに置く（S1）。段階 5 で元に戻す | 低 |
| 20 | 規模の見積もりが外れる | 各段階の plan | 段の切り方は変わらず、期間だけが伸びる。段階 4 の暗転期間が伸びたら、族ごとの切替（D9 の (b)）へ切り替えるかを裁定に上げる | 中 |

---

## 7. 採点表

### 7.1 3 案の採点（1〜5）

| 軸 | strangler（リスク先行・画面単位） | foundation（基盤先行・一括切替） | player（テスター体験先行） |
|---|---|---|---|
| 途中で止めても出荷できるか | **5**: 段も小段階も出荷でき、止めた所の状態が束単位で一貫している | **3**: 出荷はできる。ただし P4 より前に止めると、テスターへの価値が 0 のまま、暗転のコードと `--ui` の切替が配布物に残る | **4**: 段階 1 で止めても命綱が残り、価値は大きい。ただし状態ごとの描画者の分割（ホットバーは BuildMenu だけ Web など）で、止めた所の見た目が画面内で切り替わる |
| 難所を早く踏むか | **4**: P1 で共存の物理を実測し、P2 で D&D・ツールチップ・IME・入力の取り合いを本番で踏む。掴みと TreeView は P4 | **4**: P0 の実測 13 本と P2 のパイロットで、技術の難所を全部踏む（最良）。ただし配布環境の差（移行の理由そのもの）を浴びるのは P4 | **3**: IME と ESC は段階 1 で踏む。D&D と掴みは段階 3、スクロールスパイは段階 4、TreeView は段階 5 |
| 共存期間の短さ | **2**: 実行時の共存が P1〜P5 の全期間。P4 では InventoryPanel が両系に 2 つある | **4**: テスターの前で両系が並ばない。ただしコードの上の共存（P2〜P4 と退避の 1 周期）と、凍結中のバグ修正の二重化がある | **2**: 実行時の共存が段階 1〜5 の全期間。状態ごとの描画者の分割が 3 つある |
| 検証の確かさ | **4**: 段ごとに配布環境（検証機・Mac）を通し、差分が小さいので原因を追いやすい。参照キャプチャが無い | **4**: 実測・参照キャプチャ・ゲート G1〜G7・17 項目の完了の定義・4 解像度の撮影と、成果物は最も厚い。ただし UI 全体が配布環境に出るのは P4 に集中する | **5**: CEF 無し起動ラダーで、移行の目的（CEF が無くても遊べる）を段ごとに自動・累積で測れる。CEF 停止の検知が開発者に届く経路も持つ。実際に CEF を落とす確認もある |
| AI エージェント並列での実行しやすさ | **3**: 段の中で 3 本まで。束を段ごとに直列に進める | **5**: integrator・殻・波・1 単位 1 PR・依頼文の型・純ロジックの別レーン | **3**: 段の中で 2〜4 本。段階 3 と 4 は並走できるが、所有表が熱い共有ファイルになる |
| 既存裁定との整合 | **4**: 実行時の切替フラグを作らない（AGENTS.md・前例に沿う）。ADR 0072 と 0006 の改訂は、どの案でも避けられない | **3**: 配布物に `--ui` の切替が入る（AGENTS.md「デバッグ/テスト専用 public をプロダクションに残さない」と緊張）。議論の記録の agent 案と逆。最小 Kestrel も ADR 0072 の棄却案「専用の待ち受けを新設」にあたる | **3**: 装飾を段階 7 へ回すのは、ユーザー規約「推奨案に、既知の負債を据え置く・後で一括改修を置かない」に反する。ADR 0017 との一時的な逸脱がある |
| **合計** | **22** | **23** | **20** |

### 7.2 軸の選び方

- 合計は僅差だった（foundation 23、strangler 22、player 20）。そこで「長所を他の案へ移せるか」で軸を決めた。
- strangler を軸にした。理由は 3 つ。
  - strangler の長所（配布物に切替を入れない、毎週テスターと配布環境へ出る）は、出荷形そのもの。foundation へ移すと、foundation の核（スタック丸ごとの切替）を捨てることになる。
  - foundation の長所（並列の仕組み・実測・参照キャプチャ・ゲート）と player の長所（命綱が先・停止の検知・ラダー）は、出荷形と独立していて、strangler にそのまま移せる。
  - 裁定 10-18 の理由「数万の環境で UI が壊れない」と、テスターの実害（§2.2）に、最も早く効く。
- strangler の弱い 2 軸は、統合の中で手当てした。
  - 共存期間: 束の中を暗転で並列に作り、所有表の 1 行で切り替える。小段階ごとに InventoryPanel を二重に出すのをやめ、族ごとの所有表のキーも要らなくした。
  - 並列: integrator・殻・波・純ロジックの別レーンを入れた。

統合案の見込み（自己評価で、参考）:

| 軸 | 見込み |
|---|---|
| 止めても出荷できるか | 5 |
| 難所を早く踏むか | 4 |
| 共存期間の短さ | 3 |
| 検証の確かさ | 5 |
| 並列のしやすさ | 4 |
| 整合 | 4 |

### 7.3 各案から取り込んだもの

| 出どころ | 取り込んだもの |
|---|---|
| strangler | 束単位の所有表と共存の規則 R1〜R7、段ごとの共通条件（配布環境の通し検証）、委譲の伝え方、段階 6 の PR の分け方、既存裁定との対応表の大半、HttpListener の推奨 |
| foundation | 次のとおり |
| | ・実測スパイクの型（1 日で打ち切り、不可のときの手を先に書く）と、S6・S10〜S13 |
| | ・参照キャプチャ、integrator・殻・波・1 単位 1 PR、依頼文の型、純ロジックの別レーン |
| | ・ゲート G1〜G7、完了の定義の観測方法、性能の記録、`UiAssetCatalog` の起動時の検査、外部監査の回し方 |
| player | 次のとおり |
| | ・命綱を第一弾にすること（Discord の実害の根拠つき） |
| | ・WebUiHealth（AGENTS.md の「誰が気づくか」） |
| | ・CEF 無し起動ラダー、実際に CEF を落とす確認 |
| | ・HUD・通知・スキットを早く出す段 |
| 統合で新しく決めたこと | 次のとおり |
| | ・束の中を暗転で作り、Editor 限定の上書きで検証して一度に切り替える（R9） |
| | ・切替 PR → 1 周期 → 削除 PR（R3） |
| | ・委譲中の Web は表示だけで入力を受けない（R1、S15） |
| | ・研究と列車族を持ち物の束に入れる |
| | ・装飾を後回しにしない |
| | ・S14・S15 を足す |

### 7.4 調査・草案の事実誤りと訂正（本書では訂正済み）

| 誤り | どこにあったか | 実測・正しい形 |
|---|---|---|
| Web UI に依存する録画シナリオは「27 本・約 143 か所」（「24〜29 本」とも） | 調査 tests、草案 3 本 | 90 本中 41 本（実測）。内訳は 3 つ。(1) DSL の Web 操作を使う 32 本・151 か所（ClickWebUi 67 など）。(2) `PlaytestDomQuery.Query` を直接呼ぶ 19 本・27 か所。(3) `WebUiInputExclusivity` を参照する 3 本。直接呼び出しの 19 本はロケータの照会 API への置き換えが要るので、DSL の作業は見積もりより大きい |
| シナリオが使う testid は約 45 種 | 調査 tests | 文字列で 53 種（実測） |
| `Client.WebUiHost` は 134 ファイル（約 10.1k 行） | 議論の記録、調査 host | 140 の .cs ファイル・10,565 行（実測） |
| topic は「28」「約 30」 | 草案 | `protocol.ts` に 29（`playtest.dom_query` を含む。除くと 28）。action は 44 で正しい（実測） |
| Web を参照する `Client.Tests` は 65 ファイル | 草案 strangler・player | 67 ファイル（実測。`WebUiHost` で grep） |
| Web の本番コードは 15.4k 行 | 調査 screens | 15,609 行（実測。features 8,482・shared 4,002・app 922・bridge 2,203）。10-15 以降の 44 コミットで増えた |
| 裁定のファイル名「メインメニューとロード画面はuGUI現状維持（UI Toolkit 化は後日）.md」 | 調査 screens §0 | そのファイルは無い。正しくは `.decisions/2026-09-05-メインメニューとロード画面はuGUI現状維持.md` |
| 「見た目は uloop screenshot で自己確認」 | 議論の記録 | PoC の実測で、rendering モードには UITK が写らない。ScreenCapture 系か window モードを使う |
| 完了の定義の「Kestrel ホスト削除」 | 議論の記録 | 遠隔実行が Kestrel に相乗りしている（ADR 0072、`WebUiEndpoints.cs:97-101`）。遠隔実行の載せ替え（D6）の後でだけ成り立つ |
| 破壊モードの UI 停止を、CEF 停止の実害として挙げた | 草案 player | 2026-10-14 の同種の報告では、cef-unity が 2 種とも生きていた（実測。Discord ミラー）。UI の全停止を CEF と決めつけない（§3.11） |
| `PocControls.uss` を repo のパスとして引用した | 草案 3 本 | master には無い。PoC ブランチ `poc/uitk-hard-cases`（`de6c68621c`）の `Client.UitkPoc/Resources/UitkPoc/PocControls.uss` |
| NuGet の `Microsoft.Extensions.*` を ModelContextProtocol が共有しているかもしれない（未確認） | 調査 host、草案 3 本 | `ModelContextProtocol 0.2.0-preview.3` は packages.config にあるが、client と server の scripts に `using` が無い（実測）。未使用なら一緒に外せる（推測） |
| 草案 foundation の期間 9〜12 週 | 草案 foundation | 統合案は 12〜16 週（推測）。段ごとの配布検証とマージ待ちを含めた |

次の点は、正しいことを確かめた。

- `UiPointerHitTest` の呼び出し側は 23 ファイル・28 か所
- 差し込み記法は位置指定が 32 キー・190 か所、名前付きが 44 キー。mod の CSV には 0 件
- マスタのアンカーは 9 件
- 引用した `.decisions` と ADR（0006・0013・0016・0017・0019・0020・0021・0024・0035・0039・0046・0052・0057・0071・0072・0074・0082）は実在する
- ADR 0082 の「ゲームパッドは正式対応外」
- UI Test Framework は組み込み済みで、manifest には未記載
- 配布ビルドの scripting backend は Mono

---

## 8. 裁定待ちの論点

「推奨」は agent の案。期限は「どの段階に入る前に要るか」。D1〜D5 は、議論の記録が未裁定として挙げた論点（出荷形・第一弾の画面・仕様の正本・完了の定義・AI 向けの作法）。

### 8.1 本筋（段階 0 に入る前に要る）

| # | 論点 | 選択肢 | 推奨 | 理由と帰結 | 期限 | 既存裁定との関係 |
|---|---|---|---|---|---|---|
| D1 | 移行中の出荷形 | (A) 束単位の strangler。束は暗転で作り、所有表の 1 行で切り替えて毎週出す（本書） (B) UI スタックを丸ごと切り替える（起動オプション `--ui`。既定 Web で、最後に一括） (C) 移行ブランチに溜めて一括で入れる | **A** | テスターの実害（CEF の停止とメモリの伸び）に、数週間で命綱が届く。配布環境の差を束ごとに浴びられ、どこで止めても出荷できる。帰結: 見た目の混在が届き、共存の仕組み（段階 6 で消す）を持つ。(B) は配布物に切替が入り、テスターへの価値が P4 まで 0。(C) は約 2 万行が配布環境に触れないまま最後に出る | 段階 0 | 議論の記録の agent 案と同じ向き |
| D2 | 第一弾の画面 | (A) 命綱（ポーズ・設定・バグ報告・トースト） (B) 入力の無い HUD (C) ビルドメニューとホットバー (D) 議論の記録の「インベントリかホットバー」を単独で | **A** | 失うものが最も大きい口（報告とセーブ）を先に守る。束 D は単独で切り出せる。IME・ESC・フォーカスを、掴み抜きで先に通せる。(B) は CEF が死んだときに救えるものが少ない（段階 2 で続けて出す）。(D) のホットバー単独は束 B を割る。インベントリは最大の束に未熟な基盤で挑むことになる | 段階 1 | 議論の記録の候補（未裁定）とは異なる |
| D3 | 仕様の正本 | (A) 受入表を正本にする（Playwright と vitest の名前から機械抽出）＋参照キャプチャを repo の外に残す。面の削除 PR で Web のテストを消す (B) Web のテストを移行が終わるまで残す (C) Web の実装（TS）を読んで移植する | **A** | 消した後の Web のテストは走らない。受入表と Unity 側のテスト名を 1 対 1 で結べば、仕様は消えない。見た目の意図は参照キャプチャで辿れる | 段階 0 | 前例 `phase-d-cutover.md`・`disposition.md` の型 |
| D4 | 完了の定義 | (A) §5.3 の 17 項目 (B) 議論の記録の 4 項目 | **A** | 議論の記録の案は遠隔実行（ADR 0072）とぶつかり、共用部品・DSL・共存機構・名前・bd の撤去が抜けている | 段階 0（段階 6 で最終確認） | 議論の記録の「Kestrel ホスト削除」を D6 次第に改める |
| D5 | AI 向けの作法 | (A) §3.12 を `ref-uitk-design`・L2・moores-code-review の UITK 観点で固める (B) AGENTS.md の一般則だけ | **A** | 規約を決定論で止めないと、並列のエージェントごとに見た目と構造がばらつく。uloop screenshot が UITK を写さない点は、明文化しないと必ず踏む | 段階 1 | 議論の記録の「uloop screenshot で自己確認」を改める |
| D6 | 遠隔実行の待受 | (a) `HttpListener` へ載せ替え、AspNetCore 系の NuGet を外す (b) 遠隔実行専用の最小 Kestrel として残す (c) 専用の TCP 待受 | **(a)**。S14 で不可なら (b) | NuGet と Kestrel の保守面積が大きく減り、完了の定義の「Kestrel 削除」が成り立つ。今は Web ホストの起動失敗で遠隔実行も開かない。帰結 (b): Kestrel と NuGet が遠隔実行のためだけに残る | 段階 0 | **ADR 0072 と矛盾**（「入口は既存の Web UI サーバーに 1 本足す」、棄却案「専用の待ち受けを新設」）。どれを選んでも改訂が要る。トークン・起動オプション・結果の 7 種は変えない |
| D7 | Web の凍結と新機能の行き先 | (a) G1 で `web/features` の新規ファイルを止める。Web はテスターを止める不具合だけ直す。新しい UI は段階 1 の基盤ができたら UITK にだけ作る。未移行の画面に機能が要れば、その束を前倒しする (b) 全面凍結（不具合も直さない） (c) 従来どおり | **(a)** | (b) は CEF 由来の致命的な不具合も直せない。(c) は移す対象が増え続け、二重作業になる。進行中の PR（画質設定など）は完了させてから凍結する | 段階 0 | `.decisions/2026-08-17-uGUI移植を終了しWeb-UIは独自調整へ移る.md`（Web 専任）は 10-18 の裁定で覆っている |
| D8 | 文言の差し込み記法 | (a) 名前付きに統一（位置指定の 32 キー・190 か所と C# の呼び出し元を書き換える）し、型付きの書式メソッドを作る (b) 2 つの記法を併存させる (c) 位置指定に統一する | **(a)** | 翻訳者が文脈を読め、語順の違う言語に強い。引数の漏れがコンパイルエラーになる。mod の CSV に差し込みは 0 件で、波及しない。(b) は負債が残る | 段階 0 | 矛盾しない（ADR 0006 は記法を決めていない） |
| D9 | 持ち物の束の切替の粒度 | (a) 束ごと一度に切り替える（暗転で族ごとに並列に作り、Editor 上書きで検証する） (b) 族ごとの小段階で順に出す（InventoryPanel が一時的に両系に 2 つ、所有表のキーは画面×族） (c) 長命ブランチ | **(a)** | 系をまたぐ掴みが原理的に起きない。族ごとの所有表と InventoryPanel の二重実装が要らず、並列にしやすい。帰結: インベントリ系がテスターに届くのは段階 4 の終わりで、それまで機械 UI の開閉による CEF のメモリの伸びは続く。(b) は早く届くが、途中で止めると二重実装が残る | 段階 2 の終わり | なし |

### 8.2 推奨の既定で進め、台帳に記録してよいもの（異議があれば裁定）

| # | 論点 | 選択肢 | 推奨 | 理由 | 期限 | 既存裁定との関係 |
|---|---|---|---|---|---|---|
| D10 | 半透明と小さい文字 | (a) トークン表に UITK 列を持ち、部品を作る段で実機で決める。12px 以下は S7 を見て 13px へ (b) α を近似式で一括変換 (c) パネルを sRGB の RT に描いて合成 | **(a)** | Linear 合成と小さい文字は、どうせ Web と一致しない（PoC）。(b) は背景色で誤差が出る。(c) は描画の経路が増え、「環境差で壊れない」に反する恐れがある（推測） | 段階 1 の途中（S7 の後） | 画素パリティを打ち切った 08-17 の方針を継ぐ |
| D11 | アニメーション基盤 | (a) 自作の UiMotion（100〜150 行、時計を注入） (b) LitMotion v2.0.2（MIT、UITK の Bind 25 種、手動ディスパッチャ） (c) PrimeTween | **(a)** | 要件は 3 系統だけで、EditMode で決定的に検証できる。外部の版上げに振り回されない。DOTween がすでにあるので、系統をさらに増やさない。要件が増えたら (b)。(c) は再配布の制限があり、手動で進める API を確認できなかった | 段階 2 | 裁定 10-18「導入または自作」の範囲内 |
| D12 | 画像アセットの規則 | (a) ビットマップは使わない。面と図形は Painter2D と頂点カラーのメッシュ。アイコン（スキットの 6 種・×・ピン・矢印の約 10 種）は SVG の path 文字列を Painter2D で描く (b) アイコンだけ VectorImage を許す (c) ビットマップも許す | **(a)**。S11 が不可なら (b) | テキストで差分が読め、色をトークンで変えられ、AI が直せる。(c) は色トークンから切り離され、規則の趣旨に反する | 段階 1 | `ref-webui-design` の画像禁止を UITK 向けに読み替える |
| D13 | トークンの正本 | (a) USS（C# は `customStyle` で読む） (b) C# の定数から USS を生成する | **(a)** | トークンを変えても、コンパイルもドメインリロードも走らない。見る場所が 1 か所になる | 段階 1 | なし |
| D14 | Mantine の素の見た目の画面（トースト、チャレンジ、列車 HUD、ポーズのボタン） | (a) 移すときに、ホワイトリストの語彙で様式化する (b) 今の見た目を再現する | **(a)** | Mantine の既定は再現の正本にならない。負債 `moorestech-cgm` も閉じられる。見た目が変わるので、該当の段で grill に通す | 段階 1（ポーズ・トースト）、段階 2（列車 HUD）、段階 5（チャレンジ） | ADR 0014 の「他の Button は今回触らない」を、移行の範囲で解く |
| D15 | 範囲（タイトル・ロード画面・HP バー・デバッグ UI） | (a) 今回は含めない。uGUI に残した理由（CEF はゲーム内でしか起動できない）が消えたことは記録し、メインメニューの作り変えのタスクに「UITK で作る」と書く (b) 今回の移行に含める | **(a)** | 範囲を広げない。テスターの実害も出ていない | 段階 0 | `.decisions/2026-09-05-メインメニューとロード画面はuGUI現状維持.md`、ADR 0052 の例外 2〜4 と一致 |
| D16 | Mac の Universal 化・Linux クライアント | (a) 完了の後に別に判断する (b) 今回に含める | **(a)** | ffmpeg も arm64 しか無い。CEF を消しても、arm64 固定は自動では解けない | 段階 6 の後 | ADR 0071 の根拠（CEF）を「ffmpeg」へ改める |
| D17 | S9 で IME が成立しなかった場合 | (a) IME の composition を自前で扱う TextField を作る (b) 文字入力のある画面（バグ報告・検索・駅名・BP 名）だけ Web に残す | S9 の結果で決める（成立すれば不要） | (b) は完了を遅らせ、命綱の報告を日本語で書けない期間を作る | 段階 1 の前（不成立のときだけ） | なし |
| D18 | UI の演出の時間源 | (a) 実時間（パネルのスケジューラ） (b) GameUpdater のティック | **(a)** | ポーズ中もポーズ画面の演出は動くべき。AGENTS.md の「経過時間は GameUpdater のティック加算だけ」は、サーバーのゲームロジックの規則と読める。**この解釈の確認を兼ねる** | 段階 2 | AGENTS.md の解釈 |
| D19 | CEF の延命策 | (a) 停止の検知と知らせだけを入れる（WebUiHealth） (b) 何もしない (c) 自動再起動・Ctrl+ホイールの拡縮の封じ・raw input まで直す | **(a)** | 気づく経路の無い失敗は直す（AGENTS.md）。(c) は撤去する対象への投資。Ctrl+ホイールのページズーム（Discord 2026-10-11）は、Web の 1 行で封じられるなら (a) に足してよい（効くかは推測） | 段階 1 | `.decisions/2026-09-13-CEF raw input応急処置は入れず…` と一致 |
| D20 | PR の粒度とマージ | (a) 1 単位 1 PR を master へ。ユーザーは波ごとにまとめて判断する (b) 波ごとの統合ブランチ | **(a)** | 暗転の PR はテスターに影響しない。統合ブランチは長命ブランチの衝突を生む。帰結: 1 波で 4〜6 本の PR がマージ待ちになる | 段階 0 | moorestech は PR の作成で止める運用（変更なし） |

**agent の既定で決め、台帳に記録するもの**（大きく外れたら裁定に上げる）

- パネルは 1 枚。背面パネルを足すのは S1 の結果次第
- UITK は CEF の上に置く
- PanelSettings は 1280×720・Expand
- 共存中、Web の全画面状態の間は UITK の通知層を隠す
- `name` は testid と同じ文字列にし、アンカーは USS クラスで付ける
- 入力欄での ESC は、フォーカスを外すだけ
- 入力欄以外は `focusable=false`。UI のパッド操作は作らない
- 旧 UITK の SkitUI は撤去して作り直す
- 閾値は既存の定数を参照する
  - 押下は `usePressGesture`（5px）
  - ツールチップは `OFFSET`（12）
  - 通知は `NOTIFICATION_DISPLAY_MS`（7000ms）
  - 無応答は `webSocketClient.ts`（15000ms）

### 8.3 改訂が要る既存の裁定・ADR

| 裁定・ADR | この計画での扱い | 種別 | 時期 |
|---|---|---|---|
| ADR 0072（遠隔実行の入口は Web UI の Kestrel） | 段階 0 で待受を切り離す（D6） | **矛盾。改訂が要る** | 段階 0 |
| ADR 0006 決定 5（マスタ由来の表示名は Web の辞書で解決） | 「ビュー側（UITK）で解決」へ改める。DTO が Guid だけを運ぶ原則は保つ | **改訂** | 段階 1 |
| ADR 0013・0017（stage 族と viewport 族、z の層トークン、通知は全画面 UI の裏） | 原則は保つ。層は層 enum と兄弟順で表す。段階 2〜5 は通知を隠す一時的な逸脱 | 実装の節の改訂、一時的な逸脱 | 段階 2〜5 |
| ADR 0016（通知の出入り） | 意味は継ぐ。手段は transition と UiMotion にし、検証は L1 と時計の注入にする | 実装の節の改訂 | 段階 2 |
| ADR 0019、`.decisions/2026-08-19-カーソルツールチップの書式はWeb側が持つ.md` | 書式の持ち主を UITK のトークンへ移す（wire に寸法を載せない原則は保つ） | 持ち主の変更 | 段階 2 |
| ADR 0020・0074（ワープと、付いたときの再計算） | 同じフレームで置くので、前提が要らなくなる見込み（推測） | 確認が要る | 段階 2 |
| ADR 0021（テキスト選択は入力欄だけ） | UITK の既定の挙動で満たされる | 廃止の記録 | 段階 6 |
| ADR 0024 と `.decisions/2026-08-20-チュートリアルハイライトのマスク方式とテスト方針.md` | 祖先との交差で求める。判定役のテスト方針はそのまま移す | 実装の改訂 | 段階 2 |
| ADR 0052 | 例外 1（CEF の描画面）が消え、例外 2 の理由も消える。ゲートは付け替えて残す。agent 前提 1（EventSystem 判定）は維持する | 改訂 | 段階 6 |
| ADR 0057（バグ報告のバンドル） | manifest に `webUiHealth` を足し、段階 6 で外す | 追記 | 段階 1・6 |
| ADR 0071（Mac は arm64 だけ。根拠は CEF） | 根拠を ffmpeg だけにする | 改訂 | 段階 6 の後 |
| `.decisions/2026-08-18-チュートリアル提示はWebUI経路に統一しD&Dは矢印ループで示す.md` | 「UI の描画系の経路に統一」と読み替える | 表現の更新 | 段階 2 |
| `.decisions/2026-08-19-獲得通知の集約はWebUI側で行う.md` | 集約は C# の通知モデルが持つ。「表示側に置く」原則は保つ | 置き場の書き換え | 段階 2 |
| `.decisions/2026-08-19-チュートリアルUI指定はWebアンカーIDを直書きし変換も検証も持たない.md` | ID の文字列を保つので、そのまま成り立つ。スキーマの既定文字列 `web anchor id` だけ直す | 矛盾しない | 段階 6 |
| `.decisions/2026-08-26-Web操作音はC#へsound.playを送って鳴らす.md` | ビューから直接鳴らす（音を Unity に一本化する原則は保つ） | 経路の更新 | 段階 1〜5 |
| `.decisions/2026-08-23-操作ヒントの正はC#のUIStateに置きtopicで配る.md` | C# が正本のまま。topic の代わりに購読になる | 矛盾しない | 段階 2 |
| `.decisions/2026-10-01-*`（Space の封じ、メニュー中の移動、入力欄の移動停止、クラフトボタンと Space） | UITK でも、受入表として同じ挙動を守る（§3.8） | 継承 | 段階 1〜4 |
| `.decisions/2026-09-08-uGUI追加禁止はトークン検査と分類粒度の両方で守る.md` | G2 として継ぎ、G1（Web の凍結）を足す | 継承 | 段階 0 |
| `.decisions/2026-09-05-uGUI撤去は抽出PRと削除PRの2本に分ける.md` | 同じ型を使う（段階 0 の抽出、R3 の切替 PR と削除 PR） | 継承 | 全段階 |
| `.decisions/2026-09-13-CEF raw input応急処置は入れず…`、`.decisions/2026-09-09-WebSocketHubをレジストリの単一所有者にする.md` | 段階 6 で前提ごと消える | 消滅 | 段階 6 |
| 議論の記録「見た目は uloop screenshot で自己確認」「完了の定義に Kestrel 削除」 | ScreenCapture 系へ改める。D6 の結果に合わせる | **矛盾**（PoC の実測、ADR 0072） | 段階 0 |
| AGENTS.md「イベント発火に Action を使わない」 | `UIStateControl.OnStateChanged` を UniRx に直す | 既存の違反の解消 | 段階 0 |

### 8.4 ユーザーにしてもらう作業

- **日本語 IME の手動確認**（Mac と Windows 検証機の配布ビルド）
  - 段階 0: S9
  - 段階 1: バグ報告
  - 段階 3: 検索欄と BP 名
  - 段階 4: 駅名
- **段階ごとの短い目視**（1 回ずつ）
- **D1〜D9 の裁定**（段階 0 の前）
- **マージと playtest への反映**: 波ごとに PR をマージし、各段階の配布ビルドを playtest へ反映する（既存の手順のまま）
