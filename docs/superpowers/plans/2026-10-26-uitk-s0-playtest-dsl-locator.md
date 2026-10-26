# UITK段階0 0-7 プレイテストDSLの描画系非依存化 Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** プレイテストDSLのWeb UI操作を `IUiElementLocator`（照会・文字入力）の背後へ移し、`ClickWebUi` 系を `ClickUi` 系へ一度に改名して、段階1でUITK版ロケータを interface 変更なしに差し込める形にする。

**Architecture:** `Client.Playtest/UiElements/` に描画系非依存の型（interface・照会結果・文字入力結果・セッション）を置き、`Client.Playtest/WebUi/` に Web 版の実装（既存の DOM 照会 WS 往復に、状態フラグ・一致件数と、新設の文字入力 WS 往復を足したもの）を閉じ込める。DSL の操作（`PlaytestUiElementOps`）とシナリオは interface だけを見る。Web 側は `bridge/playtest/` に文字入力の応答器を足し、DOM 照会の応答に `matchCount`・`stateFlags` を足す。

**Tech Stack:** Unity C#（UniTask・InputSystem・NUnit EditMode）、React/TypeScript（vitest・jsdom）、WebSocket（`WebSocketHub` の topic publish と action 応答）、プレイテストDSL（`unity-playmode-recorded-playtest` の run-scenario.sh）

## Requirements

1. `IUiElementLocator` を新設し、`Find(testid)`（可視・クリック可否・画面矩形・文字列・状態フラグを返す1回照会）と `SetText(testid, text)` と `IsAvailable()` を持つ。受け入れ: Task 3 の NUnit が通り、`PlaytestUiElementOps` とシナリオが interface 経由でだけ照会している（`grep -rn 'PlaytestDomQuery\|CefScreenMapper\|CefInputForwarder' .agents/skills/unity-playmode-recorded-playtest moorestech_client/Assets/Scripts/Client.Playtest/Operations` が0件）
2. この段で作るロケータ実装は Web 版だけ。受け入れ: `IUiElementLocator` の実装クラスが `WebUiElementLocator` 1つ
3. Web 版 `SetText` は対象の `input`/`textarea` の値をネイティブ setter で置き換えて `input` イベントを流す（IME を通さない。段階1の UITK 版が `TextField.value` を設定して ChangeEvent を流すのと同じ意味）。受け入れ: vitest で React の制御 textarea の state が更新される／`pause-menu-hierarchy` と `developer-report-delivery` が `p.SetText` で説明文を入れて改名前と同じ合否になる
4. 状態フラグを抽象化する。Web は `data-testid` 以外の `data-*` 属性のうち値が空か `"true"` のものの名前（`data-selected="true"` → `selected`）。受け入れ: vitest と NUnit で `HasStateFlag("selected")` が真になる
5. `ClickWebUi` 系を `ClickUi` 系へ一度に改名し、旧名を残さない（改名表は Global Constraints）。受け入れ: `grep -rnE 'ClickWebUi|HoverWebUi|UntilWebUiElement|CloseWebUiPanel|PlaytestWebUiOps|WebUiTextContains|WebUiElement' .agents/skills/unity-playmode-recorded-playtest moorestech_client/Assets/Scripts/Client.Playtest` が0件
6. シナリオ内の `PlaytestDomQuery`・`DomQueryResult`・`CefScreenMapper`・`CefInputForwarder` の直接呼び出しを、ロケータの照会 API（`PlaytestUiElementOps.FindUi`・`p.SetText`・`PlaytestUiLocatorSession`）へ置き換える。`PlaytestDomQuery`・`CefScreenMapper`・`CefInputForwarder` は `internal` にして、シナリオから見えなくする
7. 改名・置換した 37 本のうち、壊れている 7 本を除く 30 本の合否が、改名前（この worktree の未変更 HEAD＝origin/master `e855a58667`）と同じ。照会 API へ置き換える 18 本は 1 本ずつ置換→実行→突き合わせる。受け入れ: `docs/uitk-migration/stage0-dsl-parity.md` の表で全 30 行が「一致」
8. スキル文書 `write-scenario.md`・`input-injection.md`・`place-blocks-via-ui.md`・`run-scenario.md`（`.agents/skills/unity-playmode-recorded-playtest/references/`）を同じ PR で新 API に直す
9. 計画 §4.4 の UITK 版手順（`Query(name:…)` 列挙で 0 件再試行・2 件以上失敗、`resolvedStyle`＋`worldBound` の可視判定、`panel.Pick` の被覆判定、両系で同じ testid が見つかったら失敗）を、段階1で interface を変えずに足せる。受け入れ: Requirements 9 の対応表（「段階1の差し込み点」節）の各手順が既存の型のどのメンバーで表せるかが書けている
10. PR 本文に「#1348（`feature/belt-conveyor-replace-impl`）のシナリオ `belt-replace-tier-via-ui.cs` と `input-injection.md`・`PlaytestDriver.cs`・`PlaytestRunner.cs` に改名の追従が要る」と書く

**やらないこと**
- UITK 版ロケータ・両系照合の合成ロケータ（段階1）
- Web UI の機能変更（`features/` 配下は触らない。触るのは `bridge/playtest/`・`bridge/transport/protocol.ts` の topic 1 行・`main.tsx` の初期化 1 行だけ）
- シナリオの操作手順の変更（置換は同じ意味の API への付け替えだけ。説明文入力の `SendCharEvent` ループ→`p.SetText` は付け替えとして扱う。判断記録 D5）
- 壊れている 10 本（bd `moorestech-wwxy` 本文 8 本＋NOTES 2 本）の修理。うち改名対象に入る 7 本は文字列置換だけ行い、合否の突き合わせから外す
- `HideDeadCefOverlay`（`CefUnity.Runtime` 直参照の vein-terrain 4 本）と `Client.Input.WebUiInputExclusivity` の参照（DSL の外。段階1以降）
- `PlaytestUiOps.WaitPointerLeaveWebUi`（Web の pointer-over 待ち合わせ。§4.4 で UITK では不要になる。段階1）
- シナリオのファイル名変更（`webui-buildmenu-click.cs` 等はそのまま）

## Global Constraints

- 1 ファイル 200 行未満、1 ディレクトリ 10 ファイルまで（AGENTS.md）。`PlaytestDriver.cs` は今 215 行なので、このPRで 200 行未満へ戻す（Task 4）
- `partial`・`Func<>` の新規使用禁止、デフォルト引数禁止、単純な getter/setter プロパティ禁止（AGENTS.md）。既存の `PlaytestReporter.Act(string, Func<UniTask>)` は既存 API として使ってよい
- try-catch は使わない（WS 応答の不正値は既存どおり型検証して `invalid_payload` で返す）
- fail-closed の縮退（受け口未登録・応答タイムアウト）は `Debug.LogWarning` で理由を出す。黙らない
- コメントは日本語1行＋英語1行のセット（AGENTS.md）
- .cs を変えたら `uloop compile --project-path ./moorestech_client` で ErrorCount 0 を確かめる
- .meta は手で作らない（`git mv` した .cs の .meta は一緒に `git mv` する。新規 .cs の .meta は Unity が作ったものをコミットする）
- Unity Editor の予算: Mac mini の同時 Editor は全セッション合計 2 本まで。起動前に `moores-wt status` で本数を確かめ、2 本以上なら Editor の要らないタスク（Task 2・Task 6 の文書）を先に進める
- Web UI を PlayMode で動かす前に `cd moorestech_web/webui && pnpm install --frozen-lockfile`（node_modules を別 checkout から複製しない）。Editor の Web UI は Vite dev server で配信されるので、`bridge/playtest/` の変更はビルド不要で PlayMode に載る
- 他レーンの持ち物（`Client.RemoteExec`・`WebUiEndpoints.cs`・`InitializeScenePipeline.cs`・`Client.WebUiHost` の topic 状態部分・`WebUiGameBinder`・`WebUiStartup.cs`・`UIStateControl`・`docs/uitk/`・`docs/webui/MIGRATION.md`）には触らない。触る必要が出たら `bd note moorestech-albof.6` に書いてマージで解く
- Steam・Discord など外へ出す操作はしない

**改名表（旧名は残さない）**

| 旧 | 新 |
|---|---|
| `p.ClickWebUi(testid)` | `p.ClickUi(testid)` |
| `p.HoverWebUi(testid)` | `p.HoverUi(testid)` |
| `p.UntilWebUiElement(testid, t)` | `p.UntilUiElement(testid, t)` |
| `p.CloseWebUiPanel()` | `p.CloseBuildMenuPanel()`（実処理は `build-menu-close` のクリック。名前を実処理に合わせる） |
| `PlaytestWebUiOps`（クラス） | `PlaytestUiElementOps` |
| `PlaytestWebUiOps.ClickWebUi(testid[, t])` | `PlaytestUiElementOps.ClickUi(testid[, t])` |
| `PlaytestWebUiOps.HoverWebUi` | `PlaytestUiElementOps.HoverUi` |
| `PlaytestWebUiOps.WaitWebUiElement` | `PlaytestUiElementOps.WaitUiElement` |
| `PlaytestWebUiOps.ScrollDownUntilWebUiElementIsClickable` | `PlaytestUiElementOps.ScrollDownUntilUiElementIsClickable` |
| `PlaytestWebUiOps.WaitWebUiTextContains` | `PlaytestUiElementOps.WaitUiTextContains` |
| `PlaytestWebUiOps.BuildMenuBlockTestId` | `PlaytestBuildMenuOps.BuildMenuBlockTestId` |
| （新） | `p.SetText(testid, text)`・`PlaytestUiElementOps.SetText(testid, text, t)`・`PlaytestUiElementOps.FindUi(testid)` |

**照会 API への置換表（Task 5 で 1 本ずつ使う）**

| 旧（シナリオ内） | 新 |
|---|---|
| `PlaytestDomQuery.Query(id, t)`（`Client.Playtest.WebUi.` 修飾付きも） | `PlaytestUiElementOps.FindUi(id)`（応答待ちの上限は Web 版内部の 3 秒。判断記録 D4） |
| `DomQueryResult`（型名） | `UiElementSnapshot`（`using Client.Playtest.UiElements;` を足す） |
| `.Found` | `.IsFound` |
| `.Visible` | `.Visible` |
| `.HitTestPassed` | `.Clickable` |
| `.Text` | `.Text` |
| `.X`・`.Y`・`.Width`・`.Height`（CSS px・左上原点） | `.ScreenRect`（Unity スクリーン px・**左下原点**）。上下の比較は向きが逆になる（下の個別指示） |
| `CefScreenMapper.TryBrowserToScreen(中心)` | `.ScreenRect.center` |
| `CefScreenMapper.IsWebUiAvailable()` | `PlaytestUiLocatorSession.Locator.IsAvailable()` |
| `CefScreenMapper.TryGetBrowser(out b)` ＋ `b.SendCharEvent` ループ | `await p.SetText(id, text)` |
| `PlaytestDomQuery.RegisterHandler()` ＋ `CefInputForwarder.StartForwarding()` | `PlaytestUiLocatorSession.Start()` |
| `CefInputForwarder.StopForwarding()` ＋ `PlaytestDomQuery.ResetPending()` | `PlaytestUiLocatorSession.Stop()` |
| `using Client.Playtest.WebUi;` | 削除（必要なら `using Client.Playtest.UiElements;`） |

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反4件・修正4件 / Driver 分割の下請けを前例 PlaytestHotbarDriver の形へ（逆参照を廃止）・SetText の待ち上限を Ops の既存定数へ・応答タイムアウトに警告ログ・解説コメントの日英順。修正範囲の再検査（fresh-context）: 違反0・弱6 を全件反映
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強1・弱6・第3バケツ1 / 5-B（Status＋空値の照会結果）はこのまま（D11）、Reset 後の応答待ちを即時打ち切り・有限値判定の二重化を解消、残りの弱はこのまま（D12）、第3バケツは PR 本文へ。修正範囲の再検査: 強0（弱は上の配置検査行に合算）

---

## 配置と前例

| # | 項目 | 配置先 | 機構 | 前例 |
|---|---|---|---|---|
| 1 | `IUiElementLocator`・`UiElementSnapshot`・`UiElementLookupStatus`・`UiRenderer`・`UiSetTextResult`・`UiSetTextStatus` | `Client.Playtest/UiElements/`（asmdef `Client.Playtest`、Editor 限定） | 純粋な型 | `Client.Playtest/Core/PlaytestResult.cs`（DSL 内の結果型） |
| 2 | `PlaytestUiLocatorSession`（static。ロケータ 1 か所保持・開閉） | `Client.Playtest/UiElements/` | static 窓口 | 置換対象 `PlaytestDomQuery.RegisterHandler/ResetPending`・`CefInputForwarder.Start/StopForwarding`（static の開閉）をそのまま束ねる |
| 3 | `UiElementScreenPointResolver`（安定待ちと座標解決） | `Client.Playtest/UiElements/` | static | 置換対象 `PlaytestWebUiOps.ResolveScreenPointUntil` を移設 |
| 4 | `WebUiElementLocator`（Web 版実装）・`PlaytestDomQuery`・`PlaytestDomSetText`・`PlaytestWebUiRequestChannel<T>`・`DomQueryResultActionHandler`・`DomSetTextResultActionHandler`・`WebUiPayloadReader` | `Client.Playtest/WebUi/`（すべて `internal`） | WS topic publish＋`IActionHandler` 応答 | 既存 `PlaytestDomQuery`（同じ往復の前例そのもの） |
| 5 | `CefScreenMapper.TryBrowserRectToScreenRect` | 既存 `Client.Playtest/WebUi/CefScreenMapper.cs` | 既存の座標写像 | 同ファイルの `TryBrowserToScreen` |
| 6 | `PlaytestUiElementOps`（DSL の UI 操作） | `Client.Playtest/Operations/`（`PlaytestWebUiOps.cs` を `git mv`） | static ops | 既存 `PlaytestWebUiOps` |
| 7 | `PlaytestWaitActions`（Driver の 200 行超過を解く委譲先。待機表示つき 4 メソッドの本体） | `Client.Playtest/`（root。10 ファイル目） | Driver から 1 行委譲。`PlaytestReporter` だけを受け取り Driver を参照しない | `PlaytestHotbarDriver`（root・reporter だけを受け取る下請け） |
| 8 | `playtest.dom_set_text` topic・`playtest.dom_set_text_result` action | `bridge/transport/protocol.ts`・`action_names.json` の `excludedFromWebContract` | 既存 playtest 往復 | `playtest.dom_query`・`playtest.dom_query_result` |
| 9 | `domSetTextResponder.ts`・`playtestTopicResponder.ts`・`testIdElement.ts` | `moorestech_web/webui/src/bridge/playtest/` | topic 購読→読み取り/書き込み→action 応答 | `domQueryResponder.ts` |

データフロー（照会）: シナリオ → `PlaytestUiElementOps` → `PlaytestUiLocatorSession.Locator`（`IUiElementLocator`）→［Web 版］`WebSocketHub.Publish(topic)` → `bridge/playtest/*Responder` → `sendAction(*_result)` → `*ResultActionHandler` → `PlaytestWebUiRequestChannel` → 照会結果。新設はすべて「既存の DOM 照会往復への書き手／読み手の追加」で、ゲーム側の状態・入力経路には触らない。

## 段階1の差し込み点（Requirements 9）

| §4.4 の UITK 版手順 | この PR の型で表す場所 |
|---|---|
| `Query(name: testid)` で列挙、0 件なら再試行 | `Find` が `UiElementLookupStatus.NotFound` を返す → `PlaytestUiElementOps`・`UiElementScreenPointResolver` が期限まで再試行（実装済みの経路） |
| 2 件以上なら理由付きで失敗 | `Find` が `UiElementLookupStatus.Ambiguous` を返す → `UiElementScreenPointResolver.ThrowIfAmbiguous` が例外（実装済み。段階0の Web 版は返さない） |
| `resolvedStyle`（display・visibility・opacity）＋`worldBound` 面積で可視判定 | `UiElementSnapshot.Visible` |
| `panel.Pick(中心)` が自身か子孫か | `UiElementSnapshot.Clickable` |
| パネル座標→画面座標→SemanticInput | `UiElementSnapshot.ScreenRect`（Unity スクリーン座標・左下原点）を `PlaytestUiElementOps` が `SemanticInput.MouseGlideTo(ScreenRect.center)` に渡す |
| `SetText`＝`TextField.value` 設定＋ChangeEvent | `IUiElementLocator.SetText` → `UiSetTextResult`（`NotTextField`＝TextField でない、`NotInteractable`＝非表示/無効） |
| 状態フラグ＝USS クラス | `UiElementSnapshot.StateFlags`（UITK 版はクラス名から同じ状態名を作る。命名規約は段階1で決める） |
| UITK を先に探し、両系で同じ testid が見つかったら失敗 | 合成ロケータ（`IUiElementLocator` を実装）を `PlaytestUiLocatorSession.Locator` に差し替える。両方 `Found` なら `Ambiguous` を返す。どちらで解決したかは `UiElementSnapshot.Renderer`（`UiRenderer` に `Uitk` を足す）と `[PlaytestUi] … resolved by …` ログで確かめる |
| WS 往復・2 回同矩形の待ちが不要 | 合成ロケータが UITK で解決した場合も `UiElementScreenPointResolver` の 2 回照会は無害に通る（UITK は同期で同値を返す）。外すかは段階1で決める |

---

### Task 1: 改名前の合否を測る（基準）

**Files:**
- Create: `docs/uitk-migration/stage0-dsl-parity.md`

**Interfaces:**
- Consumes: なし（未変更の HEAD を使う）
- Produces: `docs/uitk-migration/stage0-dsl-parity.md` の「基準」列（シナリオごとの `Success`・Assert 件数・失敗 Assert ラベル・Error 先頭行）。Task 5 が「変更後」列を埋める

- [ ] **Step 1: 未変更であることを確かめ、Editor を用意する**

```bash
cd <worktree の root（ブランチ feature/uitk-s0-dsl）> && pwd
git status --short   # 空であること（plan ファイルのコミット以外の変更が無い）
git diff --stat e855a58667 HEAD   # 差分が docs/superpowers/plans/ の plan ファイルだけであること
moores-wt status     # Editor 本数を確認。2本以上なら Task 2 を先に進めて空きを待つ
(cd moorestech_web/webui && pnpm install --frozen-lockfile)
uloop launch ./moorestech_client
uloop compile --project-path ./moorestech_client
```

Expected: ErrorCount 0

- [ ] **Step 2: 30 本を Record=false で 1 本ずつ流し、結果を表へ書く**

対象（合否突き合わせの 30 本。カテゴリ相対パス）:

照会 API へ置き換える 18 本: `building/delete-tool/delete-hold-mode-reentry` `building/delete-tool/delete-hold-text-focus` `building/placement-reason-tooltip` `building/terrain-sink-normal-and-belt-via-ui` `building/ui-place-then-open-machine` `connect/electric-wire-tool-extend-chain-via-ui` `connect/rotary-generator-ui` `graphics-quality/quality-recovery-ui` `graphics-quality/quality-ui-switching` `misc/cursor-tooltip-follows-crosshair` `misc/feedback-report-labels` `misc/pause-menu-hierarchy` `misc/sprint/sprint-toggle-mode` `misc/sprint/sprint-hold-restart` `misc/sprint/sprint-invalid-recovery` `misc/vein-hand-mining-smoke` `playtest-receiver/developer-report-delivery` `train/fluid-inventory-via-interact`

改名だけの 12 本: `building/blueprint-large-paste-flicker` `building/blueprint-place-like-block` `building/blueprint-terrain-sink-partial-paste` `connect/electric-wire-tool-isolated-place-via-ui` `connect/electric-wire-tool-pole-cycle-rotate-via-ui` `connect/electric-wire-tool-pole-ghost-hide-paths-check` `connect/electric-wire-tool-pole-ghost-visibility-check` `misc/localization-language-switch-via-ui` `misc/walk-while-menus-open` `misc/webui-buildmenu-click` `train/train-station-default-name` `train/train-timetable-via-ui`

対象外（壊れている 7 本。文字列置換だけ行う）: `building/blueprint-copy-paste-via-ui` `building/blueprint-right-click-delete-via-ui` `misc/fps-tps-view-toggle-via-ui` `misc/research-node-state-label` `server-notification-smoke` `tutorial-equip-challenge` `tutorial-pebble-challenge`

各シナリオの冒頭コメントに前提（環境変数・先行シナリオ・実行順）があれば従い、Task 5 でも同じ条件で流す。`misc/sprint/` の 3 本は `sprint-toggle-mode` → `sprint-hold-restart` → `sprint-invalid-recovery` の順に続けて流す。1 本ごとの手順:

```bash
SKILL=.claude/skills/unity-playmode-recorded-playtest
SCRATCH="$PWD/moorestech_client/PlaytestResults/scratch"; mkdir -p "$SCRATCH"
NAME=<シナリオ名>; SRC="$SKILL/scenarios/<カテゴリ>/$NAME.cs"
sed 's/Record = true/Record = false/' "$SRC" > "$SCRATCH/$NAME.cs"
uloop control-play-mode --project-path ./moorestech_client --action stop
"$SKILL/scripts/run-scenario.sh" ./moorestech_client "$SCRATCH/$NAME.cs"   # 前景で実行して終了を待つ
RESULT=$(ls -td moorestech_client/PlaytestResults/*/*/ | head -1)result.json
jq -c '{Success, asserts: (.Asserts|length), failed: [.Asserts[]|select(.Passed|not)|.Label], error: ((.Error // "")|split("\n")[0])}' "$RESULT"
```

`asserts` が 0 なのに `Success: true` の行は、シナリオがコンパイルできず 0 行で素通りした可能性がある（bd `moorestech-dww3`）。run-scenario.sh の出力に `error CS` が無いかを確かめ、あれば表の備考に書く。結果が不安定に見えたら（同じ条件で 2 回流して違う）、2 回とも表に書き「揺れ」と備考に記す。

`docs/uitk-migration/stage0-dsl-parity.md` の形:

```markdown
# 段階0 0-7 DSL 改名・照会置換の合否突き合わせ

基準: origin/master `e855a58667`（改名前）。変更後: ブランチ `feature/uitk-s0-dsl`。どちらも Record=false、同じ前提・同じ順で流した。
対象外: 壊れている 7 本（bd `moorestech-wwxy`）。

| シナリオ | 種別 | 基準 Success / Assert数 / 失敗Assert / Error先頭 | 変更後 Success / Assert数 / 失敗Assert / Error先頭 | 判定 | 備考 |
|---|---|---|---|---|---|
| building/delete-tool/delete-hold-mode-reentry | 照会置換 | true / 12 / - / - | | | |
```

- [ ] **Step 3: コミットする**

```bash
git add docs/uitk-migration/stage0-dsl-parity.md
git commit -m "docs(uitk): 段階0 DSL改名前の録画シナリオ合否を基準として記録する"
bd note moorestech-albof.6 "Task1 基準測定済み: 30本の改名前合否を docs/uitk-migration/stage0-dsl-parity.md に記録"
```

---

### Task 2: Web 側の照会応答に状態フラグ・一致件数を足し、文字入力の応答器を作る

**Files:**
- Create: `moorestech_web/webui/src/bridge/playtest/playtestTopicResponder.ts`
- Create: `moorestech_web/webui/src/bridge/playtest/testIdElement.ts`
- Create: `moorestech_web/webui/src/bridge/playtest/domSetTextResponder.ts`
- Create: `moorestech_web/webui/src/bridge/playtest/domSetTextResponder.test.ts`
- Modify: `moorestech_web/webui/src/bridge/playtest/domQueryResponder.ts`（全体を下のコードに置き換える）
- Modify: `moorestech_web/webui/src/bridge/playtest/domQueryResponder.test.ts`（ケース追加）
- Modify: `moorestech_web/webui/src/bridge/transport/protocol.ts:71-73`
- Modify: `moorestech_web/webui/src/main.tsx:11,22`

**Interfaces:**
- Consumes: なし
- Produces（Task 3 の C# 側が読む wire 契約）:
  - topic `playtest.dom_query` の応答 action `playtest.dom_query_result` に `matchCount: number`（整数≥0、`found` と `matchCount > 0` は一致）と `stateFlags: string[]` を追加
  - topic `playtest.dom_set_text`（要求 `{ requestId: string; testid: string; text: string }`）
  - action `playtest.dom_set_text_result`（`{ requestId: string; status: "applied" | "notFound" | "notTextField" | "notInteractable" | "valueMismatch"; matchCount: number; actualValue: string }`）

- [ ] **Step 1: topic を足す**

`protocol.ts` の `playtestDomQuery` 行の直後へ:

```ts
  playtestDomQuery: "playtest.dom_query",
  // プレイテストの文字入力要求も snapshot を持たない一時イベント
  // Playtest text-input requests are also transient events without snapshots
  playtestDomSetText: "playtest.dom_set_text",
```

- [ ] **Step 2: 共通部品を書く**

`playtestTopicResponder.ts`:

```ts
import { useTopicStore } from "../store/topicStore";
import { subscriptions } from "../transport/subscriptionManager";

type TopicState = ReturnType<typeof useTopicStore.getState>;

// snapshotのないプレイテスト要求topicを購読し、新しい要求ごとにhandleへ渡す
// Subscribe to a snapshot-less playtest request topic and hand each new request to handle
export function initPlaytestTopicResponder(topic: string, handle: (request: unknown) => void): () => void {
  let topicAcquired = false;
  let disposed = false;
  const unsubscribeStore = useTopicStore.subscribe((state, previousState) => {
    syncTopicSubscription(state.status);
    const request = state.topics[topic];
    if (request !== previousState.topics[topic]) handle(request);
  });
  syncTopicSubscription(useTopicStore.getState().status);

  return () => {
    if (disposed) return;
    disposed = true;
    unsubscribeStore();
    if (topicAcquired) subscriptions.release(topic);
  };

  function syncTopicSubscription(status: TopicState["status"]) {
    // snapshotのないevent topicは通常topicの復元完了後だけ購読する
    // Subscribe to the snapshot-less event topic only after regular topics restore
    if (status === "open" && !topicAcquired) {
      topicAcquired = true;
      subscriptions.acquire(topic);
      return;
    }

    // 切断時に解除し、次回のrestoringTopicsへ混ざることを防ぐ
    // Release on disconnect so it never enters the next restoringTopics set
    if (status !== "open" && topicAcquired) {
      topicAcquired = false;
      subscriptions.release(topic);
    }
  }
}
```

`testIdElement.ts`（`domQueryResponder.ts` の `isElementVisible` をここへ移す）:

```ts
import { ancestorClipRect } from "../../shared/tutorialAnchor/ancestorClip";

// testidの一致件数と先頭要素を返す（重複の判定はUnity側が行う）
// Return the match count and the first element for a testid; Unity decides how to treat duplicates
export function findByTestId(testid: string): { element: HTMLElement | null; matchCount: number } {
  const matches = document.querySelectorAll<HTMLElement>(`[data-testid="${CSS.escape(testid)}"]`);
  return { element: matches.length === 0 ? null : matches[0], matchCount: matches.length };
}

// data-testid以外のdata-*で値が空かtrueのものを状態フラグ名として返す
// Return data-* attributes other than data-testid whose value is empty or "true" as state flag names
export function readStateFlags(element: HTMLElement): string[] {
  const flags: string[] = [];
  for (const attribute of Array.from(element.attributes)) {
    if (!attribute.name.startsWith("data-") || attribute.name === "data-testid") continue;
    if (attribute.value === "" || attribute.value === "true") flags.push(attribute.name.slice("data-".length));
  }
  return flags.sort();
}

export function isElementVisible(element: HTMLElement, rect: DOMRect): boolean {
  if (element.hidden || element.closest('[aria-hidden="true"]')) return false;
  const style = getComputedStyle(element);
  if (style.display === "none" || style.visibility === "hidden") return false;

  // 祖先のoverflowと画面の交差に、実際に見える面積が必要
  // Require visible area inside both ancestor overflow clips and the viewport
  if (rect.width <= 0 || rect.height <= 0) return false;
  const clip = ancestorClipRect(element);
  return Math.max(rect.left, clip.left) < Math.min(rect.right, clip.right) &&
    Math.max(rect.top, clip.top) < Math.min(rect.bottom, clip.bottom);
}
```

- [ ] **Step 3: DOM 照会の応答器を共通部品へ載せ替え、`matchCount`・`stateFlags` を足す**

`domQueryResponder.ts` 全体:

```ts
import { Topics } from "../transport/protocol";
import { sendAction } from "../transport/webSocketClient";
import { DEFAULT_ACTION_TIMEOUT_MS } from "../transport/actions";
import { initPlaytestTopicResponder } from "./playtestTopicResponder";
import { findByTestId, isElementVisible, readStateFlags } from "./testIdElement";

type DomQueryRequest = {
  requestId: string;
  testid: string;
};

type DomQueryResult = {
  requestId: string;
  found: boolean;
  visible: boolean;
  x: number;
  y: number;
  width: number;
  height: number;
  devicePixelRatio: number;
  hitTestPassed: boolean;
  text: string;
  matchCount: number;
  stateFlags: string[];
};

let disposeResponder: (() => void) | null = null;

// DOM問い合わせの受信を一度だけ登録し、アプリ終了時に破棄できるようにする
// Register DOM query reception once and allow teardown at application shutdown
export function initDomQueryResponder(): () => void {
  if (disposeResponder !== null) return disposeResponder;
  const disposeTopic = initPlaytestTopicResponder(Topics.playtestDomQuery, respond);
  const dispose = () => {
    if (disposeResponder !== dispose) return;
    disposeTopic();
    disposeResponder = null;
  };
  disposeResponder = dispose;
  return dispose;
}

function respond(request: unknown) {
  if (!isDomQueryRequest(request)) return;

  // DOMは読み取るだけに留め、クリックやスクロールなどの操作を行わない
  // Read the DOM only; never perform interactions such as clicks or scrolling
  const result = queryElement(request);
  void sendAction("playtest.dom_query_result", result, DEFAULT_ACTION_TIMEOUT_MS).then((actionResult) => {
    // Unity側が拒否した応答はerror codeを残し、契約不整合を追跡可能にする
    // Preserve the error code for Unity-side rejections so contract mismatches remain diagnosable
    if (!actionResult.ok) console.warn(`[playtest.dom_query_result] rejected: ${actionResult.error ?? "unknown_error"}`);
  }).catch(() => {
    // 切断競合はWS境界で吸収し、Unity側の問い合わせタイムアウトへ委ねる
    // Absorb disconnect races at the WS boundary and let the Unity query time out
  });
}

function isDomQueryRequest(value: unknown): value is DomQueryRequest {
  if (typeof value !== "object" || value === null) return false;
  const candidate = value as Record<string, unknown>;
  return typeof candidate.requestId === "string" && typeof candidate.testid === "string";
}

function queryElement(request: DomQueryRequest): DomQueryResult {
  const { element, matchCount } = findByTestId(request.testid);
  const devicePixelRatio = window.devicePixelRatio;

  if (element === null) {
    return {
      requestId: request.requestId,
      found: false,
      visible: false,
      x: 0,
      y: 0,
      width: 0,
      height: 0,
      devicePixelRatio,
      hitTestPassed: false,
      text: "",
      matchCount,
      stateFlags: [],
    };
  }

  const rect = element.getBoundingClientRect();
  let hitTestPassed = false;
  const visible = isElementVisible(element, rect);
  if (visible) {
    // 矩形中心の実ヒット要素を調べ、遮蔽物による空クリックを検出する
    // Inspect the actual hit at the rectangle center to detect blocked clicks
    const hitElement = document.elementFromPoint(rect.x + rect.width / 2, rect.y + rect.height / 2);
    hitTestPassed = hitElement !== null && (
      hitElement === element || element.contains(hitElement)
    );
  }

  return {
    requestId: request.requestId,
    found: true,
    visible,
    x: rect.x,
    y: rect.y,
    width: rect.width,
    height: rect.height,
    devicePixelRatio,
    hitTestPassed,
    text: element.innerText,
    matchCount,
    stateFlags: readStateFlags(element),
  };
}
```

- [ ] **Step 4: 文字入力の応答器を書く**

`domSetTextResponder.ts`:

```ts
import { Topics } from "../transport/protocol";
import { sendAction } from "../transport/webSocketClient";
import { DEFAULT_ACTION_TIMEOUT_MS } from "../transport/actions";
import { initPlaytestTopicResponder } from "./playtestTopicResponder";
import { findByTestId, isElementVisible } from "./testIdElement";

type DomSetTextRequest = {
  requestId: string;
  testid: string;
  text: string;
};

type DomSetTextStatus = "applied" | "notFound" | "notTextField" | "notInteractable" | "valueMismatch";

type DomSetTextResult = {
  requestId: string;
  status: DomSetTextStatus;
  matchCount: number;
  actualValue: string;
};

let disposeResponder: (() => void) | null = null;

// プレイテストの文字入力要求を一度だけ受け付ける
// Register playtest text-input reception once
export function initDomSetTextResponder(): () => void {
  if (disposeResponder !== null) return disposeResponder;
  const disposeTopic = initPlaytestTopicResponder(Topics.playtestDomSetText, respond);
  const dispose = () => {
    if (disposeResponder !== dispose) return;
    disposeTopic();
    disposeResponder = null;
  };
  disposeResponder = dispose;
  return dispose;
}

function respond(request: unknown) {
  if (!isDomSetTextRequest(request)) return;
  void applyText(request)
    .then((result) => sendAction("playtest.dom_set_text_result", result, DEFAULT_ACTION_TIMEOUT_MS))
    .then((actionResult) => {
      // Unity側が拒否した応答はerror codeを残し、契約不整合を追跡可能にする
      // Preserve the error code for Unity-side rejections so contract mismatches remain diagnosable
      if (!actionResult.ok) console.warn(`[playtest.dom_set_text_result] rejected: ${actionResult.error ?? "unknown_error"}`);
    })
    .catch(() => {
      // 切断競合はWS境界で吸収し、Unity側の要求タイムアウトへ委ねる
      // Absorb disconnect races at the WS boundary and let the Unity request time out
    });
}

function isDomSetTextRequest(value: unknown): value is DomSetTextRequest {
  if (typeof value !== "object" || value === null) return false;
  const candidate = value as Record<string, unknown>;
  return typeof candidate.requestId === "string" && typeof candidate.testid === "string" && typeof candidate.text === "string";
}

async function applyText(request: DomSetTextRequest): Promise<DomSetTextResult> {
  const { element, matchCount } = findByTestId(request.testid);
  const reply = (status: DomSetTextStatus, actualValue: string): DomSetTextResult =>
    ({ requestId: request.requestId, status, matchCount, actualValue });
  if (element === null) return reply("notFound", "");
  if (!(element instanceof HTMLInputElement) && !(element instanceof HTMLTextAreaElement)) return reply("notTextField", "");
  if (element.disabled || element.readOnly || !isElementVisible(element, element.getBoundingClientRect())) return reply("notInteractable", element.value);

  // Reactの制御入力はvalueの直接代入を差分として拾わないため、ネイティブsetterで書いてinputを流す（IMEは通さない）
  // React controlled inputs ignore direct value assignment, so write via the native setter and dispatch input (no IME)
  const prototype = element instanceof HTMLInputElement ? HTMLInputElement.prototype : HTMLTextAreaElement.prototype;
  const nativeSetter = Object.getOwnPropertyDescriptor(prototype, "value")?.set;
  if (nativeSetter === undefined) return reply("notTextField", element.value);
  nativeSetter.call(element, request.text);
  element.dispatchEvent(new Event("input", { bubbles: true }));

  // 再描画で値が戻されていないことを次のタスクで確かめてから応答する
  // Confirm on the next task that re-rendering did not revert the value before replying
  await new Promise<void>((resolve) => setTimeout(resolve, 0));
  return reply(element.value === request.text ? "applied" : "valueMismatch", element.value);
}
```

`main.tsx`: `import { initDomSetTextResponder } from "./bridge/playtest/domSetTextResponder";` を `initDomQueryResponder` の import の次行に足し、`initDomQueryResponder();` の次行に `initDomSetTextResponder();` を足す。

- [ ] **Step 5: テストを書く**

`domQueryResponder.test.ts` の末尾に追加（既存の `query()` ヘルパと `element` を使う）:

```ts
describe("一致件数と状態フラグ", () => {
  it("値が空かtrueのdata属性だけを状態フラグとして返す", () => {
    element.setAttribute("data-selected", "true");
    element.setAttribute("data-ghost", "");
    element.setAttribute("data-kind", "bug");
    element.setAttribute("data-locked", "false");
    expect(query()).toMatchObject({ found: true, matchCount: 1, stateFlags: ["ghost", "selected"] });
  });

  it("同じtestidが複数あれば件数を返し、先頭要素を読む", () => {
    const duplicate = document.createElement("div");
    duplicate.dataset.testid = "hud";
    duplicate.innerText = "second";
    document.body.append(duplicate);
    expect(query()).toMatchObject({ found: true, matchCount: 2, text: "Height: 1" });
  });

  it("見つからなければ件数0で状態フラグは空", () => {
    element.remove();
    expect(query()).toMatchObject({ found: false, matchCount: 0, stateFlags: [] });
  });
});
```

`domSetTextResponder.test.ts`:

```ts
// @vitest-environment jsdom
// 既存テストは react-test-renderer だが、ネイティブsetterとdispatchEventには実DOMが要るためreact-dom/clientで描画する
// Existing tests use react-test-renderer, but the native setter and dispatchEvent need a real DOM, so render with react-dom/client
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { createElement, useState } from "react";
import { createRoot, type Root } from "react-dom/client";
import { act } from "react";
import { useTopicStore } from "../store/topicStore";
import { Topics } from "../transport/protocol";
import { sendAction } from "../transport/webSocketClient";
import { initDomSetTextResponder } from "./domSetTextResponder";

vi.mock("../transport/webSocketClient", () => ({ sendAction: vi.fn().mockResolvedValue({ ok: true }) }));
vi.mock("../transport/subscriptionManager", () => ({ subscriptions: { acquire: vi.fn(), release: vi.fn() } }));

let dispose: () => void;
let root: Root | null = null;
let observed = "";

beforeEach(() => {
  vi.mocked(sendAction).mockClear().mockResolvedValue({ ok: true });
  vi.stubGlobal("CSS", { escape: (value: string) => value });
  vi.stubGlobal("IS_REACT_ACT_ENVIRONMENT", true);
  useTopicStore.setState({ topics: {}, status: "open" });
  observed = "";
  dispose = initDomSetTextResponder();
});

afterEach(() => {
  dispose();
  act(() => root?.unmount());
  root = null;
  document.body.replaceChildren();
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

// Reactの制御textareaを描画し、onChangeで受けた値を記録する
// Render a React controlled textarea and record the value received by onChange
function renderControlledTextarea(disabled: boolean) {
  function Field() {
    const [value, setValue] = useState("");
    return createElement("textarea", {
      "data-testid": "desc",
      value,
      disabled,
      onChange: (e: { currentTarget: HTMLTextAreaElement }) => { observed = e.currentTarget.value; setValue(e.currentTarget.value); },
    });
  }
  const container = document.createElement("div");
  document.body.append(container);
  root = createRoot(container);
  act(() => root!.render(createElement(Field)));
  const textarea = container.querySelector("textarea")!;
  vi.spyOn(textarea, "getBoundingClientRect").mockReturnValue(new DOMRect(10, 10, 100, 20));
}

async function setText(testid: string, text: string) {
  await act(async () => {
    useTopicStore.setState({ topics: { [Topics.playtestDomSetText]: { requestId: "r", testid, text } } });
    await new Promise((resolve) => setTimeout(resolve, 5));
  });
  return vi.mocked(sendAction).mock.calls[0];
}

describe("プレイテストの文字入力", () => {
  it("制御textareaのonChangeが走り、値が保たれればappliedを返す", async () => {
    renderControlledTextarea(false);
    const [action, payload] = await setText("desc", "日本語の本文");
    expect(action).toBe("playtest.dom_set_text_result");
    expect(payload).toMatchObject({ requestId: "r", status: "applied", matchCount: 1, actualValue: "日本語の本文" });
    expect(observed).toBe("日本語の本文");
  });

  it("disabledの欄はnotInteractableで値を変えない", async () => {
    renderControlledTextarea(true);
    const [, payload] = await setText("desc", "x");
    expect(payload).toMatchObject({ status: "notInteractable", actualValue: "" });
  });

  it("テキスト欄でない要素はnotTextField", async () => {
    const div = document.createElement("div");
    div.dataset.testid = "plain";
    document.body.append(div);
    const [, payload] = await setText("plain", "x");
    expect(payload).toMatchObject({ status: "notTextField" });
  });

  it("見つからなければnotFound", async () => {
    const [, payload] = await setText("missing", "x");
    expect(payload).toMatchObject({ status: "notFound", matchCount: 0, actualValue: "" });
  });
});
```

`react` の `act` が使えない版なら `react-dom/test-utils` の `act` に替える（`pnpm ls react` で版を見る。package.json は `^18.3.1`）。jsdom の既定算出値が `isElementVisible` を落とす場合は、`domQueryResponder.test.ts` の `beforeEach` と同じ既定値 `<style>` をこのテストの `beforeEach` にも入れる。

- [ ] **Step 6: テスト・lint・型検査を通す**

Run: `cd moorestech_web/webui && pnpm test -- src/bridge/playtest && pnpm lint && pnpm exec tsc -b`
Expected: 全 PASS / lint エラー 0 / 型エラー 0

- [ ] **Step 7: コミットする**

```bash
git add moorestech_web/webui/src/bridge/playtest moorestech_web/webui/src/bridge/transport/protocol.ts moorestech_web/webui/src/main.tsx
git commit -m "feat(webui-playtest): DOM照会に状態フラグと一致件数を足し、プレイテスト用の文字入力応答を足す"
```

---

### Task 3: 描画系非依存の型と Web 版ロケータ（C#）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Playtest/UiElements/IUiElementLocator.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Playtest/UiElements/UiElementSnapshot.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Playtest/UiElements/UiElementLookupStatus.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Playtest/UiElements/UiRenderer.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Playtest/UiElements/UiSetTextStatus.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Playtest/UiElements/UiSetTextResult.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Playtest/UiElements/PlaytestUiLocatorSession.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Playtest/WebUi/PlaytestWebUiRequestChannel.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Playtest/WebUi/WebUiPayloadReader.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Playtest/WebUi/DomQueryResultActionHandler.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Playtest/WebUi/PlaytestDomSetText.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Playtest/WebUi/DomSetTextResultActionHandler.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Playtest/WebUi/WebUiElementLocator.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Playtest/WebUi/PlaytestDomQuery.cs`（全体を置き換え）
- Modify: `moorestech_client/Assets/Scripts/Client.Playtest/WebUi/CefScreenMapper.cs`（`internal` 化と矩形写像の追加）
- Modify: `moorestech_client/Assets/Scripts/Client.Playtest/WebUi/CefInputForwarder.cs:18`（`public static class` → `internal static class`）
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/action_names.json:48-50`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Playtest/PlaytestWebUiLocatorContractTest.cs`

**Interfaces:**
- Consumes: Task 2 の wire 契約（`matchCount`・`stateFlags`・`playtest.dom_set_text`・`playtest.dom_set_text_result`）
- Produces（Task 4・5 が使う）:
  - `public interface IUiElementLocator { bool IsAvailable(); UniTask<UiElementSnapshot> Find(string testid); UniTask<UiSetTextResult> SetText(string testid, string text); }`（namespace `Client.Playtest.UiElements`）
  - `public class UiElementSnapshot`（readonly フィールド `Testid`・`Status`・`Renderer`・`Visible`・`Clickable`・`ScreenRect`・`Text`・`StateFlags`、`bool IsFound`、`bool HasStateFlag(string)`、static `Found(...)`・`NotFound(string, UiRenderer)`・`Ambiguous(string, UiRenderer)`・`Unavailable(string, UiRenderer)`）
  - `public enum UiElementLookupStatus { Found, NotFound, Ambiguous, RendererUnavailable }`
  - `public enum UiRenderer { Web }`
  - `public enum UiSetTextStatus { Applied, NotFound, NotTextField, NotInteractable, ValueMismatch, Ambiguous, RendererUnavailable }`
  - `public readonly struct UiSetTextResult`（`Status`・`Renderer`・`ActualValue`）
  - `public static class PlaytestUiLocatorSession { public static readonly IUiElementLocator Locator; public static void Start(); public static void Stop(); }`

- [ ] **Step 1: 描画系非依存の型を書く**

`UiElements/IUiElementLocator.cs`:

```csharp
using Cysharp.Threading.Tasks;

namespace Client.Playtest.UiElements
{
    /// <summary>
    ///     testidでUI要素を照会し文字を入れる、描画系に依存しない窓口
    ///     Web版（CEF）を段階0で、UITK版と両系照合の合成を段階1で同じinterfaceに実装する
    ///     Renderer-independent gateway that looks up UI elements by testid and enters text
    ///     Web (CEF) is implemented in stage 0; UITK and the both-renderer composite implement the same interface in stage 1
    /// </summary>
    public interface IUiElementLocator
    {
        // 照会先の描画系がいま応答できるか
        // Whether the backing renderer can answer right now
        bool IsAvailable();

        // 1回だけ照会する。待ち・再試行は呼び出し側が持つ
        // Look up once; waiting and retries belong to the caller
        UniTask<UiElementSnapshot> Find(string testid);

        // テキスト欄の値を置き換えて変更イベントを流す。IMEは通さない
        // Replace a text field's value and raise its change event without IME
        UniTask<UiSetTextResult> SetText(string testid, string text);
    }
}
```

`UiElements/UiElementLookupStatus.cs`:

```csharp
namespace Client.Playtest.UiElements
{
    public enum UiElementLookupStatus
    {
        Found,
        NotFound,
        // 同じtestidが2件以上ある（UITK版と両系照合が返す）
        // Two or more elements share the testid (returned by UITK and the both-renderer composite)
        Ambiguous,
        // 描画系が応答しない（Hubなし・受け口未登録・応答タイムアウト・CEF描画面なし）
        // The renderer did not answer (no hub, unregistered receiver, response timeout, or no CEF surface)
        RendererUnavailable,
    }
}
```

`UiElements/UiRenderer.cs`:

```csharp
namespace Client.Playtest.UiElements
{
    // どの描画系で解決したか。段階1でUitkを足す
    // Which renderer resolved the element; Uitk is added in stage 1
    public enum UiRenderer
    {
        Web,
    }
}
```

`UiElements/UiElementSnapshot.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Client.Playtest.UiElements
{
    /// <summary>
    ///     1回の照会で得たUI要素の状態。Found以外では可視・矩形・文字列・状態フラグは空値
    ///     UI element state from one lookup; visibility, rect, text, and flags are empty unless Found
    /// </summary>
    public class UiElementSnapshot
    {
        public readonly string Testid;
        public readonly UiElementLookupStatus Status;
        public readonly UiRenderer Renderer;
        // 祖先のクリップと画面の内側に見える面積がある
        // Has visible area inside ancestor clips and the screen
        public readonly bool Visible;
        // 矩形中心へのクリックが自身か子孫に当たる（覆われていない）
        // A click at the rect center hits itself or a descendant (not covered)
        public readonly bool Clickable;
        // Unityスクリーン座標（左下原点・px）
        // Unity screen coordinates (bottom-left origin, pixels)
        public readonly Rect ScreenRect;
        public readonly string Text;
        // Webはdata-*属性、UITKはUSSクラスから作る状態名
        // State names built from data-* attributes on Web and USS classes on UITK
        public readonly IReadOnlyCollection<string> StateFlags;

        private UiElementSnapshot(string testid, UiElementLookupStatus status, UiRenderer renderer, bool visible, bool clickable, Rect screenRect, string text, IReadOnlyCollection<string> stateFlags)
        {
            Testid = testid;
            Status = status;
            Renderer = renderer;
            Visible = visible;
            Clickable = clickable;
            ScreenRect = screenRect;
            Text = text;
            StateFlags = stateFlags;
        }

        public bool IsFound => Status == UiElementLookupStatus.Found;

        public bool HasStateFlag(string flag)
        {
            foreach (var stateFlag in StateFlags)
            {
                if (stateFlag == flag) return true;
            }
            return false;
        }

        public static UiElementSnapshot Found(string testid, UiRenderer renderer, bool visible, bool clickable, Rect screenRect, string text, IReadOnlyCollection<string> stateFlags)
        {
            return new UiElementSnapshot(testid, UiElementLookupStatus.Found, renderer, visible, clickable, screenRect, text, stateFlags);
        }

        public static UiElementSnapshot NotFound(string testid, UiRenderer renderer)
        {
            return Empty(testid, UiElementLookupStatus.NotFound, renderer);
        }

        public static UiElementSnapshot Ambiguous(string testid, UiRenderer renderer)
        {
            return Empty(testid, UiElementLookupStatus.Ambiguous, renderer);
        }

        public static UiElementSnapshot Unavailable(string testid, UiRenderer renderer)
        {
            return Empty(testid, UiElementLookupStatus.RendererUnavailable, renderer);
        }

        private static UiElementSnapshot Empty(string testid, UiElementLookupStatus status, UiRenderer renderer)
        {
            return new UiElementSnapshot(testid, status, renderer, false, false, Rect.zero, string.Empty, Array.Empty<string>());
        }
    }
}
```

`UiElements/UiSetTextStatus.cs`:

```csharp
namespace Client.Playtest.UiElements
{
    public enum UiSetTextStatus
    {
        Applied,
        NotFound,
        // テキスト欄ではない（Webはinput/textarea、UITKはTextField以外）
        // Not a text field (other than input/textarea on Web, TextField on UITK)
        NotTextField,
        // 非表示・無効・読み取り専用で今は入力できない
        // Hidden, disabled, or read-only, so input is not possible now
        NotInteractable,
        // 設定後の値が要求と違う（文字数上限で切られた等）
        // The value after setting differs from the request (e.g. truncated by a length limit)
        ValueMismatch,
        Ambiguous,
        RendererUnavailable,
    }
}
```

`UiElements/UiSetTextResult.cs`:

```csharp
namespace Client.Playtest.UiElements
{
    public readonly struct UiSetTextResult
    {
        public readonly UiSetTextStatus Status;
        public readonly UiRenderer Renderer;
        // 設定を試みた後の欄の値（欄が無ければ空）
        // The field value after the attempt (empty when there is no field)
        public readonly string ActualValue;

        public UiSetTextResult(UiSetTextStatus status, UiRenderer renderer, string actualValue)
        {
            Status = status;
            Renderer = renderer;
            ActualValue = actualValue;
        }
    }
}
```

`UiElements/PlaytestUiLocatorSession.cs`:

```csharp
using Client.Playtest.WebUi;

namespace Client.Playtest.UiElements
{
    /// <summary>
    ///     シナリオが使うロケータを1か所で持ち、照会の受け口と入力転送を開閉する
    ///     Holds the locator used by scenarios in one place and opens/closes query receivers and input forwarding
    /// </summary>
    public static class PlaytestUiLocatorSession
    {
        private static readonly WebUiElementLocator WebLocator = new();
        // 段階1でUITK版と両系照合の合成へ差し替える
        // Replaced by the UITK + both-renderer composite in stage 1
        public static readonly IUiElementLocator Locator = WebLocator;

        // 冪等。PlaytestRunnerとRun前のprobeの両方から呼ばれる
        // Idempotent; called from both PlaytestRunner and pre-Run probes
        public static void Start()
        {
            WebLocator.StartSession();
        }

        // 成否にかかわらず転送を止め、次回実行へ応答を持ち越さない
        // Stop forwarding and discard pending responses regardless of outcome
        public static void Stop()
        {
            WebLocator.StopSession();
        }
    }
}
```

- [ ] **Step 2: Web 版の往復部品を書く**

`WebUi/PlaytestWebUiRequestChannel.cs`:

```csharp
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Playtest.WebUi
{
    /// <summary>
    ///     Web UIへの要求をrequestIdで保留し、action応答で解決する
    ///     Holds Web UI requests by requestId and resolves them from action responses
    /// </summary>
    internal class PlaytestWebUiRequestChannel<TResult> where TResult : class
    {
        private readonly Dictionary<string, TResult> _pending = new();

        public string Open()
        {
            // 配信前に保留登録し、即時応答も取りこぼさない
            // Register as pending before publishing so even an immediate response is retained
            var requestId = Guid.NewGuid().ToString();
            _pending[requestId] = null;
            return requestId;
        }

        public void Resolve(string requestId, TResult result)
        {
            // タイムアウト済みの遅延応答は捨て、待機中のrequestIdだけを解決する
            // Drop late responses after timeout and resolve only requestIds still pending
            if (_pending.ContainsKey(requestId)) _pending[requestId] = result;
        }

        public async UniTask<WebUiChannelResponse<TResult>> Await(string requestId, float timeoutSeconds)
        {
            // 応答をフレーム単位で待ち、期限切れは応答なしとして返す
            // Poll per frame and report no response once the deadline passes
            var startedAt = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - startedAt <= timeoutSeconds)
            {
                // Resetで保留が消えたら、待ち続けず応答なしとして返す
                // When Reset dropped the pending entry, stop waiting and report no response
                if (!_pending.TryGetValue(requestId, out var result)) return WebUiChannelResponse<TResult>.TimedOut();
                if (result != null)
                {
                    _pending.Remove(requestId);
                    return WebUiChannelResponse<TResult>.Responded(result);
                }
                await UniTask.Yield();
            }
            _pending.Remove(requestId);
            return WebUiChannelResponse<TResult>.TimedOut();
        }

        public void Reset()
        {
            _pending.Clear();
        }
    }

    internal readonly struct WebUiChannelResponse<TResult> where TResult : class
    {
        public readonly bool HasResponse;
        public readonly TResult Result;

        private WebUiChannelResponse(bool hasResponse, TResult result)
        {
            HasResponse = hasResponse;
            Result = result;
        }

        public static WebUiChannelResponse<TResult> Responded(TResult result)
        {
            return new WebUiChannelResponse<TResult>(true, result);
        }

        public static WebUiChannelResponse<TResult> TimedOut()
        {
            return new WebUiChannelResponse<TResult>(false, null);
        }
    }
}
```

`WebUi/WebUiPayloadReader.cs`（`PlaytestDomQuery` のローカル関数を移して共有する）:

```csharp
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Client.Playtest.WebUi
{
    /// <summary>
    ///     Web UIから届くaction payload（外部入力）を型ごとに読む
    ///     Reads action payloads arriving from the Web UI (external input) field by field
    /// </summary>
    internal static class WebUiPayloadReader
    {
        public static bool TryReadNonEmptyString(JObject payload, string name, out string value)
        {
            return TryReadString(payload, name, out value) && value.Length != 0;
        }

        public static bool TryReadString(JObject payload, string name, out string value)
        {
            value = null;
            if (payload?[name] is not JValue { Type: JTokenType.String } token) return false;
            value = (string)token;
            return true;
        }

        public static bool TryReadBool(JObject payload, string name, out bool value)
        {
            value = false;
            if (payload?[name] is not JValue { Type: JTokenType.Boolean } token) return false;
            value = (bool)token;
            return true;
        }

        public static bool TryReadFloat(JObject payload, string name, out float value)
        {
            value = 0f;
            var token = payload?[name];
            if (token?.Type != JTokenType.Float && token?.Type != JTokenType.Integer) return false;
            value = (float)token;
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        public static bool TryReadNonNegativeInt(JObject payload, string name, out int value)
        {
            value = 0;
            if (payload?[name] is not JValue { Type: JTokenType.Integer } token) return false;
            value = (int)token;
            return 0 <= value;
        }

        public static bool TryReadStringArray(JObject payload, string name, out string[] values)
        {
            values = null;
            if (payload?[name] is not JArray array) return false;

            // 要素が1つでも文字列でなければ配列ごと不正とする
            // Reject the whole array when any element is not a string
            var list = new List<string>(array.Count);
            foreach (var item in array)
            {
                if (item is not JValue { Type: JTokenType.String } stringToken) return false;
                list.Add((string)stringToken);
            }
            values = list.ToArray();
            return true;
        }
    }
}
```

`WebUi/PlaytestDomQuery.cs` 全体:

```csharp
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Client.WebUiHost.Game.Actions;
using Cysharp.Threading.Tasks;

namespace Client.Playtest.WebUi
{
    internal class DomQueryResult
    {
        public bool Found;
        public bool Visible;
        public float X;
        public float Y;
        public float Width;
        public float Height;
        public float DevicePixelRatio;
        public bool HitTestPassed;
        public string Text;
        public int MatchCount;
        public string[] StateFlags;
    }

    /// <summary>
    ///     Web UIへtestidのDOM矩形・文字列・状態フラグを問い合わせる
    ///     Queries the Web UI for a testid's DOM rect, text, and state flags
    /// </summary>
    internal class PlaytestDomQuery
    {
        private const string QueryTopic = "playtest.dom_query";
        private readonly PlaytestWebUiRequestChannel<DomQueryResult> _channel = new();
        public readonly IActionHandler ResultHandler;

        public PlaytestDomQuery()
        {
            ResultHandler = new DomQueryResultActionHandler(_channel);
        }

        public async UniTask<WebUiChannelResponse<DomQueryResult>> Query(WebSocketHub hub, string testid, float timeoutSeconds)
        {
            var requestId = _channel.Open();
            hub.Publish(QueryTopic, WebUiJson.Serialize(new DomQueryRequest { RequestId = requestId, Testid = testid }));
            return await _channel.Await(requestId, timeoutSeconds);
        }

        public void Reset()
        {
            _channel.Reset();
        }

        public static bool IsValidResult(DomQueryResult result)
        {
            // 有限性はWebUiPayloadReader.TryReadFloatが見るので、ここでは値域と発見・件数・矩形の相関だけを検証する
            // TryReadFloat already checks finiteness, so validate only ranges and the found/count/rect correlation here
            if (result.Width < 0f || result.Height < 0f || result.DevicePixelRatio <= 0f) return false;
            if (result.Found != (0 < result.MatchCount) || result.StateFlags == null) return false;
            return !result.HitTestPassed || (0f < result.Width && 0f < result.Height);
        }

        private class DomQueryRequest
        {
            public string RequestId;
            public string Testid;
        }
    }
}
```

`WebUi/DomQueryResultActionHandler.cs`:

```csharp
using Client.WebUiHost.Game.Actions;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Client.Playtest.WebUi
{
    /// <summary>
    ///     playtest.dom_query_result を検証して保留中の照会へ渡す
    ///     Validates playtest.dom_query_result and hands it to the pending query
    /// </summary>
    internal class DomQueryResultActionHandler : IActionHandler
    {
        private readonly PlaytestWebUiRequestChannel<DomQueryResult> _channel;

        public DomQueryResultActionHandler(PlaytestWebUiRequestChannel<DomQueryResult> channel)
        {
            _channel = channel;
        }

        public string ActionType => "playtest.dom_query_result";

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            // WS外部入力を型ごとに検証し、不正な応答を保留へ混入させない
            // Validate each externally sourced WS value before admitting it to pending
            if (!WebUiPayloadReader.TryReadNonEmptyString(payload, "requestId", out var requestId) ||
                !WebUiPayloadReader.TryReadBool(payload, "found", out var found) ||
                !WebUiPayloadReader.TryReadBool(payload, "visible", out var visible) ||
                !WebUiPayloadReader.TryReadFloat(payload, "x", out var x) ||
                !WebUiPayloadReader.TryReadFloat(payload, "y", out var y) ||
                !WebUiPayloadReader.TryReadFloat(payload, "width", out var width) ||
                !WebUiPayloadReader.TryReadFloat(payload, "height", out var height) ||
                !WebUiPayloadReader.TryReadFloat(payload, "devicePixelRatio", out var devicePixelRatio) ||
                !WebUiPayloadReader.TryReadBool(payload, "hitTestPassed", out var hitTestPassed) ||
                !WebUiPayloadReader.TryReadString(payload, "text", out var text) ||
                !WebUiPayloadReader.TryReadNonNegativeInt(payload, "matchCount", out var matchCount) ||
                !WebUiPayloadReader.TryReadStringArray(payload, "stateFlags", out var stateFlags))
            {
                return UniTask.FromResult(ActionResult.Fail("invalid_payload"));
            }

            // 相関制約を保留への格納前に評価する
            // Evaluate cross-field constraints before storing as pending
            var result = new DomQueryResult
            {
                Found = found,
                Visible = visible,
                X = x,
                Y = y,
                Width = width,
                Height = height,
                DevicePixelRatio = devicePixelRatio,
                HitTestPassed = hitTestPassed,
                Text = text,
                MatchCount = matchCount,
                StateFlags = stateFlags,
            };
            if (!PlaytestDomQuery.IsValidResult(result)) return UniTask.FromResult(ActionResult.Fail("invalid_payload"));
            _channel.Resolve(requestId, result);
            return UniTask.FromResult(ActionResult.Success());
        }
    }
}
```

`WebUi/PlaytestDomSetText.cs`:

```csharp
using Client.Playtest.UiElements;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Client.WebUiHost.Game.Actions;
using Cysharp.Threading.Tasks;

namespace Client.Playtest.WebUi
{
    internal class DomSetTextResult
    {
        public UiSetTextStatus Status;
        public int MatchCount;
        public string ActualValue;
    }

    /// <summary>
    ///     Web UIのテキスト欄へ値を設定させ、結果を受け取る
    ///     Asks the Web UI to set a text field's value and receives the outcome
    /// </summary>
    internal class PlaytestDomSetText
    {
        private const string SetTextTopic = "playtest.dom_set_text";
        private readonly PlaytestWebUiRequestChannel<DomSetTextResult> _channel = new();
        public readonly IActionHandler ResultHandler;

        public PlaytestDomSetText()
        {
            ResultHandler = new DomSetTextResultActionHandler(_channel);
        }

        public async UniTask<WebUiChannelResponse<DomSetTextResult>> Request(WebSocketHub hub, string testid, string text, float timeoutSeconds)
        {
            var requestId = _channel.Open();
            hub.Publish(SetTextTopic, WebUiJson.Serialize(new DomSetTextRequest { RequestId = requestId, Testid = testid, Text = text }));
            return await _channel.Await(requestId, timeoutSeconds);
        }

        public void Reset()
        {
            _channel.Reset();
        }

        private class DomSetTextRequest
        {
            public string RequestId;
            public string Testid;
            public string Text;
        }
    }
}
```

`WebUi/DomSetTextResultActionHandler.cs`:

```csharp
using Client.Playtest.UiElements;
using Client.WebUiHost.Game.Actions;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Client.Playtest.WebUi
{
    /// <summary>
    ///     playtest.dom_set_text_result を検証して保留中の文字入力へ渡す
    ///     Validates playtest.dom_set_text_result and hands it to the pending text input
    /// </summary>
    internal class DomSetTextResultActionHandler : IActionHandler
    {
        private readonly PlaytestWebUiRequestChannel<DomSetTextResult> _channel;

        public DomSetTextResultActionHandler(PlaytestWebUiRequestChannel<DomSetTextResult> channel)
        {
            _channel = channel;
        }

        public string ActionType => "playtest.dom_set_text_result";

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            // WS外部入力を型ごとに検証し、未知の状態語は契約不整合として拒否する
            // Validate external WS input and reject unknown status words as contract mismatches
            if (!WebUiPayloadReader.TryReadNonEmptyString(payload, "requestId", out var requestId) ||
                !WebUiPayloadReader.TryReadString(payload, "status", out var statusWord) ||
                !TryParseStatus(statusWord, out var status) ||
                !WebUiPayloadReader.TryReadNonNegativeInt(payload, "matchCount", out var matchCount) ||
                !WebUiPayloadReader.TryReadString(payload, "actualValue", out var actualValue))
            {
                return UniTask.FromResult(ActionResult.Fail("invalid_payload"));
            }

            _channel.Resolve(requestId, new DomSetTextResult { Status = status, MatchCount = matchCount, ActualValue = actualValue });
            return UniTask.FromResult(ActionResult.Success());
        }

        private static bool TryParseStatus(string word, out UiSetTextStatus status)
        {
            switch (word)
            {
                case "applied": status = UiSetTextStatus.Applied; return true;
                case "notFound": status = UiSetTextStatus.NotFound; return true;
                case "notTextField": status = UiSetTextStatus.NotTextField; return true;
                case "notInteractable": status = UiSetTextStatus.NotInteractable; return true;
                case "valueMismatch": status = UiSetTextStatus.ValueMismatch; return true;
                default: status = UiSetTextStatus.RendererUnavailable; return false;
            }
        }
    }
}
```

`CefScreenMapper.cs`: クラス宣言を `internal static class CefScreenMapper` にし、`TryBrowserToScreen` の写像部分を private メソッドへ切り出して矩形版から共有する。`TryBrowserToScreen` を次の 2 メソッド＋1 private で置き換える:

```csharp
        public static bool TryBrowserToScreen(Vector2 browserPosition, out Vector2 screenPosition)
        {
            screenPosition = default;
            if (!TryGetMappingContext(out var rawImage, out var browserWidth, out var browserHeight)) return false;
            if (browserPosition.x < 0f || browserWidth < browserPosition.x || browserPosition.y < 0f || browserHeight < browserPosition.y) return false;
            return TryMapBrowserToScreen(rawImage, browserWidth, browserHeight, browserPosition, out screenPosition);
        }

        public static bool TryBrowserRectToScreenRect(Rect browserRect, out Rect screenRect)
        {
            // 画面外へはみ出す矩形も写すため、範囲検査をせず両隅を線形に写す
            // Map both corners linearly without bounds checks so partially offscreen rects still map
            screenRect = default;
            if (!TryGetMappingContext(out var rawImage, out var browserWidth, out var browserHeight)) return false;
            if (!TryMapBrowserToScreen(rawImage, browserWidth, browserHeight, new Vector2(browserRect.xMin, browserRect.yMax), out var cornerA)) return false;
            if (!TryMapBrowserToScreen(rawImage, browserWidth, browserHeight, new Vector2(browserRect.xMax, browserRect.yMin), out var cornerB)) return false;
            screenRect = Rect.MinMaxRect(Mathf.Min(cornerA.x, cornerB.x), Mathf.Min(cornerA.y, cornerB.y), Mathf.Max(cornerA.x, cornerB.x), Mathf.Max(cornerA.y, cornerB.y));
            return true;
        }

        private static bool TryMapBrowserToScreen(RawImage rawImage, int browserWidth, int browserHeight, Vector2 browserPosition, out Vector2 screenPosition)
        {
            screenPosition = default;

            // CEFの上原点座標をRawImageの下原点ローカル座標へ逆変換する
            // Invert CEF's top-origin coordinates into RawImage's bottom-origin local space
            var normalizedX = browserPosition.x / browserWidth;
            var normalizedY = 1f - browserPosition.y / browserHeight;
            var rectTransform = rawImage.rectTransform;
            var rect = rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f) return false;
            var localPosition = new Vector2(rect.x + normalizedX * rect.width, rect.y + normalizedY * rect.height);

            // Canvasのカメラを通してワールド座標からUnityスクリーン座標へ戻す
            // Return from world space to Unity screen space through the canvas camera
            var canvas = rawImage.canvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var worldPosition = rectTransform.TransformPoint(localPosition);
            screenPosition = RectTransformUtility.WorldToScreenPoint(camera, worldPosition);
            return true;
        }
```

`CefInputForwarder.cs:18`: `public static class CefInputForwarder` → `internal static class CefInputForwarder`。

- [ ] **Step 3: Web 版ロケータを書く**

`WebUi/WebUiElementLocator.cs`:

```csharp
using Client.Playtest.UiElements;
using Client.WebUiHost.Boot;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Playtest.WebUi
{
    /// <summary>
    ///     IUiElementLocatorのWeb（CEF）版。DOM照会と文字入力をWS往復で行う
    ///     Web (CEF) implementation of IUiElementLocator; DOM lookups and text input go over WS round trips
    /// </summary>
    internal class WebUiElementLocator : IUiElementLocator
    {
        // 通常は数フレームで返る。CEF高負荷時の遅れを見込んだ上限
        // Responses usually arrive within frames; this cap allows for CEF lag under load
        private const float ResponseTimeoutSeconds = 3f;
        private readonly PlaytestDomQuery _domQuery = new();
        private readonly PlaytestDomSetText _domSetText = new();
        private WebSocketHub _registeredHub;
        private bool _warnedUnregistered;

        public void StartSession()
        {
            // 前回実行の保留を捨て、同一Hubへの二重登録を避ける
            // Discard pending requests from earlier runs and avoid double registration on the same hub
            _domQuery.Reset();
            _domSetText.Reset();
            _warnedUnregistered = false;
            var hub = global::Client.WebUiHost.Boot.WebUiHost.Hub;
            if (hub != null && !ReferenceEquals(hub, _registeredHub))
            {
                hub.RegisterAction(_domQuery.ResultHandler);
                hub.RegisterAction(_domSetText.ResultHandler);
                _registeredHub = hub;
            }

            // CEFが使えるときだけInputSystemのマウスをCEFへ転送する
            // Forward InputSystem mouse input to CEF only while CEF is available
            if (CefScreenMapper.IsWebUiAvailable()) CefInputForwarder.StartForwarding();
        }

        public void StopSession()
        {
            CefInputForwarder.StopForwarding();
            _domQuery.Reset();
            _domSetText.Reset();
        }

        public bool IsAvailable()
        {
            return CefScreenMapper.IsWebUiAvailable();
        }

        public async UniTask<UiElementSnapshot> Find(string testid)
        {
            if (!TryGetRegisteredHub(out var hub)) return UiElementSnapshot.Unavailable(testid, UiRenderer.Web);
            var response = await _domQuery.Query(hub, testid, ResponseTimeoutSeconds);
            if (!response.HasResponse)
            {
                // 応答なしを「見つからない」と区別できるよう理由を残す
                // Leave the reason so no-answer is distinguishable from not-found
                Debug.LogWarning($"[PlaytestUi] Web UI gave no lookup answer (timeout {ResponseTimeoutSeconds}s or session stopped): {testid}");
                return UiElementSnapshot.Unavailable(testid, UiRenderer.Web);
            }
            if (!response.Result.Found) return UiElementSnapshot.NotFound(testid, UiRenderer.Web);

            // CSS pxの矩形をdevicePixelRatioでCEFブラウザpxへ換算してから画面へ写す
            // Convert the CSS-pixel rect to CEF browser pixels with devicePixelRatio, then map to the screen
            var result = response.Result;
            var ratio = result.DevicePixelRatio;
            var browserRect = new Rect(result.X * ratio, result.Y * ratio, result.Width * ratio, result.Height * ratio);
            if (!CefScreenMapper.TryBrowserRectToScreenRect(browserRect, out var screenRect))
            {
                Debug.LogWarning($"[PlaytestUi] Web element '{testid}' was found but the CEF surface could not map its rect");
                return UiElementSnapshot.Unavailable(testid, UiRenderer.Web);
            }
            return ToSnapshot(testid, result, screenRect);
        }

        public async UniTask<UiSetTextResult> SetText(string testid, string text)
        {
            if (!TryGetRegisteredHub(out var hub)) return new UiSetTextResult(UiSetTextStatus.RendererUnavailable, UiRenderer.Web, string.Empty);
            var response = await _domSetText.Request(hub, testid, text, ResponseTimeoutSeconds);
            if (!response.HasResponse)
            {
                Debug.LogWarning($"[PlaytestUi] Web UI gave no text-input answer (timeout {ResponseTimeoutSeconds}s or session stopped): {testid}");
                return new UiSetTextResult(UiSetTextStatus.RendererUnavailable, UiRenderer.Web, string.Empty);
            }
            WarnDuplicate(testid, response.Result.MatchCount);
            return new UiSetTextResult(response.Result.Status, UiRenderer.Web, response.Result.ActualValue);
        }

        internal static UiElementSnapshot ToSnapshot(string testid, DomQueryResult result, Rect screenRect)
        {
            WarnDuplicate(testid, result.MatchCount);
            return UiElementSnapshot.Found(testid, UiRenderer.Web, result.Visible, result.HitTestPassed, screenRect, result.Text, result.StateFlags);
        }

        private static void WarnDuplicate(string testid, int matchCount)
        {
            // 段階0のWeb版は先頭一致を保ち、重複は警告だけにする（改名前と合否を変えないため）
            // Stage-0 Web keeps first-match semantics and only warns on duplicates so pass/fail stays as before
            if (1 < matchCount) Debug.LogWarning($"[PlaytestUi] testid '{testid}' matched {matchCount} Web elements; using the first");
        }

        private bool TryGetRegisteredHub(out WebSocketHub hub)
        {
            // 受け口未登録で送ると期限まで応答を待つだけなので、即座に応答不能として返す
            // Sending without registered receivers only waits out the deadline, so report unavailable immediately
            hub = global::Client.WebUiHost.Boot.WebUiHost.Hub;
            if (hub != null && ReferenceEquals(hub, _registeredHub)) return true;
            if (!_warnedUnregistered)
            {
                Debug.LogWarning("[PlaytestUi] Web UI lookup skipped: no hub or receivers not registered on it (call PlaytestUiLocatorSession.Start)");
                _warnedUnregistered = true;
            }
            return false;
        }
    }
}
```

- [ ] **Step 4: action 名のフィクスチャを更新する**

`action_names.json` の `excludedFromWebContract`:

```json
  "excludedFromWebContract": [
    "playtest.dom_query_result",
    "playtest.dom_set_text_result"
  ]
```

- [ ] **Step 5: テストを書く**

`Client.Tests/Playtest/PlaytestWebUiLocatorContractTest.cs`:

```csharp
using System.Text.RegularExpressions;
using Client.Playtest.UiElements;
using Client.Playtest.WebUi;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Playtest
{
    public class PlaytestWebUiLocatorContractTest
    {
        [Test]
        public void DomQueryResultWithFlagsResolvesPendingRequest()
        {
            var channel = new PlaytestWebUiRequestChannel<DomQueryResult>();
            var handler = new DomQueryResultActionHandler(channel);
            var requestId = channel.Open();

            var actionResult = handler.ExecuteAsync(QueryPayload(requestId, true, 1, new JArray("selected"))).GetAwaiter().GetResult();
            var response = channel.Await(requestId, 1f).GetAwaiter().GetResult();

            Assert.IsTrue(actionResult.Ok);
            Assert.IsTrue(response.HasResponse);
            Assert.AreEqual(1, response.Result.MatchCount);
            CollectionAssert.AreEqual(new[] { "selected" }, response.Result.StateFlags);
        }

        [Test]
        public void DomQueryResultWithoutStateFlagsIsRejected()
        {
            var channel = new PlaytestWebUiRequestChannel<DomQueryResult>();
            var payload = QueryPayload(channel.Open(), true, 1, new JArray());
            payload.Remove("stateFlags");

            var actionResult = new DomQueryResultActionHandler(channel).ExecuteAsync(payload).GetAwaiter().GetResult();

            Assert.IsFalse(actionResult.Ok);
            Assert.AreEqual("invalid_payload", actionResult.Error);
        }

        [Test]
        public void FoundWithZeroMatchesIsRejected()
        {
            var channel = new PlaytestWebUiRequestChannel<DomQueryResult>();
            var actionResult = new DomQueryResultActionHandler(channel).ExecuteAsync(QueryPayload(channel.Open(), true, 0, new JArray())).GetAwaiter().GetResult();
            Assert.IsFalse(actionResult.Ok);
        }

        [Test]
        public void SnapshotCarriesFieldsAndWarnsOnDuplicates()
        {
            var result = new DomQueryResult { Found = true, Visible = true, HitTestPassed = true, Text = "送信", MatchCount = 2, StateFlags = new[] { "selected" } };
            LogAssert.Expect(LogType.Warning, new Regex("matched 2 Web elements"));

            var snapshot = WebUiElementLocator.ToSnapshot("bug-report-send", result, new Rect(10f, 20f, 30f, 40f));

            Assert.AreEqual(UiElementLookupStatus.Found, snapshot.Status);
            Assert.IsTrue(snapshot.IsFound && snapshot.Visible && snapshot.Clickable);
            Assert.AreEqual(new Rect(10f, 20f, 30f, 40f), snapshot.ScreenRect);
            Assert.AreEqual("送信", snapshot.Text);
            Assert.IsTrue(snapshot.HasStateFlag("selected"));
            Assert.IsFalse(snapshot.HasStateFlag("ghost"));
        }

        [Test]
        public void NotFoundSnapshotIsEmpty()
        {
            var snapshot = UiElementSnapshot.NotFound("missing", UiRenderer.Web);
            Assert.IsFalse(snapshot.IsFound || snapshot.Visible || snapshot.Clickable);
            Assert.AreEqual(Rect.zero, snapshot.ScreenRect);
            Assert.AreEqual(string.Empty, snapshot.Text);
            Assert.IsFalse(snapshot.HasStateFlag("selected"));
        }

        [TestCase("applied", UiSetTextStatus.Applied)]
        [TestCase("notInteractable", UiSetTextStatus.NotInteractable)]
        [TestCase("valueMismatch", UiSetTextStatus.ValueMismatch)]
        public void SetTextResultMapsStatusWord(string word, UiSetTextStatus expected)
        {
            var channel = new PlaytestWebUiRequestChannel<DomSetTextResult>();
            var requestId = channel.Open();
            var payload = new JObject { ["requestId"] = requestId, ["status"] = word, ["matchCount"] = 1, ["actualValue"] = "abc" };

            var actionResult = new DomSetTextResultActionHandler(channel).ExecuteAsync(payload).GetAwaiter().GetResult();
            var response = channel.Await(requestId, 1f).GetAwaiter().GetResult();

            Assert.IsTrue(actionResult.Ok);
            Assert.AreEqual(expected, response.Result.Status);
            Assert.AreEqual("abc", response.Result.ActualValue);
        }

        [Test]
        public void AwaitStopsWaitingOnceReset()
        {
            var channel = new PlaytestWebUiRequestChannel<DomQueryResult>();
            var requestId = channel.Open();
            channel.Reset();

            // Resetで保留が消えたら、期限を待たずに応答なしで返る
            // Once Reset drops the entry, Await returns no-response without waiting out the deadline
            var awaiter = channel.Await(requestId, 10f).GetAwaiter();
            Assert.IsTrue(awaiter.IsCompleted);
            Assert.IsFalse(awaiter.GetResult().HasResponse);
        }

        [Test]
        public void UnknownSetTextStatusIsRejected()
        {
            var channel = new PlaytestWebUiRequestChannel<DomSetTextResult>();
            var payload = new JObject { ["requestId"] = channel.Open(), ["status"] = "typed", ["matchCount"] = 1, ["actualValue"] = "" };
            var actionResult = new DomSetTextResultActionHandler(channel).ExecuteAsync(payload).GetAwaiter().GetResult();
            Assert.IsFalse(actionResult.Ok);
        }

        private static JObject QueryPayload(string requestId, bool found, int matchCount, JArray stateFlags)
        {
            return new JObject
            {
                ["requestId"] = requestId, ["found"] = found, ["visible"] = found,
                ["x"] = 1, ["y"] = 2, ["width"] = 10, ["height"] = 5, ["devicePixelRatio"] = 1,
                ["hitTestPassed"] = found, ["text"] = "t", ["matchCount"] = matchCount, ["stateFlags"] = stateFlags,
            };
        }
    }
}
```

- [ ] **Step 6: コンパイルしテストを実行する**

この時点では `PlaytestWebUiOps`・`PlaytestRunner` 等がまだ旧 static API（`PlaytestDomQuery.Query` 等）を呼んでいてコンパイルが通らない。Task 4 の Step 1〜4 まで書いてからコンパイルする（Task 3 と Task 4 は同じコンパイル単位。コミットは Task 4 の最後で 1 回にまとめてよい）。

Run（Task 4 Step 4 の後）: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlaytestWebUiLocatorContractTest|WireContractActionNamesTest"`
Expected: ErrorCount 0 / 全 PASS

---

### Task 4: DSL の操作を interface 経由にして改名し、Driver を 200 行未満へ戻す

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Playtest/UiElements/UiElementScreenPointResolver.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Playtest/PlaytestWaitActions.cs`
- Modify（`git mv`）: `moorestech_client/Assets/Scripts/Client.Playtest/Operations/PlaytestWebUiOps.cs` → `moorestech_client/Assets/Scripts/Client.Playtest/Operations/PlaytestUiElementOps.cs`（.meta も `git mv`）
- Modify: `moorestech_client/Assets/Scripts/Client.Playtest/PlaytestDriver.cs`（フィールド・ctor 40〜47 行付近、`WaitBlockGameObject` 91〜97 行、UI 操作と `WaitUiState` 114〜132 行、`WaitSeconds` 202〜206 行）
- Modify: `moorestech_client/Assets/Scripts/Client.Playtest/PlaytestRunner.cs:46-50,66-71`
- Modify: `moorestech_client/Assets/Scripts/Client.Playtest/Operations/Ui/PlaytestBuildMenuOps.cs:19-27,43-61`
- Modify: `moorestech_client/Assets/Scripts/Client.Playtest/Operations/Ui/BlueprintCopyPasteScenarioChecks.cs:35-41`

**Interfaces:**
- Consumes: Task 3 の `IUiElementLocator`・`UiElementSnapshot`・`UiElementLookupStatus`・`UiSetTextResult`・`UiSetTextStatus`・`PlaytestUiLocatorSession`
- Produces（Task 5・6 のシナリオと文書が使う）:
  - `PlaytestDriver`: `UniTask ClickUi(string testid)`・`UniTask HoverUi(string testid)`・`UniTask UntilUiElement(string testid, float timeoutSeconds)`・`UniTask SetText(string testid, string text)`・`UniTask ClickBuildMenuBlock(string blockName)`・`UniTask CloseBuildMenuPanel()`
  - `Client.Playtest.Operations.Ui.PlaytestBuildMenuOps`: `string BuildMenuBlockTestId(string)`（BlockGuid由来のtestidを所有）。
  - `public static class PlaytestUiElementOps`（namespace `Client.Playtest.Operations`）: `UniTask<UiElementSnapshot> FindUi(string)`・`UniTask ClickUi(string)`・`UniTask ClickUi(string, float)`・`UniTask HoverUi(string)`・`UniTask WaitUiElement(string, float)`・`UniTask ScrollDownUntilUiElementIsClickable(string, string, float)`・`UniTask WaitUiTextContains(string, string, float)`・`UniTask SetText(string, string)`（待ち上限は `ClickUi(string)` と同じ `ActionTimeoutSeconds`）・`UniTask SetText(string, string, float)`
  - `internal class PlaytestWaitActions`（namespace `Client.Playtest`。Driver の private フィールドからだけ使い、シナリオへの 2 本目の道を作らない）: ctor `(PlaytestReporter)`、`UntilUiElement(string, float)`・`WaitUiState(UIStateEnum, float)`・`WaitBlockGameObject(Vector3Int)`・`WaitSeconds(float)`

- [ ] **Step 1: 安定待ちの解決を移す**

`UiElements/UiElementScreenPointResolver.cs`:

```csharp
using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Playtest.UiElements
{
    /// <summary>
    ///     testidをクリック可能かつ矩形が落ち着いた要素へ解決する
    ///     Resolves a testid to an element that is clickable with a settled rect
    /// </summary>
    internal static class UiElementScreenPointResolver
    {
        public static async UniTask<UiElementSnapshot> ResolveClickableUntil(IUiElementLocator locator, string testid, float timeoutSeconds)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            var lastStatus = UiElementLookupStatus.NotFound;

            // 未表示・被覆中・変動中の要素は一時状態として期限内で再試行する
            // Retry hidden, covered, or moving elements as transient until the deadline
            while (Time.realtimeSinceStartup <= deadline)
            {
                var first = await locator.Find(testid);
                ThrowIfAmbiguous(first);
                lastStatus = first.Status;
                if (IsClickable(first))
                {
                    // 2フレーム後も同じ矩形ならレイアウトが落ち着いたとみなす
                    // Treat the layout as settled when the rect is unchanged two frames later
                    await UniTask.DelayFrame(2);
                    var second = await locator.Find(testid);
                    ThrowIfAmbiguous(second);
                    if (IsClickable(second) && second.ScreenRect == first.ScreenRect) return second;
                }
                await UniTask.DelayFrame(2);
            }
            throw new TimeoutException($"UI element could not be resolved (last: {lastStatus}): {testid}");
        }

        public static bool IsClickable(UiElementSnapshot snapshot)
        {
            return snapshot.IsFound && snapshot.Clickable;
        }

        public static void ThrowIfAmbiguous(UiElementSnapshot snapshot)
        {
            // 重複testidはどれを押すか決められないため待たずに失敗させる
            // Duplicate testids cannot pick a target, so fail without waiting
            if (snapshot.Status == UiElementLookupStatus.Ambiguous)
                throw new InvalidOperationException($"UI testid matched multiple elements ({snapshot.Renderer}): {snapshot.Testid}");
        }
    }
}
```

- [ ] **Step 2: `PlaytestWebUiOps.cs` を `PlaytestUiElementOps.cs` へ `git mv` し、全体を置き換える**

```bash
git mv moorestech_client/Assets/Scripts/Client.Playtest/Operations/PlaytestWebUiOps.cs moorestech_client/Assets/Scripts/Client.Playtest/Operations/PlaytestUiElementOps.cs
git mv moorestech_client/Assets/Scripts/Client.Playtest/Operations/PlaytestWebUiOps.cs.meta moorestech_client/Assets/Scripts/Client.Playtest/Operations/PlaytestUiElementOps.cs.meta
```

```csharp
using System;
using Client.Playtest.Input;
using Client.Playtest.UiElements;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Playtest.Operations
{
    /// <summary>
    ///     testidのUI要素をロケータで解決し、セマンティック入力で操作する（描画系に依存しない）
    ///     Resolves testid UI elements through the locator and operates them with semantic input (renderer-independent)
    /// </summary>
    public static class PlaytestUiElementOps
    {
        private const float ActionTimeoutSeconds = 15f;
        private const float MouseGlideSeconds = 0.3f;

        // PlaytestBuildMenuOps・Driver・シナリオでBlockGuid由来testid生成を共有し、契約の二重定義を防ぐ
        // Share BlockGuid-derived testid generation across build-menu ops, the driver, and scenarios to prevent drift
        public static UniTask<UiElementSnapshot> FindUi(string testid)
        {
            return PlaytestUiLocatorSession.Locator.Find(testid);
        }

        public static async UniTask ClickUi(string testid)
        {
            await ClickUi(testid, ActionTimeoutSeconds);
        }

        public static async UniTask ClickUi(string testid, float timeoutSeconds)
        {
            // 安定した要素中心へ移動し、実プレイヤーと同じマウス経路でクリックする
            // Move to the settled element center and click through the same mouse path as a real player
            var target = await UiElementScreenPointResolver.ResolveClickableUntil(PlaytestUiLocatorSession.Locator, testid, timeoutSeconds);
            LogResolved(target);
            await SemanticInput.MouseGlideTo(target.ScreenRect.center, MouseGlideSeconds);
            await UniTask.DelayFrame(1);
            await SemanticInput.Click();
        }

        public static async UniTask HoverUi(string testid)
        {
            // クリックと同じ安定性判定を通し、対象中心へのポインタ移動だけを行う
            // Apply the same stability check as clicks, then only move the pointer to the target center
            var target = await UiElementScreenPointResolver.ResolveClickableUntil(PlaytestUiLocatorSession.Locator, testid, ActionTimeoutSeconds);
            LogResolved(target);
            await SemanticInput.MouseGlideTo(target.ScreenRect.center, MouseGlideSeconds);
            await UniTask.DelayFrame(1);
        }

        public static async UniTask WaitUiElement(string testid, float timeoutSeconds)
        {
            // 可視かつ中心が実際にヒットするまで照会を繰り返す
            // Repeat lookups until the element is visible and its center genuinely hits
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            var lastStatus = UiElementLookupStatus.NotFound;
            while (Time.realtimeSinceStartup <= deadline)
            {
                var snapshot = await FindUi(testid);
                UiElementScreenPointResolver.ThrowIfAmbiguous(snapshot);
                if (UiElementScreenPointResolver.IsClickable(snapshot))
                {
                    LogResolved(snapshot);
                    return;
                }
                lastStatus = snapshot.Status;
                await UniTask.DelayFrame(2);
            }
            throw new TimeoutException($"UI element did not become available (last: {lastStatus}): {testid}");
        }

        public static async UniTask ScrollDownUntilUiElementIsClickable(string testid, string scrollAreaTestid, float timeoutSeconds)
        {
            // スクロール領域へポインタを置き、枠外のスロットを実際のホイール入力で表示領域へ送る
            // Put the pointer on the scroll area and wheel clipped slots into view with real wheel input
            var scrollArea = await UiElementScreenPointResolver.ResolveClickableUntil(PlaytestUiLocatorSession.Locator, scrollAreaTestid, timeoutSeconds);
            await SemanticInput.MouseGlideTo(scrollArea.ScreenRect.center, MouseGlideSeconds);
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            var lastStatus = UiElementLookupStatus.NotFound;
            while (Time.realtimeSinceStartup <= deadline)
            {
                var target = await FindUi(testid);
                UiElementScreenPointResolver.ThrowIfAmbiguous(target);
                if (UiElementScreenPointResolver.IsClickable(target)) return;
                lastStatus = target.Status;
                SemanticInput.MouseWheel(-120f);
                await UniTask.DelayFrame(5);
            }
            throw new TimeoutException($"UI element stayed outside the scroll area (last: {lastStatus}): {testid}");
        }

        public static async UniTask WaitUiTextContains(string testid, string expected, float timeoutSeconds)
        {
            // 再描画を待ち、対象要素の実表示文字列を検証する
            // Wait for re-rendering and verify the element's rendered text
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            var lastText = string.Empty;
            while (Time.realtimeSinceStartup <= deadline)
            {
                var snapshot = await FindUi(testid);
                UiElementScreenPointResolver.ThrowIfAmbiguous(snapshot);
                if (snapshot.IsFound && snapshot.Visible && snapshot.Text.Contains(expected)) return;
                lastText = snapshot.Text;
                await UniTask.DelayFrame(2);
            }
            throw new TimeoutException($"UI text did not contain '{expected}' (last: '{lastText}'): {testid}");
        }

        public static async UniTask SetText(string testid, string text)
        {
            await SetText(testid, text, ActionTimeoutSeconds);
        }

        public static async UniTask SetText(string testid, string text, float timeoutSeconds)
        {
            // 欄の出現・有効化を期限内で待ち、置き換えた値が保たれたら完了とする
            // Wait for the field to appear and enable, completing once the replaced value holds
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            var lastStatus = UiSetTextStatus.NotFound;
            while (Time.realtimeSinceStartup <= deadline)
            {
                var result = await PlaytestUiLocatorSession.Locator.SetText(testid, text);
                if (result.Status == UiSetTextStatus.Applied)
                {
                    Debug.Log($"[PlaytestUi] {testid} text set by {result.Renderer}");
                    return;
                }
                if (IsPermanentFailure(result.Status))
                    throw new InvalidOperationException($"UI text input rejected ({result.Status}, actual '{result.ActualValue}'): {testid}");
                lastStatus = result.Status;
                await UniTask.DelayFrame(2);
            }
            throw new TimeoutException($"UI text input did not apply (last: {lastStatus}): {testid}");

            #region Internal

            bool IsPermanentFailure(UiSetTextStatus status)
            {
                return status == UiSetTextStatus.NotTextField || status == UiSetTextStatus.ValueMismatch || status == UiSetTextStatus.Ambiguous;
            }

            #endregion
        }

        private static void LogResolved(UiElementSnapshot snapshot)
        {
            // どの描画系で解決したかを残す（段階1以降の切替確認に使う）
            // Record which renderer resolved the element (used to confirm cutovers from stage 1)
            Debug.Log($"[PlaytestUi] {snapshot.Testid} resolved by {snapshot.Renderer}");
        }
    }
}
```

- [ ] **Step 3: Driver を改名し、待機表示つきの 4 メソッドの本体を `PlaytestWaitActions` へ移す**

前例は `PlaytestHotbarDriver`（root に置き、`PlaytestReporter` だけを受け取り、Driver を参照しない下請け）。待機表示（`BeginWait`/`EndWait`）を持つ本体だけを移し、Driver への逆参照を作らない。

`moorestech_client/Assets/Scripts/Client.Playtest/PlaytestWaitActions.cs`:

```csharp
using System;
using Client.Game.InGame.Block;
using Client.Game.InGame.UI.UIState;
using Client.Playtest.Core;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Playtest
{
    /// <summary>
    ///     PlaytestDriverの待機系操作の本体。待機中の表示とTimeline記録を伴う
    ///     Bodies of PlaytestDriver's wait operations, with the waiting overlay and Timeline record
    /// </summary>
    internal class PlaytestWaitActions
    {
        private readonly PlaytestReporter _reporter;

        public PlaytestWaitActions(PlaytestReporter reporter)
        {
            _reporter = reporter;
        }

        public async UniTask UntilUiElement(string testid, float timeoutSeconds)
        {
            // 要素待ちを待機表示し、成功後も録画用の操作間隔を確保する
            // Show element waiting and retain the recording pace after success
            var waitEntry = _reporter.BeginWait($"UI要素待ち: {testid}");
            await PlaytestUiElementOps.WaitUiElement(testid, timeoutSeconds);
            _reporter.EndWait(waitEntry);
            await UniTask.Delay(TimeSpan.FromSeconds(PlaytestReporter.ActionIntervalSeconds), ignoreTimeScale: true);
        }

        public async UniTask WaitUiState(UIStateEnum state, float timeoutSeconds)
        {
            var waitEntry = _reporter.BeginWait($"UI状態待ち: {state}");
            await PlaytestUiOps.WaitUiState(state, timeoutSeconds);
            _reporter.EndWait(waitEntry);
        }

        public async UniTask<BlockGameObject> WaitBlockGameObject(Vector3Int position)
        {
            var waitEntry = _reporter.BeginWait($"ブロック生成待ち: {position}");
            var blockGameObject = await PlaytestBlockOps.WaitBlockGameObjectSpawn(position, 15f);
            _reporter.EndWait(waitEntry);
            return blockGameObject;
        }

        public async UniTask WaitSeconds(float seconds)
        {
            _reporter.BeginWait($"{seconds:0.#}秒");
            await UniTask.Delay(TimeSpan.FromSeconds(seconds));
        }
    }
}
```

移す 3 メソッド（`WaitUiState`・`WaitBlockGameObject`・`WaitSeconds`）の本体は、今の `PlaytestDriver.cs` の本体をそのまま写す（上のコードと違っていたら Driver 側の現行本体を正とする）。

`PlaytestDriver.cs` の変更:
- フィールドに `private readonly PlaytestWaitActions _waits;` を足し、ctor の `Hotbar = new PlaytestHotbarDriver(_reporter);` の次行に `_waits = new PlaytestWaitActions(_reporter);`
- 114〜126 行（`ClickWebUi`〜`CloseWebUiPanel`）を次に置き換える:

```csharp
        public async UniTask ClickUi(string testid) => await _reporter.Act($"UIクリック: {testid}", () => PlaytestUiElementOps.ClickUi(testid));
        public async UniTask HoverUi(string testid) => await _reporter.Act($"UIホバー: {testid}", () => PlaytestUiElementOps.HoverUi(testid));
        public async UniTask UntilUiElement(string testid, float timeoutSeconds) => await _waits.UntilUiElement(testid, timeoutSeconds);
        // テキスト欄の値を置き換える（IMEは通さない）
        // Replace a text field's value (bypassing IME)
        public async UniTask SetText(string testid, string text) => await _reporter.Act($"UI文字入力: {testid}", () => PlaytestUiElementOps.SetText(testid, text));
        public async UniTask ClickBuildMenuBlock(string blockName) => await ClickUi(PlaytestBuildMenuOps.BuildMenuBlockTestId(blockName));
        public async UniTask CloseBuildMenuPanel() => await ClickUi("build-menu-close");
```

- `WaitBlockGameObject`・`WaitUiState`・`WaitSeconds` を 1 行委譲にする:

```csharp
        public async UniTask<BlockGameObject> WaitBlockGameObject(Vector3Int position) => await _waits.WaitBlockGameObject(position);
        public async UniTask WaitUiState(UIStateEnum state, float timeoutSeconds) => await _waits.WaitUiState(state, timeoutSeconds);
        public async UniTask WaitSeconds(float seconds) => await _waits.WaitSeconds(seconds);
```

`PlaceBlockViaUi`・`DragPlaceViaUi`・`Until` は Driver に残す（`Until` は `Func<bool>` を受ける既存 API で、移すと新しい `Func` の受け口が増えるため）。

`wc -l moorestech_client/Assets/Scripts/Client.Playtest/PlaytestDriver.cs` が 200 未満であること（目安 198）。200 以上なら、`SetupDebugEnvironment` の設定値の整形とログ出力だけを `PlaytestSetup` へ移し、`Note`・`Act` は Driver に残す（Operations 層へ `PlaytestReporter` を渡さない）。

- [ ] **Step 4: 呼び出し側を直す**

`PlaytestRunner.cs`:
- `using Client.Playtest.WebUi;` を `using Client.Playtest.UiElements;` に替える
- 46〜50 行を次に置き換える:

```csharp
                    // UI照会の受け口を開き、CEF利用時だけ入力転送を実行中に有効化する
                    // Open UI lookup receivers and enable input forwarding while CEF is in use
                    PlaytestUiLocatorSession.Start();
```

- 66〜71 行（`CefInputForwarder.StopForwarding(); PlaytestDomQuery.ResetPending();` とそのコメント）を次に置き換える:

```csharp
                // 成否にかかわらず転送を止め、次回実行へ照会応答を持ち越さない
                // Stop forwarding and discard pending lookups regardless of success or failure
                PlaytestUiLocatorSession.Stop();
```

`PlaytestBuildMenuOps.cs`:
- `using Client.Playtest.WebUi;` を消し `using Client.Playtest.UiElements;` を足す
- 19〜27 行の待ちを次へ:

```csharp
            // 画面UIの描画系が使えるようになるまで期限付きで待つ（CEFのブラウザ生成は非同期）
            // Wait with a deadline for the screen-UI renderer to become available (CEF browser creation is async)
            var rendererDeadline = Time.realtimeSinceStartup + 15f;
            while (!PlaytestUiLocatorSession.Locator.IsAvailable())
            {
                if (rendererDeadline <= Time.realtimeSinceStartup) throw new InvalidOperationException("Build menu operations require an available UI renderer");
                await UniTask.DelayFrame(5);
            }
```

- `webUiTestid` → `entryTestid`、`PlaytestWebUiOps.BuildMenuBlockTestId` → `PlaytestBuildMenuOps.BuildMenuBlockTestId`、`PlaytestWebUiOps.WaitWebUiElement` → `PlaytestUiElementOps.WaitUiElement`、`PlaytestWebUiOps.ClickWebUi` → `PlaytestUiElementOps.ClickUi`、`PlaytestWebUiOps.ScrollDownUntilWebUiElementIsClickable` → `PlaytestUiElementOps.ScrollDownUntilUiElementIsClickable`

`BlueprintCopyPasteScenarioChecks.cs:35-41`: `_playtest.UntilWebUiElement` → `_playtest.UntilUiElement`、`_playtest.ClickWebUi` → `_playtest.ClickUi`、`PlaytestWebUiOps.ScrollDownUntilWebUiElementIsClickable` → `PlaytestUiElementOps.ScrollDownUntilUiElementIsClickable`

確認:

```bash
grep -rnE 'ClickWebUi|HoverWebUi|UntilWebUiElement|CloseWebUiPanel|PlaytestWebUiOps|WebUiTextContains|WaitWebUiElement|PlaytestDomQuery\.(Query|RegisterHandler|ResetPending)' moorestech_client/Assets/Scripts
```

Expected: 0 件

- [ ] **Step 5: コンパイルとテスト**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value '^(Client\.Tests\.Playtest\.|Client\.Tests\.WebUi\.WireContractActionNamesTest(\.|$))'`
Expected: ErrorCount 0 / 全 PASS。`uloop get-logs --project-path ./moorestech_client --log-type Error` が 0 件

- [ ] **Step 6: 疎通を 1 本で確かめる**

改名だけの `misc/webui-buildmenu-click` を、Task 5 Step 1 の改名を手元の scratch コピーにだけ当てて流し（`sed` で改名表を当てる）、Success と `[PlaytestUi] … resolved by Web` のログを確かめる。

```bash
uloop get-logs --project-path ./moorestech_client --log-type Log | grep '\[PlaytestUi\]' | head
```

- [ ] **Step 7: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Playtest moorestech_client/Assets/Scripts/Client.Tests/Playtest/PlaytestWebUiLocatorContractTest.cs moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/action_names.json
git status --short   # 新規 .cs の .meta（Unity生成）も含める
git commit -m "feat(playtest): UI照会をIUiElementLocatorの背後へ移し、DSLのWebUi系APIをUi系へ改名する"
bd note moorestech-albof.6 "Task3-4 済み: IUiElementLocator・Web版ロケータ・PlaytestUiElementOps、Driver 200行未満"
```

---

### Task 5: シナリオの改名と照会 API への置換（合否の突き合わせ）

**Files:**
- Modify: 37 本（`.agents/skills/unity-playmode-recorded-playtest/scenarios/` 配下。Task 1 Step 2 の 30 本＋対象外 7 本）
- Modify: `docs/uitk-migration/stage0-dsl-parity.md`（「変更後」列と判定）

**Interfaces:**
- Consumes: Task 4 の Driver・`PlaytestUiElementOps`、Task 3 の `UiElementSnapshot`・`PlaytestUiLocatorSession`
- Produces: 旧名・内部型を参照しないシナリオ 37 本、合否突き合わせ表

- [ ] **Step 1: 37 本に改名表を機械的に当てる**

```bash
SC=.agents/skills/unity-playmode-recorded-playtest/scenarios
FILES=$(grep -rlE 'ClickWebUi|HoverWebUi|UntilWebUiElement|CloseWebUiPanel|PlaytestWebUiOps|PlaytestDomQuery|DomQueryResult|CefScreenMapper|CefInputForwarder|Client\.Playtest\.WebUi' $SC --include='*.cs')
echo "$FILES" | wc -l   # 37
for f in $FILES; do
  sed -i '' -E \
    -e 's/\.ClickWebUi\(/.ClickUi(/g' \
    -e 's/\.HoverWebUi\(/.HoverUi(/g' \
    -e 's/\.UntilWebUiElement\(/.UntilUiElement(/g' \
    -e 's/\.CloseWebUiPanel\(/.CloseBuildMenuPanel(/g' \
    -e 's/PlaytestWebUiOps\.ScrollDownUntilWebUiElementIsClickable\(/PlaytestUiElementOps.ScrollDownUntilUiElementIsClickable(/g' \
    -e 's/PlaytestWebUiOps\.WaitWebUiTextContains\(/PlaytestUiElementOps.WaitUiTextContains(/g' \
    -e 's/PlaytestWebUiOps\.WaitWebUiElement\(/PlaytestUiElementOps.WaitUiElement(/g' \
    -e 's/PlaytestWebUiOps\.ClickWebUi\(/PlaytestUiElementOps.ClickUi(/g' \
    -e 's/PlaytestWebUiOps\.HoverWebUi\(/PlaytestUiElementOps.HoverUi(/g' \
    -e 's/PlaytestWebUiOps\.BuildMenuBlockTestId\(/PlaytestBuildMenuOps.BuildMenuBlockTestId(/g' \
    "$f"
done
grep -rnE 'ClickWebUi|HoverWebUi|UntilWebUiElement|CloseWebUiPanel|PlaytestWebUiOps' $SC --include='*.cs'   # 0件
```

コメント・`p.Note` の文言にある「Web UI」は操作の説明なので残してよい（API 名ではない）。

- [ ] **Step 2: 改名だけの 12 本を流し、表を埋める**

Task 5 では 1 本流すごとに、重複 testid の警告も数えて表の備考に `重複警告 N 件（testid: …）` と書く（挙動は変えない。D3 の実測。段階1で Web 版の扱いを見直す材料）:

```bash
uloop get-logs --project-path ./moorestech_client --log-type Warning | grep -o "testid '[^']*' matched [0-9]* Web elements" | sort | uniq -c
```

Task 1 Step 2 と同じ手順・同じ条件で 12 本を流し、「変更後」列と判定（基準と `Success`・Assert 数・失敗 Assert ラベルが同じなら「一致」）を書く。一致しない行は、変更後をもう 1 回流す。それでも基準と違えば、作業をコミットしてから同じ worktree で `git switch --detach e855a58667` → `uloop compile` → 旧シナリオ（`$SKILL/scenarios/...` がその時点の旧版になる）を流し直し、`git switch feature/uitk-s0-dsl` → `uloop compile` で戻る（`git stash` は使わない）。基準の再計測でも違うなら原因を調べて直す（DSL 側の不具合なら Task 4 のコードを直してコンパイル・コミットし、表に再計測を書く）。基準の再計測で揺れと分かったら、両方の結果を表に書き備考に「揺れ」と記す。

- [ ] **Step 3: 照会 API へ置き換える 18 本を 1 本ずつ置換→実行→突き合わせる**

1 本ごとに: 置換表を当てる → scratch コピーで流す → 表の「変更後」と判定を書く → 一致したらそのファイルだけコミット（`git commit -m "test(playtest): <シナリオ名> の照会をロケータAPIへ置き換える"`）。一致しなければ Step 2 と同じ扱い。

個別の指示（置換表で決まらないもの）:
- `misc/sprint/sprint-toggle-mode`・`sprint-hold-restart`・`sprint-invalid-recovery`: Run 前のトップレベル probe を次の形へ（3 本とも同形）。`sprint-invalid-recovery` の `PlaytestDomQuery.Query("pause-menu", 3f)` 2 か所は `PlaytestUiElementOps.FindUi("pause-menu")`、`.Found` → `.IsFound`

```csharp
PlaytestUiLocatorSession.Start();
try { await PlaytestUiElementOps.WaitUiElement("inventory-sort", 15f); }
finally
{
    PlaytestUiLocatorSession.Stop();
}
```

  `using Client.Playtest.WebUi;` を `using Client.Playtest.UiElements;` に替える
- `building/terrain-sink-normal-and-belt-via-ui`: `Client.Playtest.WebUi.PlaytestDomQuery.RegisterHandler();` → `Client.Playtest.UiElements.PlaytestUiLocatorSession.Start();`（冪等。Runner が既に開いている）。`Query("cursor-tooltip", 3f)` → `PlaytestUiElementOps.FindUi("cursor-tooltip")`、`.Found` → `.IsFound`
- `connect/rotary-generator-ui`: `new Client.Playtest.WebUi.DomQueryResult[testIds.Length]` → `new Client.Playtest.UiElements.UiElementSnapshot[testIds.Length]`、`PlaytestDomQuery.Query(testIds[index], 1f)` → `PlaytestUiElementOps.FindUi(testIds[index])`、`row.Found` → `row.IsFound`。表示順の検査は上下が逆になるので次へ（意味は同じ「上から 状態→供給→歯車→電力網→歯車網」）:

```csharp
        p.Assert(rows[0].ScreenRect.yMax > rows[1].ScreenRect.yMax && rows[1].ScreenRect.yMax > rows[2].ScreenRect.yMax && rows[2].ScreenRect.yMax > rows[3].ScreenRect.yMax &&
                 rows[3].ScreenRect.yMax > rows[4].ScreenRect.yMax && rows[4].ScreenRect.yMax > rows[5].ScreenRect.yMax, "状態→供給→歯車→電力網→歯車網の表示順");
```

- `graphics-quality/quality-ui-switching`: `var rect = await PlaytestDomQuery.Query(id, 5f);` → `var rect = await PlaytestUiElementOps.FindUi(id);`。見切れ検査を次へ（CSS px と Screen px の混在だったものをスクリーン px にそろえる）:

```csharp
            var screenRect = rect.ScreenRect;
            p.Assert(rect.Visible && screenRect.xMin >= 0 && screenRect.yMin >= 0 && screenRect.xMax <= Screen.width && screenRect.yMax <= Screen.height, $"トップ見切れなし {id}: {screenRect}");
```

- `graphics-quality/quality-recovery-ui`: `Query("graphics-recovered", 5f)` 2 か所 → `FindUi("graphics-recovered")`、`!cleared.Found` → `!cleared.IsFound`
- `misc/cursor-tooltip-follows-crosshair`: `PollUntilQueryAsync` の戻り型を `UniTask<UiElementSnapshot>` に、`PlaytestDomQuery.Query(testid, 1f)` を `PlaytestUiElementOps.FindUi(testid)` に、`!result.Found` を `!result.IsFound` に。ツールチップ中心の計算（browser 座標の計算と `CefScreenMapper.TryBrowserToScreen` とその失敗分岐）を `var tooltipScreenPoint = tooltip.ScreenRect.center;` 1 行に置き換え、`Note` の `css=` 表記は `rect={tooltip.ScreenRect}` にする。`using Client.Playtest.UiElements;` を足す
- `misc/pause-menu-hierarchy`・`playtest-receiver/developer-report-delivery`: 説明文入力の `CefScreenMapper.TryGetBrowser` の Assert と `SendCharEvent` ループを `await p.SetText("bug-report-description", description);` 1 行に置き換える（直前の `p.ClickUi("bug-report-description")` は残す）。`pause-menu-hierarchy` の Assert 件数は 1 減る（`CEF browserを解決してtextareaへ入力できる`）。`developer-report-delivery` も 1 減る（`CEF入力先が存在する`）。表の備考に「Assert 1 件削除（CEF 直参照の前提確認）」と書き、それ以外の Assert で一致を判定する
- `connect/electric-wire-tool-extend-chain-via-ui`: `p.Assert(CefScreenMapper.IsWebUiAvailable(), "Web UI(CEF)が利用可能");` → `p.Assert(PlaytestUiLocatorSession.Locator.IsAvailable(), "UIの描画系が利用可能");`、`using Client.Playtest.UiElements;` を足す。Assert ラベルの変更を備考に書く
- 残り（`delete-hold-mode-reentry`・`delete-hold-text-focus`・`placement-reason-tooltip`・`ui-place-then-open-machine`・`feedback-report-labels`・`vein-hand-mining-smoke`・`fluid-inventory-via-interact`）: 置換表どおり（`Query(id, t)` → `FindUi(id)`、`.Found` → `.IsFound`、`.HitTestPassed` → `.Clickable`、`Client.Playtest.WebUi.` 修飾を `PlaytestUiElementOps.` に）

- [ ] **Step 4: 対象外 7 本も置換して残りを片付ける**

`research-node-state-label`・`server-notification-smoke`（`0 < result.Width` → `0 < result.ScreenRect.width`）・`tutorial-equip-challenge`・`tutorial-pebble-challenge` に置換表を当てる（実行はしない。壊れているため）。

```bash
grep -rnE 'PlaytestDomQuery|DomQueryResult|CefScreenMapper|CefInputForwarder|Client\.Playtest\.WebUi' .agents/skills/unity-playmode-recorded-playtest/scenarios --include='*.cs'
```

Expected: 0 件

- [ ] **Step 5: 表を締めてコミットする**

表の 30 行がすべて「一致」（揺れは備考付きで一致扱い可。揺れの判定は基準・変更後とも 2 回流した結果で書く）であることを確かめ、冒頭に集計 1 行（`一致 30 / 不一致 0`）を書く。

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios docs/uitk-migration/stage0-dsl-parity.md
git commit -m "test(playtest): 録画シナリオをUi系DSLへ改名し、改名前と合否が一致することを記録する"
bd note moorestech-albof.6 "Task5 済み: シナリオ37本を改名・照会置換、30本の合否一致（docs/uitk-migration/stage0-dsl-parity.md）"
```

---

### Task 6: スキル文書を新 API に直す

**Files:**
- Modify: `.agents/skills/unity-playmode-recorded-playtest/references/write-scenario.md:22-25,41,91-106`
- Modify: `.agents/skills/unity-playmode-recorded-playtest/references/input-injection.md:45`
- Modify: `.agents/skills/unity-playmode-recorded-playtest/references/place-blocks-via-ui.md:38-40`
- Modify: `.agents/skills/unity-playmode-recorded-playtest/references/run-scenario.md:55`

**Interfaces:**
- Consumes: Task 4 の API 名
- Produces: なし

- [ ] **Step 1: `write-scenario.md` を直す**

雛形コメント（22〜25 行）を次に:

```
// UI probeはready → Start() → 期限付きIsAvailable()待機 → probeの順で実行する
// For a UI probe, wait for ready, call Start(), wait for IsAvailable() with a deadline, then probe
// 成功・失敗を問わずfinallyでPlaytestUiLocatorSession.Stop()を呼び、失敗理由を記録する
// Always call PlaytestUiLocatorSession.Stop() in finally and record any failure reason
```

41 行の本文中の `PlaytestDomQuery.RegisterHandler()`と`CefInputForwarder.StartForwarding()` → `PlaytestUiLocatorSession.Start()`、`CefInputForwarder.StopForwarding()`と`PlaytestDomQuery.ResetPending()` → `PlaytestUiLocatorSession.Stop()`、「CEF利用可能状態」→「Start後の期限付きIsAvailable待機」。順序はready → Start → 期限付きIsAvailable待機 → probeとし、finallyのStopを成功・失敗の両方へ適用する。

91〜106 行の節を次に置き換える:

```markdown
### UI 操作（testid で要素を解決し通常マウス経路で操作。描画系に依存しない）
| API | 用途 |
|---|---|
| `ClickUi(testid)` | `testid` の要素中心へ注入マウスを滑走→クリック。2 回照会の矩形一致（レイアウト安定）＋被覆チェック込み。要素出現まで内部リトライ（15s） |
| `HoverUi(testid)` | 同解決でポインタ移動のみ（ツールチップ検証等） |
| `UntilUiElement(testid, timeout)` | 要素が可視かつクリック可能（遮蔽なし）になるまで待つ |
| `SetText(testid, text)` | テキスト欄の値を置き換えて変更イベントを流す（IME は通さない。Web は input/textarea、段階1以降の UITK は TextField）。欄の出現・有効化まで内部リトライ（15s） |
| `ClickBuildMenuBlock(blockName)` | ビルドメニューのブロックスロットを1行でクリック（testid `build-menu-entry-block-{BlockGuid}` を自動解決） |
| `CloseBuildMenuPanel()` | ビルドメニューの閉じるボタン（`build-menu-close`）をクリック |
| `PlaytestUiElementOps.FindUi(testid)` | 1 回照会して `UiElementSnapshot` を返す（`IsFound`・`Visible`・`Clickable`・`ScreenRect`〈Unity スクリーン座標・左下原点〉・`Text`・`HasStateFlag(name)`）。待たない |
| `PlaytestUiElementOps.WaitUiTextContains(testid, text, timeout)` | 要素の表示文字列が `text` を含むまで待つ |
| `PlaytestUiElementOps.ScrollDownUntilUiElementIsClickable(testid, scrollAreaTestid, timeout)` | スクロール領域上でホイールを回して要素を表示領域へ送る |

状態フラグ: Web は `data-testid` 以外の `data-*` 属性のうち値が空か `true` のもの（`data-selected="true"` → `HasStateFlag("selected")`）。
シナリオから `PlaytestDomQuery`・`CefScreenMapper`・`CefInputForwarder` は見えない（`internal`）。照会は必ず上の API を通す。
同じ testid が複数あると Web 版は先頭を使い `[PlaytestUi] … matched N Web elements` を警告する（UITK 版は失敗させる予定）。

testidは `moorestech_web/webui/src/features/**` をgrepして実在確認する。ビルドメニューのエントリは
`build-menu-entry-{kind}-{id}`（kind: block/trainCar/connectTool/blueprintCopy/blueprint、
idは種別を問わず設置対象のGuid文字列。blockのtestidは`PlaytestBuildMenuOps.BuildMenuBlockTestId(名前)`で組み立てる）。
表示名はスロットではなく詳細サイドバー(`build-menu-detail`)に出るため、名前を絵に残すには`HoverUi`してからScreenshotする。
カテゴリ切替は`build-menu-category-{categoryGuid}`（カテゴリ名ではなくGuid）。PlaceBlock遷移で視点は変わらないため、
シナリオ側での追加のカメラ待ちは不要。
仕組み: DSL は `IUiElementLocator`（`PlaytestUiLocatorSession.Locator`）だけを見る。Web 版は DOM 矩形を WS 往復（`playtest.dom_query`）で取得→CEF ブラウザ座標→Unity スクリーン座標へ逆変換→`SemanticInput` で注入→プレイテスト専用 `CefInputForwarder` が InputSystem マウスを CEF へ転送（パッケージの legacy Input 転送は注入不感のため）。文字入力は `playtest.dom_set_text` の往復でネイティブ setter＋`input` イベント。どの描画系で解決したかは `[PlaytestUi] <testid> resolved by Web` のログに出る。
```

- [ ] **Step 2: 残り 3 本を直す**

`input-injection.md:45`:

```markdown
- 画面UIの要素は `data-testid` で指定し、DSLの `ClickUi(testid)` / `HoverUi(testid)` / `UntilUiElement(testid, timeout)` / `SetText(testid, text)` を使う（ロケータが要素を解決→注入マウス。Web 版は DOM 矩形→座標逆変換→`CefInputForwarder`。write-scenario.mdのUI操作参照）
```

同ファイルの次行「ビルドメニューは … CEFが使えなければ例外」→「ビルドメニューは `OpenBuildMenuAndSelectBlock(name)`（`PlaytestBuildMenuOps`。UIの描画系が15秒以内に使えなければ例外）」。

`place-blocks-via-ui.md:38-40`:

```markdown
- ビルドメニューのスロット選択は**UIロケータ経由のクリック経路のみ**
  （ロケータが要素を解決→注入マウス。Web 版は DOM 矩形→座標逆変換→`CefInputForwarder` が CEF へ転送。write-scenario.mdのUI操作参照）。
  画面UIはWeb UI一本（ADR 0052）でuGUI経路は撤去済み。UIの描画系が15秒以内に使えなければ`PlaytestBuildMenuOps`が例外を投げる
```

`run-scenario.md:55` の該当文: 「Web UIをprobeする場合はreadyとCEF利用可能状態を確認し、`PlaytestDomQuery.RegisterHandler()` → `CefInputForwarder.StartForwarding()` → probeの順に実行する。probe失敗でRunへ進まない場合は`CefInputForwarder.StopForwarding()`と`PlaytestDomQuery.ResetPending()`で片付け」→「UIをprobeする場合はreadyを確認して`PlaytestUiLocatorSession.Start()`を呼び、期限付きで`PlaytestUiLocatorSession.Locator.IsAvailable()`を待ってからprobeする。成功・失敗を問わずfinallyで`PlaytestUiLocatorSession.Stop()`を呼び、失敗理由を記録する」。

- [ ] **Step 3: 旧名が残っていないことを確かめてコミットする**

```bash
grep -rnE 'ClickWebUi|HoverWebUi|UntilWebUiElement|CloseWebUiPanel|PlaytestWebUiOps|PlaytestDomQuery|CefInputForwarder\.(Start|Stop)' .agents/skills/unity-playmode-recorded-playtest
```

Expected: 0 件（`write-scenario.md` の「仕組み」節にある `CefInputForwarder` という名前の説明は `\.(Start|Stop)` を含まないので許容）

```bash
git add .agents/skills/unity-playmode-recorded-playtest/references
git commit -m "docs(playtest-skill): DSLのUi系APIとロケータ・SetText・状態フラグを文書化する"
```

---

### Task 7: unityプレイ録画テスト（提出用の録画）

**Files:**
- なし（`moorestech_client/PlaytestResults/` は git 管理外）

**Interfaces:**
- Consumes: Task 4〜6 の成果
- Produces: 録画 2 本の目視結果（PR 本文に書く）

- [ ] **Step 1: Record=true で 2 本流す**

`misc/webui-buildmenu-click`（改名した `ClickUi`・`ClickBuildMenuBlock` の通し）と `misc/pause-menu-hierarchy`（`SetText` で説明文を入れてバグ報告を送る）を、`Record = true` のまま run-scenario.sh で流す。

- [ ] **Step 2: 目視する**

`recording.mp4` が 0 byte でないこと、ビルドメニューのスロットがクリックされて PlaceBlock へ移ること、バグ報告の説明欄に文字列が入って送信後にトップへ戻ることを目視し、スクショのパスと所見を `bd note moorestech-albof.6` に書く。`uloop get-logs --project-path ./moorestech_client --log-type Error` が 0 件であること。

---

### Task 8: moores-code-review による全ブランチレビュー

**必ずmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）**

- [ ] **Step 1:** moores-code-review を全ブランチ（`origin/master...HEAD`）に対して実行する（Codex ホストでは `references/codex-host.md` の runner）。シナリオ `.cs` はレビュー対象 diff から除外される（`.decisions/2026-08-16-プレイテストシナリオはレビューのdiffから除外する.md`）
- [ ] **Step 2:** 機械的修正を当て、設計判断は plan の `## 判断記録（ADR）` の既定で決めて PR 本文「ユーザーに見てほしい判断」へ列挙する（このレーンではユーザーに質問しない）
- [ ] **Step 3:** 反映がソースの判定経路・条件式（`UiElementScreenPointResolver`・`PlaytestUiElementOps`・`WebUiElementLocator`・Web 応答器）に触れたら、Task 7 の録画 2 本と、触れた API を使う照会置換シナリオを反映後のバイナリで再実施してから完了とする
- [ ] **Step 4:** コンパイル・テストを再実行し、コミットする

---

### Task 9: PR を作り、セッションを閉じられる状態にする

**セッション終了可能状態にすること: pr-createスキルでPRを作成し、masterとのコンフリクトがあればmasterをマージして解消・コンパイル確認のうえpushする（解消の実作業はpr-create経由でopus subagentに委譲される）。全作業がコミット・push済みで、このセッションをそのまま閉じてもPRがマージ可能な状態になっていることを確認して終える。** 全実装タスク完了＝完了ではない。PR未作成のまま終わるのはplan未完了である。

- [ ] **Step 1:** pr-create スキルで PR を作る。本文に必ず入れるもの:
  - 合否突き合わせの集計と `docs/uitk-migration/stage0-dsl-parity.md` への参照
  - 「#1348（`feature/belt-conveyor-replace-impl`）がマージされる前後どちらでも、`belt-replace-tier-via-ui.cs`・`input-injection.md`・`PlaytestDriver.cs`・`PlaytestRunner.cs` の旧名（`ClickWebUi` 系・`PlaytestDomQuery`・`CefInputForwarder`）を改名表に沿って追従させる必要がある」
  - 「ユーザーに見てほしい判断」節: 判断記録 D1〜D14 のうち「agent 既定（ユーザー未裁定）」の行
  - 「本PR外のリファクタ提案」節: 既存 `moorestech_web/webui/src/shared/tutorialAnchor/resolveAnchor.ts` の `resolveTutorialAnchor` と新設 `bridge/playtest/testIdElement.ts` の `findByTestId`＋`isElementVisible` は、data 属性で要素を引き、一致件数で重複を見て、hidden・aria-hidden・display・visibility・面積で可視を判定する同じ役割（設計検査 Phase 2.6 の第3バケツ。このPRでは統合しない）
  - 録画 2 本の目視所見
- [ ] **Step 2:** master とのコンフリクトがあれば master をマージして解消し、コンパイル確認のうえ push する
- [ ] **Step 3:** `bd note moorestech-albof.6 "PR #<番号> 作成"`、`moores-wt rm` は指揮セッションの指示に従う（このレーンでは worktree を畳まない）

---

## 判断記録（ADR）

設計 ADR: なし（計画 `docs/uitk-migration/plan.md` §2.1 0-7・§4.4 と、裁定 `.decisions/2026-10-18-CEFを撤去しUI-Toolkitへ全面移行する.md`・PR #1555 の `.decisions/2026-10-26-UITKが安定するまでWeb-UIの変更を続け現行機能をすべてUITKへ移す.md` が入力）。段階0の他レーンの前提（裁定 1=A・裁定 2=A・裁定 3=(a)・UI 演出は実時間）は agent 既定（ユーザー未裁定）で、このレーンの中身には影響しない。

planning 中の判断:

| # | 判断 | 採用 | 棄却 | 理由 | 出所 |
|---|---|---|---|---|---|
| D1 | Web 版 `SetText` の方式 | WS 往復（`playtest.dom_set_text`）で Web 側がネイティブ setter＋`input` イベント | (b) クリック後に CEF の `SendCharEvent` を 1 文字ずつ送る（既存 2 シナリオの方式） | (b) は追記にしかならず「値を置き換える」契約を満たせない（全選択＋削除のキー注入が要る）。結果の値を確かめる手段も無い。(a) は UITK 版（`TextField.value`＋ChangeEvent）と同じ意味になり、適用後の値を返せる | agent 既定（ユーザー未裁定） |
| D2 | 状態フラグの Web 表現 | `data-testid` 以外の `data-*` で値が空か `"true"` のものの名前 | 属性名を個別に列挙する許可リスト | 計画「Web は属性、UITK はクラス」に沿い、`data-selected={x \|\| undefined}` 等の既存の書き方をそのまま拾える。値付き属性（`data-kind="bug"`）は状態ではないので除く | agent 既定（ユーザー未裁定） |
| D3 | 同じ testid が複数ある場合の Web 版（Task 5 で警告件数を実測して表に残す） | 先頭一致を保ち警告ログ（`Ambiguous` は返さない） | Web 版も `Ambiguous` で失敗させる | 完了条件が「改名前と合否が同じ」で、既存シナリオが重複 testid に依存していないかは実測していない。UITK 版・両系照合は `Ambiguous` で失敗させる（§4.4）。段階1で Web 版の扱いを見直す | agent 既定（ユーザー未裁定）。user-simulator 予測も (a) 維持・確信中（シミュレーター予測、ユーザー未承認） |
| D4 | `Find` に応答待ちの上限を引数で渡すか | 渡さない。Web 版内部で 3 秒。応答が無いときは警告ログを出す | `Find(testid, timeout)` | 応答待ちは WS 往復を持つ Web 版だけの事情で、UITK 版は同期で返る。interface に描画系の事情を入れない。旧シナリオの 1〜10 秒は応答待ちの上限で、通常は数フレームで返る。代償: Web が応答しないとき、`WaitUiElement(id, 1f)` 等の待ちが指定秒を最大 3 秒超えて終わる | agent 既定（ユーザー未裁定） |
| D5 | 説明文入力の `SendCharEvent` ループを `p.SetText` へ替えるのは「操作手順の変更」か | 付け替えとして行う（2 本。pauseは同じCEF前提Assertが実行2回分減る〈41→39〉、developerは1件減る〈8→7〉） | CEF 直参照を残す | シナリオから CEF への直参照を無くすのが 0-7 の目的で、手順（クリック→入力→送信）は変えない。打鍵経路の検証は IME と同じく §4.9 の人手の範囲 | agent 既定（ユーザー未裁定）。user-simulator 予測も (a) 付け替え・確信高（シミュレーター予測、ユーザー未承認） |
| D6 | `ScreenRect` の座標系 | Unity スクリーン座標（左下原点） | CSS 互換の左上原点 | `SemanticInput.MouseGlideTo`・`Screen.width/height` と同じ座標系で、UITK 版もパネル座標→画面座標へ写すため。上下比較の向きが逆になるシナリオ 1 本（`rotary-generator-ui`）は検査式を書き換える | agent 既定（ユーザー未裁定） |
| D7 | interface の実装が 1 つ（抽象の受益者） | 段階0で interface を置く | 段階1まで具象クラスのまま | 計画 §4.4 が Web 版と UITK 版の 2 本と両系照合を定め、段階1の UITK 版が受益者として名指しできる。0-7 の要件が interface の導入そのもの | 計画 §2.1 0-7 |
| D8 | `CloseWebUiPanel` の新名 | `CloseBuildMenuPanel` | `CloseUiPanel` | 実処理は `build-menu-close` のクリックで、汎用の「パネルを閉じる」ではない（AGENTS.md「名前は実処理と一致させる」） | agent 既定（ユーザー未裁定） |
| D9 | `PlaytestDriver.cs`（215 行）の分割 | 待機表示つき 4 メソッド（`UntilUiElement`・`WaitUiState`・`WaitBlockGameObject`・`WaitSeconds`）の本体を root の `PlaytestWaitActions`（reporter だけを受け取る）へ移し、Driver は 1 行委譲 | (1) `p.Ui.ClickUi` のようなサブファサード化 (2) `PlaceBlockViaUi` 等を Driver を逆参照する下請けへ移す (3) `this PlaytestDriver` 拡張メソッド（`PlaytestInteractOps` の前例） | (1) はシナリオの呼び方が変わり機械置換で済まない。(2) は前例 `PlaytestHotbarDriver` と依存の向きが逆（配置検査 F1）。(3) は Driver の public API しか使えず `BeginWait`・`Step` を呼べない。`Until` は `Func<bool>` の受け口を増やさないため Driver に残す | agent 既定（ユーザー未裁定）。配置検査 F1 で修正 |
| D11 | `UiElementSnapshot` を判別子＋ペイロード型に分けるか（Phase 2.6 5-B 強。`UiSetTextResult` は下の別扱い） | 分けない。`Status` と、Found 以外は空値（`Visible=false`・`Clickable=false`・`Rect.zero`・空文字・空集合）のフィールドを 1 つの型に置き、ctor を private にして factory 4 つ（`Found`・`NotFound`・`Ambiguous`・`Unavailable`）だけで作らせる | `Status` ＋ Found 時だけの nullable ペイロード | 空値はどれも「見えない・押せない・文字なし」で安全側に倒れ、誤って読んでも合格側へ転ばない。ペイロードを分けるとシナリオの `s.IsFound && s.Text.Contains(…)` が null 判定の連鎖になる。シナリオはレビュー対象外の使い捨て台本（`.decisions/2026-08-16-…`）。`UiSetTextResult` は public ctor の struct のまま: `ActualValue` は `Applied` 以外（`ValueMismatch`・`NotInteractable`）でも診断に意味を持つ値で、Found 時だけのペイロードではないため | agent 既定（ユーザー未裁定） |
| D12 | Phase 2.6 の弱 6 件 | 2 件を直す（`PlaytestWebUiRequestChannel.Await` が Reset 後に期限まで待ち続ける → 即時に応答なし／`IsValidResult` の有限値判定が `TryReadFloat` と二重 → 削除）。残りはこのまま: 5-C `Try*` の失敗理由（呼び出し側でログ済み）、5-F `Start/Stop` の 2 か所呼び出し（冪等にして Runner と probe の両方が使う前提）、5-F `CancellationToken` 無し（既存 DSL に前例が無く、シナリオ全体の打ち切りは `PlaytestRunner` の `.Timeout` が持つ）、5-G 契約型の置き場（同じ Editor 限定 asmdef の中）、検査6 の期限ループ・判定の繰り返し（既存 `PlaytestWebUiOps` の形を保つ） | — | 直すのは誤動作に至る 1 件と純粋な重複 1 件だけ | agent 既定（ユーザー未裁定） |
| D13 | `PlaytestUiLocatorSession`（`UiElements/`）が `WebUiElementLocator`（`WebUi/`）を直接 new する（配置検査の注目点） | このまま。Session はロケータの組み立て役で、描画系非依存の型と同じ場所に置く。開閉（`StartSession`/`StopSession`）は interface に入れない | Session を root へ置く／開閉を interface に入れる | 開閉は WS 受け口と CEF 入力転送という Web 版だけの事情で、UITK 版は開閉が要らない。段階1の差し替えは Session の 1 行（と開閉の委譲）で済み、interface は変わらない | agent 既定（ユーザー未裁定） |
| D14 | プレイテスト橋渡しが DOM へ書く（`domQueryResponder.ts` の「読み取るだけ」の線を `domSetTextResponder.ts` で越える。配置検査の注目点） | 書く応答器を別ファイルに分け、照会の応答器は読み取り専用のまま保つ | `CefInputForwarder` に文字転送を足し `SemanticInput` から打鍵する | §4.4 で UITK 版 `SetText` は値の設定と決まっており、Web 版も同じ意味にする（D1）。打鍵案は追記にしかならない | agent 既定（ユーザー未裁定） |
| D10 | unityプレイ録画テスト | 含める（Task 5 の 30 本の合否突き合わせ＋Task 7 の録画 2 本） | — | DSL はプレイテストの入力経路そのもの | — |

| D15 | NUnit検証の対象 | `Client.Tests.Playtest.` とWireContractActionNamesTestをアンカー/名前空間境界付きで選択 | `Client.Tests.Playtest` の前方一致 | 前方一致はPlaytestReceiverの明示本番送信テストにも一致する。ローカル検証を本番送信へ接続しない | agent既定（ユーザー未裁定） |
