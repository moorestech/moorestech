# UI Toolkit 全面移行 計画案（プレイヤー価値・テスター体験先行）

- 作成: 2026-10-23。読むだけで作った案で、repo・bd・Unity には何も触れていない。状態は「案」（ユーザー裁定の前）。
- 判定: 最初に届けるのは「CEF が落ちても Esc でセーブ・バグ報告ができる」命綱。次に「画面を開いていない時間（HUD・通知・チュートリアル・ストーリー）」を CEF から外す。その後は生産ループ → 建築 → 残り → 撤去 → 磨き込みの順に、CEF 無しで遊べる範囲を毎週の playtest で広げる。段階は 0〜7 の 8 つ。
- パス表記: 特に断りがなければ repo（moorestech リポジトリ）からの相対パス。`Scripts/` は `moorestech_client/Assets/Scripts/`、`web/` は `moorestech_web/webui/src/` を指す。「推測」と書いた所は実測していない。
- 入力（すべて読んだ）
  - 裁定 `.decisions/2026-10-18-CEFを撤去しUI-Toolkitへ全面移行する.md`
  - 議論の記録 `docs/webui/ui-toolkit-migration-discussion-2026-10-18.md`
  - PoC `docs/uitk-migration/poc/hard-cases/index.html`
  - 調査 5 本（`docs/uitk-migration/research/understand-{screens,host,input,tests,shipping}.md`。以下 screens・host・input・tests・shipping と略す）
  - 追加で読んだもの: ADR 0006・0013・0016・0017・0019・0021・0052・0071・0072、`.decisions/2026-09-05-メインメニューとロード画面はuGUI現状維持.md`、bd の CEF 関連 12 件（読み取りのみ）、Discord ミラー（`moores-discord search`。同期はせず、最終同期時点の内容）

---

## 1. 方針

### 1.1 要約

- **移し方は画面単位の strangler。** 移し終えた画面から毎週の playtest（`origin/master` を焼く。`.agents/skills/moores-playtest-release/SKILL.md`）へ出す。
- **順番は「その画面が CEF と一緒に死んだときにテスターが失うもの × 触る頻度」で決める。** CEF が死ぬと、バグ報告の口（ポーズメニュー）まで死ぬ。だから報告とセーブの口を最初に CEF から外す。
- **各段の完了は「CEF を起動しなくても X ができる」で測る**（§4.3「CEF 無し起動ラダー」）。テスター環境で CEF がどう壊れても、移した範囲は遊べることを毎段で示す。
- **見た目は「トークン（色・寸法・層）と機能が合う」まで。** 画素の一致と装飾の再現（GamePanel の 4 辺フェード、四隅ブラケット、drop-shadow など）は段階 7 に回す。

### 1.2 この方針を選ぶ理由（テスターに起きている実害）

| 実害 | 根拠 | 効く段 |
|---|---|---|
| CEF の helper（`cef-unity-rust-helper.exe`）のメモリが増え続け、14.6GB 付近で UI が固まった。Esc・R・機械 UI の開閉を連打するだけで 51MB → 170MB に伸びる | Discord #feedback「メモリの消費量」2026-10-01 https://discord.com/channels/888315641709330433/1554924554697187389 | 1〜3 で開閉の多い画面から外し、6 で消える |
| 6 時間放置したら cef-unity のプロセスが消え、Tab・Esc・T・R・機械 UI が一切開かなくなった。Esc が開かないのでゲーム内のバグ報告も送れない | Discord「6時間ほど放置していたら、cef-unity~2種のプロセスがいなくなっており…」2026-10-02 https://discord.com/channels/888315641709330433/1555576468828987532 | 1（命綱） |
| インベントリを開いたまま UI が固まり、閉じられなくなった | 同スレッド lanQube 2026-10-03 | 1（停止の検知と、凍った絵を隠す） |
| 破壊モードで UI が閉じられず、報告も送れなかった（開発者は CEF のクラッシュを疑った。のちの更新で解消。CEF が原因だったかは未確定＝推測） | Discord「〈バグ〉破壊モードで固まる」2026-10-12 https://discord.com/channels/888315641709330433/1559209564363161740 | 1 |
| Ctrl+ホイールで UI 全体が拡大縮小して揺れる（Chromium のページズームとみられる。推測） | Discord 2026-10-11 https://discord.com/channels/888315641709330433/1558846967193280602/1558854940980674623 | 移した画面から消える。Web 側で塞ぐかは D13 |
| Esc や R で UI を閉じるとき、反応しないことがある | Discord「メモリの消費量」2026-10-01 maitan | 1〜5（入力の判定が同じフレームの同期判定になる） |
| 展示機が熱いと CEF の FPS が極端に下がる | bd `moorestech-vfn3o.19`（gamescom の FB） | 2 でゲーム画面の CEF の描画を空にし、6 で消える |
| 健常時でも CEF の busy-wait が Unity のメインスレッドを毎フレーム平均 2〜3ms（最大 7.7ms）使う。外部負荷で約 1fps まで崩れる | `docs/webui/cef-unity-frame-rate-followup-2026-07-29.md` §1・§2.2 | 2（描くものが無ければ待機抑止に入る見込み。推測）、6 |
| D3D12 クラッシュの未確定の候補に、CEF の共有テクスチャと fence の同期不足がある | `docs/research/2026-10-01-d3d12-crash/cef.md`（原因は未確定） | 6 |
| Web ホストの起動に失敗すると、UI がゼロのままゲームが続き、ログは LogWarning だけ。配布物では開発者に届かない | `Scripts/Client.Starter/Initialization/Boot/WebUiStartup.cs`（コメント「UIはWeb一本なので起動失敗時は非表示で続ける」）、`Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs:48-55` | 0（状態の移設）、1（停止の知らせ） |
| 配布物に CEF が Windows で 237 ファイル約 368MB、Mac で 252 ファイル約 298MB 入る。Mac は arm64 専用 | shipping §1.1、ADR 0071 | 6 |
| 開発側: Steam 版と Editor の CEF キャッシュ衝突（Init failed -6）、worktree で CEF 未接続、EditMode テストが code -6 で落ちる | `docs/development/macmini/unity-editor.md`、bd `moorestech-1gc8`・`-3xq6z` | 6（開発が速くなり、修正がテスターに早く届く） |

まとめると、テスターが一番困っているのは「見た目」ではなく「UI がまるごと死ぬ・重くなる・報告もできない」こと。だから「環境差で壊れない」を先に届け、見た目は後に回す。

### 1.3 捨てるもの

- **一度に切り替える綺麗さ。** 共存期は「所有表」「二重のロケータ」「二系統の入力の裁き」「UI 停止の検知」を持つ。どれも段階 6 で消す（§5.3 で完了の定義に入れる）。
- **移行中の見た目の統一。** 毎週のビルドで、Web 製と UITK 製の画面が混ざる。半透明の濃さ（Linear 合成）と小さい文字の太さが画面ごとに違って見える。
- **段階 1〜6 での装飾の再現。** GamePanel の 4 辺フェード・右下グリップ・下向き三角、レシピの四隅ブラケット、段付き罫線、drop-shadow、App のヴィネット、Mantine の素の見た目の画面（toast・challenge・trainHud）の様式化は段階 7。
- **Web 側の新機能。** 移行期間中、新しい UI は UITK にだけ作る。Web はテスターを止める不具合だけ直す（D12）。
- **CEF の修理と延命。** 7 機構の修理、raw input、プロセスの自動再起動は作らない。入れるのは「停止の検知と知らせ」だけ（D13）。
- **議論記録の「第一弾はインベントリかホットバー」案。** 単独では束を割り（screens §7.1）、テスターに届くのが数か月後になるため、この計画ではポーズ一式を第一弾にする（D2）。
- **Editor 無しで回る開発ループ（vitest・Playwright）。** 裁定どおり。画面を移すたびに、その画面の分を消す。

### 1.4 段階の一覧

| 段 | 名前 | テスターに届くもの | 完了時に CEF 無しで動く範囲 | 出荷 | 規模（推測） |
|---|---|---|---|---|---|
| 0 | 地ならしと実測 | 配布物が少し軽くなる（OneJS の除去）。見た目の変化なし | なし。ただし Web ホストの起動に失敗しても研究・チャレンジなどの状態が生きる | 可 | 小〜中・PR 5〜6 本 |
| 1 | 命綱 | Esc 以下（ポーズ・セーブ・セーブして終了・設定・バグ報告）が CEF に依存しない。UI が止まったら画面に知らせる | ポーズ・セーブ・設定・バグ報告 | 可 | 中・PR 6〜8 本 |
| 2 | 画面を開いていない時間 | HUD・通知・ツールチップ・チュートリアル枠・ワールドピン・スキットが CEF に依存しない。ゲーム画面で CEF が何も描かない | ＋オープニングのスキットを抜け、歩いて採掘・設置し、通知とチュートリアルを受け取れる | 可 | 大・PR 8〜10 本 |
| 3 | 生産ループ | インベントリ・手クラフト・機械やチェストの UI | ＋手でクラフトし、機械に入れて取り出せる | 可 | 最大・PR 10〜14 本 |
| 4 | 建築 | ビルドメニュー・ホットバーへの割当・BP の名前付け | ＋ビルドメニューから選んで建てられる | 可 | 中・PR 4〜6 本 |
| 5 | 残り | 研究・チャレンジ一覧・列車系の UI・出展モードの言語ゲート | 全機能（CEF は何も描かない） | 可 | 中・PR 5〜7 本 |
| 6 | 撤去 | 配布物から CEF が消える（Windows で約 368MB、Mac で約 298MB 減）。CEF のプロセスが無くなる | CEF が存在しない | 可 | 中（大半は削除）・PR 4〜6 本 |
| 7 | 磨き込み | 見た目の統一、装飾、演出 | — | 可 | 継続 |

段 3 と段 4 は、段 3 の最初の波（スロット・GamePanel・ツールチップ・スクロールの部品）が入った後なら並走できる。インベントリの束とビルドメニューの束は、実行時のやり取り（掴み・D&D）を持たないため（screens §7.1、input §3.7）。

---

## 2. 段階の詳細

### 段階 0: 地ならしと実測（見た目の変化なし）

**目的**: 画面を移し始める前に、共存が成り立つかを実測で確かめる。あわせて、挙動を変えずに移行の土台を整える（状態の置き場・DSL・台帳・凍結）。

**作業項目**

| # | 作業 | 中身と根拠 |
|---|---|---|
| 0-1 | OneJS と未使用パッケージを外す | `com.singtaa.onejs`（参照 0 件。配布物に quickjs と Resources 2.2MB が入り、Editor に `[InitializeOnLoad]` が 4 本ある）と `com.annulusgames.debug-ui`（参照 0 件）を manifest と lock から消す（shipping §5・§1.3） |
| 0-2 | 共存の実測 | PoC ブランチ `poc/uitk-hard-cases` の worktree で行い、master へは結論と UI Test Framework の導入だけを入れる。測る項目は下の a〜i |
| 0-3 | 状態を Web ホストの外へ移す（抽出 PR。挙動は変えない） | `ResearchTopic._nodeStates`、`ChallengeTopicState`、`BlockNetworkInfoCache`、`BlockInventoryTopic.TrackBlock` の監視判断、`OpenTrainTimetableTracker`・`TrainTimetableFetcher`、`NotificationTopic` の連番と移行告知、`WebUiModalService`、`BlueprintNameInputWebBridge` の状態部分、`EventLanguageGate`。移す先は Client.Game の DI（MainGameStarter の LifetimeScope）。今はこれらが `WebUiGameBinder.Bind()` の中でしか生成されず、Hub が null だと丸ごと作られない（host §5.2） |
| 0-4 | プレイテスト DSL の照会口を描画系非依存にする | `ClickWebUi` などの中身を `IUiElementLocator` に差し替え、名前を `ClickUi` などへ一度に変える。この段の実装は Web 版だけ。シナリオ 27 本（数え方で 24〜29）とスキル文書 4 本（`references/write-scenario.md`・`input-injection.md`・`place-blocks-via-ui.md`・`run-scenario.md`）を同じ PR で直す（tests §6.4） |
| 0-5 | 台帳・受入表・凍結 | `docs/webui/MIGRATION.md` を UITK 移行台帳へ置き換える。棚卸しは 3 軸で突き合わせる（`web/features/*`、`web/app/App.tsx` のマウント点と Portal、`WebUiGameBinder` の topic と action の登録）。状態機械の外にあるもの（常駐 HUD・Ctrl+U・背景スキット・カットシーン・カーソル追従）は別表にする（過去にオーバーレイを見落とした。shipping §6.2）。受入表（§4.1）を画面ごとに機械抽出する。`WebUiGateAuditTest` を広げ、`web/features` への新規ファイル追加を落とす |
| 0-RX | 遠隔実行の待ち受けを Kestrel から外す（並走トラック。段階 6 の前に必須） | `/api/remote-exec` は Web UI の Kestrel に相乗りしている（`Scripts/Client.WebUiHost/Boot/WebUiEndpoints.cs:97-101`、ADR 0072）。今は Web ホストの起動に失敗すると遠隔実行も開かず、検証機で CEF の不具合を調べる手段まで失う（`WebUiStartup.cs` の警告文「遠隔実行も開きません」）。行き先は D10 |

0-2 で測る項目（すべて未実測。input §7、tests §9.4、shipping §1.5）:

- a. UITK パネルと CEF の uGUI Canvas（`MainGameUI.prefab`）の前後を、PanelSettings の sortingOrder で決められるか
- b. UITK の上で `EventSystem.IsPointerOverGameObject` と `panel.Pick` が何を返すか（Pick 方式を採るかの判断材料）
- c. Unity Recorder（`Client.Playtest/Recording/PlaytestRecorder.cs:33` の GameViewInputSettings）と、バグ報告の録画（`ScreenCapture.CaptureScreenshotIntoRenderTexture`）に UITK が写るか
- d. `DefaultInputActions` の Navigate（WASD と矢印）・Submit（Enter、Space）・Tab が UITK のフォーカスを動かすか。専用の UI アクション資産で止まるか
- e. TextField の上で Esc を押したとき、EventSystem（UITK のイベント処理）と `UIStateControl.Update` のどちらが先に処理するか
- f. 日本語 IME（変換・確定・取消）が UITK の TextField で使えるか。Windows と macOS の実機で手で確かめる（自動化できない。**人手が要る**）
- g. UI Test Framework（`com.unity.ui.test-framework`、Unity 6000.3.8f1 組み込みの 1.0.0）を manifest に入れ、CI の batchmode で EditMode テストが 1 本通るか。`experimental.animation` と transition が `FrameUpdateMs` で進むか
- h. PanelSettings（Scale With Screen Size・基準 1280×720・Expand）が Web の `min(w/1280, h/720)` と同じ倍率になるか（720p・1080p・ウルトラワイド）
- i. CEF が常時 `Input.imeCompositionMode = On` にしていることが、UITK の TextField と取り合うか

**完了条件（観測できる形）**

- manifest と lock に `com.singtaa.onejs` が無い。Windows のビルドログで `Plugins/x86_64` に `quickjs_unity.dll` が無い
- 0-2 の a〜i の結果が、台帳に証拠（テスト名・ログの行・スクショ）付きで書かれている。a・c・f・g が「成立」。成立しないものがあれば、この段で裁定に出す（D14）
- Editor 限定のスイッチで Web ホストを起動せずに PlayMode を始め、ResearchTree と ChallengeList に入ると、研究とチャレンジの状態クラスが生成されていることを EditModeInPlayingTest で確かめる（緑）
- 改名した録画シナリオが、改名前と同じ合否になる（壊れている 8 本〈bd `moorestech-wwxy`〉は除く）
- 凍結ゲートが `web/features` への新規ファイルを落とすことを、テストで確かめている

**出荷**: 可。見た目は変わらない。

**主なリスクと潰し方**

- a が成立しない（前後を決められない）→ 段階 1 に入らない。CEF の Canvas 側の sortingOrder を動かす、または UITK を RenderTexture に描いて CEF の上に重ねる（推測。コストが大きい）を比べて裁定に出す
- f（IME）が成立しない → 命綱のバグ報告を日本語で書けない。D14 で決める
- 0-3 で挙動が変わる → 既存の C# の WebUi テスト 285 件と EditModeInPlayingTest を、緑のまま通す
- 0-4 でシナリオが壊れる → 改名は機械置換に限り、ロジックは変えない

**規模感（推測）**: 0-1 は 2 ファイル。0-3 は約 20 ファイルで、移動が中心の約 1,500 行。0-4 は 5 ファイル・約 670 行の作り替えとシナリオ 27 本。0-RX は 3〜5 ファイル・約 300 行とテスト（`RemoteExecEndpointTest`、`PlaytestBootUnityCallbackBindingTest`）。ファイルが重ならないので 3〜4 エージェントで並列。

---

### 段階 1: 命綱（Esc 以下を CEF から外す）

**目的**: CEF が死んでも、Web ホストが起動しなくても、Esc でポーズを開き、セーブ・セーブして終了・バグ報告・設定ができるようにする。UI が止まったことを、テスターの画面とバグ報告の両方に出す。

**対象**

- pauseMenu（トップ・サブページ・切断の表示・入れ子のポーズ〈スキット中 skitPause・乗車中 trainPause〉）
- settings（画質のプリセットと個別設定・言語・ダッシュ方式）
- バグ報告フォーム（種別・本文・送信・配送状況の 1 行・下書き）
- トースト（バグ報告が使う分だけ。Web の action 失敗のトーストは Web の画面が残る間 Web に残す）
- UI 停止の知らせ（§3.11）

**この画面群を第一弾にする理由**

- Esc はすべてのテスターが触る。セーブと報告は、UI が死んだときに失うもの（進行と報告の口）が最も大きい
- 束 D（pauseMenu・settings・toast）は、掴み・D&D を持たず、単独で切り出せる（screens §7.1）。ポーズのアンカー 6 個（`pause.*`）はマスタから参照されていない（v8 で 0 件。tests §8）ので、チュートリアル枠が UITK に無くても実害が無い
- 文字入力（IME）・Esc・フォーカス・ローカライズ・フォントという基盤の難所を、掴みや D&D 抜きで最初に通せる

**作業項目（基盤。§3 の設計に従う）**

- UiRoot（UIDocument 1 枚、PanelSettings は 1280×720・Expand）を MainGame シーンに置く。共存中は CEF の Canvas より前に置く（D4）
- 層マネージャ。この段で使うのは backdrop・stage・modal・toast・system-notice
- 所有表（§3.3）。PauseMenu・入れ子ポーズの状態で、ポーズのパネルと暗幕を UITK に振る。常駐 HUD（ホットバーなど）は Web のまま（`web/shared/uiState/uiScreenRouting.ts:25` で pauseMenu は alwaysOnHud=true）
- 入力: `UiPointerHitTest` に Pick を論理和で足す。`WebUiInputExclusivity` を `UiInputExclusivity` へ汎用化して、UITK のテキストフォーカスを流す。Esc のラッチ、UI 専用のアクション資産、入力欄以外は `focusable=false`
- テーマ USS（PoC の 146 行が初版）と、この段で要るトークン。暗幕は単色（ヴィネットは段階 7）
- フォント（TextCore の FontAsset。日本語・英数・韓国語のフォールバック）
- ローカライズ: `LocalizedLabel` 部品、差し込み記法の統一（D5）、数値書式を InvariantCulture に固定
- 部品: PanelActionButton、ModeSwitch、ScrollView の様式（components.md §8.10）、複数行の TextField、トースト層
- アンカー: `TutorialAnchorIds`（C# の定数）と `TutorialAnchor.Mark` だけ先に作り、ポーズのアンカーを付ける（枠の描画は段階 2）
- DSL: `IUiElementLocator` の UITK 版（name で探す・可視と被覆の判定・パネル座標から画面座標への変換）
- L1 の土台（偽モデル・記録コマンド・本番のテーマとフォントを当てた UITestFixture）
- UI 停止の検知（§3.11）と、凍った CEF の絵を隠す処理

**作業項目（画面）**

- `PauseMenuState` と `PauseMenuStateService` は C# のまま使い、ビューと Presenter だけを作る
- `pause_menu.*` と `bug_report.submit` のハンドラを「型付きコマンド」と「JSON アダプタ」に分け、UITK は型付きコマンドを呼ぶ（host §3）
- Web の `features/pauseMenu`・`features/settings` と、その vitest・Playwright を同じ PR で消す。`web/app/App.tsx:103` の PauseMenuPanel のマウントを外し、pause 系の状態の暗幕を Web 側で止める（R1: 描画者は 1 つ）

**完了条件（観測できる形）**

- Web ホストを起動しない設定で PlayMode を始め、「Esc → 設定で言語を切り替え → バグ報告を日本語の本文で送る → セーブして終了」を録画プレイテスト（`ClickUi` 経由）で通す。報告バンドルの manifest に `webUiHealth=dead` が入っている
- Windows 検証機の配布ビルドで、ゲーム中に CEF のプロセスを落とす。画面に知らせが出て、凍った CEF の絵が消え、Esc でセーブと報告ができる（人手で 1 回。結果を台帳へ）
- 受入表の pauseMenu・settings・bugReport・toast の行（ポーズ・バグ報告・設定の 61 件と、i18n の該当行）がすべて L0〜L4 か「捨てる（理由）」に分類され、L0/L1 が CI の EditMode シャードで緑
- 所有表のテストで、PauseMenu 系の状態のポーズパネルと暗幕を Web が描いていない
- IME（変換・確定・取消・入力中の Esc で画面が閉じない）を Windows と macOS で手で確かめた記録がある
- Web の `features/pauseMenu` と `features/settings` が repo に無い

**出荷**: 可。テスターから見える変化は、ポーズ画面の見た目（半透明の濃さ・字の太さ）と、UI が止まったときの知らせ。

**主なリスクと潰し方**

- Esc の二重処理（Web の全域 Esc ハンドラと C#）→ ポーズ由来の Web の Esc 経路を同じ PR で消す。Esc のラッチを L1 で固定する（Playwright `regression/inputLayer.spec.ts` の読み替え）
- 入れ子ポーズで、UITK のポーズが Web のスキット窓の下に隠れる → 0-a の実測で前後を確定し、録画で確かめる
- 言語を切り替えたとき、Web と UITK の片方だけが再描画される → 両方を同じ録画で確かめる
- 毎週のビルドで見た目が混ざる → playtest のパッチノートに「UI 移行中」と書く（推測: テスターの混乱が減る）

**規模感（推測）**: C# 約 3,000〜3,500 行・50〜70 ファイル、UXML/USS 約 800〜1,000 行。共有ファイル（層・所有表・入力）は 1 エージェントが直列で持ち、画面と L1 テストは 2 エージェント。

---

### 段階 2: 画面を開いていない時間（HUD・通知・チュートリアル・ストーリー）

**目的**: テスターが一番長く見ている「ゲーム画面」と「ストーリー」を CEF から外す。完了時、GameScreen・PlaceBlock・DeleteBar・Story・TrainHUDScreen の状態では CEF が何も描かない。

**対象**

- 常駐 HUD: クロスヘア、採掘ゲージ、キーヒント、キー操作ヒント、配置 HUD と削除モードの帯、目標 HUD（アンカー `challenge.current-hud`。マスタが参照している）、乗車 HUD
- 通知（獲得通知の集約と出入りの演出）、ワールド上のカーソルツールチップ（topic `ui.tooltip`）、ワールドピン
- UITK 側のチュートリアル枠（UITK のアンカーだけを照らす。§4.5）
- スキット（会話窓・タイプライター・選択肢・ツールバー・暗転・背景スキットの 1 行）。旧 UITK の `Scripts/Client.Skit/UI/SkitUI.cs` などは消す（bd `moorestech-lnsf.8`）
- ホットバーと装備 HUD の「表示」と、ゲーム画面でのホイールによる装備の切り替え。所有表で状態ごとに振る（D3）
  - ホットバー: BuildMenu 状態だけ Web（ビルドメニューからの D&D のため）。ほかの状態は UITK
  - 装備 HUD: インベントリ系の状態（playerInventory・subInventory・researchTree）だけ Web（装備枠のクリックは掴みの移動のため）。ほかの状態は UITK
  - ホイールで装備を切り替える処理は、装備 HUD を描く系が持つ

**理由**

- CEF が死ぬと、スキットの送り（Enter・クリックは Web が処理。input §1.4）が止まり、オープニングから進めなくなる（推測: 新しいテスターにとって最悪の止まり方）
- ゲーム画面で CEF が描くものが無くなれば、CEF が待機抑止の経路に入って busy-wait が減る見込み（推測。`cef-unity-frame-rate-followup-2026-07-29.md` §2.2「rAF アニメーションは待機抑止経路に入らない」の裏返し）。熱で FPS が落ちる件（bd `moorestech-vfn3o.19`）への部分的な手当てになる
- 通知とチュートリアルは、新しいテスターの最初の 30 分を導く

**作業項目**

- 基盤: アニメーション基盤 UiMotion（§3.6）、ツールチップ層（カーソル追従・画面端での反転。オフセットは既存の `OFFSET` を参照〈現在 12〉）、アンカーの登録表と UITK のチュートリアル枠（§4.5）、Painter2D の図形部品（FadeRule・GaugeBar・削除モードの斜線帯・GamePanel の hud 面の簡易版）、SVG アイコンの置き換え（D6）
- データ: 通知の集約（`web/features/notification/notificationStore.ts`）を C# の通知モデルへ移す。ワールドピンは同じフレームで投影し、WS を通さない
- Web 側: `features/commonHud`・`progress`・`keyHint`・`modeHud`・`trainHud`・`notification`・`skit`、`shared/tooltip` の CursorTooltip、tutorial の `KeyControlHintHud` と `WorldPinOverlay` を同じ PR で消す。Web の `TutorialOverlay` は Web 側のアンカーのために段階 5 まで残す。Web の SCREEN_TRAITS を直し、HotbarPanel は buildMenu の画面だけ、EquipmentPanel はインベントリ系の画面だけで描く

**完了条件（観測できる形）**

- Web ホストを起動しない設定で、「オープニングのスキットを選択肢まで進める → 歩いて採掘する（ゲージが出る）→ ホットバーで選んで設置する → 獲得通知と、目標 HUD を指すチュートリアル枠が出る」を録画プレイテストで通す
- 所有表のテストで、GameScreen・PlaceBlock・DeleteBar・Story・TrainHUDScreen の各状態で Web が描く部品が 0
- ゲーム画面での CEF の 0F-wait（`_enableLog=1` の `block_avg`）を、段階 2 の前後で比べて台帳に書く。下がらなくても完了は妨げない。結果で段階 3 以降の優先を見直す
- 受入表の HUD 52 件・通知 46 件・チュートリアルのうち UITK 側の分・スキット 11 件の行が分類済みで、L0/L1 が緑。ADR 0024 の判定役テスト（祖先のクリップ）の UITK 版が 1 本ある
- Web の上記の feature フォルダが repo に無い

**出荷**: 可。

**主なリスクと潰し方**

- 通知が Web の全画面 UI の上に出る（ADR 0017 では全画面 UI の裏）→ D4 の推奨どおり、Web が描く全画面の状態の間は UITK の通知層を隠す。段階 5 の後に ADR 0017 の形（stage の背面）へ戻す
- チュートリアルの二重描画・取りこぼし → 同じアンカーが両系で見つかったら理由をログに出す。この段で UITK 側に来るマスタのアンカーは `challenge.current-hud` の 1 件だけなので、その 1 件を録画で確かめる。系をまたぐ dragGuide は新設しない（マスタは 0 件）
- ホイールの持ち主が状態で変わる → 「装備 HUD を描く系がホイールの切り替えも持つ」を所有表の 1 列にして、テストで固定する
- スキットの入力が二重に送られる → Web の skit を同じ PR で消す（単一消費者）
- ホットバーの見た目が、ビルドメニューを開いた瞬間に切り替わる（Web 製になる）→ 段階 4 までの既知の差として台帳に書く
- 演出の時間源 → UI は実時間（D15）

**規模感（推測）**: Web 側の対象は約 2,800 行。C# 約 3,500〜4,500 行・70〜90 ファイル、UXML/USS 約 1,000 行。4 エージェントで並列（HUD・通知・ツールチップ／チュートリアル・アンカー・ワールドピン／スキット／ホットバーと装備の表示・ホイール）。共有ファイル（層・所有表・アンカー登録表）は 1 エージェントが先に入れる。

---

### 段階 3: 生産ループ（インベントリ・クラフト・機械 UI）

**目的**: Tab のインベントリ、手クラフト、機械やチェストの UI を CEF から外す。テスターが最も頻繁に開閉する画面群で、CEF の helper のメモリが伸びる操作（機械 UI の開閉の連打）もここに当たる。

**対象（束 A）**

- inventory: メイングリッド、整理、掴み、分割ドラッグ、右ドラッグで配る、ダブルクリック、Shift
- 装備 HUD（全状態）、GrabOverlay（掴んだアイテムの絵）、HoverTooltip（スロットのツールチップ）
- RecipeViewer と ItemListPanel: 長押しクラフト、Enter、ドラッグスクロール、作れる数
- BlockInventoryPanel のうち列車系以外: チェスト、機械（レシピ選択とインベントリの往復・ゴースト・モジュール・分間生産数）、発電機、採掘機、ポンプ、パイプ、歯車、ベルトの流量、電力網、歯車網、フィルタ分配器、電気→歯車
- インベントリ内の D&D 誘導（DragGuide）
- 所有表: subInventory はブロックの種類で振る。列車駅・アイテムと液体のプラットフォーム・列車のインベントリは Web のまま（段階 5）。researchTree の InventoryPanel は UITK、研究パネルは Web（掴みの往復が無いので同居できる）

**作業項目**

- 部品の最初の波（1〜2 エージェントで直列）: スロット系（SlotFrame・ItemSlot・SlotGrid・アイコン・個数の縁取り・ゴースト・不足表示）、GamePanel の default と craft の簡易版、press ジェスチャ・DnD・掴み層（閾値は既存の `usePressGesture` を参照〈現在 5px〉）、HoverTooltip、BarSeries（Painter2D）、コンテナクエリの代わり（`GeometryChangedEvent` で寸法を計算）
- 純ロジックの移植（vitest を NUnit へ 1 対 1）: `web/shared/itemMove`（191 行・30 件）、`craftLogic`、`holdCraftLogic`、`detailLogic`、`machineSlotGhosts`、`equipmentLogic`、`ownedCounts`
- 画面（次の波を 4〜5 エージェントで並列）: インベントリと装備／レシピとアイテム一覧／機械・発電機・採掘機・ポンプ／歯車・ベルト・電力網・フィルタ分配器・液体
- Web 側: `features/recipe` と `features/blockInventory` の列車系以外を消す。Web の `features/inventory` と GrabOverlay は、列車系の subInventory のためだけに段階 5 まで残す（描く状態を所有表で絞る。既存のコードを触らずに残すだけで、新しく書く捨てコードは無い）

**完了条件（観測できる形）**

- Web ホストを起動しない設定で、「Tab → 手クラフト（長押し）→ 石窯に燃料と素材を入れる（掴み・Shift・右ドラッグ）→ 取り出す → 整理」を録画プレイテストで通す
- 機械 UI を開いたまま中身が更新される件（`.decisions/2026-10-18-液体UIは開いたままRefresh更新をunityプレイ録画テストで検証する.md`）の録画シナリオを、UITK で通す
- 受入表のインベントリ・装備 93 件、ブロック UI 133 件のうち列車系以外、レシピ 48 件、共通部品 77 件の行が分類済みで L0/L1 が緑。itemMove の 30 件が NUnit に移っている
- マスタの `recipe.craft-button` と `itemViewHighLight`（`recipe.item-<itemId>`）のチュートリアル枠が UITK で出る
- 所有表のテストで、playerInventory と subInventory（列車系以外）で Web が描く部品が 0

**出荷**: 可。

**主なリスクと潰し方**

- 掴みが系をまたぐ → 所有表で「同じ状態・同じブロック種別では、掴みに関わる面をすべて同じ系にする」を固定する
- ドラッグ捕捉中も下の要素に PointerEnter が届く（PoC の罠）→ DnD 層に最初から抑止を入れる
- `MouseMove(from, to)` が途中のスロットに PointerEnter を出さない可能性（L1）→ 1 スロットずつ動かす（tests §5.5）
- セクションの種類が多い（11 種以上）→ 先に SectionStackView と共通部品を入れてから、セクションを 2 群に分けて並列にする
- 一覧の要素数が多い画面の性能 → スロット数は数百程度なので素の VisualElement で足りる見込み（推測）。足りなければ ListView（PoC で 1 万行・行要素 28 個）

**規模感（推測）**: Web 側の対象は約 4,000〜4,500 行。C# 約 5,000〜6,500 行・110〜140 ファイル、UXML/USS 約 1,500 行。

---

### 段階 4: 建築（ビルドメニュー・ホットバー・BP）

**目的**: B キーのビルドメニュー、ホットバーへの割当、BP（ブループリント）の名前付けを CEF から外す。

**対象（束 B）**

- buildMenu: 3 カラム、検索（IME）、スクロールスパイ、smooth scroll、詳細の固定表示、セッション内の状態の保存、BP の右クリック削除、不足時のツールチップ
- hotbar（BuildMenu 状態を含む全状態。D&D での割当・入替・解除）
- モーダル層（BP の名前入力）。`WebUiModalService` と `BlueprintNameInputWebBridge` は単一消費者なので、描画者を UITK に一本化する

**作業項目**: スクロールスパイの計算（`web/features/buildMenu/hooks/useBuildMenuCategoryScroll.ts` 305 行）を C# へ移し、L1 で固定してからビューを作る。smooth scroll は UiMotion で動かす。モーダル層（幕・フォーカスの閉じ込め・Esc は UIState が持つ）。Web の `features/buildMenu`・`features/hotbar`・`features/modal` と `BlueprintNameInputWebBridge` の Web 送り部分を同じ PR で消す。

**完了条件（観測できる形）**

- Web ホストを起動しない設定で、「B → 検索 → エントリをホットバーへドラッグ → 選んで設置 → BP をコピーして名前を付ける」を録画プレイテストで通す
- 受入表のビルドメニュー 71 件・ホットバー 16 件・モーダル 11 件が緑。例として「エントリを 5px より多く動かして枠 1 に落とすと `AssignHotbar(1, id)` が 1 回だけ発行され、`SelectBuildMenu` は発行されない」の L1 版がある（tests §3 の例 1）
- 所有表のテストで、BuildMenu 状態で Web が描く部品が 0

**出荷**: 可。

**主なリスクと潰し方**: スクロールスパイは PoC で未実証 → 先に L1 で固定する。検索欄の IME → 段階 0 と段階 1 の確認結果に沿う。ホットバーの所有の切り替え（段階 2 で作った状態別の振り分け）を、この段で外す。

**規模感（推測）**: Web 側の対象は約 1,500 行。C# 約 2,000〜2,500 行・40〜50 ファイル。2〜3 エージェントで並列。

---

### 段階 5: 残り（CEF が何も描かない状態へ）

**目的**: 残りの画面を移し、所有表の Web 列を空にする。

**対象**

- research（TreeView のパンとズーム・詳細ペイン・研究ノードのアンカー。マスタが 7 件参照）
- challenge 一覧（TreeView・カテゴリのボタン列）
- 列車系のブロック UI（駅名の入力〈IME〉・プラットフォームの搬送モード・列車のインベントリ・時刻表の楽観更新）と、それに伴う Web の inventory の残り
- eventLanguageGate（出展モード。DI より前に表示するので、UiRoot を起動の早い段で作る）
- Web の TutorialOverlay（全アンカーが UITK 側に来るので消す）
- Mantine の素の見た目の画面（challenge・trainHud・toast）は機能だけ移す。様式化は段階 7

**完了条件（観測できる形）**

- Web ホストを起動しない設定で、「研究を 1 つ完了する → チャレンジ一覧を開く → 駅名を日本語で付ける → 時刻表を組んで自動運転を入れる」を録画プレイテストで通す
- 所有表の Web 列が、全状態で空
- CEF を起動したまま検証機で 1 時間遊び、CEF の helper のメモリが伸びないことを確かめる（Web が何も描かないため。推測）
- 受入表の残り（研究 76 件・チャレンジ 11 件・列車 26 件・画面遷移と入力層 44 件）が緑

**出荷**: 可。

**主なリスクと潰し方**: TreeView の性能 → 研究ツリーのノード数を先に数え、PoC のレシピ図（24 ノードで 60fps）を超える規模かを見る。仮想化した行に置いたアンカーは画面外で存在しない（tests §7.2）→ チュートリアルが指す一覧は仮想化しないか、Bind と Unbind でクラスを付け替える。出展モードの言語ゲートは DI より前 → 起動順のテスト（`Client.Tests/EditModeInPlayingTest/Boot/EventLanguageGateCursorTest.cs` の読み替え）。

**規模感（推測）**: Web 側の対象は約 1,850 行。C# 約 2,300〜3,000 行・50〜60 ファイル。3 エージェントで並列。

---

### 段階 6: 撤去

**目的**: §5 の順序で CEF と Web の層を消し、完了の定義（§5.3）を満たす。

**テスターに届くもの**: 配布物から CEF が消える（Windows で約 368MB、Mac で約 298MB の減。shipping §1.1）。CEF のプロセスが無くなる。ゲーム中の IME の常時 ON が消える。

**完了条件**: §5.3 のチェックリストがすべて満たされる。

**出荷**: 可。

**主なリスクと潰し方**

- ビルド工程・CI・リリース検査の契約テストが同時に赤くなる → 同じ PR で直す（`scripts/playtest/tests/test-release-playtest-mac.sh` の 3 ケースと stubs。shipping §1.3）
- NuGet の `Microsoft.Extensions.*` を ModelContextProtocol などが共有している可能性 → 先に依存グラフを確かめる（host §9）
- `MainGameUI.prefab` の CEF の Canvas を消す → `uloop execute-dynamic-code` で行う（AGENTS.md）

**規模感**: 削除が中心（`moorestech_web` 約 24.5k 行〈テスト込み〉、`Client.WebUiHost` 約 10.5k 行のうち移設後の残骸、`Client.Tests` の 65 ファイル、DSL の Web 部約 670 行）。manifest・BuildPipeline・CI など共有ファイルが多いので、1〜2 エージェントで直列。

---

### 段階 7: デザインの磨き込み

**対象**

- Linear 色空間での半透明を、全トークンで実機に合わせ直す（`rgb(… / x%)` と `rgba` が 67 箇所。shipping §2.4）
- 小さい文字（10〜13px が 18 宣言）を見直す
- GamePanel の 4 辺フェード（頂点色メッシュ）・右下グリップ・下向き三角、レシピの四隅ブラケット、段付き罫線、drop-shadow の代わり、App のヴィネット
- Mantine の素の見た目の画面の様式化（toast〈bd `moorestech-cgm`〉・challenge・trainHud）
- `components.md` と `ref-uitk-design`（仮称）の最終化、演出の尺の調整
- ADR 0013・0017 を UITK の層として書き直す

**完了条件**: 台帳の「見た目」列が、全画面で「UITK 版で確認済み」になっている。確認は、フレーム番号の透かしを入れた ScreenCapture の画像と、目視の記録で行う。画素の一致は目標にしない（過去に parity-check 47/47 が足かせになり、打ち切った。shipping §6.2）。

**出荷**: 可。

**規模感**: 継続。1〜3 エージェント。

---

## 3. 共通基盤の設計方針

### 3.1 置き場と層の分け方

- 新しい asmdef `Client.UI`（仮称）を作る。`Client.UI/Foundation/{Root, Layers, Routing, Input, Text, Theme, Motion, Overlay, DragDrop, Anchor}` と `Client.UI/Screens/<画面>/{View, Presenter}` に分ける。1 ディレクトリ 10 ファイルまで、1 ファイル 200 行まで（AGENTS.md）
- UXML と USS は `Assets/Asset/UI/Uitk/<画面>/` に置く（推測: 既存の `Asset/UI/NewUI/Skit` の置き方に倣う）
- 依存の向き: `Client.UI` → `Client.Game`（モデル・コマンド）・`Client.Localization`・`Client.Input`。`Client.Game` は `Client.UI` を知らない。モーダルの要求はインターフェース越しにする（`WebUiModalService` が static だった理由〈依存の向き〉は、ここで消える）
- View は VisualElement を受け取って組み立てるだけで、MonoBehaviour に依存しない。Presenter がモデルを購読してビューへ push し、操作を型付きコマンドへ渡す。MonoBehaviour は UiRoot（UIDocument 1 枚）と DI の配線だけ。L1 テストで、同じビューを `UITestFixture` の `rootVisualElement` に組めるようにするため（tests §5.3）

### 3.2 データ購読層

- **正本には手を入れない**（host §7.5 R2）。正本は `Client.Game`・`Client.Skit`・`Client.Input`・`Client.Localization` とサーバーにあり、28 の topic のうち 18 前後は購読先を変えるだけで済む（host §0）
- 各 `XxxTopic` を「型付き DTO と変化通知（`IObservable<Unit>`）」と「JSON アダプタ」に分ける。前者を `Client.Game` 側へ置き、UITK の Presenter が購読する。Web は後者を使い続ける。分けるのは、その段で使う topic だけ
- フレーム末に 1 回へ畳む処理（PostLateUpdate。INFRA-7）は残す（中間状態を描かないため）。100ms・`IntervalFrame(4)` のサンプリングは WS の帯域のためなので、UITK では毎フレーム読む
- revision・snapshot・restoring・再接続・心拍・zod の検証は、UITK 側には持たない。zod に込めた不変条件（FluidSlot の空と中身ありの判別、変換機の詳細の排他、Tooltip の可視と行数、Hotbar の 9 枠、PlacementMode の判別）は、C# の型（sealed 継承の判別 union）で表す（host §5.1）
- action は「型付きコマンド（UI に依存しない。前提条件の検証を含む）」と「JSON アダプタ」に分ける。共存中も両系が同じ検証を通る（例: `BuildMenuActions.cs:39` の state==BuildMenu）
- `UIStateControl.OnStateChanged`（`event Action`）は UniRx に置き換える（AGENTS.md「イベント発火のために Action を使わない」）
- 区分 B の状態（段階 0 で移設したもの）は、UITK と Web の両方が同じインスタンスを読む

### 3.3 パネル・層・所有表

- **パネルは 1 枚。** ルートの直下に、固定の順で層のコンテナを並べる: backdrop → behind-stage（通知）→ stage（パネル）→ viewport HUD → grab → modal → tooltip → toast → tutorial → system-notice（UI 停止の知らせ）→ full-screen-gate。USS に z-index は無いので、C# の層 enum と兄弟の順で表す（screens §1.3）
- 層のコンテナと backdrop は `PickingMode.Ignore` を既定にし、テストで固定する。忘れると「パネル外の右短押し」とワールドのクリックが効かなくなる（input §3.1）
- stage 族は中央の 1280×720 の子、viewport 族はルートの辺に付く absolute、背面 viewport 族は stage の前の兄弟。ADR 0013・0017 の三分法をそのまま表す
- **所有表 `UiOwnershipTable`**: (UIState〈と subState・ブロック種別〉, 部品) → Web か Uitk。正本は JSON 1 枚で、`Client.Tests/WebUi/WireFixtures/` に置き、C# のテストと vitest の両方が読む（同じフィクスチャを共用する前例がある。tests §1）。テストで固定すること:
  - 各組で、描く系がちょうど 1 つ
  - 掴みや D&D でつながる面は同じ系
  - 単一消費者（モーダル・スキットの入力・BP の名前入力）の描画者は 1 つ
  - 暗幕は、その状態の主パネルを描く系が描く
  - 段階 6 で Web 列ごと消す
- 共存中の前後: UITK のパネルを CEF の Canvas より前に置く（D4。0-a の実測が前提）。Web が描く全画面の状態の間は、UITK の通知層を隠す（ADR 0017 からの一時的な逸脱）

### 3.4 部品と作る順

| 段 | 部品 |
|---|---|
| 1 | PanelActionButton、ModeSwitch、ScrollView の様式、複数行の TextField、Toast、LocalizedLabel、単色の暗幕 |
| 2 | FadeRule、GaugeBar、KeyCap（kbd）、IconButton、GamePanel の hud 面の簡易版、斜線帯、CursorTooltip、通知の行、チュートリアル枠とラベル、ワールドピン |
| 3 | SlotFrame・ItemSlot・SlotGrid・アイコン・BlockIcon・FluidSlot、GamePanel の default と craft の簡易版、ProgressArrowGlyph、BarSeries、HoverTooltip、RecipeRow、掴み層、DnD 層 |
| 4 | モーダル、スクロールスパイ |
| 5 | TreeView（パンとズーム 0.4〜2.5・慣性 τ=325ms・Painter2D の線） |
| 7 | GamePanel の完全版（4 辺フェード・グリップ・三角）、四隅ブラケット、段付き罫線、ヴィネット |

USS に無いものの扱いは screens §5 の対応表に従う（grid は flex-wrap と固定幅、gap は margin、`::before` は子要素、`:first-child` は C# でクラスを付ける、z-index は層、コンテナクエリは `GeometryChangedEvent`）。

### 3.5 テーマ USS とトークン

- **トークンの正本は USS**（D7）。`tokens.uss` に Web のトークン約 238 個（`tokens.css` 192・`skitTokens.css` 38・`slotTokens.css` 8）を移す
  - rem と em は px にする（1rem=16px）
  - `color-mix` と `rgb(from …)` は事前に計算した色にする
  - `calc()` で導くトークン（70 箇所）は、結果の値を書き、式をコメントに残し、一致をテストで見る
- C# が寸法を要るとき（Painter2D・配置の計算）は、`customStyle`（`CustomStyleProperty<float>`）で解決後の値を読む。トークンを C# の定数に二重に持たない
- `theme-dark.uss`: PoC の 146 行（`Client.UitkPoc/Resources/UitkPoc/PocControls.uss`）を初版にする。スライダーの入力部が高さ 0 になる癖への対処も含める
- data 属性による状態（40 箇所）は、USS のクラス（例 `slot--selected`）と `EnableInClassList` にする
- 半透明: トークン表に「Web の値」と「UITK の値」の 2 列を持ち、Web の値を流用しない。段階 1 で中核（`--surface-navy` 80%・`--tooltip-face` 94%・モーダルの幕）だけ実機で合わせ、残りは段階 7（D9）
- 静的検査（L2）: 機能側の USS に色の値を直書きしない、`:nth-child` を使わない、UXML の `text=` に生の文字を書かない、name とアンカーを重複させない、UI Builder 専用の状態を持たない

### 3.6 アニメーション

- 入退場とホバーは USS の transition で書く
- ループ・キーフレーム・ばね・smooth scroll は、自作の `UiMotion`（100〜150 行の見込み）で書く（D8）
  - `VisualElement.schedule` で動かし、時計を差し込めるようにする（L1 で `FrameUpdateMs` に合わせて進める）
  - 要素がパネルから外れたら自動で止まる
  - 「頭から回し直す」API を持つ（通知の集約で寿命を張り直す。`notificationStore.ts:41`）
- 尺と曲線はトークンに置く。通知の生存は既存の `NOTIFICATION_DISPLAY_MS` を参照（現在 7000ms）、入場 160ms・退場 200ms（ADR 0016）、脈動 1200ms（ADR 0039）、誘導 3200ms
- PoC で踏んだ罠: `height: auto` は transition できない、高さ 0 の親の中では子の文字が潰れる、`schedule.Execute` はレイアウトより先に走る（FLIP は `GeometryChangedEvent` の新旧の矩形で書く）
- ref-webui-design §6（装飾アニメーションは基本入れない、テストで尺を 0 にする抜け道は作らない）は継ぐ。時計を進めるのは「尺を 0 にする」ことではない

### 3.7 ツールチップ・D&D・掴み・モーダルの層

- ツールチップ層
  - CursorTooltip: `MouseCursorTooltipState` を購読する。カーソルに追従し、画面端で反転する。カーソルがロックされている間は画面中央を基準にする（ADR 0020 のワープ前提は要らなくなる見込み。推測）
  - HoverTooltip: 祖先がスクロールしたら引っ込める
  - 書式は ADR 0019 の値（18px・padding 6/10・max-width 320）を USS のトークンへ移す
  - ドラッグの捕捉中は出さない（PoC の罠）
- press ジェスチャ: tap・drag・abort の状態機械。閾値は既存の 5px を参照。`CapturePointer` を使う。D&D・パン・ドラッグスクロール・長押しで共通にする
- D&D 層: 落とし先は `panel.Pick` と、落とし先の印のクラスで決める。ゴーストを出す。掴み層（GrabOverlay）は 1 つで、所有表で掴みに関わる面と同じ系にする
- モーダル層: 幕とフォーカスの閉じ込め。Esc は UIState（C#）が持つ。描画者は 1 つ
- 右クリックメニューが要れば、`GenericDropdownMenu` を暗色のテーマで使う（PoC）

### 3.8 入力とフォーカス

- `UiPointerHitTest.IsPointerOverAnyUi` = カーソルがロックされていない、かつ（UITK の Pick が層のコンテナ以外を返す ／ 共存中は `IsPointerOverWebUi` ／ デバッグ用 uGUI のための EventSystem）。呼び出し側の 23 ファイルは変えない（input §3.3 の案 A）
- ホイールを通す印は USS のクラス（`wheel-passthrough`）にして、Pick の結果の祖先で判定する
- テキストのフォーカス: ルートで `FocusInEvent` と `FocusOutEvent` を TrickleDown で受け、`focusedElement` が TextField かを `UiInputExclusivity` の ReactiveProperty へ同期で書く。共存中は Web の値との論理和
- Esc: 「このフレームの Esc は UI が使った」というラッチを、`InputKey` の抑止判定に足す（TextField の上の Esc で画面まで閉じないため。input §3.4）
- UI 専用のアクション資産: Point・Click・RightClick・MiddleClick・ScrollWheel を残し、Navigate・Submit・Cancel のキーボード束縛を外す（`DefaultInputActions` は Navigate に WASD を持つ。`Assets/Scenes/Game/MainGame.unity:8435`）
- 入力欄以外は `focusable=false`。Space（`.decisions/2026-10-01-Web UI全体で文字入力欄以外のSpace既定動作を封じる.md`）・Enter・Tab でボタンが押されたりフォーカスが動いたりしないことを、L1 で固定する
- Enter でのクラフトやスキットの送りなどは、Presenter か UIState の C# のキー処理に置く（Web の全域のキー処理を移す）
- ゲームパッドは正式対応外（ADR 0082）なので、ナビゲーションは作らない

### 3.9 ローカライズ

- C# の `Localize` に一本化する。3 段のフォールバック・型付きキー・`ContentLocalizationKeys` はすでにある（input §4.1）
- ADR 0006 の決定 5（Web 側の Guid 辞書で解決）を、「ビュー側（UITK の Presenter）で解決し、言語切替は再バインドで済ませる」へ改訂する
- `LocalizedLabel`: キーと引数を持ち、`Localize.OnLanguageChanged` で再解決する。DTO は Guid とキーだけを運ぶ
- **差し込み記法は名前付きへ統一する**（D5）
  - `Localization/localization.csv` の位置引数 32 キー（`{p0}` などが 190 出現）を名前付きに書き換え、`LocalizationTextInterpolator` を名前付きだけにする
  - C# の呼び出し元（`Client.Game/InGame/UI/Tooltip/TooltipLine.cs`、`Client.MainMenu/ConnectServer.cs`、`Client.Starter/InitializeProprieties.cs`、`Client.Starter/RemoteConnectionDenyReason.cs`、`Client.Starter/Initialization/Progress/LoadingProgressLog.cs` ほか）を同じ PR で直す
  - mod 同梱の CSV（`moorestech_master/server_v8/mods/moorestechAlphaMod_8/localization/localization.csv` ほか）に差し込みは 0 件なので、mod への波及は無い
- 数値の書式: 表示用の書式ヘルパーで InvariantCulture を明示する（de 列があり、ドイツ語の OS では小数点が「,」になる。input §4.2）
- kbd の差し込み（LocalizedShortcutHint）は、KeyCap 部品と文字列の分割で作る
- 辞書が届く前の文言表（`preDictionaryText.ts`・`DictionaryIndependentText`）は、C# の固定文言に移す（出展モードの言語ゲート）
- 静的検査: UXML の `text=` に生の文字を書かない（`noJsxVisibleLiteral` の後継）

### 3.10 フォント

- UITK 用の TextCore の FontAsset（SDF）を Editor で作ってコミットする。既存の `Assets/Dependencies/Font/*SDF*.asset` は TMP_FontAsset で型が違う。`FontDefinition.FromFont` はビットマップで、拡大すると崩れる（PoC）
- 英数は NotoSans-Regular、日本語は NotoSansJP-Medium、韓国語は NotoSansKR-Medium を、明示のフォールバックに入れる（Web はハングルを OS のフォントへ落としていた。推測）
- 言語ごとに、ルートのクラスでフォントの順を切り替える（`:root[data-locale]` の代わり）
- 合成の太字は使わない（`-unity-font-style: bold` を禁止する）
- 小さい文字（11〜12px が Chrome より細い）: 段階 1 で、1920×1080 と 1280×720 の拡縮後の物理 px で比べ、最小のトークンを決める（D9 と同じ実機確認で）。本格的な調整は段階 7
- 文中アイコン（SpriteAsset）は内部 API に頼る（PoC）。構築と描画のテストを置く（Unity を更新したら最初に壊れる箇所）

### 3.11 UI 停止の検知と、誰が気づくか

AGENTS.md「失敗の扱い」に従い、共存中に CEF が死んだことを誰がどこで気づくかを決める。今は Web ホストの起動失敗が LogWarning だけで、配布物では開発者に届かない。報告の口（Web のポーズ）も一緒に死ぬ。

- `WebUiHealth`（`Client.WebUiHost` の中に置き、段階 6 で一緒に消える）が、Alive か Dead かを持つ。Dead にする条件:
  - Web ホストの起動に失敗した
  - WS の接続が一定時間無い
  - ping が途絶えた（Web は 5 秒ごとに ping を送る。host §1.1。今のホストは pong を返すだけで、最後に受けた時刻を持っていないので足す）
- 気づく経路
  1. テスター: UITK の system-notice 層に「一部の画面が応答していません。Esc でセーブとバグ報告ができます」と出す
  2. 開発者: バグ報告の manifest に `webUiHealth` と「最後の ping からの秒数」を必ず入れる（ADR 0057 の受け口から Mac mini に届く）
  3. LogError
- Dead になったら CEF の RawImage を隠す。CEF が固まると最後のフレームが画面に残り、閉じたはずのインベントリが見えたままになる可能性がある（推測。Discord 2026-10-03 の「インベントリ開いたままだったので閉じれなくなってました」）。RawImage は moorestech 側の `WebUiCefNavigator` と同じ prefab にあるので、外部パッケージを変えずに隠せる見込み（推測）
- CEF の自動再起動は作らない（撤去する対象への投資になるため。D13）

### 3.12 AI 向けの作法（議論記録の未裁定論点への案）

- UXML と USS はテキストで持ち、UI Builder 専用の状態を作らない
- ビュー側の C# を薄く保ち、判断は Presenter とモデルに置く。USS と UXML の変更ではドメインリロードを起こさない（トークンを C# から生成しない理由。D7）
- 見た目の自己確認は ScreenCapture 系で撮り、フレーム番号か時刻の透かしを入れて古いフレームを見ない。`uloop screenshot` の rendering モードには UITK が写らない（PoC）
- name は testid と同じ綴りにし、アンカーは USS のクラスで付ける（§4.5）
- ref-webui-design の原則（ホワイトリスト、面は GamePanel だけ、スロットは共有部品だけ、装飾は 6 語彙、色はトークンだけ、族と層）を `ref-uitk-design`（仮称）へ写す。§0 と §10（mock-host・Playwright・mock の撮影）は、EditMode での組み立てと ScreenCapture に置き換える（shipping §2.5）
- moores-code-review の webui の ts/tsx 観点を、UITK の観点（PickingMode の既定、所有表、トークンの直書き、`Func<>` の禁止など）に作り替える。段階 1 で最低限を入れ、段階 6 で ts の観点を消す

---

## 4. 検証の作り方

### 4.1 受入表（vitest と Playwright の読み替え）

- 画面ごとに 4 列の表を作る: 前提（モデルの状態）／操作／期待（発行されるコマンドと表示）／由来（ファイル: テスト名）
- Playwright のテスト名 213 件と、vitest の describe・it の名前を機械的に抜き出す。ほぼすべて日本語の挙動文になっている（tests §3）
- 全行を L0〜L4 か「捨てる（理由）」のどれかに分類する。未分類は計画の欠陥とみなす（`docs/webui/disposition.md` の型）
- 読み替えの目安（tests §2.1。件数は正規表現で数えた値で、議論記録のランナー実測〈809 件・211 件〉とは少しずれる）

| 種別 | 件数 | 行き先 |
|---|---:|---|
| ロジック | 311 | 約 290 件を L0（C# の純ロジックと NUnit） |
| 画面フロー（Playwright） | 213 | 約 205 件を L1。うちレイアウトや見た目の主張の約 52 件は、L1 のレイアウト照合か L4 の目視 |
| コンポーネント | 235 | 約 200 件を L1 |
| 多言語 | 61 | 約 25 件を L0・L2。HTTP の辞書取得まわりの約 36 件は捨てる |
| 静的規約 | 91 | 意図の約 35 件を L2。Mantine の禁止・CSS Modules・TS の import 構造の約 56 件は捨てる |
| 通信契約 | 167 | 捨てる |

- C# の WebUi テスト 285 件: ビューモデル 90 件・アクション 36 件・状態ストア 28 件は残し、型付きの DTO とコマンドへ付け替える。wire の 131 件は捨てる（設定系の約 10〜15 件は設定サービスのテストへ移す）
- 画面を移す PR で、その画面の受入表が緑になったら、Web の feature・vitest・Playwright を同じ PR で消す

### 4.2 検証の層

| 層 | 中身 | 道具 | PlayMode |
|---|---|---|---|
| L0 純ロジック | スロットの計画、長押しクラフト、ビルドメニューの分類、ビューポートの計算、ルーティング表、所有表、通知のストア、ビューモデル、コマンド | NUnit | 不要 |
| L1 ビュー | UXML・USS・Presenter を偽モデルと記録コマンドでつなぎ、操作をシミュレートして、発行コマンド・クラス・テキスト・レイアウトを照合する | UI Test Framework の `UITestFixture` と `PanelSimulator` | 不要 |
| L2 静的規約 | USS と UXML をテキストとして読む（§3.5 の検査） | NUnit でファイルを読む | 不要 |
| L3 結合 | ゲームを起動し、UI 状態の遷移・パネルの表示・コマンドのサーバー到達を確かめる。CEF 無し起動の確認もここ | EditModeInPlayingTest | 要（少数に絞る） |
| L4 録画プレイテスト | 実際の入力経路と見た目 | プレイテスト DSL | 要 |

### 4.3 CEF 無し起動ラダー（この計画の物差し）

- Editor 限定のスイッチ（`#if UNITY_EDITOR` で囲み、ファイルの末尾に置く。AGENTS.md）で、Web ホストを起動せずに PlayMode を始める。Hub が null の経路（起動失敗と同じ形）を再現する
- 段ごとに録画シナリオを 1 本足し、前の段のシナリオも毎回流す（退行を検知するため）

| 段 | シナリオ（仮称） | 内容 |
|---|---|---|
| 1 | `uitk-lifeline` | Esc → 設定で言語切替 → 日本語でバグ報告 → セーブして終了 |
| 2 | `uitk-opening-hud` | オープニングのスキット → 採掘 → ホットバーで設置 → 通知とチュートリアル枠 |
| 3 | `uitk-production-loop` | 手クラフト → 石窯に入れて取り出す → 整理 |
| 4 | `uitk-build-bp` | ビルドメニュー → ホットバーへドラッグ → 設置 → BP の名前付け |
| 5 | `uitk-research-train` | 研究 → チャレンジ一覧 → 駅名 → 時刻表と自動運転 |

- ゲームの途中で CEF が死ぬ場合は、Windows 検証機で人手で 1 回確かめる（段階 1）。Mac mini では、並走しているほかのセッションの CEF を巻き込むので、プロセス名で落とさない（自分の Editor を親に持つ PID だけを落とす）

### 4.4 EditMode の UI テスト（L1）

- UI Test Framework 1.0.0 は Unity 6000.3.8f1 に組み込まれているが、manifest にはまだ入っていない。段階 0 で入れる
  - `UITestFixture`: EditMode ではウィンドウ無しの Editor パネルを使い、描画はしないがレイアウトとイベントは処理する
  - `PanelSimulator`: Click・DragAndDrop・ScrollWheel・KeyPress・TabKeyPress・TypingText・FrameUpdateMs
- 画面ごとに `FakeXxxModel`（ReactiveProperty で組んだフィクスチャ）と `RecordingXxxCommands`（発行されたコマンドの記録）を作る。Playwright の `setUiState` と `payloadsOf` の代わり
- `panelSize` は基準の 1280×720、`themeStyleSheet` には本番のテーマ USS、FontAsset も本番のものを当てる。描画しないので、半透明と文字の質は L4 で見る
- 時間はパネルのスケジューラか、差し込んだ時計で動かす。`MonoBehaviour.Update` と `Time.deltaTime` で動かすと、`FrameUpdateMs` で時間を進められない
- CI の EditMode シャードに載せる。Playwright は今 CI で実行されていないので、検証の面はむしろ広がる（tests §5.5）

### 4.5 録画プレイテスト（L4）とアンカーの付け替え

- DSL の `IUiElementLocator` は Web 版と UITK 版の 2 本。UITK を先に探し、同じ ID が両方で見つかったら、二重描画として理由をログに出して失敗させる（黙って先勝ちにしない）
- testid は、VisualElement の name に同じ綴りで付ける。シナリオの約 143 か所を変えずに済む。`data-selected` などの属性の照合は USS のクラスに置き換える
- アンカー
  - `web/shared/tutorialAnchor/anchorIds.ts`（静的 19 個・動的 prefix 6 種）を C# の `TutorialAnchorIds` に移し、唯一の正本にする。共存中は Web 側と突き合わせるテストで同期を守る（`TutorialAnchorContractTest` を全 ID へ広げ、段階 6 で消す）
  - `TutorialAnchor.Mark(element, ids)` で USS のクラスとして付け、Attach と Detach のイベントで登録表に出し入れする。見つからなければ missing、2 件以上なら duplicate
  - マスタの ID の文字列は変えない（v8 で 9 件。`.decisions/2026-08-19-チュートリアルUI指定はWebアンカーIDを直書きし変換も検証も持たない.md` と矛盾しない）。直すのはスキーマ `VanillaSchema/challenges.yml` の説明文の「web anchor id」という呼び名だけ
  - 共存中の解決: 表示内容のストア（`TutorialPresentationStateStore`）は描画系に依存しないので、Web と UITK がそれぞれ自分の木の中で探して描く。共通のリゾルバは作らない。系をまたぐ dragGuide は新設しない
  - 祖先のクリップ（ADR 0024）: `overflow: hidden` を持つ祖先の `worldBound` との交差で求める。判定役テストを UITK で 1 本作る（`.decisions/2026-08-20-チュートリアルハイライトのマスク方式とテスト方針.md` の型）
  - 仮想化した ListView の行に置くアンカーは、画面外では存在しない。チュートリアルが指す一覧（ビルドメニュー・アイテム一覧・研究）は仮想化しないか、Bind と Unbind でクラスを付け替え、missing の扱いを決める

### 4.6 実機での確認を配布の工程に組み込む

- IME（入力欄 4 つ）、4K、UI スケール、Windows の入力は、プレイテスト配布の工程（Windows 検証機の通し検証・遠隔実行・Mac mini の Steam）の確認項目に入れる。チェックボックスで後回しにしない（過去に 3 か月滞留した。shipping §6.2）

---

## 5. 撤去の順序と完了の定義

### 5.1 段ごとに消すもの

| 段 | 消すもの |
|---|---|
| 0 | `com.singtaa.onejs`、`com.annulusgames.debug-ui` |
| 1 | Web の `features/pauseMenu`・`features/settings` とそのテスト、ポーズ由来の Web の Esc 経路 |
| 2 | Web の `features/commonHud`・`progress`・`keyHint`・`modeHud`・`trainHud`・`notification`・`skit`、CursorTooltip、WorldPinOverlay、KeyControlHintHud。旧 UITK の SkitUI 一式と `SkitPresentationMode.WebUiEnabled` |
| 3 | Web の `features/recipe`、`features/blockInventory` の列車系以外 |
| 4 | Web の `features/buildMenu`・`features/hotbar`・`features/modal`、`BlueprintNameInputWebBridge` の Web 送り部分 |
| 5 | Web の `features/inventory`・`research`・`challenge`・`blockInventory/train`・`eventLanguageGate`・TutorialOverlay、`shared` の残り |
| 6 | 残りすべて（§5.2） |

### 5.2 段階 6 の撤去の順序

依存の向き（host §6.2）に沿って、上から順に消す。

1. 遠隔実行の待ち受けを Kestrel から外し終えていること（0-RX）
2. 共用部品を移す。`CefLfsPointer` は中立の名前に変えて残す（ffmpeg と Harmony の同梱検査が使う）。`EnvironmentScrubOnEditorLoad` は `Client.ExternalProcess` 側へ移すか、要否を確かめる。`Client.ExternalProcess` は残す（ビルド工程が使う）
3. `Client.Starter` の起動配線（`WebUiStartup`、`InitializeScenePipeline.cs:73` の KestrelPort、`MainGameInitializationFinalizer.cs:57` の Bind、`EventModeStartGate` の Binder）
4. `Client.WebUiHost`（asmdef ごと）、`Client.Playtest/WebUi` と DSL の Web 実装、`Client.Tests` の WebUiHost 参照。`WebUiGateAuditTest` は「新規 uGUI 禁止」の安全網として対象を付け替えて残す（ADR 0052 の agent 前提 2）
5. `jp.juha.cefunity` と、`MainGameUI.prefab` の CEF の Canvas・RawImage・`CefUnityBrowserSample`・`WebUiCefNavigator`（`uloop execute-dynamic-code` で）
6. `CefRuntimeBundler` と `BuildPipeline.cs:90` の呼び出し、`CefPackageLfsValidator`、`scripts/setup-cef.*`、CI の cef-unity の取得と pnpm の準備（`.github/workflows/build.yml`）、`scripts/playtest/lib/release-artifact.sh` の CEF helper の検査と契約テスト、`scripts/playtest/README.md` の CEF raw input の確認。すべて同じ PR で
7. NuGet の `Microsoft.AspNetCore.*`（遠隔実行を HttpListener に載せ替えた場合）と、使われなくなった `Microsoft.Extensions.*`（依存グラフを確かめてから）
8. `moorestech_web/` と、CI の `webui-test` ジョブと `needs_webui`
9. `activeInputHandler` を InputSystem だけに寄せられるかを確かめて裁定に出す（IME の実装次第。推測）
10. 文書とスキル: `docs/webui/*` を archive へ、`ref-webui-design` を `ref-uitk-design` へ、moores-code-review の ts/tsx 観点を撤去、`docs/development/macmini/unity-editor.md` の CEF の節
11. 名前: `WebPinId`（13 箇所）、`LocalizationKeys` の `WebModal*`、所有表の Web 列（`WebUiInputExclusivity` の改名は段階 1 で済ませる）
12. ADR と裁定の改訂の記録（§7）

### 5.3 完了の定義（観測できる形）

- [ ] manifest と lock に `jp.juha.cefunity` と `com.singtaa.onejs` が無い
- [ ] `moorestech_client/Assets/Scripts` を `CefUnity|Client\.WebUiHost|WebUi` で grep して 0 件（docs の archive を除く）
- [ ] `moorestech_web/` が無い。CI に webui-test・pnpm・cef-unity の手順が無い
- [ ] 配布ビルドのログに CEF のファイル（Windows の `Plugins/x86_64`、Mac の `PlugIns/cef-unity-server.app`）が無い
- [ ] 遠隔実行が配布ビルドで動く（`scripts/playtest/remote-exec.sh` で検証機に 1 回）
- [ ] 録画シナリオがすべて UITK のロケータで通り、DSL に Web の実装が無い
- [ ] 受入表の全行が分類済みで、L0/L1 が CI で緑
- [ ] 共存のための仕組み（所有表、二重のロケータ、`WebUiHealth`、通知層を隠す処理）が消えている。過去に「恒久 true にしたゲート参照 26 箇所が死に分岐で残った」ことの再発を防ぐため（shipping §6.2）
- [ ] ADR 0052 の例外 1（CEF の描画面）が消えた改訂がある。`com.unity.ugui` は例外 2〜4（MainMenu とローディング・HP バー・デバッグ UI）のために残る
- [ ] CEF の無い配布ビルドで、Windows 検証機の通し検証と Mac mini の Steam での確認が通る
- [ ] CEF 無し起動ラダーの 5 本が、CEF の存在しないビルドでそのまま通る

議論記録の完了の定義（CEF・Kestrel・`Client.WebUiHost`・`moorestech_web`・`CefRuntimeBundler` の撤去）に足したもの: 遠隔実行の載せ替え、共用部品の移設、DSL の付け替え、CI とリリース検査、NuGet、共存の仕組みの撤去、ADR の改訂。「Kestrel の削除」は、遠隔実行を載せ替えたときだけ成り立つ（D10）。

---

## 6. ユーザーの裁定が要る論点

期限は「どの段に入る前に要るか」。

| # | 論点 | 選択肢 | 推奨と理由 | 期限 | 矛盾する既存の裁定 |
|---|---|---|---|---|---|
| D1 | 移行中の出荷形 | A. 画面単位の strangler で毎週の playtest に出す／B. 移行ブランチに溜めて一括で入れる | **A**。命綱を数週間で届けられる。B だと数か月 CEF のままで、その間の CEF の停止やメモリの増加をすべてテスターが被る。代償は見た目の混在と、共存の仕組み（段階 6 で消す） | 段 1 の前 | なし（議論記録の未裁定論点） |
| D2 | 第一弾の画面 | A. ポーズ一式（命綱）／B. インベントリの束／C. ホットバーとビルドメニュー／D. 入力の無い HUD | **A**。失うものが最も大きい口を先に守れる。束 D は単独で切れる。IME・Esc・フォーカス・ローカライズ・フォントを、掴み抜きで先に通せる。B と C は難度が高く、テスターに届くのが数か月後。D は CEF が死んだときに救えるものが少ない | 段 1 の前 | 議論記録の「インベントリかホットバー」（未裁定の案）とは異なる |
| D3 | 共存中に、同じ部品の描画者を状態で分けてよいか（ホットバーは BuildMenu だけ Web、装備 HUD はインベントリ系だけ Web、subInventory は列車系だけ Web） | A. よい（所有表とテストで管理し、段階 6 で消す）／B. だめ（束ごと一度に移す） | **A**。段階 2 でゲーム画面を CEF から外せ、段階 3 を列車抜きで早く出せる。新しく書く捨てコードは無い（Web 側は既存のまま、描く状態を絞るだけ）。B だと、ゲーム画面の CEF への依存が段階 4 まで残る。代償は所有表の管理と、ビルドメニューを開いたときにホットバーの見た目が切り替わること | 段 2 の前 | なし |
| D4 | 共存中のパネルの構成と前後 | A. UITK のパネル 1 枚を CEF の前に置き、Web の全画面の間は通知を隠す／B. 2 枚で CEF を挟む（背面に HUD と通知、前面にポーズなど）／C. 1 枚を CEF の後ろに置く | **A**（0-a の実測が前提）。最終形と同じ 1 枚なので、Pick とフォーカスが 1 枚の中で閉じる。Web の常駐 HUD はもともと stage より前なので、ずれるのは通知だけ。C だと UITK のポーズが Web のスキット窓に隠れる | 段 1 の前 | ADR 0017（通知は全画面 UI の裏）を、段 2〜5 の間だけ「隠す」で代える |
| D5 | 文言の差し込み記法 | A. 名前付きに統一（位置引数の 32 キーと C# の呼び出し元を書き換え）／B. C# に名前付きを足して 2 記法を併存させる／C. 位置引数に統一 | **A**。UITK 側の主な利用者（Web から移る 44 キー）がすでに名前付き。名前付きは語順の違う翻訳に強い。mod の CSV に差し込みは 0 件で波及しない。B は 2 記法が恒久に残る（負債が残る）。C は翻訳者に意味が見えなくなる | 段 1 の前 | なし |
| D6 | 画像アセットの規則の読み替え（ref-webui-design「UI 装飾の画像アセット化は禁止。CSS/DOM/インライン SVG で再現する」） | A. 面と装飾は Painter2D と頂点色メッシュ、アイコン（スキットの 6 種・×・ピン・矢印）は SVG を VectorImage として許す／B. すべて Painter2D のコードで描く／C. 9-slice などのビットマップも許す | **A**。インライン SVG を許していた規則の趣旨（ビットマップを増やさない）を保てる。B はアイコン 9 種を手で描き直す工数がかかる。C は色トークンから切り離され、規則の趣旨に反する | 段 2 の前 | ref-webui-design の文言を UITK 向けに改める |
| D7 | トークンの正本 | A. USS（C# は customStyle で読む）／B. C# の定数から USS を生成する | **A**。トークンを変えるたびにコンパイルとドメインリロードが走らない（AI の作法）。B は導出値（calc の 70 箇所）を型安全に書けるが、生成と一致テストの 2 段になる | 段 1 の前 | なし |
| D8 | アニメーション | A. 自作の UiMotion／B. LitMotion v2（MIT、UITK 用の Bind が 25 種、手動ディスパッチャがある） | **A**。要件は通知の出入り・脈動・誘導の 3 系統と、smooth scroll・慣性だけ。時計を差し込んで L1 で決定的に進められる。外部の版上げに振り回されない。B でも決定性は保てるので、選んでも計画は変わらない | 段 2 の前 | なし（裁定「導入か自作」の範囲内） |
| D9 | Linear 色空間での半透明と、小さい文字 | A. 段 1 で中核のトークン（`--surface-navy`・`--tooltip-face`・幕）と最小のフォントだけ実機で合わせ、残りは段 7／B. α を近似式（2.2 乗）で一括で変換する／C. パネルを sRGB の RenderTexture に描いて合成する（要 PoC） | **A**。テスターが読む所（ツールチップと幕が薄くなる）だけを先に直す。B は背景の色で誤差が出る。C は描画の経路が増え、「環境差で壊れない」に反する恐れがある（推測） | 段 1 の途中 | なし |
| D10 | 遠隔実行の待ち受け | A. `System.Net.HttpListener` に載せ替え、AspNetCore 系の NuGet を外す／B. Kestrel を遠隔実行専用の最小のホストとして残す | **A**（Mono での挙動をテストで確かめ、だめなら B）。完了の定義の「Kestrel の削除」が成り立ち、NuGet（AspNetCore の DLL 24 個）を外せる。今は Web ホストの起動失敗で遠隔実行も開かず、検証機で CEF の不具合を調べる手段まで失う | 段 6 の前（推奨は段 0 で並走） | ADR 0072「入口は既存の Web UI サーバー（Kestrel）に 1 本足す」を改訂 |
| D11 | メインメニューとローディング | A. uGUI のまま、この移行の範囲外／B. この移行に含める | **A**。テスターの実害が出ていない（参加同意のゲートも uGUI で作ったばかり）。ただし残した理由（CEF の起動位置）は消えるので、メインメニューの作り替えのタスクと合わせて後で決める。B を選ぶ場合も段 7 の後 | 段 6 の後 | なし（`.decisions/2026-09-05-メインメニューとロード画面はuGUI現状維持.md` と一致） |
| D12 | 移行期間の Web への変更 | A. 新しい UI は UITK だけに作り、Web はテスターを止める不具合だけ直す／B. Web を全面的に凍結する／C. 従来どおり | **A**。移行中も遊べる状態を守れる（B だと CEF 由来の致命的な不具合も直せない）。C だと移す対象が増え続ける。ゲートで `web/features` への新規ファイルを落とす | 段 0 | `.decisions/2026-08-17-uGUI移植を終了しWeb-UIは独自調整へ移る.md`（10-18 の裁定で覆り済み） |
| D13 | CEF の延命策 | A. 停止の検知と知らせだけ入れる（§3.11）／B. 何もしない／C. 自動再起動・Ctrl+ホイールの拡縮の封じ・raw input まで直す | **A**。気づく経路の無い失敗は直す（AGENTS.md）。C は撤去する対象への投資。Ctrl+ホイールの拡縮は、Web 側で封じられるなら安く済み、テスターの不満も出ているので、A に足してよい（CEF の OSR でページズームが Web の preventDefault で止まるかは未確認。推測） | 段 1 | `.decisions/2026-09-13-CEF raw input応急処置は入れず…md` とは一致（raw input は直さずに消す） |
| D14 | 0-f で IME が UITK の TextField で成立しなかった場合 | A. IME の合成文字列を自前で扱う TextField を作る／B. 文字入力のある画面（バグ報告・検索・駅名・BP 名）だけ Web に残す | 段 0 の結果が出てから決める。B は完了を遅らせ、命綱の報告文を日本語で書けない期間を生む | 段 1 の前（不成立のときだけ） | なし |
| D15 | UI の演出の時間源 | A. 実時間（パネルのスケジューラ）／B. timeScale | **A**。ポーズ中もポーズ画面の演出は動くべき。AGENTS.md の「経過時間は GameUpdater のティック加算だけ」は、サーバーのゲームロジックの規則と読める（この解釈の確認を兼ねる） | 段 2 の前 | なし（解釈の確認） |
| D16 | 段 6 の後の配布先 | Mac の Universal 化（ffmpeg の x64 版が要る。ADR 0071）、Linux クライアントの再開（`build.yml:185`）、`com.unity.ugui` の扱い | 段 6 が終わってから決める。Intel Mac のテスターが遊べるようになる（テスターにとっての価値）。CEF が消えても、arm64 専用の理由は ffmpeg に残る | 段 6 の後 | ADR 0071 の理由の改訂 |

---

## 7. 既存の裁定・ADR との関係

| 裁定・ADR | この計画での扱い | 矛盾 | いつ |
|---|---|---|---|
| `.decisions/2026-10-18-CEFを撤去しUI-Toolkitへ全面移行する.md` | 前提 | — | — |
| `.decisions/2026-08-17-uGUI移植を終了しWeb-UIは独自調整へ移る.md`、`…クラフトUI改修はWebUI専任…` | 10-18 の裁定で覆り済み | 解消済み | — |
| ADR 0052 の例外 1（CEF の描画面） | 段 6 で消える。agent 前提 2（ゲートの縮小）は、Web の凍結へ広げる | 改訂が要る | 段 6 |
| `.decisions/2026-09-05-メインメニューとロード画面はuGUI現状維持.md` | 維持（D11）。残した理由は消える | なし | 段 6 の後に再検討 |
| ADR 0006 の決定 5（Web 側の Guid 辞書で解決） | ビュー側（UITK の Presenter）での解決に変える | **矛盾** | 段 1 |
| ADR 0013・0017（stage 族・viewport 族・背面 viewport 族） | 層として継ぐ。段 2〜5 は通知を隠す一時的な逸脱 | ADR 0017 と**一時的に矛盾** | 段 2〜5、段 7 で書き直す |
| ADR 0016（通知の出入り） | 継ぐ（実装の手段だけ変わる） | なし | 段 2 |
| ADR 0019（ツールチップの書式は Web が持つ） | 持ち主を UITK の USS トークンへ変える | **矛盾**（持ち主の変更） | 段 2 |
| ADR 0020（ロック時にカーソルをワープ） | ワープの前提が要らなくなる見込み（推測） | 確認が要る | 段 2 |
| ADR 0021（テキストの選択は入力欄だけ） | UITK の既定の挙動と同じになる | 廃止の記録 | 段 6 |
| ADR 0024（祖先のクリップ） | 作り直し、判定役テストを移す | なし | 段 2 |
| ADR 0039（チュートリアルの赤と脈動） | 継ぐ | なし | 段 2 |
| ADR 0071（Mac の arm64 の理由が CEF） | 段 6 の後、理由が ffmpeg だけになる | 改訂が要る | 段 6 の後 |
| ADR 0072（遠隔実行の入口は Web UI の Kestrel） | HttpListener へ載せ替える（D10） | **矛盾** | 段 0〜6 |
| ADR 0074（要素が付いたときにツールチップの位置を再計算） | 同じフレームで置くので要らなくなる見込み（推測） | 確認が要る | 段 2 |
| `.decisions/2026-08-18-チュートリアル提示はWebUI経路に統一しD&Dは矢印ループで示す.md` | UITK の経路にする | **矛盾**（経路の変更） | 段 2 |
| `.decisions/2026-08-19-獲得通知の集約はWebUI側で行う.md` | C# の通知モデルへ移す | **矛盾**（置き場の変更） | 段 2 |
| `.decisions/2026-08-19-カーソルツールチップの書式はWeb側が持つ.md` | UITK の USS トークンへ | **矛盾**（持ち主の変更） | 段 2 |
| `.decisions/2026-08-19-チュートリアルUI指定はWebアンカーIDを直書きし変換も検証も持たない.md` | 維持（文字列を変えない）。呼び名だけ「UI アンカー」に | なし | 段 2 |
| `.decisions/2026-08-26-Web操作音はC#へsound.playを送って鳴らす.md` | ビューから直接鳴らす | 形の変更 | 段 1〜5 |
| `.decisions/2026-09-08-uGUI追加禁止はトークン検査と分類粒度の両方で守る.md` | 継ぎ、Web の features への新規ファイルの禁止を足す | なし | 段 0 |
| `.decisions/2026-09-13-CEF raw input応急処置は入れず…md` | 一致（直さずに消す） | なし | 段 6 |
| `.decisions/2026-08-23-CEF未接続は別issueとしStep3は13-15で完了とする.md` | 段 6 で問題ごと消える | なし | 段 6 |
| `.decisions/2026-10-01-Web UI全体で文字入力欄以外のSpace既定動作を封じる.md` | UITK でも同じ要件にして L1 で固定する | なし | 段 1 |
| ref-webui-design（画像アセットの禁止・§6 のアニメ） | D6 で読み替える。§6 は継ぐ | 文言の改訂 | 段 2 |
| AGENTS.md の時間の規則 | UI の演出は実時間（D15） | 解釈の確認 | 段 2 |
| bd の CEF 関連（`moorestech-1gc8`・`-3xq6z`・`-7179.1/.2/.3`・`-rhvub.3`・`-vfn3o.19`・`-s2zk.1`・`-lc9ty.3`・`-rtilw.3`）、`-lnsf.8`（旧 SkitUI）、`-fn8`（操作ヒントの Web 再実装）、`-cgm`（トーストの様式） | 行き先を UITK に変えるか、撤去で閉じる。bd への記入はユーザーかメインのセッションで行う | — | 各段 |

---

## 8. 推測・未実測の一覧（段階 0 で潰すもの）

| 項目 | 確かめ方 | 外れたときの影響 |
|---|---|---|
| UITK のパネルと CEF の uGUI Canvas の前後を sortingOrder で決められる | 0-a。共存中の画面で sortingOrder を変えて目視 | D4 の前提が崩れる。段 1 に入れない |
| UITK の上で `IsPointerOverGameObject` と `panel.Pick` が期待どおりに返す | 0-b。PlayMode で判定値をログに出す | §3.8 の判定式を変える |
| Unity Recorder とバグ報告の録画に UITK が写る | 0-c。録画テスト 1 本 | 録画プレイテストで移行を検証できない |
| Navigate・Submit・Tab を専用のアクション資産で止められる | 0-d。ボタンのクリック後に WASD・Space・Tab を注入する | WASD で歩くとフォーカスが動く |
| TextField の上の Esc の処理順 | 0-e。同じフレームのログの順 | Esc のラッチの設計が変わる |
| 日本語 IME が UITK の TextField で使える（Windows と macOS） | 0-f。人手 | D14 |
| UI Test Framework が CI の batchmode で動き、アニメが `FrameUpdateMs` で進む | 0-g | L1 を L3 に寄せる（遅くなる） |
| PanelSettings の Expand が Web の倍率と一致する | 0-h | viewport 族の配置を C# で計算する |
| CEF の IME の常時 ON が UITK と取り合わない | 0-i | 共存中の文字入力に不具合が出る |
| ゲーム画面で CEF が何も描かなければ busy-wait が減る | 段 2 の前後で `block_avg` を比べる | 段 2 の FPS の効果が出ない（命綱と HUD の生存の効果は残る） |
| CEF が固まると最後のフレームが画面に残る | 検証機で CEF を止めて目視 | 凍った絵を隠す処理が要らなくなるだけ |
| Ctrl+ホイールのページズームを Web の preventDefault で止められる | Web で 1 行足して確かめる | D13 の追加分をやめる |
| `HttpListener` が Mono の配布ビルドで遠隔実行に使える | 0-RX のテスト | D10 を B（最小の Kestrel）に変える |
| 研究ツリーのノード数が TreeView の性能の範囲内 | 段 5 の前に数える | 仮想化か描画の間引きが要る |
| 各段の規模（行数・ファイル数・PR 数） | Web の行数の 1.2〜1.5 倍で見積もった。PoC の比（ツールチップ約 90 行、D&D 約 140 行）からの推測 | 段の分け方は変わらず、期間だけが延びる |

---

## 付録: この計画が人手を要する所

- 0-f（IME の実機確認。Windows と macOS）
- 段階 1 の「ゲーム中に CEF を落とす」確認（Windows 検証機で 1 回）
- §6 の裁定（D1〜D7・D10・D12・D13 は段階 1 に入る前に要る）
