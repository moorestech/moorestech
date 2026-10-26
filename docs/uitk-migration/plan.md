# UI Toolkit 全面移行 統合計画

- 2026-10-23 レビュー反映版（Fable 4 観点・Codex gpt-6-astra 2 観点）。**裁定ではない。** 作成時は repo を読んだだけで、コードには触れていない。
- 採点表・調査と草案の事実誤りの訂正・レビューでの訂正は `docs/uitk-migration/plan-notes.md` に分けた（計画の中身ではないため）。
- 数の確認: master HEAD `e8e611f1c0` で数え直した。本文で「実測」と書いた数がそれにあたる。規模・期間の数字はすべて推測。
- 入力: 裁定 `.decisions/2026-10-18-CEFを撤去しUI-Toolkitへ全面移行する.md`、議論 `docs/webui/ui-toolkit-migration-discussion-2026-10-18.md`、PoC 報告 `docs/uitk-migration/poc/hard-cases/index.html`・`docs/uitk-migration/poc/editor/index.html`（コードはブランチ `poc/uitk-hard-cases` の `de6c68621c`。master には無い）、調査 5 本 `docs/uitk-migration/research/understand-{screens,host,input,tests,shipping}.md`（以下 screens・host・input・tests・shipping）
- 表記: パスは repo 相対。`Scripts/` は `moorestech_client/Assets/Scripts/`、`web/` は `moorestech_web/webui/src/`。「推測」と書いた所は実測していない。

---

## 0. 判定と、着手前に聞くこと

**判定: 束単位の strangler を軸にする。** 束の中身は master の中で暗転のまま並列に作り、そろったら所有表の 1 行で切り替えて毎週の playtest に出す。配布物に切替フラグは入れない。第一弾は「命綱」（ポーズ・設定・バグ報告）。

**段階 0 の前に、次をまとめて 1 回で聞く**（選択肢と理由は §7.1）

| # | 論点 | 推奨 | 帰結 |
|---|---|---|---|
| 裁定 1（旧 D1） | 移行中の出荷形 | 束単位で暗転で作り、所有表の 1 行で切り替えて毎週出す。持ち物の束も束ごと一度に切り替える | インベントリ系の CEF 不具合（helper のメモリの伸び）は段階 4 の終わりまで残る |
| 裁定 2（旧 D2） | 第一弾の画面 | 命綱（ポーズ・設定・バグ報告・トースト）。CEF の延命は §3.11 の最小限だけ | CEF が死んでも Esc でセーブと報告ができる。自動再起動は作らない |
| 裁定 3（旧 D6） | 遠隔実行の待受 | `HttpListener` へ載せ替える。配布ビルドで動かなければ遠隔実行専用の最小 Kestrel | **ADR 0072 と矛盾**。どれを選んでも改訂が要る |
| 裁定 4（旧 D7。**2026-10-26 裁定済み**） | Web の扱い | UITK が安定稼働するまで Web の変更（不具合修正・新機能）を続け、凍結しない。移す範囲は現行 Web の全機能（`.decisions/2026-10-26-UITKが安定するまでWeb-UIの変更を続け現行機能をすべてUITKへ移す.md`） | 移す対象が移行中も動く。G1 は凍結でなく追従ゲートにする（§4.7）。束の切替 PR が、暗転の起点以降に入った Web の差分を全部写す |
| 確認（旧 D18） | 時間の規則の読み方 | AGENTS.md の時間の規則はサーバーのゲームロジックの規則と読み、UI の演出は実時間で動かす | 異議が無ければこのまま |
| 様式化（旧 D14） | Mantine の素の見た目の 4 画面（トースト・チャレンジ・列車 HUD・ポーズのボタン） | 移すときに、ホワイトリストの語彙で 4 画面を一度に様式化する（moores-grill-with-docs を 1 回） | 見た目が変わる。負債 `moorestech-cgm` を閉じられる |

ほかの論点は agent の既定として進め、台帳に記録する（§7.2）。

**段階 0 の中で裁定したもの（2026-10-26。一覧は §7.1）**: 旧 SkitUI の削除（0-12）、差し込み記法の名前付きへの統一（0-6）、Codex で進められない段階 0 の実装を Claude Code で進めること、UITK が主パネルを出しているあいだの Web の全面オフ（R1・§3.8・S15）。

**何を移すか**
- CEF（cef-unity）と Web UI 一式を、C#・UXML・USS の UI Toolkit（以下 UITK）に置き換える
  - React の本番コード: TS/TSX 12.2k 行＋CSS 3.4k 行（テスト除く。features・shared・app・bridge。実測）
  - `Client.WebUiHost` 140 ファイル・10,565 行（実測）、Kestrel・WebSocket
  - 配布物の CEF（Windows 237 ファイル・約 368MB、Mac 252 ファイル・約 298MB。shipping §1.1）
- 移す単位は「束」。同時に表示され、掴みや D&D でつながる面の集まり。共存中は、どの画面状態でも描く系をちょうど 1 つにする（所有表。§3.3）

**規模・期間（推測）**
- 合計 12〜16 週（段階 2 と 3 の暗転作業は重なる）。律速は Editor の本数（§3.13）と、独立レビューの裁定
- PR は 55〜70 本。UITK の新規コードは本体約 2.3〜3.2 万行（§1 の合計）・テスト約 1 万行。削除は約 45〜50k 行

---

## 1. 段階一覧

各段階は「切替 PR → 1 周期 → 削除 PR」で進む（§3.3 R3）。物差しは CEF 無し起動ラダー（§4.4）。どの段階も出荷でき、暗転中も master は出荷できる。「Web の `x`」は `web/features/x` を指す。

| 段階 | 中身 | テスターに届くもの | 物差し | 消えるもの | 規模・期間（推測） |
|---|---|---|---|---|---|
| 0 地ならしと実測 | 遠隔実行を Kestrel から外す、状態を DI へ移す、OneJS と旧 SkitUI の撤去、差し込み記法、DSL の付け替え、台帳・参照キャプチャ・追従ゲート、実測 5 本 | 見た目は変わらない。配布物から quickjs が消える | Web ホストを起動しなくても、状態クラスと遠隔実行が生きている | `com.singtaa.onejs`、`com.annulusgames.debug-ui`、遠隔実行の Kestrel 依存、旧 UITK の `SkitUI` と `SkitPresentationMode` | 40〜60 ファイル。新規 1〜2k 行、移設 2〜3k 行、シナリオ約 40 本。1.5〜2 週 |
| 1 基盤と命綱 | UITK の基盤、ポーズ・設定・バグ報告・トースト、CEF 停止の記録 | CEF が死んでも Esc でセーブ・設定・バグ報告ができる | `uitk-lifeline` | Web の `pauseMenu`・`settings` とテスト、ポーズ系の Web の ESC 経路 | 基盤 60〜90 ファイル（C# 4〜5.5k 行）、画面 25〜35 ファイル（1.5〜2.5k 行）。2.5〜3 週 |
| 2 常駐表示 | 入力の無い HUD 9 種、通知、ツールチップ、ワールドピン、チュートリアル枠、スキット | ゲーム画面とストーリーで、CEF が操作できるものを描かない | `uitk-opening-hud` | Web の `commonHud`・`progress`・`keyHint`・`modeHud`・`trainHud`・`notification`・`skit`、CursorTooltip、WorldPinOverlay、KeyControlHintHud | 70〜90 ファイル、3.5〜4.5k 行。1.5〜2 週 |
| 3 建築の束 | ビルドメニュー、ホットバー、BP 名入力モーダル | ビルドメニューから選んで建てられる | `uitk-build-bp` | Web の `buildMenu`・`hotbar`・`modal`、`BlueprintNameInputWebBridge` の Web 送り | 40〜55 ファイル、3〜4k 行。1〜1.5 週（段階 2 と並走で暗転） |
| 4 持ち物の束 | インベントリ・装備・掴み・レシピ・ブロック UI 全族（列車を含む）・研究を、束ごと一度に切り替える | 生産ループ全体が CEF に依存しない | `uitk-production-loop`、`uitk-research-train` | Web の `inventory`・`recipe`・`blockInventory`・`research`、`shared/itemMove` などの掴み系 | 100〜150 ファイル、8〜11k 行、テスト 300〜400 件。3〜4 週 |
| 5 最後の束と撤去 | チャレンジ一覧・出展モードの言語ゲート・Web の横断層を R3 の型で出し、その削除 PR に続けて撤去する | 配布物から CEF が消える | `uitk-challenge-gate`。撤去後はラダー 6 本が CEF の存在しないビルドで通る | `web/features` の残りすべて。CEF・Web ホスト・`moorestech_web`・Kestrel と AspNetCore 系 NuGet（裁定 3 で HttpListener の場合）・DSL の Web 実装・所有表と委譲・Web という名前（§2.6） | 最後の束 25〜40 ファイル・1.5〜2.5k 行、撤去 PR 5 本。1 週＋1 周期＋1〜1.5 週 |

---

## 2. 各段階の詳細

### 2.0 全段階の共通条件

各段階の終わりに、次の 3 つを満たす。

1. ラダーが、前段ぶんも含めて、Web ホストを起動しない設定で緑（§4.4）。
2. 録画シナリオが緑。移した面に触るシナリオは、DSL の UITK ロケータで解決している（ログで確かめる）。
   - 全本を流すのは 3 回だけ: 段階 1 の終わり、段階 4 の切替前、段階 5 の撤去。ほかの段は「移した面に触るシナリオ＋ラダー」
   - 対象外は壊れている 10 本（bd `moorestech-wwxy` の本文 8 本＋NOTES の 2 本）
3. Unity の Error ログが 0。

配布は既存の工程（moores-playtest-release）に乗せ、次の 2 つを足す。
- Mac の確認項目を、その段のラダー先頭の操作のスクショに置き換える（段階 1: Esc でポーズ、2: 採掘ゲージと通知、3: B でビルドメニュー、4: Tab でインベントリ、5: チャレンジ一覧）。Windows 検証機の通し検証は UI を開かないため（`Scripts/Client.Starter/PlaytestSmoke/StandalonePlaytestSmokeRunner.cs:67-75`）
- パッチノートに、その段で UITK に移った画面と既知の差分を書く。受け口の無人修正（bug-report-auto-fix）が既知の差分を拾わないようにするため

オーナーの目視は各段で 1 回、staging ビルドで、反映の前に行う。

### 2.1 段階 0 地ならしと実測（見た目は不変）

**目的**: 挙動を変えずに、後回しにすると全段階を塞ぐものを片付ける。段階 1 の前提を実測で確定する（§4.8 の 5 本）。並列作業の土台（台帳・参照キャプチャ・追従ゲート・bd）を置く。

| # | 作業 | 中身と根拠 |
|---|---|---|
| 0-1 | 使っていないパッケージを外す | `com.singtaa.onejs`（manifest 13 行目）と `com.annulusgames.debug-ui`（3 行目）を manifest と lock から外す。どちらも参照は 0 件。OneJS は配布物に quickjs と Resources 2.2MB を入れている（shipping §5） |
| 0-2 | 実測 5 本（§4.8） | 使い捨ての隔離 worktree で行う。PoC ブランチを土台にしてよい。1 本 1 日で打ち切り、「不可」も結果として記録する。master へ入れるのは結論と、UI Test Framework の manifest 追加だけ |
| 0-3 | 遠隔実行を Kestrel から切り離す（裁定 3） | 下の「0-3」 |
| 0-4 | 状態を Hub の外へ移す（抽出 PR。挙動は変えない） | 下の「0-4」 |
| 0-5 | `UIStateControl.OnStateChanged` を UniRx にする | 今は `event Action<UIStateEnum>`（`Scripts/Client.Game/InGame/UI/UIState/UIStateControl.cs:16`）。AGENTS.md「イベント発火に Action を使わない」に反している |
| 0-6 | 差し込み記法を名前付きに統一する（2026-10-26 裁定） | 下の「0-6」 |
| 0-7 | DSL を描画系に依存しない形にする（この段は Web 実装だけ） | 下の「0-7」 |
| 0-8 | 台帳・参照キャプチャ | 下の「0-8」 |
| 0-9 | 追従ゲート（裁定 4） | G1（§4.7）を段階 0 の最初の PR で入れる。Web の変更は止めない。新しい Web 画面が入ったら台帳のその束の行に足す（例: Tier9 の #1551・#1552 が足す `blockInventory/details/{rocket,spaceElevator}` は段階 4 の特殊族） |
| 0-10 | Web ホストを起動しない Editor 限定スイッチ | `#if UNITY_EDITOR` で囲み、ファイル末尾に置く。Hub が null の経路（起動失敗と同じ形）を PlayMode で再現する。ラダー（§4.4）の土台 |
| 0-11 | bd | epic を立て、段階と単位を子にする。派生は `--parent` で系譜を残す |
| 0-12 | 旧 UITK の SkitUI を消す（純削除。2026-10-26 裁定） | `SkitUI.prefab`（UIDocument・`SkitPanelSettings`）、`Scripts/Client.Skit/UI/SkitUI.cs`、`SkitPresentationMode` を消す（bd `moorestech-lnsf.8`）。`SkitManager.cs:197` が true に固定しているので、非 Web の分岐は死にコード。prefab とシーンの参照は uloop で外す。段階 1 からパネルが 1 枚になる |

**0-3 遠隔実行**
- `/api/remote-exec` は Web UI の Kestrel に相乗りしている（`Scripts/Client.WebUiHost/Boot/WebUiEndpoints.cs:97-101`、ADR 0072）。Web ホストの起動に失敗すると遠隔実行も開かず（`WebUiStartup.cs:19`）、検証機で CEF の不具合を調べる手段まで失う
- `RemoteExecEndpoint` を `HttpContext` に依存しない形にして裁定 3 の待受に載せ、起動の Web 依存（`InitializeScenePipeline.cs:70-73` の `KestrelPort` 受け渡し）を外す。`RemoteExecEndpointTest` と `PlaytestBootUnityCallbackBindingTest` を直し、`scripts/playtest/remote-exec.sh` は `access.json` 経由のまま使えるようにする。ADR 0072 の改訂を同じ PR に入れる
- 配布ビルドは Mono で、`managedStrippingLevel` は Standalone 未設定（既定）。この設定で `HttpListener` が動かなければ、遠隔実行専用の最小 Kestrel にする

**0-4 状態の移設**
- 対象: `ResearchTopic._nodeStates`、`ChallengeTopicState`、`BlockNetworkInfoCache`、`BlockInventoryTopic.TrackBlock` の監視判断、`OpenTrainTimetableTracker`・`TrainTimetableFetcher`、`NotificationTopic` の連番と移行告知、`WebUiModalService`（static をやめ、`Client.Game` のインターフェースにする）、`BlueprintNameInputWebBridge` の状態部分、`EventLanguageGate`
- 移す先は `Client.Game` の DI（MainGameStarter の LifetimeScope）。今はこれらが `WebUiGameBinder.Bind()` の中でしか作られず、Hub が null だと丸ごと作られない（`Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs:48-55`）
- 移した topic は「型付きの購読元」と「JSON アダプタ」に分ける（§3.2）。前例は ADR 0052 の「抽出 PR と削除 PR の 2 本」

**0-6 差し込み記法**
- `LocalizationTextInterpolator` は今 `{pN}` だけを埋める。名前付き（44 キー・370 か所。実測）は Web の `i18nStore.ts` が解いているので、C# の補間器を名前付きに対応させる（必須）
- 位置指定 `{pN}` の 32 キー（全言語列で 190 か所。実測）は、同じ PR で名前付きへ機械的に書き換える。C# の呼び出し元（`TooltipLine.cs`・`ConnectServer.cs`・`RemoteConnectionDenyReason.cs`・`LoadingProgressLog.cs` など）と `Localize.GetFormatted` の位置指定シグネチャも名前付きへ改める
- `LocalizationPlaceholderParityTest` は残す。型付きの書式メソッドの生成器は作らない（§7.3 の 08-26 裁定）。mod 同梱の CSV に差し込みは 0 件で、mod への波及は無い（実測: `moorestech_master/server_v8/mods/moorestechAlphaMod_8/localization/localization.csv`）

**0-7 DSL**
- `IUiElementLocator` を入れる。Find（可視・矩形・文字列・状態フラグ）と `SetText(testid, text)` を持つ。`SetText` は、UITK では `TextField.value` を設定して ChangeEvent を流す（IME は通さない）。今の DSL に文字を打つ口は無く（`Scripts/Client.Playtest/Operations/PlaytestWebUiOps.cs:28-83`・`Scripts/Client.Playtest/Input/SemanticInput.cs:35-61`）、既存シナリオは state への直書きで回避している（`blueprint-copy-paste-via-ui.cs:2`）
- `ClickWebUi` 系を `ClickUi` 系へ一度に改名し、旧名は残さない。対象は約 40 本（式で確定。数え方: `grep -rlE 'WebUi|DomQuery|Cef' .agents/skills/unity-playmode-recorded-playtest/scenarios --include='*.cs'`）
- `PlaytestDomQuery` の直接呼び出しは、ロケータの照会 API に置き換える。`data-selected` などの属性は「状態フラグ」として抽象化する（Web は属性、UITK はクラス）
- スキル文書 4 本（`write-scenario.md`・`input-injection.md`・`place-blocks-via-ui.md`・`run-scenario.md`）も同じ PR で直す

**0-8 台帳・参照キャプチャ**
- 台帳は `docs/uitk/TODO.md` 1 枚。行は `web/features` の 20 機能と、`web/app/App.tsx` のオーバーレイ数行（背景スキット・Ctrl+U・カットシーン中の退避・カーソル追従など、状態機械の外にあるもの。前回は状態機械だけで洗い出して背景スキットを見落とした。shipping §6.2）。agent の既定と実測の結論もここに書く
- 写さないテストの理由は `docs/uitk/disposition.md` 1 枚に書く（§4.1）。旧 `docs/webui/MIGRATION.md` は archive への案内にする。所有表の初版（全部 Web）もここで置く
- 参照キャプチャ: Web 版の全画面と主要な状態を撮る（Playwright の mock-host と PlayMode）。通知・脈動・ドラッグガイドは短い動画で残す。置き場は repo の外（`../moorestech_logs` か share）で、台帳からリンクする。束を消した後も「意図した見た目」を辿れるようにするため

**完了条件**
- Windows のビルドログで、`Plugins/x86_64` に `quickjs_unity.dll` が無い。それ以外の差分が無い
- Windows 検証機の配布ビルドで、`--remoteExec` 付きなら `remote-exec.sh` が `Succeeded` を返し、付けなければ待受が開かず、Web ホストの起動を失敗させても遠隔実行は開く（`HttpListener` が配布ビルドで動くかの確認を兼ねる）
- Editor 限定スイッチで Web ホストを起動しない PlayMode でも、研究・チャレンジ・ブロック詳細・時刻表・モーダル・通知の状態が DI から取れる（EditModeInPlayingTest）
- 改名したシナリオの合否が、改名前と同じ
- 実測 5 本の報告が証拠（ログの行・スクショ・数値）付きでそろっている。成立しないものがあれば、段階 1 に入らず裁定に上げる
- G1 が、暗転の起点より後の未記帳の Web 変更と、切替済みの束の Web 変更のそれぞれで赤になる

**リスクと潰し方**

| リスク | 潰し方 |
|---|---|
| 移設で topic の配信タイミングが変わる | 抽出 PR は「移動と配線」だけにする。既存の C# テスト（ビューモデル 90・アクション 36・状態ストア 28）と、移した topic に触る録画で回帰を見る |
| 遠隔実行の載せ替えで、検証機の運用（以降の全段階の検証路）が止まる | 旧経路と新経路の切替を 1 PR にまとめ、検証機で通してから master へ入れる |
| DSL の改名でシナリオが壊れる | 機械的な置換に限る。照会 API への置き換えは 1 本ずつ合否を突き合わせる |

**規模と並列（推測）**: 同時に走らせるのは Editor 要 2 本＋Editor 不要 2 本（§3.13 の勘定）。Editor の要る作業は 3 レーン（遠隔実行／状態の移設と 0-5／DSL）と実測 1 本（S1・S2・S3・S15 を順に）で、0-6 と 0-12 は先に空いたレーンが持つ。Editor の要らない作業は OneJS・台帳・追従ゲート・参照キャプチャの Web 側。S9（IME）は人手（§4.9）。

### 2.2 段階 1 基盤と命綱

**目的**: 全画面が乗る基盤を作る。ポーズ一式を UITK に移し、CEF が死んでも Web ホストが起動しなくても、Esc でセーブ・セーブして終了・設定・バグ報告ができる状態を毎週の playtest に出す。CEF が止まったことを開発者に届ける（§3.11）。

**命綱を第一弾にする理由**（裁定 2）
- テスターの実害が「UI がまるごと死ぬ・報告もできない」に集まっている（実測。Discord ミラー）
  - 6 時間放置で cef-unity のプロセスが消え、Tab・Esc・T・R と施設の UI が一切開かなくなった（2026-10-02 https://discord.com/channels/888315641709330433/1555576468828987532 ）
  - helper のメモリが伸び続け、約 14,600MB 使っていたところで UI がフリーズした。Esc の連打だけで 51MB → 170MB に伸びる（https://discord.com/channels/888315641709330433/1554924554697187389 ）
- 束 D（ポーズ・設定・トースト）は掴みも D&D も持たず、単独で切り出せる（screens §7.1）。ポーズのアンカー（`pause.*`）は、マスタからの参照が v8 で 0 件（実測）
- IME・ESC・フォーカス・ローカライズ・フォント・モーダル層の難所を、掴み抜きで最初に通せる

**対象**: PauseMenuPanel（トップ、サブページ、切断の表示、入れ子のポーズ〈スキット中 skitPause・乗車中 trainPause。ADR 0035〉）、設定 3 種（画質のプリセットと個別設定、言語、ダッシュ方式）、バグ報告フォーム（種別・本文・送信・配送状況の 1 行・下書き）、トースト層（バグ報告の結果と、良性でないコマンド拒否の分。通信失敗のトーストは Web が残る間 Web に残る）、CEF 停止の記録（§3.11）。

**作業（基盤。設計は §3）**
- UI ルート: `GameUiRoot`（UIDocument 1 枚）を MainGameUI.prefab に事前配置する（`uloop execute-dynamic-code`）。PanelSettings は Scale With Screen Size、基準 1280×720、Expand。層 enum と層コンテナ（`PickingMode.Ignore` を既定にする）
- ルータと所有表: SCREEN_TRAITS の C# 版、所有表（R2）、Web への委譲と全面オフ（R1）、Editor 限定の上書き（R4）
- 入力: `UiPointerHitTest` に Pick を論理和で足す（呼び出し側の 23 ファイル・28 か所は変えない。実測）。UITK が主パネルを出しているあいだは Web の値を読まない（§3.8）。`WebUiInputExclusivity` を `UiInputExclusivity` へ改名して汎用化する。入力欄以外は `focusable=false`
  - ESC ラッチ。レーンの先頭で、TextField の上の ESC を EventSystem と `UIStateControl.Update` のどちらが先に処理するかを確かめ（S5）、置き場を決める
  - UI 専用のアクション資産で、MainGame の `DefaultInputActions`（`Assets/Scenes/Game/MainGame.unity:8435`）を uloop で差し替える。レーンの先頭で、資産で WASD・矢印の Navigate、Space・Enter の Submit を止められるかを確かめ（S4）、止められなければルートで NavigationSubmitEvent を打ち消す
- テーマ: レーンの先頭で、Linear 色空間の半透明 3 案（実機で決め直す・α の近似補正・sRGB の RT で合成）と、10〜13px の文字（720p・1080p・4K）を実機で比べ（S7）、§7.2 の既定を確かめる。`Tokens.uss` に全トークン約 238 個を機械的に移す。`Theme.uss` は PoC の `PocControls.uss`（146 行、PoC ブランチ）を初版にする
- フォント: TextCore の SDF FontAsset（JP・EN・KR）とフォールバック。レーンの先頭で、Editor で作ってコミットできるかを確かめ（S10）、できなければ PoC と同じく実行時に生成する
- UiMotion（§3.6）。段階 3 の smooth scroll と段階 4 の研究ツリーの慣性も使う
- ローカライズ: `LocalizedLabel`、InvariantCulture の数値書式、UXML の生文字の禁止
- アンカー: `AnchorIds` を C# の唯一の正本にする（`web/shared/tutorialAnchor/anchorIds.ts` から移す）。`TutorialAnchor.Mark` と登録簿も作る。枠の描画は段階 2
- テストの土台: `UiTestHost`（本番のテーマ・フォント・1280×720 を当てる）、`FakeXxxModel` と `RecordingXxxCommands` の雛形、L2 の規約テスト。最初の L1 テストを既存 CI に載せる（S13）
- DSL は `IUiElementLocator` の UITK 版（§4.4）。スキルとレビューは `ref-uitk-design` と moores-code-review の UITK 観点の初版

**作業（画面）**
- `PauseMenuState` と `PauseMenuStateService` は C# のまま使い、ビューと Presenter だけを作る。`pause_menu.*` と `bug_report.submit` のハンドラを、型付きコマンドと JSON アダプタに分ける（host §3）
- ポーズ系の状態（入れ子の列車中・スキット中を含む）では、Web を全面的にオフにする（R1）。常駐 HUD（ホットバー・装備 HUD）、列車 HUD、スキットの表示も、ポーズのあいだは消える
- 操作音は、ビューからサウンドのサービスを直接呼ぶ。ポーズのボタンとトーストは §0 の様式化に従う
- GamePanel の初出で、720p と 21:9 の 2 解像度を 1 回撮る（§4.6）
- 切替 PR の 1 周期後の削除 PR で、Web の `features/pauseMenu`・`features/settings`・テスト・`App.tsx` のマウントを消す

**完了条件**
- 共通条件（§2.0）を満たす。ラダー 1 `uitk-lifeline`（§4.4）が緑。本文は SetText で入れ、IME は §4.9 の人手で確かめる
- 報告バンドルの manifest に、CEF の生の値 3 つ（§3.11）が入っている。最後の WS 受信から閾値を超えたとき、入力排他の Web 側の値が無視される（L0）
- ポーズ系の状態で、CEF の RawImage が非表示で、CEF へ入力が転送されず、Web が action を発行しない（action ログが 0 件）。ポーズを閉じた直後に、ポーズ前の Web の pointerOverUi・textInputFocused が効かない（L0）
- IME の手動確認（Windows と Mac）の記録がある。バグ報告の本文（複数行）で、変換・確定・取消と、変換中の ESC で画面が閉じないことを見る
- 入れ子ポーズの 2 経路（スキット中・列車中）の録画がある。最初の L1 テストが既存 CI で通る（S13）

**出荷**: テスターから見える変化は、ポーズ画面の見た目（半透明の濃さ・字の太さ・ボタンの様式）と、ポーズ中に Web の HUD（ホットバー・装備 HUD・列車 HUD）とスキットの表示が消えること。パッチノートに書く（§2.0）。

**リスクと潰し方**

| リスク | 潰し方 |
|---|---|
| ESC が二重に処理される | ESC は C# の UIState に一本化する。ポーズ系の Web の ESC 経路は、委譲中は CEF へ入力を転送しないので動かない。ESC ラッチを L1 で固定する |
| 入れ子ポーズで、UITK のポーズが Web のスキット窓の下に隠れる | S1 で前後を確定し、録画で確かめる。段階 2 でスキットも UITK になる |
| 言語切替で、片方の系だけが再描画される | 同じ録画で両系を確かめる |
| 基盤を作り込みすぎて段階 1 が長引く | 部品は「その段で使うもの」だけを作る（§3.4）。例外は全画面が使う基盤（層・入力・テーマ・i18n・アンカーの登録簿）と UiMotion |

**規模と並列（推測）**
- 基盤 60〜90 ファイル（C# 4〜5.5k 行、USS 0.8〜1.2k 行）、画面 25〜35 ファイル（1.5〜2.5k 行）、テスト 120〜180 件
- 共有ファイル（層 enum・所有表・composition root・Tokens.uss・AnchorIds・DSL のロケータ本体）は integrator 1 人が直列で持つ
- integrator が骨組み PR（UI ルート・層・ルータ・所有表・`UiTestHost`・Tokens 初版）を先に 1 本出し、3 レーン（テーマ・フォント・GamePanel・UiMotion／入力・DSL・テストの土台／ポーズ・設定・バグ報告・CEF 停止の記録）はそのマージ後に始める

### 2.3 段階 2 常駐表示（画面を開いていない時間）

**目的**: テスターが一番長く見ている「ゲーム画面」と「ストーリー」を CEF から外す。完了時、GameScreen・PlaceBlock・DeleteBar・Story・TrainHUDScreen の各状態で、Web が描くのは「表示だけのホットバーと装備 HUD」だけになる。

**対象**
- 入力の無い HUD 9 種: Crosshair、KeyHintHud、KeyControlHintHud、ProgressBar（採掘ゲージ）、TrainRidingHud、CurrentChallengeHud（アンカー `challenge.current-hud`。マスタが参照）、PlacementModeHud、DeleteModeWarningBands、DeleteBoxHeightHud。列車 HUD の見た目は §0 の様式化に従う
- 通知（獲得通知の集約と出入りの演出）、CursorTooltip（topic `ui.tooltip`）とワールドピン
- UITK 側のチュートリアル枠（UITK のアンカーだけを照らす。Web の TutorialOverlay は段階 5 まで残る）
- スキット（会話窓・タイプライタ・選択肢・ツールバー・暗転・背景スキット）、UITK 側の Ctrl+U とカットシーン中の全体非表示
- 対象外: ホットバーと装備 HUD。束に縛られるため、段階 3・4 まで Web に残る

**作業**
- 基盤: ツールチップ層（オフセットは既存の `OFFSET` を参照。現在 12）。アンカーの枠（祖先クリップとの交差、脈動、ラベルの反転、二重解決のログ。§4.5）。Painter2D の図形（FadeRule・GaugeBar・削除モードの斜線帯）。GamePanel の hud と skit の面（装飾を含めて完成形で作る）
- データ: 通知の集約（`web/features/notification/notificationStore.ts`）を C# の通知モデルへ移す。生存尺は `NOTIFICATION_DISPLAY_MS` を参照（現在 7000ms）、入場 160ms・退場 200ms（ADR 0016）。ワールドピンは同じフレームで投影し、WS を通さない
- スキット: 入力は単一消費者なので、描画者を 1 つにする
- 通知の重なり（ADR 0017 は「全画面 UI の裏」）: UITK は CEF の上に描くので、Web の全画面パネルが出ている間は UITK の通知層を隠す。S1 で背面パネルが安く作れると分かれば、背面パネルに置いて ADR 0017 の形を保つ。どちらにするかは agent の既定で決め、台帳に書く
- ホイール: ホットバー（Web）の上のホイールは、今どおり Web の装備切替へ通す。C# 側の `UiPointerHitTest` は `wheel-passthrough` を尊重する

**完了条件**
- 共通条件（§2.0）を満たす。ラダー 2 `uitk-opening-hud`（§4.4）が緑
- マスタの `challenge.current-hud` のハイライトが UITK で出る録画がある
- ADR 0024 の判定役テスト（祖先のクリップ）の UITK 版が 1 本ある（`.decisions/2026-08-20-チュートリアルハイライトのマスク方式とテスト方針.md` の型）
- 同じアンカーが両系で解決したら、エラーログが出る（テスト）
- ゲーム画面での CEF の 0F-wait（`_enableLog=1` の `block_avg`）を段階 2 の前後で比べて台帳に書く。下がらなくても完了は妨げない（`docs/webui/cef-unity-frame-rate-followup-2026-07-29.md` §2.2。効果は推測）

**リスクと潰し方**

| リスク | 潰し方 |
|---|---|
| チュートリアルの二重描画・取りこぼし | 両系がそれぞれ自分の木で探す（共通のリゾルバは作らない）。系をまたぐ dragGuide は新設しない（マスタは 0 件） |
| スキットの入力が二重に送られる | Web のスキットの入力経路は委譲中は動かない。削除 PR で消す |
| タイプライタで、行の折り返しが途中で変わる | 残りの文字を透明にして配置を固定する（推測。shipping §3.1） |
| 通知を隠すことで、全画面 UI を開いている間に通知を見落とす | ADR 0017 でも全画面 UI の裏なので、実害は小さい（推測）。段階 5 で元の形に戻す |

**規模と並列（推測）**: 70〜90 ファイル、C# 3.5〜4.5k 行、UXML/USS 約 1k 行。同時は Editor 要 2 本＋Editor 不要 2 本（§3.13）。Editor の要る 4 レーン（HUD とツールチップ／チュートリアル・アンカー・ワールドピン／スキット／通知）を 2 本ずつ回し、Editor の要らない 2 本で段階 3 と段階 4 の純ロジック（`shared/itemMove` など。L0）を移す。

### 2.4 段階 3 建築の束（ビルドメニュー・ホットバー・モーダル）

**目的**: 最初の操作の多い画面で、D&D・ホバーツールチップ・IME 付きの検索欄・スクロールスパイ・入力の取り合いを本番で踏む。ホットバーは常駐 HUD なので Web のインベントリ画面と同時に出て、共存の入力規則（R1、§3.8）が本番で試される。

**対象**
- BuildMenuPanel 一式: カテゴリのサイドバー、検索欄（IME）、1 本のスクロールに並ぶカテゴリ群（スクロールスパイ・smooth scroll）、ホバーで固定される詳細サイドバー、BP の右クリック削除、不足時のツールチップ、セッション中の状態の保持（2026-08-05 の裁定群）
- HotbarPanel（9 枠、選択、D&D の割当・入替・解除）
- モーダル層と BP 名入力: `WebUiModalService` の後継。単一消費者なので、描画者を UITK に一本化する。モーダルを使うのは BP 名入力だけ（実測）

**作業**
- 束の中身は段階 2 の間に暗転で作り始め、そろったら所有表を切り替える
- 部品: press ジェスチャ（閾値は既存の `usePressGesture` を参照。現在 5px）、D&D 層（`panel.Pick` と落とし先の印のクラス）、HoverTooltip（祖先がスクロールしたら引っ込める）、SlotFrame・ItemSlot・SlotGrid・PlacementTargetFace、縦の ModeSwitch、IconButton、スクロール領域（components.md §8.10 の様式）、TextField（§8.9）
- ロジックの移植: `buildMenu/logic`、`hotbarDnd.ts`、`placementTarget`、`materialTooltipText`。`useBuildMenuCategoryScroll.ts`（305 行）の計算部分は、先に L1 で固定してからビューを作る
- データ: `build_menu.entries` と `local_player.hotbar` を型付きの購読元にし、`build_menu.select`・`blueprint.delete`・`hotbar.*` を型付きコマンドにする
- 床の一致: 装備 HUD は段階 4 まで Web に残る。ホットバーと床を共有するトークン（`--equipment-bottom: var(--hotbar-bottom)`。ADR 0013）が、両系で同じ値になることをテストで見る
- アンカー: `build-menu.entry-*` とホットバー系を UITK で付ける

**完了条件**
- 共通条件（§2.0）を満たす。ラダー 3 `uitk-build-bp`（§4.4）が緑
- 移すテスト（§4.1）: buildMenu（P 19・C 22・L 30）、hotbar（P 6・C 7・L 3）、modal（vitest 8）（tests §2）。例:「エントリを 5px を超えて動かし、ホットバーの枠 1 に落とすと、`AssignHotbar(1, id)` が 1 回だけ発行され、`SelectBuildMenu` は発行されない」
- 共存の受入: Web のインベントリ画面を開いたまま UITK のホットバーをクリックしても、Web の action が 0 件。UITK の検索欄に入力している間は、ゲームのキーも Web の document 全域のキー処理も動かない。検索欄の ESC は、フォーカスを外すだけで画面は閉じない

**リスクと潰し方**

| リスク | 潰し方 |
|---|---|
| Web が UITK 上のクリックを拾って二重に発火する | R1 と S15。共存の受入テストで、action ログが 0 件であることを見る |
| 捕捉中のドラッグにホバーツールチップが出る | PoC の罠。press ジェスチャ側で最初から抑止する |
| スクロールで子の `GeometryChangedEvent` が来ず、スクロールスパイやアンカーが追従しない | `scrollOffset` を監視して判定する |
| 装備 HUD（Web）とホットバー（UITK）の床がずれる | 床トークンの一致テスト |

**規模と並列（推測）**: 40〜55 ファイル、3〜4k 行、テスト 100〜140 件。部品（D&D・ジェスチャ・スロット）を 1 本が先に持ち、その後ビルドメニューとホットバー・モーダルの 2 本で並べる。

### 2.5 段階 4 持ち物の束（束ごと一度に切り替える）

**目的**: 掴みでつながる面を、UITK にまとめて切り替える（裁定 1）。研究画面も InventoryPanel を同時に出し、掴みを許す画面なので同じ束に入れる（`uiScreenRouting.ts` の SCREEN_TRAITS で researchTree は `grab: true`、`web/app/App.tsx:95,100` で InventoryPanel と ResearchTreePanel が同時にマウントされる。実測）。列車族も subInventory で InventoryPanel を使うので、同じ束に入れる。

**対象**
- InventoryPanel、EquipmentPanel（HUD とホイールの循環。循環は C# の 1 か所へ移す。`web/features/inventory/EquipmentPanel/index.tsx:55-80`）、GrabOverlay、スロットの HoverTooltip、RecipeViewer と ItemListPanel
- BlockInventoryPanel の全族
  - 汎用族: SectionStackView（チェスト・機械・発電機・採掘機・ポンプ・パイプと液体）
  - 特殊族: FilterSplitter、ElectricToGear、GearToElectric、ElectricPole、Gear・Shaft・GearBeltConveyor、ベルトの流量、電力網、歯車網
  - 列車族: 列車インベントリ、駅名、プラットフォーム、時刻表
- ResearchTreePanel（TreeView・ノード 4 状態・詳細ペイン・研究ボタン）、インベントリ内の DragGuide
- アンカー: `recipe.craft-button`、`recipe.item-<itemId>`、`research.node-<guid>`（マスタは 7 件）

**作り方（暗転のまま波で並列に作る）**

| 波 | 中身 | 本数 |
|---|---|---|
| 4-α 中核 | `shared/itemMove` の計画の移植（左・右・ダブルクリック・Shift 配分）、掴み層、分割ドラッグ、右ドラッグ配り、GamePanel の default と craft、SectionStackView とレジストリ、RecipeRow（`.decisions/2026-08-20-レシピ行は独自gridを維持しSlotGridの例外とする.md`） | 1〜2 本が直列で先に作る |
| 4-β 並列 | 次の 5 群。(1) インベントリ・装備・レシピ・一覧（長押しクラフトはパネルのスケジューラで動かす。コンテナクエリは `GeometryChangedEvent` で計算する）、(2) 機械と汎用族（`.decisions/2026-08-30-機械UIはタブを廃しレシピ選択とインベントリをSatisfactory方式で往復する.md`）、(3) 特殊族（BarSeries、←→ での窓の切替）、(4) 列車族（駅名の IME、時刻表の楽観更新）、(5) 研究（TreeView: パンとズーム 0.4〜2.5、慣性 τ=325ms、Painter2D の線） | 同時は Editor 要 2 本＋Editor 不要 2 本（§3.13） |
| 4-γ 統合 | 同時に出る組（playerInventory、subInventory、researchTree）を確かめる。Editor 限定の上書きで全録画を回す。所有表の切替 PR を出す | integrator |

切替後の流れ: staging → 検証機・Mac → playtest。1 周期後に削除 PR を出す（R3）。

**完了条件**
- 共通条件（§2.0）を満たす。ラダー `uitk-production-loop` と `uitk-research-train`（§4.4）が緑。駅名は SetText で入れ、IME は §4.9 の人手
- 移すテスト（§4.1。tests §2）: インベントリ・装備（P 20・C 16・L 57）、レシピ（P 18・C 10・L 19）、ブロック UI（P 42・C 27・L 45）、列車（P 6・C 6・L 14）、研究（P 18・C 21・L 37）
- 機械 UI を開いたまま中身が更新される録画を、UITK で通す（`.decisions/2026-10-18-液体UIは開いたままRefresh更新をunityプレイ録画テストで検証する.md`）
- マスタのアンカー（`recipe.craft-button` と研究ノード 7 件）が実ゲームで解決する
- 削除 PR の後、Web 側で掴みを扱うコードが 0。`screenAllowsGrab` の参照も 0
- インベントリと研究ツリーを開いたときの UITK の CPU 時間（`UIElementsUpdatePanels` と `UIElementsRepaintPanels` の合計）を台帳に 1 回記録する。目安は 1ms 以下（推測。PoC では CSS 表現 12 項目で 0.27ms）

**リスクと潰し方**

| リスク | 潰し方 |
|---|---|
| 最大の束を一度に切り替えるので、退行の幅が大きい | (1) 切替の前に、Editor 限定の上書きで全録画を回す。(2) staging から検証機を通してから playtest に出す。(3) 戻すのは切替 PR の 1 行（R3） |
| ブロック UI（11 種以上）の網羅漏れ | 切替前の全録画（機械 UI の全族）と、削除 PR で Web の `blockInventory` が空になることで捕まえる |
| 族の間で衝突する | 4-α でレジストリとセクションの枠を確定させてから 4-β に入る |
| 仮想化した一覧の行のアンカーが、画面外では存在しない | 研究ツリーは仮想化しない（transform で動かす）。仮想化する一覧は Bind 時にアンカーを付け替える |
| `MouseMove(from, to)` が途中のスロットに PointerEnter を出さない（L1） | 1 スロットずつ動かす（tests §5.5） |
| 暗転の期間中、テスターはインベントリ系の CEF の不具合を被り続ける | 裁定 1 の帰結として受け入れる。段階 1 の命綱で、止まってもセーブと報告ができる |

**規模（推測）**: 100〜150 ファイル、8〜11k 行、テスト 300〜400 件。エージェント日数で 15〜20 日。

### 2.6 段階 5 最後の束と撤去

**目的**: 最後の束を R3 の型で出し、Web が描くものを 0 にする。その削除 PR に続けて、CEF・Kestrel・Web ホスト・Web 資産・共存の仕組みを消し、完了の定義（§5.1）を満たす。

**最後の束の対象**
- ChallengePanel（研究の TreeView を再利用。カテゴリのボタン列）。見た目は §0 の様式化に従う
- EventLanguageGate（出展モード）: DI より前に出す全画面ゲート。`GameUiRoot` を起動の早い段で使えるようにするか、Starter が所有するゲート用のパネルを分けるかは、段階 5 の最初に決める（推測）。辞書を通さない文言（`DictionaryIndependentText`・`preDictionaryText.ts`）は C# の定数へ移す
- Web の横断層の残り: TutorialOverlay、ToastHost、DragGuide、再接続オーバーレイ、辞書エラー
- `AppErrorBoundary` の代わり（「UI の例外に誰が気づくか」）: UITK のビューの例外は、LogError とバグ報告の manifest に出す
- 通知を ADR 0017 の形（stage の背面）へ戻す

**最後の束の完了条件**
- 共通条件（§2.0）を満たす。ラダー 5 `uitk-challenge-gate`（§4.4）が緑
- 切替の後、DSL の Web ロケータが一度も要素を返さない（ログで確かめる）
- 1 周期後の削除 PR で、`web/features` が空になり、`App.tsx` は空のシェルになる

**撤去の PR**（直列。この順に出す）

| PR | 中身 |
|---|---|
| 5a 起動配線と共存機構 | 起動配線: `Client.Starter` の `WebUiStartup`、`InitializeScenePipeline.cs:73` の `KestrelPort`、`MainGameInitializationFinalizer` の `Bind`、`EventModeStartGate` の Hub 依存。共存機構: 所有表・委譲・Editor 限定の上書き、DSL の Web ロケータ、`UiPointerHitTest` と `UiInputExclusivity` の Web の項、manifest の CEF の値と無応答の判定、通知を隠す処理、Web ホストを起動しない Editor スイッチ |
| 5b CEF と配布工程 | `jp.juha.cefunity`（manifest 45 行目）。`CefUnity` を参照する 3 ファイル（`Client.WebUiHost/Cef/WebUiCefNavigator.cs`、`Client.Playtest/WebUi/CefScreenMapper.cs`、`Client.Playtest/WebUi/CefInputForwarder.cs`）。MainGameUI.prefab の CEF 一式（Canvas〈Overlay・sortingOrder 0。実測〉・RawImage・`CefUnityBrowserSample`・`WebUiCefNavigator`）を `uloop execute-dynamic-code` で外す。`CefRuntimeBundler` と `BuildPipeline` からの呼び出し、`CefPackageLfsValidator`、`scripts/setup-cef.*`、CI の CefUnity 事前取得。`scripts/playtest/lib/release-artifact.sh` の CEF helper 検査とその契約テスト（`scripts/playtest/tests/test-release-playtest-mac.sh`）、`scripts/playtest/README.md` の CEF raw input の確認。共用部品は消さずに整理する（`CefLfsPointer` は中立な名前へ改名。ffmpeg と Harmony の同梱検査が使う。`EnvironmentScrubOnEditorLoad` は `Client.ExternalProcess` 側へ移す） |
| 5c Web ホストと Web 資産 | `Client.WebUiHost`（asmdef ごと）、C# の通信契約テスト（131 件）、`WebUiActionInvoker`、`Client.Playtest/WebUi/PlaytestDomQuery`、`Client.Tests` のうち WebUiHost を参照する 67 ファイル（実測）、`moorestech_web/`、CI の `webui-test` ジョブ・`needs_webui`・pnpm の step、`WebUiProductionArtifactBuilder`、`StreamingAssets/WebUi` とその `.gitignore` |
| 5d NuGet | AspNetCore 系（裁定 3 で HttpListener の場合）と、使われなくなった `Microsoft.Extensions.*`。依存グラフを確かめてから消す。`packages.config` は 74 件で、47 件が `Microsoft.AspNetCore.*` か `Microsoft.Extensions.*`（実測）。`ModelContextProtocol 0.2.0-preview.3` は client と server の scripts に `using` が無い（実測）。未使用なら同じ PR で外す（推測） |
| 5e 文書・スキル・改名・ADR・bd | 改名: `WebPinId`（13 か所。実測）、`LocalizationKeys.Ui.Blueprint.WebModal*`、`VanillaSchema/challenges.yml` の `web anchor id`（3 か所。実測）、`WebUiGateAuditTest`（名前を変え、対象を付け替えて残す）。スキル: `ref-webui-design` を廃止して `ref-uitk-design` を完成させ、moores-code-review の ts/tsx 観点 12 本を撤去し、DSL のスキル文書を更新する。文書: `docs/webui` を archive にして入口を `docs/uitk` にし、`docs/development/macmini/unity-editor.md` の CEF の節を消す。ADR と裁定の改訂か廃止の記録（§7.3）。bd: CEF 関連の open 課題 12 件（shipping §1.2）と `moorestech-cgm` を、閉じるか台帳へ転記する |

- 5a を先にする。起動しなくなれば、残りは参照されないコードになる
- 5b を 5c より先にする。5c を先に出すと、`MainGameUI.prefab` に missing script（guid `592d2edb…`＝`WebUiCefNavigator`）が残ったまま CEF が起動し続ける
- 5b は配布物が変わるので、PR の中で Windows と Mac のビルドを回し、Windows 検証機・Mac の Steam・`test-release-playtest-mac.sh` を通してから playtest へ反映する
- 5e は実体が消えてから書く

**規模と並列（推測）**: 最後の束は 25〜40 ファイル、1.5〜2.5k 行で、並列は 3 本（チャレンジ／言語ゲート／横断層と例外の経路）。撤去は PR 5 本で、並列は 1〜2 本。削除の内訳は `moorestech_web` 約 28k 行（テスト込み）、`Client.WebUiHost` 10.5k 行、テスト 6〜8k 行、DSL の Web 部約 0.7k 行。

---

## 3. 共通基盤

### 3.1 置き場と構成

- **asmdef**: `Client.Ui` を新設する。名前に技術名（Uitk）を入れない（Web という名前の改名を繰り返さないため）。参照先は `Client.Game`（モデル・コマンド・状態の正本）、`Client.Input`、`Client.Localization`。`Client.Game` は `Client.Ui` を参照せず、モーダルの要求などはインターフェース越しにする。DI は既存の VContainer、イベントは UniRx
- **ディレクトリ**: 基盤 `Client.Ui/Foundation/{Root,Layers,Routing,Input,Motion,Tooltip,DragDrop,Modal,Anchor,Text,Theme}`、部品 `Client.Ui/Components/<部品>`、画面 `Client.Ui/Screens/<機能>/`。AGENTS.md の上限（1 ディレクトリ 10 ファイル、1 ファイル 200 行）に合わせる
- **3 層**: View は VisualElement を組むだけで、MonoBehaviour に依存しない。Presenter はモデルを購読して View へ push し、操作を型付きコマンドへ渡す。Mount は `GameUiRoot` に載せる薄い層。テストでは `UITestFixture` の `rootVisualElement` に同じ View を組める（tests §5.3）
- **アセットのロード**: UXML と USS は機能の横に置き、1 か所の `UiAssetCatalog` を通してロードする。起動時に全部ロードし、欠けていたら起動の段階で止める（AGENTS.md「必須の値は最も早い段階で止める」）。L2 で参照パスの実在を検査する
- **実行時の形**: `GameUiRoot` は MainGameUI.prefab に事前配置し、シングルトンの規約どおり Awake で `_instance` を設定する。パネルは 1 枚で、Pick とフォーカスが 1 枚の中で閉じる

### 3.2 データ購読層

- **正本には手を入れない**: `Client.Game`・`Client.Skit`・`Client.Input`・`Client.Localization`・サーバー（host §7.5 R2）。サーバー状態の同期（イベントパケット＋初期データ＋購読）も変えない。UITK 化はクライアントの内側だけの話
- **topic を 2 つに分ける**: ①型付きのスナップショットと変化通知（`IReadOnlyReactiveProperty<XxxViewModel>`）を `Client.Game` に置く。②それを JSON にするアダプタ（Web が使う）。分けるのは、その段で使う topic だけ。撤去の 5c で②を消す
- **フレーム末の畳み込みと間引き**: フレーム末に 1 回へ畳む処理（PostLateUpdate）は残す（中間状態を描かないため）。WS の帯域のための間引き（`ui.progress` の 100ms と歯車の `IntervalFrame(4)`）はアダプタ側へ移し、UITK では毎フレーム読む。`BlockInventoryTopic` はコメント（4tick）と実装（`IntervalFrame(4)`）が食い違っているので、移植するときに意図を確かめる
- **ViewModel**: 不変の sealed class にする。判別 union は抽象基底と sealed 派生で表す。zod の不変条件をこの型へ移す（FluidSlot、変換機の詳細、Tooltip、Hotbar の 9 枠、PlacementMode）
- **コマンド**: action ごとに型付きメソッドにし、`CommandResult`（Accepted か、Rejected と理由の enum）を返す。前提条件の検証（state の一致・解放判定・遷移許可表）はコマンド側に置き、共存中も両系が同じ検証を通る。拒否の理由は開発者ログへ出す（AGENTS.md の fail-closed）。良性の拒否（Web の `BENIGN_ERRORS`）は理由の性質として enum に持ち、トーストを出すかどうかをそれで決める
- **Presenter の規約**: `Update()` で毎フレームの同値判定をしない。画面は起動時に 1 回だけマウントし、表示・非表示はルータが切り替える。非表示の間は描画を止め、表示するときに最新値で 1 回描く

### 3.3 層・所有表・共存の規則

**層の並び**
- ルート直下に、層 enum の固定順で兄弟コンテナを並べる（USS に z-index は無い）。順序の正本は `web/app/tokens.css` の `--z-*` と `web/app/zLayerTokens.test.ts` で、C# の enum と不変条件テストへ写す
- 大筋の順（細部は写すときに確かめる）: backdrop → 背面（通知）→ stage（画面・stage 内のオーバーレイ）→ viewport の HUD → 掴み → モーダル → ツールチップ → トースト → チュートリアル → 全画面ゲート
- 層コンテナと backdrop は `PickingMode.Ignore` を既定にし、L1 で固定する。忘れると「パネル外の右短押し」（ADR 0046）とワールドのクリックが効かなくなる（input §3.1）
- stage 族・viewport 族・背面 viewport 族の三分法（ADR 0013・0017）はそのまま表す

**共存の規則**

| # | 規則 | 守り方 |
|---|---|---|
| R1 | 同じ画面状態で、同じ位置に両系の操作できる要素を置かない。UITK が主パネル（幕を張る画面）かモーダルを出しているあいだは、Web を全面的にオフにする（2026-10-26 裁定） | CEF の RawImage を隠し、CEF への入力の転送を止め、Unity 側は Web の pointerOverUi と textInputFocused を読まない。閉じたら Web の古い値を捨ててから描画と転送を戻す。Web 側の `pointer-events: none` には頼らない（S15 で pointerOverUi が true のまま固まった。PR #1558）。CEF は RawImage 全面で入力を受け、領域単位で止める API が無いため（host §1）。オフにする範囲は §7.2 |
| R2 | 所有表は「(画面状態, 面) → Web か UITK」の静的なコード表。どの組も所有者はちょうど 1 つ | 所有表を (画面状態, 面) → 所有者 の辞書にして型で担保する。Web へは `ui_state.current` に委譲の印を足して伝え、Web の `screenForUiState` は委譲画面で主パネルを描かない |
| R3 | 面を移すときは「切替 PR → 1 周期 → 削除 PR」の 2 本にする。削除 PR で、Web の該当コンポーネント・document 全域のキー処理・テストを全部消す | 1 周期 ＝ 切替を含むビルドの playtest 反映から 7 日以上、かつその間の Discord と受け口の報告を 1 回見た後。前例は ADR 0052 の抽出 PR・削除 PR の型。延ばすならユーザーの裁定を取る。戻すのは切替 PR の 1 行で、戻しの PR は当日中に通常の配布工程で反映を指示する（Mac 確認は `--skip-mac-check` 可） |
| R4 | 暗転中の束の所有者は Web のまま。Editor 限定の上書きでだけ UITK にして検証する。配布物に切替フラグを入れない | 上書きの API は `#if UNITY_EDITOR` の中でファイル末尾に置く。外から参照すると、日次ビルドがプレイヤービルドで落として知らせる |

### 3.4 部品と作る段階

部品は、その段で使うものだけを作る。例外は、全画面が使う基盤（段階 1）。各部品は、作る段で装飾を含めた完成形にする。磨き込みの段は設けない（後回しの負債を作らないため）。

| 段階 | 部品 |
|---|---|
| 1 | 層マネージャ、ルータ、所有表、GamePanel（default）、PanelActionButton、横の ModeSwitch、IconButton（× は Painter2D）、スクロール領域、TextField（単行・複数行）、トースト、LocalizedLabel、KeyCap、UiMotion |
| 2 | CursorTooltip、FadeRule、GaugeBar、斜線帯、GamePanel（hud・skit）、通知の行、チュートリアルの枠とラベル、ワールドピン、スキットの選択肢の板とツールバーのアイコン |
| 3 | press ジェスチャ、D&D 層、HoverTooltip、SlotFrame・ItemSlot・SlotGrid・PlacementTargetFace、縦の ModeSwitch、スクロールスパイ、モーダル層 |
| 4 | 掴み層、分割ドラッグ、GamePanel（craft）、RecipeRow、ProgressArrow、BarSeries、FluidSlot、SectionStackView、TreeView、四隅のブラケット、コンテナクエリの代わり |
| 5 | 全画面ゲート |

**GamePanel の描き方**（PoC で実証した部分と、未実証の部分がある。推測を含む）: 面のグラデと 4 辺のフェードは `generateVisualContent` の頂点カラーのメッシュで描く。inset の 2 層は border の入れ子にする。右下のグリップと下向きの三角は Painter2D で描く。

### 3.5 テーマ USS とトークン

- **ファイル**: `Tokens.uss`（`tokens.css` 192 個、`skitTokens.css` 38 個、`slotTokens.css` 8 個、合わせて約 238 個を `:root` へ）。`Theme.uss`（ランタイムの既定コントロールを暗色にする。スライダーの入力部の高さが 0 になる癖への対処も含める）。`Typography.uss` と、部品ごとの USS
- **トークンの正本は USS**（§7.2）。C# が値を要るとき（Painter2D・配置の計算）は、`CustomStyleProperty` で解決後の値を読む。トークンを C# の定数に二重に持たない
- **CSS からの変換規則**（screens §5）

  | CSS の側 | UITK での扱い |
  |---|---|
  | rem・em | px にする（1rem=16px） |
  | `calc()` 70 か所 | 定数だけの式は計算済みのリテラルにし、導出式をコメントに残して L2 で検算する。ビューポートや要素の寸法・割合に依存する式（`App.module.css:62-65` の viewport 層の位置など。倍率と寸法は `App.tsx:39-47` が実行時に更新する）は、USS の配置指定（% や中央寄せ）か C# の幾何計算に置き換え、§4.6 の解像度確認で見る |
  | `color-mix`・`rgb(from …)` | 事前に計算する |
  | data 属性 40 か所 | `--modifier` クラスと `EnableInClassList` |
  | z-index 44 か所 | 層 enum |
  | `:first-child`・`:nth-child` など | C# でクラスを付ける |
  | `::before` | 子要素 |
  | grid・gap | flex-wrap と固定幅・margin |
  | コンテナクエリ | `GeometryChangedEvent` |

- **半透明（67 か所）は Web の値を流用しない**（PoC の実測: 同じ指定が UITK では `#664f29`、Chrome では `#3e311a`）。トークン表に「Web の値」「UITK の値」「決め方」の 3 列を持ち、UITK の値はその部品を作る段で実機で決める（§7.2）
- **L2 で落とす規約**: 色は `Tokens.uss` の外では `var()` だけ。構造擬似クラスと z-index は禁止。UXML の `text=` にリテラルを書かない。`name` の重複は禁止。ビットマップの参照は禁止（§7.2）。合成の太字（`-unity-font-style: bold`）は禁止

### 3.6 アニメーション

- **入退場とホバーは USS の transition** で書く。追加した直後にクラスを外すと補間されないので、`GeometryChangedEvent` を契機にする。`height: auto` は補間できないので、px に固定してから動かす。並べ替えの FLIP は `GeometryChangedEvent` の新旧の矩形で書く（`schedule.Execute` はレイアウトより先に走る。PoC の罠）
- **ループ・キーフレーム・smooth scroll・慣性は、自作の `UiMotion` で書く**（100〜150 行の見込み。段階 1 で作る。§7.2）
  - 使い手: 通知の出入り、チュートリアルの脈動、ドラッグガイドの往復、ビルドメニューの smooth scroll、研究ツリーの慣性
  - 持たせる API: 時計の注入、頭からの再生し直し（通知の集約で寿命を張り直す）、予約した退場の取り消し、完了の通知。要素がパネルから外れたら止まる
- **時間源は実時間**（パネルのスケジューラ、timeScale に依らない。§0 の確認）。テストでは `FrameUpdateMs` で進める
- **尺と曲線はトークンに置き、既存の値を参照する**: 通知の生存は `NOTIFICATION_DISPLAY_MS`（現在 7000ms）、入場 160ms・退場 200ms（ADR 0016）、脈動は 1200ms（ADR 0039）
- ref-webui-design §6 を継ぐ。テストのために尺を 0 にする抜け道は作らない。時計を進めるのは、尺を 0 にすることではない

### 3.7 ツールチップ・D&D・掴み・モーダル

- **CursorTooltip**: `MouseCursorTooltipState` を購読し、同じフレームのポインタから位置を決める。画面端では反転させる。カーソルがロックされている間は画面中央を基準にする（ロック中の UITK のポインタは画面外になるため。input §3.2）。書式は ADR 0019 の値（18px、padding 6/10、max-width 320）をトークンで持つ
- **HoverTooltip**: 祖先がスクロールしたら引っ込める。ドラッグの捕捉中は出さない（PoC の罠: 捕捉中も下の要素に PointerEnter が届く）
- **press ジェスチャ**: tap・drag・abort の 1 つの状態機械にし、`CapturePointer` を使う。D&D・パン・ドラッグスクロール・長押しで共通にする
- **D&D**: 自作する（PoC で約 140 行）。落とし先は、`panel.Pick` の結果の祖先から印のクラスを持つ要素を探して決める。ゴーストは掴み層に出す
- **掴み絵**は掴み層に 1 つだけ置き、同じフレームの `PointerMoveEvent` で動かす。今は CEF の mousemove 頼みで、カーソルのワープ後に追従しない経路があった（ADR 0020・0074）
- **モーダル**: 同時に 1 件だけ出す。幕を張り、フォーカスを中に閉じ込める。ESC は UIState が持ち、入力欄には自動でフォーカスする
- **右クリックメニュー**が要れば、`GenericDropdownMenu` を暗色のテーマで使う（PoC）

### 3.8 入力とフォーカス

入力の裁きは `UiPointerHitTest` と `UiInputExclusivity` の 2 か所だけで行う。共存中、主パネルが Web か無い状態では、どちらも UITK と Web の論理和にする（input §3.7）。UITK が主パネルかモーダルを出しているあいだは Web の値を読まず、閉じたときに捨てる（R1）。最後の WS 受信から閾値を超えたら、Web 側の値は無視する（§3.11）。

| 項目 | 方針 | 根拠 |
|---|---|---|
| UI 用のアクション資産 | moorestech 専用の資産を作る。Point・Click・RightClick・MiddleClick・ScrollWheel は残し、Navigate は空、Submit と Cancel は使わない | `DefaultInputActions` は Navigate に WASD を含み、メニュー中も WASD で歩く仕様（`.decisions/2026-10-01-メニュー中の視点は回さず移動だけ解放する.md`）とぶつかる |
| フォーカス | 入力欄以外は `focusable=false`。Tab の既定のフォーカス移動はアクション資産では止められない（Unity の仕様で、Tab は入力モジュールを通らない）ので、ルートで打ち消す（Web の `activeLayer.ts:36` と同じ）。Space・Enter でボタンが押されず、Tab でフォーカスが動かないことを L1 で固定する | `.decisions/2026-10-01-Web UI全体で文字入力欄以外のSpace既定動作を封じる.md`、`…クラフトボタンはSpaceに反応させずジャンプ専用にする.md` |
| ポインタ | `UiPointerHitTest.IsPointerOverAnyUi` を次の形にする。ロック中は false。そうでなければ「UITK の Pick が層コンテナ以外を返す」か「Web の pointerOverUi（共存中で、Web が全面オフでないときだけ）」か「EventSystem（デバッグ用の uGUI）」 | input §3.3 の案 A。呼び出し側の 23 ファイル・28 か所は変えない（実測）。EventSystem 判定は ADR 0052 の agent 前提 1 |
| ホイール | ホイールを通す印を USS のクラス（`wheel-passthrough`）にし、Pick の結果の祖先で判定する | 装備の循環は段階 4 で C# の 1 か所へ移す（UI の上なら止める、印のある HUD の上は通す、建築ツールが占有中なら止める、1 枠以下なら何もしない） |
| 文字入力 | ルートで `FocusInEvent`・`FocusOutEvent` を TrickleDown で受ける。フォーカス中の要素が TextField かを、`UiInputExclusivity` の ReactiveProperty へ同期で書く | 同じスレッド・同じフレームで書くので、lock も `ObserveOnMainThread` も要らない。`.decisions/2026-10-01-文字入力欄にフォーカス中は移動を止めたままにする.md` をテストで守る |
| ESC | 「このフレームの ESC は UI が使った」というラッチを、`InputKey` の抑止判定に足す。入力欄での ESC はフォーカスを外すだけにする | 今は WS の遅れで偶然防げているだけ（`docs/webui/design/input-focus-exclusivity.md`） |
| キー処理の置き場 | 画面のキー処理（ESC・←→・Enter でのクラフトとスキット送り）は、C# の UIState か画面の Presenter に集める | Web の document 全域のキー処理は削除 PR で消す（host §7.3） |
| IME | CEF が常時 `imeCompositionMode=On` にしている。共存中にこれが UITK の TextField と取り合うかは S9 で確かめる | input §1。CEF が消えればゲーム中の常時 ON も消える |
| ゲームパッド | UI のパッド操作は作らない | ADR 0082（sprint-toggle）の 7 項で「ゲームパッドは正式対応外」 |
| `activeInputHandler` | Both（2）のまま。InputSystem だけにするかは、完了の後に別に判断する | IME の実装次第（推測） |

### 3.9 ローカライズ

- **C# の `Localize` に一本化する。** 辞書の合成、解決順（対象言語 → english → source → `[!key]`）、型付きキーはすでにある（input §4.1）。HTTP の辞書取得・再試行・409 は消える
- **足すもの**: 名前付き補間（0-6）。`LocalizedLabel`（キーと引数を持ち、`Localize.OnLanguageChanged` で解決し直す）。表示用の数値書式ヘルパー（InvariantCulture を明示する。de 列があるので `ToString()` の「,」が実害になる。Web の `toFixed`〈12 か所〉は常に「.」）。kbd の差し込み（KeyCap 部品と文字列の分割。`LocalizedShortcutHint` 相当）
- **DTO は Guid とキーだけを運び、ビューが解決する。** ADR 0006 の原則を保ち、決定 5 の「Web 側で解決」を「ビュー側で解決」へ改める（§7.3）。スキットは今どおり、C# が解決した文字列を push する
- **静的検査**: UXML の `text=` に生の文字を書かない（`noJsxVisibleLiteral` の後継）。全画面の文言がキーを通る（`allScreensI18n` の後継）

### 3.10 フォント

- **TextCore の SDF FontAsset を Editor で作ってコミットする**（不可なら実行時に生成。§2.2）。既存の `Assets/Dependencies/Font/*SDF*.asset` は TMP_FontAsset で型が違う。`FontDefinition.FromFont` はビットマップで描くので、拡大すると崩れる（PoC）
- **フォールバック**: NotoSans-Regular → NotoSansJP-Medium → NotoSansKR-Medium の順。ロケールごとに、ルートのクラスで順を切り替える（`:root[data-locale]` の代わり）。韓国語は、Web では OS のフォントへ落ちていた（推測）。UITK は自動では落ちないので、明示する
- **最小の文字寸法**: 11〜12px の和文は Chrome より細い（PoC）。段階 1 の実機の比べ（S7）を見て、12px 以下を 13px へ上げる。アイコン上の個数は `-unity-text-outline` で縁取るので、別に扱う
- **文中アイコンの SpriteAsset** は内部 API に頼る（PoC）。構築と描画のテストを置き、Unity の版上げで最初に壊れる箇所として台帳に書く

### 3.11 CEF 停止の記録

AGENTS.md「失敗の扱い」に従い、「誰がどこで気づくか」を先に決める。

**今の問題**
- Web ホストの起動に失敗すると、LogWarning だけを出して続行する（`Scripts/Client.Starter/Initialization/Boot/WebUiStartup.cs:19,24`。コメント「UIはWeb一本なので起動失敗時は非表示で続ける」）。配布物では開発者に届かない
- Web の入力欄にフォーカスしたまま JS が止まり、WS の接続だけ残ると、切断時のリセット（`WebSocketHub.cs:153-159`）を通らない。移動と Esc（`CloseUI` は Keyboard 抑止の対象。`InputManager.cs:110`）が抑止され続け、命綱の入口が塞がれる

**やること**（実装は `Client.WebUiHost`、バグ報告から見るインターフェースは `Client.Game`。撤去の 5a で消す）
1. バグ報告の manifest に生の値を 3 つ入れる: Web ホスト起動の成否、CEF プロセスの生存、最後の WS 受信からの秒数。ADR 0057 の受け口から Mac mini に届く。bug-report-auto-fix の読み手も同じ PR で対応する
2. `WebUiStartup.cs` の LogWarning を LogError にする
3. 最後の WS 受信から閾値（`webSocketClient.ts` の 15000ms を参照）を超えたら、入力排他の Web 側の値（文字入力フォーカス・pointerOverUi）を無視し、UITK 側の値だけを使う。L0 のテストで確かめる

ホストは今 pong を返すだけで最後の受信時刻を持たない（`WebSocketMessageDispatcher.cs:54-55`）ので、それを足す。停止の判定、テスターへの知らせ、CEF の絵を隠す処置、自動再起動は作らない（裁定 2。残る負債は §6）。

**原因を CEF と決めつけない**: UI の全停止がすべて CEF 由来とは限らない。2026-10-14 の「破壊モードで橋脚にカーソル合わせるだけで…UI操作がたぶんできなくなる」は、cef-unity が 2 種とも生きていた（実測。Discord ミラー）。manifest に判定ではなく生の値を入れるのはこのため。

### 3.12 AI 向けの作法

- UXML と USS はテキストで持ち、UI Builder にしか無い状態を作らない。インラインの style は、計算で決まる幾何（位置・寸法）にだけ使う
- 見た目の調整は USS を中心に回す。USS と UXML の変更は、コンパイルもドメインリロードも起こさない。ビュー側の C# は薄く保ち、判断は Presenter とモデルに置く
- 名前: testid は `name` に同じ綴りで付ける（シナリオの testid を変えずに済む）。アンカーは USS クラスで付ける（`name` とは別の契約。`docs/webui/anchor-convention.md` の「testid とアンカーは別」を継ぐ）。状態はクラスで表す（`--selected` など）
- 見た目の自己確認は、ScreenCapture 系か window モードの撮影で行う。**`uloop screenshot` の rendering モードには UITK が写らない**（PoC の実測）。撮影にはフレーム番号か時刻の透かしを入れ、古いフレームを見ないようにする
- 規約違反は L2 で止める。moores-code-review に UITK の観点を足す（PickingMode の既定、所有表、トークンの直書き、`Func<>`・partial の禁止）
- `ref-uitk-design`（段階 1 で初版）: `ref-webui-design` の原則を写す（ホワイトリスト、面は GamePanel だけ、スロットは共有部品だけ、装飾は 6 語彙、色はトークンだけ、族と層）。§0 と §10（mock-host・Vite・Playwright・目視 QA）は、EditMode での組み立てと ScreenCapture に置き換える
- AGENTS.md の規約を守る: 200 行以下、1 ディレクトリ 10 ファイル、`#region Internal`、partial と `Func<>` の禁止、イベントは UniRx、日英 2 行のコメント

### 3.13 並列実行の設計

**作業の切り方**
1. **1 単位 = 1 PR。** 単位は、ファイルの所有が重ならないように切る。新しく作るディレクトリの中で閉じるのが基本。
2. **共有ファイルは integrator が直列で所有する**: 層 enum、ルータと所有表、composition root（DI の Installer の登録）、`Tokens.uss`・`Theme.uss`、`AnchorIds`、DSL のロケータ本体、`Localization/localization.csv`、台帳、prefab とシーン（uloop で 1 本ずつ）。ルータ登録は integrator が切替 PR で行う。
3. **部品の昇格**: 1 機能だけが使う部品は `Screens/<機能>/` に置く。2 機能目で使われたら、integrator が波の境で `Components/` へ移す。
4. **純ロジックの移植は別レーン**: vitest を仕様にして、C# の NUnit へ 1 対 1 で移す（約 4,000 行、L0 約 290 件）。レーンの先頭で、Unity の外で `dotnet test` が回るかを確かめる（S12。前例 `mooresmaster.Tests`）。回らなければ Editor のレーンだけで回す。AGENTS.md のコンパイル必須は、PR の前の `uloop compile` で守る。
5. **挙動を変えるのは grill を通したものだけ**: 移植はテスト名を写したパリティが基本。外れるもの（Mantine の素の見た目の様式化など）は §0 の裁定に従う。各段階の着手前に、moores-writing-plans で詳細計画を切る。

**Editor の予算と役割**

| 項目 | 中身 |
|---|---|
| Mac mini | 実装に使える Editor ＝ 3 − apply 稼働数 − 配布ビルド中の 1。3 は `CLAUDE.local.macmini.md` の目安。pr-apply は apply のたびに Editor を起動し、配布ビルドは同じ機械で batchmode の Unity を回す |
| macbook-pro | 起きているときだけ、1 本を足せる |
| CI | L1 を client shard に載せる（S13） |
| 指揮 | 波を起動し、完了を確かめ、台帳を更新する |
| integrator | 共有ファイルの所有、部品の昇格、ルータ登録と所有表の切替 PR |
| 単位エージェント | 実装を担う（opus）。worktree は `moores-wt new` で切り、Editor を起動する前に `moores-wt status` で本数を確かめる。枠が無ければ、Editor の要らない作業（L0・テスト名の写し・UXML の下書き）を先に進める |

**レビューとマージ**
- どの PR も moores-code-review を通してから出す。pr-independent-review は、切替 PR と段階 1 の基盤 PR（共有ファイルを持つもの）だけに付ける。暗転の単位 PR は moores-code-review まで
- moorestech は PR の作成で止まり、マージはユーザーが行う。律速はマージではなく、独立レビューの裁定

---

## 4. 検証

### 4.1 テストの移し方（仕様の正本）

- Web のテスト名を、そのまま NUnit のテスト名に写す。Playwright と vitest のテスト名はほぼすべて日本語の挙動文になっている（tests §3）
- 削除 PR で、消えた Web テスト名と増えた NUnit テスト名をスクリプトで突き合わせる。差分は `docs/uitk/disposition.md` 1 枚に理由付きで書く（目視で済ませるものは証跡のパス、捨てるものは理由）。見た目の意図は参照キャプチャ（0-8）で辿る
- 前例: uGUI から Web へ移ったときも、画面ごとの操作パリティを最終検証にし（`docs/webui/plans/phase-d-cutover.md:33-37`）、捨てるものを `docs/webui/disposition.md` に書いた

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
| L0 純ロジック | スロットの計画、長押しクラフト、ビルドメニューの分類とスクロール追従、ビューポートの計算、ルーティング表、所有表、通知のストア、ビューモデル、コマンド、無応答時の入力排他 | NUnit（`dotnet test` が回れば Unity の外でも） | 不要 |
| L1 ビュー | UXML・USS・Presenter を偽モデルと記録用コマンドでつなぎ、操作をシミュレートして、発行コマンド・クラス・テキスト・レイアウトを照合する | UI Test Framework の `UITestFixture` と `PanelSimulator` | 不要 |
| L2 静的規約 | USS と UXML をテキストとして読む（§3.5、§3.12） | NUnit | 不要 |
| L3 結合 | ゲームを起動し、UI 状態の遷移、パネルの表示、コマンドのサーバー到達を確かめる。CEF 無しでの起動もここで見る | EditModeInPlayingTest（数は絞る） | 要 |
| L4 録画 | 実際の入力経路と見た目 | プレイテスト DSL | 要 |

Playwright は今 CI で走っていない（型検査だけ。`run_test.yml`）。L1 を CI に載せるので、移行後のほうが CI での検証面は広くなる。

### 4.3 EditMode の UI テスト（L1）

- **導入**: UI Test Framework 1.0.0（`com.unity.ui.test-framework`）は Unity 6000.3.8f1 に組み込まれているが、manifest には無い（実測）。段階 0 で manifest に足し、`Client.Tests.asmdef` から `Unity.UI.TestFramework.Runtime` と `.Editor` を参照する
- **本番に揃える**: `panelSize` は 1280×720、`themeStyleSheet` は本番の暗色テーマ、フォントは本番の FontAsset。文字の幅で決まるレイアウトがずれないようにするため
- **mock-host の代わり**: 画面ごとに `FakeXxxModel`（ReactiveProperty で組んだフィクスチャ）と `RecordingXxxCommands`（発行の記録）を用意する。Playwright の `setUiState` は偽モデルに、`payloadsOf` は記録リストになる
- **時間**: パネルのスケジューラか、注入した時計で動かす。`MonoBehaviour.Update` と `Time.deltaTime` で動かすと、`FrameUpdateMs` で時間を進められない
- **描かれない部分**: L1 は描画しないので、半透明の濃さや文字のにじみは L4 か ScreenCapture で見る
- **罠**（段階 1 で確かめる）: `MouseMove(from, to)` は途中の要素に PointerEnter を届けない可能性がある。ScrollView の子は、スクロールしても `GeometryChangedEvent` が発火しない可能性がある

### 4.4 録画プレイテスト（L4）と CEF 無し起動ラダー

**DSL**
- `IUiElementLocator` を Web 版と UITK 版の 2 本で持つ。UITK を先に探す。同じ testid が両系で見つかったら、失敗させる（tests §6.4）
- UITK 版の手順
  1. ゲームのパネルから `Query(name: …)` で一致する要素を列挙する（`Q` は先頭の 1 件しか返さない）。0 件なら再試行し、2 件以上なら理由付きで失敗させる
  2. 自分と祖先の `resolvedStyle`（display・visibility・opacity）と、`worldBound` の面積で可視かを判定する
  3. `panel.Pick(中心)` が対象自身かその子孫かで、覆われていないかを判定する
  4. パネル座標を画面座標へ変換し、SemanticInput で注入する
- 文字は `SetText(testid, text)` で入れる（`TextField.value` を設定して ChangeEvent を流す。IME は通さない）
- UITK の画面では次のものが要らなくなる: WS の往復と「2 回続けて同じ矩形なら安定」の待ち、`CefInputForwarder`・`CefScreenMapper`・`PlaytestDomQuery`、`PlaytestUiOps.cs:134-138` の pointerOverUi の待ち合わせ

**CEF 無し起動ラダー**（各段階の物差し）
- Editor 限定のスイッチ（0-10）で、Web ホストを起動せずに PlayMode を始める
- 段ごとにシナリオを足し、前の段のシナリオも毎回流す（退行を検知するため）。撤去の後は、同じシナリオが CEF の存在しないビルドでそのまま通ること

| 段階 | シナリオ（仮称） | 内容 |
|---|---|---|
| 1 | `uitk-lifeline` | Esc → 設定で言語を切り替える → 日本語の本文でバグ報告を送る（本文は SetText）→ セーブして終了 |
| 2 | `uitk-opening-hud` | オープニングのスキットを選択肢まで進める → 歩いて採掘する（ゲージが出る）→ 数字キーで選んで設置する（ホットバーの表示は段階 3 から）→ 獲得通知と、目標 HUD を指すチュートリアル枠が出る |
| 3 | `uitk-build-bp` | B → 検索 → エントリをホットバーへドラッグ → 選んで設置 → BP をコピーして名前を付ける（検索語と BP 名は SetText） |
| 4 | `uitk-production-loop` | Tab → 長押しクラフト → 石窯に燃料と素材を入れる（掴み・Shift・右ドラッグ）→ 取り出す → 整理 |
| 4 | `uitk-research-train` | 研究を 1 つ完了 → 駅名を日本語で付ける（駅名は SetText）→ 時刻表を組んで自動運転を入れる |
| 5 | `uitk-challenge-gate` | チャレンジ一覧を開く → 出展モードの言語ゲートを通る |

本文と駅名は SetText で入れ、IME は §4.9 の人手で確かめる。

### 4.5 アンカーの付け替え

- **ID の文字列は変えない**: マスタに直書きされた ID は v8 で 9 件（実測: `challenge.current-hud` 1、`recipe.craft-button` 1、`research.node-<guid>` 7）。これに、`itemViewHighLight` から作る `recipe.item-<itemId>` が加わる。ID を保てば、マスタも `.decisions/2026-08-19-チュートリアルUI指定はWebアンカーIDを直書きし変換も検証も持たない.md` もそのまま成り立つ
- **語彙の正本は C# の `AnchorIds` 1 か所**（静的 19 個と動的 prefix 6 種）。`TutorialAnchorIdMapper` もここへまとめる。共存中は、Web の `anchorIds.ts` と突き合わせるテストで同期を守り、撤去で消す
- **付け方**: `TutorialAnchor.Mark(VisualElement, params string[] ids)` の 1 か所で、USS クラスとして付ける。`AttachToPanelEvent`・`DetachFromPanelEvent` で登録簿に出し入れする。Detach は同期で消すので、枠がアンカーより長生きしない
- **解決**: 0 件なら missing、2 件以上なら duplicate。ほかに非表示と画面外も判定し、開発者ログへ出す。同じアンカー ID が両系で同時に解決したら、二重描画として理由をログに出す（黙って先勝ちにしない）。位置は `GeometryChangedEvent` に加え、スクロールによる移動を拾うため、毎フレーム `worldBound` を見る
- **祖先のクリップ**（ADR 0024）: overflow を隠す祖先の `worldBound` との交差で求める。祖先の overflow を公開 API で読めなければ、クリップする容器に印のクラスを付ける（推測）
- **カバレッジテスト**: 全静的 ID と全 prefix が、どこかの画面の L1 マウントで登録されることを確かめる

### 4.6 見た目の確かめ方

- 画素のパリティは目標にしない。前回は uGUI の正本が消えて追従できず打ち切った（`.decisions/2026-08-17-uGUI移植を終了しWeb-UIは独自調整へ移る.md`）。今回は参照キャプチャ（0-8）を残して意図を辿る
- 一致させるもの: 機能、トークン（色・寸法・層）、移したテスト。比べ方: 参照キャプチャと、UITK の ScreenCapture（透かし入り）を並べる
- 撮る解像度: 720p と 21:9 の 2 種を、GamePanel の初出（段階 1）で 1 回
- 波ごとに画面の写真を外部監査（codex-audit、画像付き）にかける。ユーザーの目視は段階ごとに 1 回（§2.0）

### 4.7 決定論ゲート

| # | ゲート | 中身 | 入れる段階 |
|---|---|---|---|
| G1 | Web 追従 | (1) 束ごとに暗転の起点コミットを台帳に書く。切替 PR で、起点以降にその束の Web パスを触ったコミットが、台帳で全部「写した」か「捨てた（理由）」になっていなければ落とす。(2) 切替済みの束の Web パスへの変更を落とす（描かれないコードへの変更は失われるため。直すなら UITK 側） | 0（最初の PR） |
| G2 | uGUI 追加の禁止 | `WebUiGateAuditTest` を残す。撤去の 5e で名前を変え、対象を付け替える（`.decisions/2026-09-08-uGUI追加禁止はトークン検査と分類粒度の両方で守る.md` の、両方で守る形を保つ） | 既存 |
| G3 | USS・UXML の規約 | §3.5 と §3.12 | 1 |
| G4 | 撤去後 | `WebUi`・`Cef`・`webui` のトークンが、許可リストの外で 0 | 5（撤去） |

### 4.8 実測（段階 0）

段階 0 で行うのは、段階 1 の前提になる 5 本。1 本 1 日で打ち切り、証拠付きで台帳に書く。PR #1558 の報告では S1・S2・S3 が合格、S15 が不可（下の表）。S9 は人手の確認待ち（bd `moorestech-albof.8`）。

| # | 確かめること | 不可だったときの手 |
|---|---|---|
| S1 | UITK パネルと CEF の Canvas の前後を、PanelSettings の sortingOrder で決められるか（CEF の Canvas は Overlay・sortingOrder 0。実測）。背面パネルを足せるか | CEF の Canvas の sortingOrder を動かす。それでも駄目なら、委譲中に Web を空にする範囲を広げる |
| S2 | Unity Recorder（`PlaytestRecorder.cs:33` の GameViewInputSettings）、`PlaytestScreenshot`、バグ報告の `ScreenFrameReader`（`CaptureScreenshotIntoRenderTexture`）に、UITK が写るか | 撮影経路を ScreenCapture の連番へ直すまで、段階 1 に入らない（**赤信号**。§6） |
| S3 | `QueueStateEvent` で注入した入力が、`InputSystemUIInputModule` を経て UITK に届くか。パネル座標と画面座標の往復。`IsPointerOverGameObject` が UITK を拾うか | 入力資産・EventSystem・注入経路を直す。UITK のイベントの直接合成は L1 に限り、L4 の代わりにしない（L4 は実際の入力経路を確かめる層のため） |
| S9 | **日本語 IME**（Windows 検証機の配布ビルドと Mac）。単行・複数行で、変換・確定・取消・ESC を試す。フォーカスを往復した後に入力が戻るか。CEF を起動した状態と、CEF を初期化しない状態の 2 条件で同じ手順を行う。後者は S9 用の PoC ビルドを、CEF の起動を外してもう 1 本焼く（使い捨て。Editor 限定スイッチは配布ビルドで効かず、配布物に切替フラグも入れないため）。CEF の常時 `imeCompositionMode=On` が結果を変えるため | **赤信号**（§6） |
| S15 | 委譲中の Web を `pointer-events: none` にしたとき、CEF 経由の pointerOverUi が false になり、Web が action を出さないか。**不可**: action は止まったが、pointerOverUi が true のまま固まった（PR #1558） | 委譲中の状態では、Web の HUD も描かない（ポーズ中に HUD が消える）。2026-10-26 の裁定で、UITK が主パネルかモーダルを出しているあいだは Web を全面的にオフにする（R1） |

- S13（UI Test Framework が CI の batchmode で動くか）は、段階 1 の最初の L1 テスト（本番テーマの要素で schedule と transition を進める）を既存 CI に載せて確かめる。動かなければ §6 の 3
- ほかは作業の中で確かめる: S4・S5（入力）、S7（半透明と小さい文字）、S10（フォント）は段階 1 の各レーンの先頭（§2.2）。S12（`dotnet test`）は L0 レーンの先頭（§3.13）。S14（`HttpListener`）は 0-3 の完了条件

### 4.9 IME と実機

- IME は自動操作では確かめられない。人が Mac と Windows 検証機の配布ビルドで確かめる。回数は 3 回に絞る
  - 段階 0: S9（2 条件）。オーナーの週次の反映作業と同じ日に置き、PoC ビルドはその日の反映の後に staging へ上げる
  - 段階 1: バグ報告（本番で最初の入力欄。複数行）
  - 段階 5: 撤去の後、CEF を含まない配布ビルドで入力欄を一巡する
- 段階 3・4 の入力欄（検索・BP 名・駅名）は同じ TextField 部品を使うので、部品を変えない限り L1 と録画で済ませる。部品を変えたら、その段で 1 回足す
- 実機で見る項目: UI スケールと DPI、Steam オーバーレイ、言語 4 種（数値の書式とフォールバックを含む）、Ctrl+U・カットシーン・出展モード

---

## 5. 完了の定義

### 5.1 完了の定義（観測できる形）

| # | 条件 | 観測のしかた |
|---|---|---|
| 1 | manifest・prefab・配布ビルドに CEF が無い | manifest と lock に `jp.juha.cefunity` が無い。MainGameUI.prefab に CEF の Canvas・RawImage が無い。配布ビルドの Windows `Plugins/x86_64` と Mac `PlugIns/cef-unity-server.app` に CEF のファイルが 0（`release-artifact.sh` で検査） |
| 2 | G4 が緑 | `Client.WebUiHost`・`moorestech_web/`・Web 用の Kestrel が無いことを含む |
| 3 | 遠隔実行が検証機で動く | Windows 検証機で、ADR 0072 の完了条件 3 点（有効時に結果が返る、Harmony で差し込める、オプション無しでは開かない）を確かめる |
| 4 | 全録画とラダーが、CEF の存在しないビルドで緑 | ラダー 6 本と全録画。壊れている 10 本は直すか削除して bd `moorestech-wwxy` を閉じる |
| 5 | 撤去ビルドが playtest で 1 周期通る | 1 周期は R3 の定義。UI が原因で進行を阻む不具合が 0 か、全件修正済み |

文書・スキル・ADR・bd の後片付けは、撤去の 5e の作業項目に置く。

### 5.2 完了の定義に入れないもの（別に判断する）

- `com.unity.ugui` の撤去（ADR 0052 の例外 2〜4〈タイトルとロード、HP バー、デバッグ UI〉が残る）
- `activeInputHandler` を InputSystem だけにすること
- Mac の Universal 化と Linux クライアントの再開。CEF を消しても、ffmpeg も arm64 版だけを同梱しているので、arm64 固定は自動では解けない（ADR 0071）
- タイトルとロード画面の UITK 化（`.decisions/2026-09-05-メインメニューとロード画面はuGUI現状維持.md`）

---

## 6. リスク

| # | リスク | 兆候・検知 | 潰し方 | 赤信号か |
|---|---|---|---|---|
| 1 | 日本語 IME が UITK の TextField で成立しない | S9 | ユーザーへすぐ上げる。2 条件（CEF あり・なし）の結果を切り分けてから、IME の composition を自前で扱う TextField を作るか、文字入力のある画面（バグ報告・検索・駅名・BP 名）だけ Web に残すかを決める。後者は完了を遅らせ、命綱の報告を日本語で書けない期間を作る | **赤** |
| 2 | 録画・バグ報告の動画に UITK が写らない | S2 | 撮影経路を直すまで段階 1 に入らない。写らないまま進むと、録画での検証とテスターの報告動画が全部欠ける | **赤** |
| 3 | UI Test Framework が CI の batchmode で動かない | 段階 1 の最初の L1 テスト（S13） | 原因を調べて直す。直せないと分かった時点で、L1 をローカルの Editor レーンへ移すかを裁定に上げ、並列度を下げた日程に組み直す。transition が進まなければ UiMotion の時計を差し込む | 中 |
| 4 | 並行セッションとの衝突（液体 UI が `BlockInventoryTopic` を触る等） | マージ時の衝突 | integrator が共有ファイルを直列で持つ。topic 群ごとに短い作業窓を bd で告知する | 中 |
| 5 | 独立レビューの裁定と Editor の本数が律速になる | 裁定待ちの PR 数 | 独立レビューは切替 PR と段階 1 の基盤 PR だけに付ける。Editor の要らない作業（L0・テスト名の写し・UXML の下書き）を並べる | 中 |
| 6 | 規模の見積もりが外れる | 各段階の plan | 段の切り方は変わらず、期間だけが伸びる。段階 4 の暗転期間が伸びたら、族ごとの切替へ変えるかを裁定に上げる | 中 |
| 7 | 移行中も Web が変わり続け、暗転中の束が追いつかない（裁定 4 の帰結） | G1 の未記帳件数。束ごとに台帳へ数を書く | 暗転の期間を短く保つ（束の中身をそろえてから起点を切る）。切替 PR の直前に差分を写す。追いつかない束は切替を延ばすかを裁定に上げる | 中 |

**負債が残るもの**: 段階 1〜5 の間、CEF が固まると最後のフレームが画面に残る（Discord 2026-10-03「インベントリ開いたままだったので閉じれなくなってました」の形）。UITK のポーズはその上に出るので、セーブと報告はできる。テスターへの知らせはパッチノートで代える。

---

## 7. 裁定と既定

### 7.1 着手前に聞くこと（段階 0 の前に 1 回で）

「推奨」は agent の案。議論の記録が未裁定とした 5 点のうち、出荷形・第一弾はここで聞き、仕様の正本・完了の定義・AI 向けの作法は §7.2 の既定にした。

| # | 論点 | 選択肢 | 推奨 | 理由と帰結 | 既存裁定との関係 |
|---|---|---|---|---|---|
| 裁定 1（旧 D1） | 移行中の出荷形 | (A) 束単位の strangler。束は暗転で作り、所有表の 1 行で切り替えて毎週出す。持ち物の束も束ごと一度に切り替える (B) UI スタックを丸ごと切り替える（起動オプション `--ui`。既定 Web で、最後に一括） (C) 移行ブランチに溜めて一括で入れる (D) (A) のうち持ち物の束だけ族ごとの小段階で出す | **A** | テスターの実害（CEF の停止とメモリの伸び）に、数週間で命綱が届く。配布環境の差を束ごとに浴びられ、どこで止めても出荷できる。持ち物の束を一度に切り替えるので、系をまたぐ掴みが起きず、InventoryPanel の二重実装も族ごとの所有表も要らない。帰結: 見た目の混在が届き、共存の仕組み（撤去で消す）を持つ。インベントリ系の CEF 不具合は段階 4 の終わりまで残る。(B) は配布物に切替が入り、テスターへの価値が最後まで 0。(C) は約 2 万行が配布環境に触れないまま最後に出る。(D) は早く届くが、途中で止めると InventoryPanel の二重実装が残る | 議論の記録の agent 案と同じ向き |
| 裁定 2（旧 D2） | 第一弾の画面 | (A) 命綱（ポーズ・設定・バグ報告・トースト） (B) 入力の無い HUD (C) ビルドメニューとホットバー (D) 議論の記録の「インベントリかホットバー」を単独で | **A**。CEF の延命は §3.11 の 3 つだけ（manifest の生の値・LogError・無応答時に入力排他の Web 側を無視）。自動再起動や raw input の手当ては作らない | 失うものが最も大きい口（報告とセーブ）を先に守る（§2.2）。IME・ESC・フォーカスを、掴み抜きで先に通せる。(B) は CEF が死んだときに救えるものが少ない（段階 2 で続けて出す）。(D) のホットバー単独は建築の束を割り、インベントリは最大の束に未熟な基盤で挑むことになる。延命は撤去する対象への投資になる | 議論の記録の候補（未裁定）とは異なる。延命しないのは `.decisions/2026-09-13-CEF raw input応急処置は入れず…` と一致 |
| 裁定 3（旧 D6） | 遠隔実行の待受 | (a) `HttpListener` へ載せ替え、AspNetCore 系の NuGet を外す (b) 遠隔実行専用の最小 Kestrel として残す (c) 専用の TCP 待受 | **(a)**。配布ビルドで動かなければ (b)（0-3） | NuGet と Kestrel の保守面積が大きく減り、Web 用の Kestrel を消せる。今は Web ホストの起動失敗で遠隔実行も開かない。帰結 (b): Kestrel と NuGet が遠隔実行のためだけに残る | **ADR 0072 と矛盾**（「入口は既存の Web UI サーバーに 1 本足す」、棄却案「専用の待ち受けを新設」）。どれを選んでも改訂が要る。トークン・起動オプション・結果の 7 種は変えない |
| 裁定 4（旧 D7。**2026-10-26 に (c) で裁定済み**。UITK が安定稼働するまで Web の変更を続ける。G1 は追従ゲートへ変えた） | Web の凍結と新機能の行き先 | (a) G1 で `web/features` の新規ファイルを止める。Web はテスターを止める不具合だけ直す。新しい UI は段階 1 の基盤ができたら UITK にだけ作る。未移行の画面に機能が要れば、その束を前倒しする (b) 全面凍結（不具合も直さない） (c) 従来どおり | **(a)** | (b) は CEF 由来の致命的な不具合も直せない。(c) は移す対象が増え続け、二重作業になる。10-15 以降の 44 コミットは master 済み。凍結時に web を触る open PR は #1348（web 2 ファイル）だけで、許可リストに入れるか先にマージする。G1 は段階 0 の最初の PR で入れる | `.decisions/2026-08-17-uGUI移植を終了しWeb-UIは独自調整へ移る.md`（Web 専任）は 10-18 の裁定で覆っている |
| 確認（旧 D18） | UI の演出の時間源 | (a) 実時間（パネルのスケジューラ） (b) GameUpdater のティック | **(a)** | ポーズ中もポーズ画面の演出は動くべき。AGENTS.md「経過時間は GameUpdater のティック加算だけ」は、サーバーのゲームロジックの規則と読む。この読み方の確認 | AGENTS.md の解釈 |
| 様式化（旧 D14） | Mantine の素の見た目の画面（トースト・チャレンジ・列車 HUD・ポーズのボタン） | (a) 移すときに、ホワイトリストの語彙で様式化する (b) 今の見た目を再現する | **(a)**。4 画面を同じ語彙で一度に決める | Mantine の既定は再現の正本にならない。負債 `moorestech-cgm` も閉じられる。語彙は ref-webui-design のホワイトリストで共通 | ADR 0014 の「他の Button は今回触らない」を、移行の範囲で解く |

**段階 0 の中で出た論点（2026-10-26 裁定済み）**

| 論点 | 裁定 | 計画への反映 | 記録 |
|---|---|---|---|
| 旧 UITK の SkitUI | 削除し、会話 UI の予備を持たない | 0-12 | `.decisions/2026-10-26-旧UITKのSkitUIは削除し会話UIの予備を持たない.md` |
| 差し込み記法（旧 D8） | 名前付きに統一し、`GetFormatted` も名前付きに改める。全言語一致テストは残す | 0-6・§7.3 | `.decisions/2026-10-26-翻訳文の差し込みは名前付きに統一しGetFormattedも名前付きに改める.md` |
| Codex で進められない実装 | 段階 0 の遠隔実行・状態の移設・DSL の残りは、Claude Code で進める | §7.3 | `.decisions/2026-10-26-UITK段階0でCodexで進められない実装はClaude-Codeで進める.md` |
| S15 の不可（委譲中の Web の入力） | UITK が主パネルを出しているあいだは、Web を全面的にオフにする。Web の HUD も消える | R1・§2.2・§3.8・§4.8・§7.2 | `.decisions/2026-10-26-UITKの主パネル表示中はWeb-UIを全面的にオフにする.md` |

### 7.2 agent の既定として進め、台帳に記録するもの（大きく外れたら裁定に上げる）

- 仕様の正本（旧 D3）: Web のテスト名を NUnit へ写し、削除 PR で突き合わせる（§4.1）。見た目の意図は参照キャプチャで辿る
- 完了の定義（旧 D4）: §5.1 の 5 項目。議論の記録の「Kestrel ホスト削除」は裁定 3 の結果に合わせる
- AI 向けの作法（旧 D5）: §3.12 を `ref-uitk-design`・L2・moores-code-review の UITK 観点で固める
- Web を全面オフにする範囲（S15 の裁定の読み方）: UITK が主パネル（Web の `SCREEN_TRAITS` で幕を張る画面。`web/shared/uiState/uiScreenRouting.ts`）かモーダルを出しているあいだだけ。幕の無い状態（GameScreen・PlaceBlock・DeleteBar・Story・TrainHUDScreen）では、HUD・通知・ツールチップ・ワールドピン・チュートリアル枠・トースト・スキットが UITK でも Web を消さない。段階 2 以降は常駐表示が UITK になり、表示のたびに消すと未移行の Web 画面とホットバーまで消え続けるため
- 半透明と小さい文字（旧 D10）: トークン表に UITK 列を持ち、部品を作る段で実機で決める。12px 以下は 13px へ上げる
- アニメーション基盤（旧 D11）: 自作の UiMotion（100〜150 行、時計を注入）を段階 1 で作る。要件が増えたら LitMotion v2.0.2
- 画像（旧 D12）: ビットマップは使わない。面と図形は Painter2D と頂点カラーのメッシュ。単純なアイコン（×・ピン・矢印）は Painter2D の命令で描き、既存の SVG を流用したいアイコン（スキットの 6 種など）は VectorImage を許す
- トークンの正本（旧 D13）: USS。C# は `customStyle` で読む
- パネルは 1 枚。背面パネルを足すのは S1 の結果次第。UITK は CEF の上に置く
- PanelSettings は Scale With Screen Size・1280×720・Expand・scale 1（倍率は `min(w/1280, h/720)` で、Web の `App.tsx:39` と同じ）
- 共存中、Web の全画面状態の間は UITK の通知層を隠す
- `name` は testid と同じ文字列にし、アンカーは USS クラスで付ける
- 入力欄での ESC は、フォーカスを外すだけ。入力欄以外は `focusable=false`。UI のパッド操作は作らない
- 閾値は既存の定数を参照する: 押下は `usePressGesture`（5px）、ツールチップは `OFFSET`（12）、通知は `NOTIFICATION_DISPLAY_MS`（7000ms）、無応答は `webSocketClient.ts`（15000ms）

### 7.3 改訂が要る既存の裁定・ADR

**矛盾するもの**

| 裁定・ADR | この計画での扱い | 時期 |
|---|---|---|
| ADR 0072（遠隔実行の入口は Web UI の Kestrel） | 待受を切り離す（裁定 3）。同じ PR で改訂する | 段階 0 |
| ADR 0006 決定 5（マスタ由来の表示名は Web の辞書で解決） | 「ビュー側（UITK）で解決」へ改める。DTO が Guid とキーだけを運ぶ原則は保つ | 段階 1 |
| 議論の記録「見た目は uloop screenshot で自己確認」「完了の定義に Kestrel 削除」 | ScreenCapture 系へ改める（rendering モードに UITK が写らない。PoC の実測）。Kestrel は裁定 3 の結果に合わせる | 段階 0 |
| `.decisions/2026-08-26-プレースホルダ規約は全言語一致テストで守る.md` | パリティテストは残す。「`GetFormatted` のシグネチャは変えない」は、0-6 の統一の時点で位置指定から名前付きへ改める（2026-10-26 裁定） | 段階 0 |
| `.decisions/2026-10-16-moores-writing-plansの実装引き継ぎはCodexセッションに固定する.md` | 段階 0 のうち Codex で進められない遠隔実行・状態の移設・DSL の残りは、例外として Claude Code で進める（2026-10-26 裁定） | 段階 0 |

**実装の節を書き換える ADR・裁定**（その段で触り、撤去の 5e で改訂か廃止をまとめて記録する）
- ADR 0013・0017（段階 2〜5 は通知を隠す一時的な逸脱）、0016、0019、0020・0074、0021、0024、0052、0057、0071
- `.decisions/` の `2026-08-18-チュートリアル提示はWebUI経路に統一しD&Dは矢印ループで示す.md`、`2026-08-19-獲得通知の集約はWebUI側で行う.md`、`2026-08-19-カーソルツールチップの書式はWeb側が持つ.md`、`2026-08-19-チュートリアルUI指定はWebアンカーIDを直書きし変換も検証も持たない.md`、`2026-08-20-チュートリアルハイライトのマスク方式とテスト方針.md`、`2026-08-23-操作ヒントの正はC#のUIStateに置きtopicで配る.md`、`2026-08-26-Web操作音はC#へsound.playを送って鳴らす.md`、`2026-10-01-*`（Space の封じ、メニュー中の移動、入力欄の移動停止、クラフトボタンと Space）、`2026-09-08-uGUI追加禁止はトークン検査と分類粒度の両方で守る.md`、`2026-09-05-uGUI撤去は抽出PRと削除PRの2本に分ける.md`、`2026-09-13-CEF raw input応急処置は入れず…`、`2026-09-09-WebSocketHubをレジストリの単一所有者にする.md`
- AGENTS.md「イベント発火に Action を使わない」の既存の違反は 0-5 で解消する

### 7.4 ユーザーにしてもらう作業

- **§0 の残りの裁定 3 件・確認 1 件・様式化**を、段階 0 の前に 1 回で答える（裁定 4 は 2026-10-26 に済み）
- **日本語 IME の手動確認**（Mac と Windows 検証機の配布ビルド。§4.9 の 3 回。段階 0 の S9 は bd `moorestech-albof.8`）
- **段階ごとの短い目視**（1 回ずつ。staging ビルドで、反映の前）
- **独立レビューの裁定**（切替 PR と段階 1 の基盤 PR だけ）
- **マージと playtest への反映**: 波ごとに PR をマージし、各段階の配布ビルドを playtest へ反映する。戻しの PR は当日中に反映を指示する
