# 計画書レビュー（観点: 事実と整合）

- 対象: `docs/uitk-migration/drafts/plan-v1.md`（1,420 行）
- 照合先: repo master HEAD `e8e611f1c0`（`git rev-parse HEAD` で確認。読むだけ。Editor 起動なし）、`../moorestech_master` は `.moorestech-external-revisions.json` のピン `0d129c8b` と HEAD が一致、bd、Discord ミラー（`moores-discord`）
- 抜き取りは 60 点超（末尾の一覧）。不一致は 6 点、§8.3 の抜けは 1 点、節間の矛盾は 2 点。
- 数え方はすべて末尾の一覧に書いた。「推測」と書いた所は確かめていない。

---

## 指摘（重要な順・9 件）

### 1. 誤り（事実）: 「画質設定などの Web の PR が進行中」は HEAD では既に終わっている

- 対象: §2.1 0-9、§6 #8、§8.1 D7
- 根拠:
  - `git log --oneline --since=2026-10-15 -- moorestech_web | wc -l` → 44（計画の 44 と一致）。ただしその 44 本はすべて master HEAD の祖先で、画質設定 `65e61835dd`（10-21）も `git merge-base --is-ancestor 65e61835dd HEAD` が真
  - `gh pr list --state open` → open PR は #1348（`feature/belt-conveyor-replace-impl`、最終コミット 2026-09-11）の 1 本だけ。origin の未マージ枝 6 本のうち `moorestech_web` に差分があるのは同枝の 2 ファイルのみ
- 何が起きるか: 0-9「進行中の Web の PR は完了させてから凍結する」が、存在しない PR を待つ条件になる。逆に #1348 の web 2 ファイルは凍結 G1 の後にそのまま衝突する
- 最小の直し方: 0-9・§6 #8・D7 を「10-15 以降の 44 コミットは全部 master 済み。凍結時に web を触る open PR は #1348（web 2 ファイル）だけで、G1 の許可リストに入れるか先にマージする」に書き換える

### 2. 誤り（事実）: 壊れている録画シナリオは 8 本ではなく 10 本

- 対象: §2.0 共通条件 3、§5.3 #9
- 根拠: `bd show moorestech-wwxy` の本文は 8 本だが、NOTES に「research-node-state-label.cs / server-notification-smoke.cs も削除済み研究GUIDを送るため壊れていると判明し、注記を追加済み（計10本）」
- 何が起きるか: 共通条件 3「録画シナリオが全本緑。対象外は 8 本だけ」が、段階 0 の最初の判定からその 2 本分だけ満たせない。現場で例外を 10 本に広げると、その判断が台帳に残らない
- 最小の直し方: 「壊れている 10 本（wwxy の本文 8＋NOTES 2）」に直し、§5.3 #9 の「直すか削除」の対象も 10 本にする

### 3. 欠落（§8.3 に無い既存裁定）: 差し込み記法の変更は 2026-08-26 のプレースホルダ裁定とぶつかる

- 対象: §8.1 D8、§3.9「CSV から作る型付きの書式メソッド」、§2.1 0-6、§8.3
- 根拠:
  - `.decisions/2026-08-26-プレースホルダ規約は全言語一致テストで守る.md`: 決定は「`LocalizationPlaceholderParityTest` を足す。`Localize.GetFormatted` のシグネチャは変えない」。棄却案に「引数の個数を型へ持ち上げる（生成器が `{pN}` の最大値でキー型を分けオーバーロードし、個数不一致と名前付きキーへの誤呼び出しをコンパイルエラーにする）」があり、理由は「本PRの範囲を大きく超えるので別タスク」
  - 計画の D8(a)＋§3.9 は「型付きの書式メソッドを作る。引数の漏れがコンパイルエラーになる」で、この棄却案そのもの。`Localize.GetFormatted(key, string[])` の呼び出し元（`Client.MainMenu/ConnectServer.cs:59,65`、`Client.Starter/Initialization/Progress/LoadingProgressLog.cs:24,31`）を変える
  - D8 の「既存裁定との関係」は「矛盾しない（ADR 0006 は記法を決めていない）」で、確認先が違う。`{p0}` の運用は ADR 0033:14,50・ADR 0049:8 にも書かれている
- 何が起きるか: 0-6 の PR が moores-code-review の裁定照合で 08-26 裁定との矛盾として差し戻される。`LocalizationPlaceholderParityTest` を名前付きに読み替えるのか消すのかも決まっていない
- 最小の直し方: §8.3 に 08-26 裁定を「改訂（当時『別タスク』とした棄却案を採る。パリティテストは `{name}` 集合の一致に読み替えて残す）」で足し、D8 の「既存裁定との関係」をそれに合わせる

### 4. 順序（節間の矛盾）: 「D1〜D9 は段階 0 の前」と §8.1 表の期限が合わない

- 対象: §0「本筋の D1〜D9 は段階 0 に入る前に決めておく」、§8.1 見出し「本筋（段階 0 に入る前に要る）」、§8.4「D1〜D9 の裁定（段階 0 の前）」 vs §8.1 表の期限列: D2＝段階 1、D5＝段階 1、D9＝段階 2 の終わり
- 何が起きるか: ユーザーへ段階 0 前の裁定として 9 件を一度に求めるが、表は 3 件を後でよいとしている。D9（持ち物の束の切替粒度）は段階 2 の暗転の実績を見てから決めるほうが情報が多く、前倒しは根拠の薄い裁定を求めることになる
- 最小の直し方: 表を正にして §0・§8.4 を「段階 0 の前に要るのは D1・D3・D4・D6・D7・D8。D2・D5 は段階 1 の前、D9 は段階 2 の終わり」に揃える

### 5. 誤り（事実）: 「managed stripping は Standalone で 3＝High（実測）」は設定の読み違い

- 対象: §4.8 S14、§6 #6
- 根拠: `moorestech_client/ProjectSettings/ProjectSettings.asset:921-933` の `managedStrippingLevel` に Standalone のキーは無い（EmbeddedLinux・PS4・WebGL 等の 13 項目だけ。全部 1）。`Standalone: 3` は同ファイル 944 行目の `apiCompatibilityLevelPerPlatform` の値で、stripping ではない（読み違いの出どころは推測）。`scriptingBackend: Standalone: 0`＝Mono は正しい
- 何が起きるか: S14 が「High stripping で HttpListener が削られるか」という存在しない条件を試す。既定（未設定）で可と出ても、将来 High に上げたときの挙動は未検証のまま残る
- 最小の直し方: S14 と §6 #6 を「managedStrippingLevel は Standalone 未設定（Unity 既定）。Mono。既定設定の配布ビルドで確かめる」に直す

### 6. 誤り（実測の再現不可）: 「Web UI に依存する録画シナリオ 41 本」の内訳が再現できない

- 対象: §2.1 0-7、§7.4 の 1 行目
- 根拠（`.agents/skills/unity-playmode-recorded-playtest/scenarios/` の .cs を grep）:
  - 総数 90 本: 一致
  - `*WebUi*(` のメソッド呼び出しがある本: 23 本・139 か所（ClickWebUi 66。文字列・コメント込みなら 68）。計画は「32 本・151 か所（ClickWebUi 67）」
  - `PlaytestDomQuery.Query`: 17 本・27 か所（`.RegisterHandler`・`.ResetPending` を含めると 19 本）。計画は「.Query を直接呼ぶ 19 本・27 か所」
  - `WebUiInputExclusivity`: 3 本。一致
  - `WebUi` を含む全ファイル 38 本、`WebUi|DomQuery|Cef` で 42 本。41 を出す式は見つからなかった
- 何が起きるか: 0-7 の完了条件「改名したシナリオの合否が改名前と同じ」の母集合が決まらず、対象外にした本の改名漏れが段階 1 以降の録画で赤になる。G4 の分母も揺れる
- 最小の直し方: 0-7 に数え方（grep の式）を 1 行で書き、対象の一覧を台帳に置く。本数は「約 40 本（式で確定）」にする

### 7. 誤り（引用）: 「parity-check 47/47 が足かせになって打ち切った」は 08-17 裁定の内容ではない

- 対象: §4.6
- 根拠: `.decisions/2026-08-17-uGUI移植を終了しWeb-UIは独自調整へ移る.md` に 47/47 は無い。理由は「uGUI描画は Phase1 で恒久停止済みで、参照すべき実物がもう無い」。47/47 は `docs/webui-parity/iteration-log.md:22,35`（機械チェック 47/47）の値
- 何が起きるか: 「画素パリティを目標にしない」根拠として、前例の教訓（正本が消えると追従できない）を「検査が重かった」に取り違える。今回は参照キャプチャを正本として残す計画なので、教訓の中身が逆に効く
- 最小の直し方: 「前回は uGUI の正本が消えて追従不能になり打ち切った（08-17 裁定）。機械チェック 47/47 の運用は `docs/webui-parity/iteration-log.md`」と書き分ける

### 8. 誤り（軽微・事実）: 「React の本番コード 15.6k 行」は CSS を含む数

- 対象: §0、§7.4
- 根拠: features 8,482・shared 4,002・app 922・bridge 2,203 は `.ts/.tsx/.css`（`*.test.*` 除く）の合計と完全一致。`.ts/.tsx` だけなら features 6,623・shared 3,154・app 222・bridge 2,203＝12,202
- 何が起きるか: §0「UITK の新規コードは本体約 2 万行」が CSS 3.4k を含む数から出ているなら、USS 0.8〜1.2k（§2.2）と二重に数えている（推測）
- 最小の直し方: 「TS/TSX 12.2k＋CSS 3.4k（テスト除く）」と内訳を書く

### 9. 矛盾（軽微・節間）: 段階 2 の「HUD 10 種」と対象の列挙 9 種

- 対象: §1 段階 2「HUD 10 種」 vs §2.3 対象「入力の無い HUD」の列挙（Crosshair・KeyHintHud・KeyControlHintHud・ProgressBar・TrainRidingHud・CurrentChallengeHud・PlacementModeHud・DeleteModeWarningBands・DeleteBoxHeightHud＝9）
- 何が起きるか: G4 の分母がずれ、1 種の取りこぼし（または数え間違い）が段階 5 の「Web が描くものを 0 にする」まで見つからない
- 最小の直し方: §1 を 9 種にするか、§2.3 に 10 種目を書く

---

## その他の気づき（指摘には数えない）

- ADR は同じ番号のファイルが複数ある（0013・0014・0016・0019・0035・0039・0046・0071・0082 は 2〜3 本ずつ）。計画は括弧の説明で区別できているが、§7.4「ADR … は実在する」は番号だけの確認。改訂を書くとき（§8.3）はファイル名で引くほうが安全
- §8.2 D16「ffmpeg も arm64 しか無い」: ADR 0071:26-29 は「ffmpeg も arm64 版だけを同梱する」（選択）で、「しか無い」と書いているのは CEF（`osx-arm64` のみ）のほう。D16 の帰結「CEF を消しても自動では解けない」自体は変わらない
- §2.1 0-7 は `WebUiInputExclusivity` を参照する 3 本を段階 0 の対象に数えるが、その改名（`UiInputExclusivity`）は §2.2 で段階 1。段階 0 でこの 3 本に何をするかが書かれていない
- §0「UITK の新規コード本体約 2 万行」は §1 の段階別（1〜2k＋5.5〜8k＋3.5〜4.5k＋3〜4k＋8〜11k＋1.5〜2.5k＝22.5〜32k）の下限より小さい。どちらも推測なので指摘にしない
- §2.7 6b「C# の通信契約テスト（131 件）」と §4.1「C# の WebUi 系 285」: `Client.Tests` で `WebUiHost` を参照する 67 ファイルの `[Test]/[TestCase]/[UnityTest]` は 294。属性の数え方の差と見て指摘にしない
- 設計の話なので他観点に譲る: §3.3 R1 は「主パネルが UITK の状態」の Web を止める規則で、段階 3〜4 の「主パネルが Web（インベントリ）＋UITK のホットバー」の組は R1 の対象外。§2.4 の受入「Web の action が 0 件」を何が保証するかは書かれていない（CEF は RawImage 全面で入力を受けるという host §1 の前提どおりなら、UITK が消費したクリックも CEF に届く。推測）

---

## この観点での総評

- 「実測」と書かれた数値・パス・行番号は大半が master HEAD と一致し、引用した ADR・裁定・bd・Discord も実在し内容が合う（抜き取り 60 点超、不一致 6 点）。
- 不一致は「進行中の Web PR」「壊れた 8 本」「stripping High」「41 本の内訳」のように段階 0 の着手・完了条件に直接効くものに集中しているので、段階 0 の前に直す。
- §8.3 の抜けは 08-26 のプレースホルダ裁定の 1 件、節間の矛盾は D1〜D9 の期限と HUD の種数の 2 件で、いずれも表を正にして揃えれば済む。

---

## 確かめて合っていた点（数え方つき）

### 行数・件数

1. `Client.WebUiHost` 140 ファイル・10,565 行: `find … -name '*.cs' | wc -l` と `cat | wc -l`
2. `web/features` の機能ディレクトリ 20: `ls -d features/*/ | wc -l`
3. `Localization/localization.csv` 422 キー、位置指定 32 キー・190 か所、名前付き 44 キー・370 か所: Python で列 2〜6 の `{pN}` と `{name}` を数えた（`{pN}` を含む行 32、名前付きのみの行 76−32＝44、か所 560−190＝370）
4. mod の CSV に差し込み 0: `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/localization/localization.csv`（435 行）で `{` が 0
5. 録画シナリオ総数 90: `scenarios/` の .cs を `find | wc -l`
6. `WebUiInputExclusivity` を参照するシナリオ 3 本: `grep -rl`
7. `PlaytestDomQuery.Query` 27 か所: `grep -rho | wc -l`
8. Playwright のテスト名 213: `e2e/` の `test(`・`test.skip(` 等を正規表現で数えた（53 spec ファイル）。議論の記録の 211 はランナー実測で、計画の注記どおり別の数
9. `protocol.ts` の topic 29（`playtest.dom_query` 込み）、`ACTION_TYPES` 44: `Topics` の要素を数えた／`awk` で配列の文字列を数えた
10. `anchorIds.ts` 静的 19・動的 prefix 6: ファイルを読んで数えた
11. `UiPointerHitTest` の呼び出し側 23 ファイル・28 か所: `grep -rn "UiPointerHitTest\."` から定義ファイルと `Tests` を除く
12. `tokens.css` 192・`skitTokens.css` 38・`slotTokens.css` 8（計 238）: `grep -cE '^\s*--[a-z0-9-]+\s*:'`
13. CSS の `calc()` 70・`z-index` 44・data 属性セレクタ 40: `grep -rho` で数えた
14. 半透明 67 か所: `(rgb|hsl|oklch|hwb)(… / α)` の構文が 67（`rgba(`・8 桁 hex は 0 なので、この数え方でだけ 67 になる。トークン 3 ファイル内は 13）
15. `toFixed(` 12 か所（テスト除く）
16. `webSocketClient.ts` の無応答 15000ms（:89）・ping 間隔 5000ms（:94）
17. `useBuildMenuCategoryScroll.ts` 305 行
18. 10-15 以降の `moorestech_web` コミット 44 本（マージ込み。`--no-merges` なら 38）
19. `packages.config` 74 件、`Microsoft.AspNetCore*`・`Microsoft.Extensions*` 47 件（`Microsoft.AspNetCore` 本体を含む数え方。末尾ドット付きなら 46）、`ModelContextProtocol 0.2.0-preview.3` あり・`using` 0
20. `WebPinId` 13 か所、`VanillaSchema/challenges.yml` の `web anchor id` 3 か所（:282,334,337）
21. `Client.Tests` で `WebUiHost` を参照するファイル 67
22. moores-code-review の ts/tsx 観点 12 本: `reviewers/core-ts_tsx-*.md` が 12
23. マスタのアンカー 9 件: `../moorestech_master/server_v8` で `challenge.current-hud` 1・`recipe.craft-button` 1・`research.node-<guid>` 7。`pause.` 0・`dragGuide` 0・`itemViewHighLight` 1 ファイル
24. bd の CEF 関連 open 課題: `bd list --status open | grep -i cef` が 13 件で、うち 1 件は計画後に立った epic `moorestech-albof`。除くと 12

### パス・行番号・内容

25. manifest 3 行目 `com.annulusgames.debug-ui`、13 行目 `com.singtaa.onejs`、45 行目 `jp.juha.cefunity`。両パッケージの `Scripts/` と asmdef からの参照 0
26. OneJS の `Resources` 2.2MB: pr-apply worktree の `PackageCache/com.singtaa.onejs@d16ed851ec34/Resources` が `du -sh` で 2.2M（計画は shipping §5 引用。本体 22M）
27. `UIStateControl.cs:16` `public event Action<UIStateEnum> OnStateChanged;`
28. `WebUiEndpoints.cs:97-101` の `RemoteExecEndpoint.Path` 分岐
29. `WebUiStartup.cs:12` コメント「UIはWeb一本なので起動失敗時は非表示で続ける」、`:19` 警告「起動が完了せず、遠隔実行も開きません」
30. `InitializeScenePipeline.cs:73` の `KestrelPort` 受け渡し（ブロックは 70〜73 行）
31. `WebUiGameBinder.cs:48-55` の Hub null で `return`
32. `WebSocketMessageDispatcher.cs:54-56` は `ping` に `pong` を返すだけ（最終受信時刻は持たない）
33. `PlaytestRecorder.cs:33` `GameViewInputSettings`、`PlaytestUiOps.cs:133-138` の pointerOverUi 待ち
34. `MainGame.unity:8435` の `m_ActionsAsset` guid `ca9f5fa9…` は InputSystem パッケージの `DefaultInputActions.inputactions.meta` の guid と一致
35. `MainGameUI.prefab` の Canvas は `m_RenderMode: 0`（Overlay）・`m_SortingOrder: 0`、`CefUnityBrowserSample`・`WebUiCefNavigator` あり
36. `SkitPanelSettings.asset` は 1920×1080（`m_ReferenceResolution`）
37. `uiScreenRouting.ts:22` researchTree `grab: true`、`App.tsx:95` `screenAllowsGrab(screen) && <InventoryPanel />`、`:100` `ResearchTreePanel`
38. `EquipmentPanel/index.tsx:55-80` のホイール循環（`isWheelPassthrough`・`wheelOwnedByBuildTool`・1 枠以下）
39. `tooltipPosition.ts:1` `OFFSET = 12`、`notificationStore.ts:26` `NOTIFICATION_DISPLAY_MS = 7000`、`usePressGesture.ts` の閾値コメント（5px の数値は読めず。推測）、`treeView/viewport/viewport.ts` の `0.4`・`2.5`・`PAN_FRICTION_TAU_MS = 325`
40. `BlockInventoryTopic.cs:76-78` コメント「4tick間隔」と実装 `IntervalFrame(4)` の食い違い。`ProgressTopic.cs:32` の 100ms
41. `WebUiModalService` を要求するのは `BlueprintNameInputWebBridge` だけ（他は `ModalActions`・`ModalTopic`・`WebSocketHub` の配線）。`LocalizationKeys.Ui.Blueprint.WebModal*` は同ファイル :53-55
42. `ResearchTopic._nodeStates`（`Topics/Research/ResearchTopic.cs:27`）、`BlockInventoryTopic.TrackBlock`、`ChallengeTopicState`・`BlockNetworkInfoCache`・`OpenTrainTimetableTracker`・`TrainTimetableFetcher`・`NotificationTopic`・`EventLanguageGate`・`EventModeStartGate`・`MainGameInitializationFinalizer`・`TutorialAnchorIdMapper`・`MouseCursorTooltipState`・`LocalizationTextInterpolator`・`CefRuntimeBundler`・`CefPackageLfsValidator`・`CefLfsPointer`・`EnvironmentScrubOnEditorLoad`・`WebUiProductionArtifactBuilder`・`WebUiGateAuditTest`・`RemoteExecEndpointTest`・`PlaytestBootUnityCallbackBindingTest`・`WebUiActionInvoker`・`CefInputForwarder`・`CefScreenMapper`・`PlaytestDomQuery`・`SkitPresentationMode`・`Client.Skit/UI/SkitUI.cs`・`scripts/setup-cef.{sh,ps1}` はすべて実在
43. `WebUiHealth`・`GameUiRoot`・`UiInputExclusivity` は未存在（新設予定どおり）
44. Unity `6000.3.8f1`、`com.unity.ui.test-framework` 1.0.0 は Editor の `BuiltInPackages` にあり manifest・lock には無い。`scriptingBackend: Standalone: 0`（Mono）、`activeInputHandler: 2`（Both）
45. `run_test.yml`: `webui-test` ジョブは `tsc -b`・`tsc -p e2e/tsconfig.json --noEmit`・`vitest run` だけで Playwright は走らない。`needs_webui`（:149）、`build.yml:234` の CefUnity 事前取得
46. `release-artifact.sh:54-62` の CEF helper 検査（`cef-unity-server.app`）、`remote-exec.sh:49` の `access.json`
47. `docs/webui/plans/phase-d-cutover.md:35-36` 「画面ごとの操作パリティ受け入れ表」、`cef-unity-frame-rate-followup-2026-07-29.md` §2.2 の `_enableLog=1`・`block_avg`、`anchor-convention.md:25-27` の testid 分離、`input-focus-exclusivity.md:30-32` の ESC、`MIGRATION.md`・`disposition.md`・`unity-editor.md`・DSL スキル文書 4 本は実在
48. `web` 側の `BENIGN_ERRORS`・`screenAllowsGrab`・`AppErrorBoundary`・`DictionaryIndependentText`・`preDictionaryText.ts`・`zLayerTokens.test.ts`・`hotbarDnd.ts`・`shared/placementTarget`・`materialTooltipText.ts`・`buildMenu/logic` は実在
49. `Assets/Dependencies/Font/` に `NotoSans-Regular SDF.asset`・`NotoSansJP-Medium SDF 1/2.asset` あり（TMP 用かは未確認。推測）
50. `Assets/Plugins/Demigiant/DOTween` あり（D11「DOTween がすでにある」）

### ADR・裁定・議論の記録・bd・Discord

51. 計画が引く `.decisions` 22 本はすべて実在（§7.4 の訂正どおり `2026-09-05-メインメニューとロード画面はuGUI現状維持.md`）
52. ADR 0082（sprint-toggle）:27 「ゲームパッドは正式対応外」、ADR 0017 通知は全画面 UI の裏、ADR 0016 160ms/200ms・7 秒、ADR 0039 1200ms、ADR 0019 18px・padding 6/10・max-width 320、ADR 0046 パネル外の右短押し、ADR 0035 skitPause/trainPause、ADR 0021 入力欄だけ選択可、ADR 0024 祖先クリップ、ADR 0020/0074 ワープと付与時の再計算、ADR 0013 `--equipment-bottom: var(--hotbar-bottom)`（tokens.css:43 にも実在）、ADR 0014:18-19 「他のButtonは今回触らない」、ADR 0006 決定 5「Web の Guid 辞書解決へ統一」、ADR 0057 Mac mini の inbox
53. ADR 0052: 例外 4 種（1 CEF描画面、2 MainMenu/ローディング、3 HP バー、4 デバッグ UI）、agent 前提 1（`EventSystem` 判定を残す）、`com.unity.ugui` は manifest に残す
54. ADR 0072: 「入口は既存の Web UI サーバー（Kestrel）に1本足す」（:22）、棄却案「専用の待ち受けを新設」（:24）、完了条件 3 点（:47）、結果の 7 種（:53）、`--remoteExec`（:51）
55. ADR 0071:10,29 CEF の Mac ランタイムは `osx-arm64` のみ、:26 ffmpeg は arm64 版だけ同梱（上記「その他」参照）
56. 裁定 10-18: 「数万環境」「アニメーションは導入または自作」「OneJS は経由しない」。08-17 の Web 専任裁定を覆すと明記
57. 裁定 09-24: staging → 検証機 → playtest は Web UI で手動
58. 裁定 09-08: Rules が 1 本に畳まれて `WebUiGateAuditTest` が無効化された経緯と「両方で守る」
59. 裁定 09-05（メインメニュー）: 「CEF の起動はゲーム内だけに限定」「UI TK 化はあとから」
60. 裁定 10-01（メニュー中の視点）: 移動だけ解放
61. 議論の記録: `Client.WebUiHost` 134 ファイル・約 10.1k 行（:17）、vitest 809・Playwright 211（:18,53）、未裁定 5 点（出荷形・仕様の正本・完了の定義・AI 向けの作法・第一弾）と完了の定義 4 項目（:52-56）、「uloop screenshot で自己確認」（:55）、第一弾「インベントリかホットバー」（:56）
62. bd: `moorestech-lnsf.8`（SkitUI/SkitPresentationMode 撤去）、`moorestech-cgm`（ToastHost の Mantine 剥き出し）は実在し内容も一致
63. Discord ミラー: 10-02 の 6 時間放置スレッド（id 1555576468828987532）、メモリの消費量スレッド（id 1554924554697187389）の 14,600MB と Esc 連打 51MB→170MB、10-03「インベントリ開いたままだったので閉じれなく」、10-14 橋脚スレッド:36「cef-unity~~~は2種類ともプロセスが生きてる」、10-11 の Ctrl+ホイールぐりぐり（スレッド 1558846967193280602）はすべて一致

### 確かめられなかったもの

- 配布物の CEF ファイル数・容量（Windows 237/368MB、Mac 252/298MB。shipping §1.1）: ビルド成果物が手元に無い
- vitest の分類別件数（L 311・C 235・I 61・S 91・W 167）: 分類の正規表現が計画に無い。`it(`/`test(` の総数は 879
- PoC ブランチ `poc/uitk-hard-cases` の `PocControls.uss` 146 行: origin に枝はある（10-22）が、master のレビューなので checkout していない
- `Assets/Dependencies/Font/*SDF*.asset` が TMP_FontAsset であること: 型の guid まで照合していない
