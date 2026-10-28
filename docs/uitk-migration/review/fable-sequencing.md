# 計画書レビュー: 順序・依存・実現性（Fable）

- 対象: `docs/uitk-migration/drafts/plan-v1.md`（2026-10-23 版）
- 根拠の repo: moorestech リポジトリ master `e8e611f1c0`（読むだけ。パスは repo 相対、`Scripts/` = `moorestech_client/Assets/Scripts/`、`web/` = `moorestech_web/webui/src/`）
- 指摘 6 件。重要な順。

---

## 指摘

### 1. 欠落／順序: ラダー 1・4 の「日本語で入力する」に、DSL の経路が無い

- 対象: §4.4 ラダー表（`uitk-lifeline`「日本語でバグ報告」、`uitk-research-train`「駅名を日本語で付ける」）、§2.2 完了条件、§2.1 0-7、§2.5 完了条件
- 根拠:
  - 今の DSL の Web 操作は Click・Hover・Wait・ScrollDown・WaitTextContains だけ（`Scripts/Client.Playtest/Operations/PlaytestWebUiOps.cs:28-83`）。キー注入も `KeyDown`/`KeyUp`/`TapKey`（`Scripts/Client.Playtest/Input/SemanticInput.cs:35-61`）で、文字列を打つ口は無い
  - 既存シナリオはこれを回避している。`.agents/skills/unity-playmode-recorded-playtest/scenarios/building/blueprint-copy-paste-via-ui.cs:2`「DSL cannot type into the web modal」、名前は `nameState.Confirm("chest-bp")`（同 :99）で state に直接書いている
  - 計画の 0-7 は `IUiElementLocator` を「Find（可視・矩形・文字列・状態フラグ）」と `ClickUi` 系に限り（§2.1）、§4.4 の UITK 版手順 1〜4 もポインタ操作だけ
- 何が起きるか: 段階 1 の完了条件「ラダー 1 が緑」を DSL で満たせない。担当 agent は BP シナリオと同じく state 直書きへ逃げ（UITK の TextField → 送信の経路が録画で一度も通らないまま出荷）、または段階 1 の終わりで DSL を作り足して止まる。段階 4 の駅名も同じ
- 最小の直し方: 0-7 の `IUiElementLocator` に `SetText(testid, text)`（UITK は `TextField.value` を設定して ChangeEvent を流す。IME は通さない）を 1 つ足し、§4.4 のラダー 1・4 に「本文と駅名は SetText で入れ、IME は §4.9 の人手」と書く

### 2. 順序／見積: 段階 0 の Editor 需要が予算 3 本を超え、実測 15 本の置き場が段階 0 に偏っている

- 対象: §2.1「規模と並列」、§4.8、§3.13「Editor の予算」
- 根拠:
  - §2.1 は「4〜5 本で並列（Editor 3）」に加えて「実測は別に Editor を 1〜2 本使う」と書く。遠隔実行・状態移設・DSL の 3 レーンは AGENTS.md のコンパイル必須と録画回帰（§2.1 リスク表）で各 1 本の Editor を要するので、同時 4〜5 本になり §3.13 の「同時 3 本が目安」と食い違う
  - S1〜S15 のうち Editor を要するのは S9（実機）・S12（dotnet）・S14（検証機）以外の 12 本。1 本 1 日で 12 Editor 日。段階 0 の 1.5〜2 週（8〜10 営業日）に 3 レーン分の Editor 占有を足すと 36〜42 Editor 日の需要に対し供給は 24〜30
  - 一方、計画自身が消費先を後段に置いている実測がある: S7 → D10（段階 1 の途中）、S8 → UiMotion（段階 2）、S10 → フォント（段階 1）、S11 → D12（段階 1）、S6 → PanelSettings（段階 1）、S12 → L0 レーン
- 何が起きるか: 段階 0 が 1.5〜2 週で終わらず、しかも遅れる原因が「段階 1 の前提ではない実測」になる。あるいは Editor 4〜5 本で走らせて Mac mini のメモリで Editor が落ち、`docs/development/macmini/unity-editor.md` の「ログを残さず消える」を踏む
- 最小の直し方: §4.8 の表に「どの段で行うか」を付け、段階 0 に残すのは段階 1 の前提（S1・S2・S3・S4・S5・S9・S13・S14・S15）だけにする。S6・S7・S10・S11 は段階 1 のテーマ／フォント／入力レーンの先頭、S8 は段階 2 の UiMotion、S12 は L0 レーンの先頭へ移す。これで段階 0 の Editor 需要は 3 レーン＋実測 1 本に収まる

### 3. 順序: 段階 3 の暗転が、段階 2 で作る UiMotion に依存する

- 対象: §2.4「作業」（「束の中身は段階 2 の間に暗転で作り始め」）、§3.4 の表（UiMotion は段階 2）、§3.6（smooth scroll・慣性は UiMotion）、§2.5 4-β(5)（TreeView の慣性 τ=325ms）
- 根拠: 段階 3 のビルドメニューは「スクロールスパイ・smooth scroll」（§2.4 対象）を持ち、§3.6 はそれを自作 `UiMotion` で書くと決めている。UiMotion は §3.4 で段階 2 の部品、§2.3 では「通知と UiMotion」レーンの成果物。段階 3 の暗転は段階 2 と同時に始まる
- 何が起きるか: ビルドメニューのレーンが UiMotion のマージを待つか、自前の補間を書く。後者だと動きの実装が 2 系統になり、§3.4「部品は作る段で完成形」と §3.13-4「部品の昇格」の前提（1 機能だけが使う部品は後で昇格）を崩す。段階 4 の研究ツリー（慣性）も同じ依存を持つ
- 最小の直し方: §3.4 の表で UiMotion（D11、100〜150 行）を段階 1 の行へ移す。または「段階 2 の先頭 PR として integrator が出し、段階 3 の暗転レーンはそのマージ後に始める」と §2.4 に 1 行書く

### 4. 順序: 段階 6 を 6b → 6c の順に出すと、prefab の missing script と CEF の起動が 1 周期残る

- 対象: §2.7 の PR 表と 6b・6c の詳細、§5.2
- 根拠:
  - `MainGameUI.prefab:106` は guid `592d2edb173e04ee4a91f67a25b875f1` の MonoBehaviour を持ち、これは `Scripts/Client.WebUiHost/Cef/WebUiCefNavigator.cs.meta` の guid。6b は `Client.WebUiHost` を asmdef ごと消すが、prefab の CEF 一式を外すのは 6c
  - 6a で起動配線を外した後も、prefab の `CefUnityBrowserSample` は CEF プロセスを起動する。`WebUiCefNavigator` は URL が空なら何もしない（`WebUiCefNavigator.cs:42-43`）ので、`_url` の about:blank のまま CEF が立ち上がり続ける
  - `CefUnity` を参照する .cs は 4 本だけ（`WebUiCefNavigator.cs`、`Client.Playtest/WebUi/CefScreenMapper.cs`、同 `CefInputForwarder.cs`、`Editor/Build/Bundlers/CefRuntimeBundler.cs`）
- 何が起きるか: 6b がマージされた周期の playtest ビルドは、スクリプト欠損の prefab を抱えたまま CEF プロセスを起動する。描くものが無いのに helper のメモリと起動失敗の経路（移行の動機そのもの）が残り、§5.3-13「段階 6 の後 1 周期で UI 起因の不具合 0」の観察が 1 周期ずれる
- 最小の直し方: §5.2 の順を 6a → 6c → 6b → 6d → 6e にし、`CefUnity` を参照する上の 3 ファイルを 6c の削除対象へ移す。6c の後は Web ホストが接続先の無いまま起動するだけで害は無い

### 5. 過剰／順序: 旧 UITK の SkitUI の撤去は、段階 2 でなく段階 0 の純削除でよい

- 対象: §2.3「スキット」（`SkitUI`・`SkitPresentationMode` の撤去）、§3.1「パネルは 1 枚」、§5.1
- 根拠:
  - `Assets/Asset/Skit/SkitUI.prefab` は UIDocument が有効（`m_Enabled: 1`、`SkitPanelSettings` 1920×1080、sortingOrder 0）で、`MainGameStarter.cs:88` の SerializeField からシーンに生きている。つまり今も UITK パネルが 1 枚、CEF の Canvas と同じ sortingOrder 0 で存在する
  - `SkitManager.cs:197` が `new SkitPresentationMode(true)` を固定で登録しており、`ShowTextCommand.cs:12-15`・`SelectionCommand.cs:15,53`・`TransitionCommand.cs:22`・`TextCommand.cs:39` の非 Web 分岐は死にコード
- 削ったときに減るもの: 段階 1 で `GameUiRoot` を足した時点から「パネルは 1 枚」が真になり、S1（パネルと CEF Canvas の前後）の計測対象が GameUiRoot だけになる。段階 2 のスキットレーンは「作る」だけになり、prefab・シーンの uloop 編集（integrator 直列）が段階 1 の `GameUiRoot` 配置と同じ 1 本にまとまる。挙動は変わらない（分岐が全て `true` 側で固定のため）
- 最小の直し方: §2.1 の表に「0-12 旧 SkitUI（prefab の UIDocument・SkitPanelSettings）と `SkitPresentationMode` の撤去（bd `moorestech-lnsf.8`）」を足し、§2.3・§5.1 の該当行を段階 0 へ移す

### 6. 順序: 段階 1 の 3 レーンは integrator の骨組みに依存し、同時には始められない

- 対象: §2.2「規模と並列」、§3.13-2・3
- 根拠: 画面レーン（ポーズ・設定・バグ報告・WebUiHealth）は UI ルート・層・ルータ・所有表・`LocalizedLabel`・TextField・トースト・`UiTestHost` を使う。これらは §3.13 で integrator の直列所有（層 enum・ルータ・所有表・composition root・Tokens・AnchorIds・DSL ロケータ本体）か、他の 2 レーン（テーマ・フォント／入力・DSL・テストの土台）の成果物。§2.2 は「ほかに 3 本を並べる」とだけ書き、着手順が無い
- 何が起きるか: 画面レーンが骨組み前に始まると、`LocalizedLabel` や TextField を自前で持つか、integrator の共有ファイルに触る（§3.13 の依頼文で禁止）。2.5〜3 週の見積は 3 レーン同時開始を前提にしており、実態は「骨組み → 部品 → 画面」の直列が 1 段隠れている
- 最小の直し方: §2.2「規模と並列」に「integrator が骨組み PR（UI ルート・層・ルータ・所有表・`UiTestHost`・Tokens 初版）を先に 1 本出し、3 レーンはそのマージ後に始める」と 1 行足す。期間はそのままでよい（骨組みは 2〜3 日の見込み）

---

## 確認した前提（指摘なし。計画のとおりで進められる）

- 命綱（段階 1）は後段の仕組みに依存しない。依存先はすべて `Client.Game`／`Client.Input`／`Client.Localization` にある: セーブと終了は `GameSaveRequester`・`SaveAndQuitPresenter`（`Scripts/Client.WebUiHost/Game/Actions/PauseMenuActions.cs:10-41`）、報告は `BugReportSubmitter.SubmitAsync(description, kind)`（`BugReportSubmitActionHandler.cs:45`）、設定は `GraphicsQualityPreference`・`SprintInputModePreference`・`Localize.TrySetChosenLanguage`（`Settings/GraphicsQualityActions.cs:27,59`、`Settings/SettingsActions.cs:38`、`Localization/LocalizationActions.cs:35`）。WebUiHost 側のハンドラは JSON の解釈だけなので、§2.2「型付きコマンドと JSON アダプタに分ける」は小さい
- ESC は C# が持つ: `PauseMenuStateService.HandleCloseKey`（`:27`）、`PlayerInventoryState.cs:45`、`GameScreenState.cs:45`。Web の ESC は入力欄の blur だけ（`docs/webui/design/input-focus-exclusivity.md`「フォーカス中の Esc」）。§2.2 リスク表の「ESC は C# の UIState に一本化」は現状どおり
- 開幕スキットの skip は C# の `SkitPresentationStateStore.TrySkip`（`Scripts/Client.Playtest/Operations/Ui/PlaytestSkitOps.cs:33`）で、Web 無しでもラダー 1 は開幕を抜けられる。数字キーの設置は C#（`GameScreenState.cs:59` `HotbarTapInputService`）で、ラダー 2 の「ホットバー表示なしで数字キー設置」も成立する
- 束の切り方は repo と一致する: インベントリ→ホットバーの D&D は設計上存在しない（`web/features/hotbar/hotbarDnd.ts:7`）。`researchTree` は `grab: true, alwaysOnHud: false`、`subInventory` は `grab: true`（`web/shared/uiState/uiScreenRouting.ts:20-28`）。`BlockInventoryPanel` は常時マウント（`web/app/App.tsx:97`）。dragGuide の from/to はマスタ由来で 0 件（`Scripts/Client.Game/InGame/Tutorial/UIHighlight/UiDragGuideTutorialManager.cs:16`）
- 0-10（Web ホスト無し起動）は Error 0 と両立する: `WebUiGameBinder.Bind` は hub null で LogWarning（`WebUiGameBinder.cs:48-55`）、`EventModeStartGate` の LogError は出展モード時だけ（`EventModeStartGate.cs:41`）、`PlaytestRunner.cs:50` と `PlaytestDomQuery.cs:38-45,55-56` は Web 無しを許容する
- 共存中の入力の取り合いの前提は正しい: CEF は Unity の `Input.mousePosition` とキー入力から入力を受ける（PackageCache `jp.juha.cefunity@2355a29d59ed/Runtime/CefUnityBrowserSample.cs:943-1064`）ので、UITK が消費したクリック・キーも CEF に届く。防波堤は R1 の `pointer-events: none`（S15）だけ、という計画の読みどおり。Web の window keydown は `RecipeSelectionKeyHandler` 1 本（`useGameLayerKeydown` の使用箇所）なので、キーの二重処理面は小さい
- `UiPointerHitTest.IsPointerOverAnyUi` は `EventSystem.current.IsPointerOverGameObject()` を含み（`:16-18`）、MainGame.unity の EventSystem は `InputSystemUIInputModule` + `DefaultInputActions`（`:8435`）。S3 が通れば uGUI 経由でも UITK を拾えるので、§3.8 の案 A（Pick を論理和で足す）はそれを待たずに書ける

---

## この観点での総評

- 段階 0〜6 の順と束の切り方は repo の依存（掴み・D&D・アンカー・UIState・入力経路）と一致しており、命綱は後段で作る仕組みに依存しない。
- 直すべきは「段階の中の順序」で、テキスト入力の DSL（指摘 1）・実測の置き場（2）・UiMotion の段（3）・段階 6 の PR 順（4）はいずれも段階を増やさず並べ替えで済む。
- 見積は段階 0 だけが Editor 予算と合わない（指摘 2）。他段階の期間・PR 数・行数は、Web 側の実行数（features 8.5k 行、持ち物の束で約 3.3k 行）と照らして妥当な範囲。
