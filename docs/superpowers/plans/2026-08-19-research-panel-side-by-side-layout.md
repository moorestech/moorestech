# 研究パネル棲み分けレイアウト Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** 研究画面の研究パネルを「ステージ全域を占有し持ち物を上へ重畳する」形から「持ち物パネルの右隣から画面端までを占有し、何とも重ならない」形へ差し替え、研究画面ではホットバーと装備HUDを非表示にする。

**Architecture:** レイアウトの正はCSS。`.stage` に研究画面用の修飾クラスを足して左paddingだけを0にし持ち物をx=0へ寄せる。`.researchArea` は絶対配置のまま、左端だけを `calc(持ち物幅 + 列gap)` の共有トークンで決め、上右下は0のまま画面端へ密着させる。絶対配置の基準はstageのpadding boxなので、stageのpadding変更は `.researchArea` と `.viewportOverlay` に影響しない（持ち物だけがフロー内grid itemとして動く）。常時表示HUDの出し分けは、`shared/uiState` の純粋述語＋hookをHotbarPanel/EquipmentPanel自身が読む形にする（HotbarPanelが既に `useBlockingSkitActive()` で自己ゲートしている前例と同型で、コンポーネントに画面名を持ち込まない）。

**Tech Stack:** React + TypeScript + CSS Modules（`moorestech_web/webui`）、vitest（ユニット）、Playwright（e2e・mock host）、Unity C#（コメント修正のみ）

## Requirements

設計対話（2026-08-19 grill）で確定した要件。受け入れ基準を各行に含む。

- R1. 研究パネルの左端は持ち物パネルの右端＋stageグリッド既存の列gap（2.1875rem＝35px）。受け入れ: 研究画面で研究パネルのboundingBoxが持ち物パネルのboundingBoxと横方向に一切重ならない（`expectSeparatedHorizontally`）。
- R2. 研究パネルの上・右・下はstageのpaddingを無視して実画面端に密着する（y=0 / x=stage右端 / y=stage下端）。受け入れ: 研究パネルのtop/right/bottomがstageのそれと誤差1.5px以内で一致する。
- R3. 持ち物パネルは研究画面のときだけ左padding 59.7pxを詰めて x=0 へ寄せる。縦位置・幅378px・見た目・grab操作は変えない。受け入れ: 同一ビューポートで PlayerInventory画面→ResearchTree画面 と切り替えたとき、持ち物グリッドのxが約59.7px（誤差1.5px）左へ動き、yは動かない。
- R4. 持ち物画面・サブインベントリ画面のレイアウトは一切変えない。受け入れ: 既存の inventory 系 e2e・vitest がすべて無改変で通る。
- R5. 研究パネルは持ち物の背後へ回り込まない。持ち物へ入れた `z-index: var(--z-stage-overlay-panel-chrome)` のパンチスルーと、そのためだけに `InventoryPanel` へ通していた `screen` prop を撤去する。受け入れ: `InventoryPanel` の props が空に戻り、`chromeZ` の記述がリポジトリから消える。
- R6. 研究画面ではホットバー（画面下中央）と装備HUD（画面右端）を描画しない。受け入れ: 研究画面で `hotbar-grid` と `equipment-slots` がDOMに存在しない（count 0）。
- R7. R6の判定は `shared/uiState/uiScreenRouting.ts` の純粋述語に置き、HotbarPanel・EquipmentPanel自身がhook経由で読む。両コンポーネントに画面名リテラルを持ち込まない。受け入れ: 両ファイルに `"researchTree"` の文字列が現れない。
- R8. R6は研究画面限定。持ち物・ビルドメニュー・チャレンジ一覧・ポーズ・trainHud・GameScreen では従来どおり両HUDを表示する。受け入れ: 述語のユニットテストが全 `UiScreen` 値を網羅し、`researchTree` のみ false になる。
- R9. 左上のチャレンジHUD・研究キー操作ヒント・採掘進捗バーは従来どおり研究パネルの上に見える（遮蔽されない）。受け入れ: 既存の遮蔽ガードe2e 2本が無改変で通る。
- R10. ホットバーの役割（保持するのは配置対象＝block/trainCar/connectTool/blueprint/blueprintCopyのみで持ち物アイテムは入らない・割当の唯一の発生源はビルドメニューのエントリのドラッグ）をコード側コメントへ明文化する（bd moorestech-4ed）。受け入れ: `hotbarDnd.ts`・`HotbarPanel/index.tsx`・`HotbarDtos.cs` の3箇所に規約準拠（日本語1行→英語1行）のコメントが入っている。
- R11. 様式ドキュメント（webui-design SKILL）を新レイアウトへ更新する。様式が先・実装が後。受け入れ: §1の例外記述と§8.14が「ステージ全域＋重畳」ではなく「持ち物の右を占有＋常時表示HUD非表示」を述べている。
- R12. PR #1176 の添付スクリーンショットを新レイアウトで撮り直す。受け入れ: `docs/pr-assets/research-ui-refresh/` の4枚が新レイアウトで更新され、overview-wide.png で持ち物とツリーが重なっていない。

**やらないこと（スコープ境界）:**
- 持ち物パネルを上端y=0まで詰めること（チャレンジHUDと衝突するため裁定で棄却）。
- 全画面共通でstage左paddingを廃止すること（3カラム画面のuGUI実測ピン留めが崩れるため棄却）。
- モーダル系画面全般で常時表示HUDを引っ込めること（ビルドメニューのホットバー割当が死ぬため棄却）。
- サーバー側（Game.Research）・プロトコル・DTO契約の変更。今回の差分はレイアウトと表示ゲートのみ。
- 研究ノードカードの4状態・種類別解放セクション（ADR 0014 決定1・3）への変更。既に実装済みで今回触らない。

## 機能パリティ死活表（研究画面でHUDを消すことで巻き添えになる操作）

ホットバー・装備HUDにぶら下がる全操作を、触る/触らないに関わらず列挙した。**研究画面でのみ**の死活であり、他画面は全て現状維持。

| 操作 | 研究画面で生きるか | 根拠 |
|---|---|---|
| ホットバー枠のクリック選択 | 死ぬ（元から無効） | `uiStateAcceptsHotbarSelect` は GameScreen と PlaceBlock のみ true。研究画面では元々タップが無効化されている |
| 数字キーでのホットバー選択 | 影響なし | Unity側 `HotbarKeyInput` が正で、Webパネルはキーを一切listenしない |
| ビルドメニューのエントリ→枠へのドラッグ割当 | 影響なし | 割当元はビルドメニュー画面にしか存在せず、研究画面とは同時に開かない |
| ホットバー枠同士のswap | 死ぬ | HUDが無いのでドラッグ元・先が存在しない。**ユーザー裁定 2026-08-19 で許容**（ビルドメニュー側で行う操作） |
| ホットバー枠→枠外dropでのclear | 死ぬ | 同上。**ユーザー裁定 2026-08-19 で許容** |
| ホイールでの装備切替 | 死ぬ | `useGameLayerWheel` の登録者がEquipmentPanelのため、unmountで降りる。**ユーザー裁定 2026-08-19 で許容**（研究ツリー側がホイールズームを使うので競合も消える） |
| 装備スロットのクリック/アイテム移動 | 死ぬ | HUDが無い。**ユーザー裁定 2026-08-19 で許容**（装備の付け替えは持ち物画面で行う） |
| 持ち物パネル内のgrab・アイテム移動 | 生きる | `screenAllowsGrab` は変更せず、持ち物パネルも従来どおり描画する |
| 常駐チャレンジHUD・研究キーヒント・採掘進捗バー | 生きる | 常時表示族に含めない（R9・既存の遮蔽ガードe2e 2本で担保） |

死ぬ4件はいずれも 2026-08-19 のユーザー裁定で明示的に許容されたもので、planが独断で確定した「既知の制限」ではない。

## Global Constraints

- 作業場所: worktree `/Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/research-ui-refresh`、ブランチ `feature/research-ui-refresh`（PR #1176）。**メインワークツリーでは作業しない。**
- 設計正本: `docs/adr/0014-research-ui-four-states-fullstage-unlock-sections.md` の決定2（2026-08-19差し替え版）。裁定の詳細は `.decisions/2026-08-19-研究パネルは持ち物と棲み分け重畳をやめる.md` / `.decisions/2026-08-19-研究画面のときだけ持ち物を画面左端へ寄せる.md` / `.decisions/2026-08-19-研究画面ではホットバーと装備HUDを非表示にする.md`。
- コメント規約: 主要処理に「日本語1行 → 英語1行」の2行セット。各言語1行に必ず収める（折り返し禁止）。自明なコメントは書かない。日本語本文の目安は処理・変数20字、メソッド30字。
- 色・寸法のハードコード禁止。重なり順は `--z-*` トークンのみ。数値z-indexの直書き禁止（webui-design §1）。
- 単純なgetter/setterプロパティ禁止。`Func<>` 禁止。`partial` 禁止。try-catch は外部境界のみ。デフォルト引数禁止。
- 1ファイル200行以下。1ディレクトリ10ファイルまで。
- `.cs` を変更したら必ず `uloop compile --project-path ./moorestech_client` を実行する（Task 4）。
- `.meta` ファイルを手で作らない。Unity固有のYAMLファイルを直接編集しない。
- テストコマンドは worktree の `moorestech_web/webui` で実行する。e2eはポート衝突で偽の失敗を出すため、他セッションと同時に走らせない。

---

### Task 1: webui-design SKILL を新様式へ更新する（様式が先）

ADR 0014 決定2の差し替えに合わせ、様式ドキュメントを先に直す。ここが実装の参照元になる。

**Files:**
- Modify: `.agents/skills/webui-design/SKILL.md:28-33`（§1の全画面UI例外）
- Modify: `.agents/skills/webui-design/SKILL.md:341`付近（§8.14 チャレンジHUDの安全帯記述）

**Interfaces:**
- Consumes: なし（ドキュメントのみ）
- Produces: Task 3のCSS実装が参照する様式記述。後続タスクはこの記述と矛盾してはならない。

- [x] **Step 1: §1の例外記述を差し替える**

`.agents/skills/webui-design/SKILL.md` の以下3行（`- **全画面UIは作らない。**` ブロック内）を探す:

```markdown
  - 例外（ADR 0014・ユーザー裁定 2026-08-18）: 研究ツリー画面のみ、半透明GamePanelがステージ全域
    （安全帯含む上下左右端まで）を占有してよい。面は従来どおり半透明で世界は透ける。
    チャレンジHUD・キー操作ヒント・持ち物パネルはこのパネルより上の層（`--z-stage-overlay-panel-chrome`）に重畳する。
```

次の内容へ置き換える:

```markdown
  - 例外（ADR 0014・ユーザー裁定 2026-08-19）: 研究ツリー画面のみ、半透明GamePanelが
    「持ち物パネルの右隣から画面右端まで・画面上端から下端まで」を占有してよい。面は従来どおり半透明で世界は透ける。
    持ち物パネルとは重ならない（重畳ではなく棲み分け）。研究画面では持ち物をstage左paddingごと画面左端へ寄せ、
    常時表示族のホットバー・装備HUDは描画しない。チャレンジHUD・キー操作ヒント・採掘進捗バーは
    このパネルより上の層（`.viewportOverlay` の `--z-stage-overlay-panel-chrome`）に残す。
```

- [x] **Step 2: §8.14 の安全帯記述を差し替える**

同ファイルの以下1行を探す:

```markdown
  研究画面はADR 0014の例外として安全帯を覆う全域パネルを敷き、HUDはその上に重畳される。
```

次の内容へ置き換える:

```markdown
  研究画面はADR 0014の例外として安全帯を覆うパネルを持ち物の右側だけに敷き、チャレンジHUDはその上に残る。
  同画面では常時表示族（ホットバー・装備HUD）を描画しないため、パネル下端は下安全帯を超えて画面下端まで伸びる。
```

- [x] **Step 3: 記述の矛盾が残っていないか検索して確認する**

Run: `cd /Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/research-ui-refresh && grep -n "ステージ全域\|全域パネル\|重畳" .agents/skills/webui-design/SKILL.md`

Expected: 研究画面に関する「ステージ全域」「全域パネル」「重畳」の記述がヒットしない（他機能の重畳記述はヒットしてよい。研究文脈の行が残っていたらStep 1/2の置換漏れなので直す）。

- [x] **Step 4: コミットする**

```bash
cd /Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/research-ui-refresh
git add .agents/skills/webui-design/SKILL.md
git commit -m "docs: 研究画面の様式を持ち物との棲み分けへ差し替える"
```

---

### Task 2: 研究画面で常時表示HUD（ホットバー・装備）を出さない

画面述語を `uiScreenRouting.ts` に足し、hook経由でHotbarPanel・EquipmentPanelが自分で引っ込む。

**Files:**
- Modify: `moorestech_web/webui/src/shared/uiState/uiScreenRouting.ts`（末尾へ述語追加）
- Modify: `moorestech_web/webui/src/shared/uiState/uiScreenRouting.test.ts`（述語のテスト追加）
- Create: `moorestech_web/webui/src/shared/uiState/useAlwaysOnHudVisible.ts`
- Modify: `moorestech_web/webui/src/shared/uiState/index.ts`（re-export追加）
- Modify: `moorestech_web/webui/src/features/hotbar/HotbarPanel/index.tsx:15-25`
- Modify: `moorestech_web/webui/src/features/inventory/EquipmentPanel/index.tsx:83-85`
- Test: `moorestech_web/webui/src/shared/uiState/uiScreenRouting.test.ts`、`moorestech_web/webui/e2e/tests/research/research.spec.ts`

**Interfaces:**
- Consumes: `UiScreen`（`uiScreenRouting.ts` の既存型）、`screenForUiState`、`Topics.uiState`、`useTopicSelector`
- Produces:
  - `screenShowsAlwaysOnHud(screen: UiScreen): boolean` — `uiScreenRouting.ts` からexport
  - `useAlwaysOnHudVisible(): boolean` — `shared/uiState` からexport。Task 3以降は使わない

- [x] **Step 1: 述語の失敗するテストを書く**

`moorestech_web/webui/src/shared/uiState/uiScreenRouting.test.ts` の末尾へ追記する:

```ts
describe("screenShowsAlwaysOnHud", () => {
  // 常時表示族(ホットバー・装備HUD)を引っ込めるのは研究画面だけ
  // Only the research screen withdraws the always-on family (hotbar + equipment HUD)
  it("研究画面だけ false を返す", () => {
    expect(screenShowsAlwaysOnHud("researchTree")).toBe(false);
  });

  it("研究画面以外の全画面で true を返す", () => {
    const others: UiScreen[] = ["none", "playerInventory", "subInventory", "buildMenu", "challengeList", "pauseMenu", "trainHud", "trainPause"];
    for (const screen of others) expect(screenShowsAlwaysOnHud(screen)).toBe(true);
  });
});
```

同ファイル先頭のimport行へ `screenShowsAlwaysOnHud` と型 `UiScreen` を追加する（既存のimport文の形に合わせる。`import { screenAllowsGrab, screenForUiState, screenShowsAlwaysOnHud, uiStateAcceptsHotbarSelect, type UiScreen } from "./uiScreenRouting";` の形になる。既存import内容は変えず追加のみ）。

- [x] **Step 2: テストを実行して失敗を確認する**

Run: `cd /Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/research-ui-refresh/moorestech_web/webui && npx vitest run src/shared/uiState/uiScreenRouting.test.ts`

Expected: FAIL。`screenShowsAlwaysOnHud is not a function` もしくはTSの型解決エラー。

- [x] **Step 3: 述語を実装する**

`moorestech_web/webui/src/shared/uiState/uiScreenRouting.ts` の末尾（`screenAllowsGrab` の下）へ追記する:

```ts
// 常時表示族(ホットバー・装備HUD)は、画面全体を使う研究画面でだけ引っ込む
// The always-on family (hotbar + equipment HUD) withdraws only on the research screen, which uses the full width
// 常駐チャレンジHUDと採掘進捗バーはこの族に含まず、研究画面でも出したままにする
// The resident challenge HUD and the mining progress bar are not in this family and stay visible there
export function screenShowsAlwaysOnHud(screen: UiScreen): boolean {
  return screen !== "researchTree";
}
```

- [x] **Step 4: テストを実行して通ることを確認する**

Run: `cd /Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/research-ui-refresh/moorestech_web/webui && npx vitest run src/shared/uiState/uiScreenRouting.test.ts`

Expected: PASS（追加2件を含む全件）。

- [x] **Step 5: hookを実装する**

`moorestech_web/webui/src/shared/uiState/useAlwaysOnHudVisible.ts` を新規作成する。既存の `useGrabInteractive.ts` と同じ形にする:

```ts
import { Topics, useTopicSelector } from "@/bridge";
import { screenForUiState, screenShowsAlwaysOnHud } from "./uiScreenRouting";

// 常時表示族のHUDが自分で引っ込むための購読。画面名はここだけが知る
// Subscription that lets the always-on HUDs withdraw themselves; only this file knows the screen names
export function useAlwaysOnHudVisible(): boolean {
  return useTopicSelector(Topics.uiState, (d) => screenShowsAlwaysOnHud(screenForUiState(d?.state ?? null, d?.subState)));
}
```

- [x] **Step 6: re-exportを足す**

`moorestech_web/webui/src/shared/uiState/index.ts` を開き、既存の `export { screenAllowsGrab, screenForUiState, uiStateAcceptsHotbarSelect, type UiScreen } from "./uiScreenRouting";` の行へ `screenShowsAlwaysOnHud` を追加し、その近くへ新規行を足す:

```ts
export { useAlwaysOnHudVisible } from "./useAlwaysOnHudVisible";
```

- [x] **Step 7: HotbarPanel を自己ゲートさせる**

`moorestech_web/webui/src/features/hotbar/HotbarPanel/index.tsx` のimport行を変更する。変更前:

```ts
import { useBlockingSkitActive, uiStateAcceptsHotbarSelect } from "@/shared/uiState";
```

変更後:

```ts
import { useAlwaysOnHudVisible, useBlockingSkitActive, uiStateAcceptsHotbarSelect } from "@/shared/uiState";
```

続けて、`const blockingSkitActive = useBlockingSkitActive();` の直下へ1行足す:

```ts
  const alwaysOnHudVisible = useAlwaysOnHudVisible();
```

そして既存の `if (blockingSkitActive) return null;` の直下へ2行足す:

```ts
  // 研究画面のように全幅を使う画面ではHUDごと引っ込む
  // Withdraw the whole HUD on screens that use the full width, such as the research screen
  if (!alwaysOnHudVisible) return null;
```

（`useTopicSelector` を使う `selectAccepted` の行より前にhookを置くこと。すべてのhook呼び出しが早期returnより上にある状態を保つ）

- [x] **Step 8: EquipmentPanel を自己ゲートさせる**

`moorestech_web/webui/src/features/inventory/EquipmentPanel/index.tsx` のimport行を変更する。変更前:

```ts
import { isPointerOverWebUi, isWheelPassthrough, useGameLayerWheel, useGrabInteractive } from "@/shared/uiState";
```

変更後:

```ts
import { isPointerOverWebUi, isWheelPassthrough, useAlwaysOnHudVisible, useGameLayerWheel, useGrabInteractive } from "@/shared/uiState";
```

`const grabInteractive = useGrabInteractive();` の直下へ1行足す:

```ts
  const alwaysOnHudVisible = useAlwaysOnHudVisible();
```

既存の `if (!inventory) return null;` の直上へ3行足す（`useGameLayerWheel` の呼び出しより後・全hookの後であることを必ず確認する）:

```ts
  // 研究画面ではHUDごと引っ込み、ホイールの装備切替も同時に降りる
  // On the research screen the HUD withdraws entirely, which also retires its wheel-driven equipment switching
  if (!alwaysOnHudVisible) return null;
```

- [x] **Step 9: 非表示のe2eを書く**

`moorestech_web/webui/e2e/tests/research/research.spec.ts` の末尾へ追記する:

```ts
test("研究画面ではホットバーと装備HUDを描画しない", async ({ page }) => {
  await setUiState(page, "ResearchTree");
  await page.goto("/");
  await expect(page.getByTestId("research-tree")).toBeVisible();
  await expect(page.getByTestId("hotbar-grid")).toHaveCount(0);
  await expect(page.getByTestId("equipment-slots")).toHaveCount(0);

  // 持ち物画面へ戻せば両HUDが復帰する（研究画面限定であることの担保）
  // Both HUDs return on the inventory screen, proving the hiding is research-only
  await setUiState(page, "PlayerInventory");
  await expect(page.getByTestId("hotbar-grid")).toBeVisible();
  await expect(page.getByTestId("equipment-slots")).toBeVisible();
});
```

- [x] **Step 10: テストを実行して通ることを確認する**

Run: `cd /Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/research-ui-refresh/moorestech_web/webui && npx vitest run src/shared/uiState && npx playwright test e2e/tests/research/`

Expected: vitest 全PASS。playwright は research 配下が全PASS（`researchViewport.spec.ts` は Task 3 でコメントのみ直すが、この時点では通る）。

- [x] **Step 11: コミットする**

```bash
cd /Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/research-ui-refresh
git add moorestech_web/webui/src/shared/uiState moorestech_web/webui/src/features/hotbar/HotbarPanel/index.tsx moorestech_web/webui/src/features/inventory/EquipmentPanel/index.tsx moorestech_web/webui/e2e/tests/research/research.spec.ts
git commit -m "feat: 研究画面でホットバーと装備HUDを引っ込める"
```

---

### Task 3: 研究パネルを持ち物の右側へ棲み分けさせる

レイアウトの本体。トークン化 → stage修飾 → researchArea の左端確定 → 重畳撤去 → 既存レイアウトe2eの差し替え。

**Files:**
- Modify: `moorestech_web/webui/src/app/tokens.css`（レイアウトトークン2本追加）
- Modify: `moorestech_web/webui/src/app/App.module.css`（`.stage` の column-gap をトークン化、`.stageResearch` 追加）
- Modify: `moorestech_web/webui/src/app/App.tsx:87-89`（stage修飾クラスの適用、InventoryPanel の prop 撤去）
- Modify: `moorestech_web/webui/src/features/research/style.module.css:1-8`（`.researchArea`）
- Modify: `moorestech_web/webui/src/features/inventory/InventoryPanel/index.tsx`（`screen` prop と `chromeZ` の撤去、幅のトークン化）
- Modify: `moorestech_web/webui/e2e/tests/research/research.spec.ts:138-158`（全域占有テストを棲み分けテストへ差し替え）
- Modify: `moorestech_web/webui/e2e/tests/research/researchViewport.spec.ts:14-18`（装備HUD前提のコメント修正）
- Test: `moorestech_web/webui/e2e/tests/research/research.spec.ts`

**Interfaces:**
- Consumes: Task 2 で追加した `screenShowsAlwaysOnHud`（研究画面でHUDが消えている前提のe2eアサーション）
- Produces:
  - CSSカスタムプロパティ `--inventory-panel-width: 378px`、`--stage-column-gap: 2.1875rem`（`tokens.css` の `:root`）
  - CSSクラス `.stageResearch`（`App.module.css`）
  - `InventoryPanel` のシグネチャは `export default function InventoryPanel()`（引数なし）へ戻る

- [x] **Step 1: レイアウトの失敗するe2eを書く**

`moorestech_web/webui/e2e/tests/research/research.spec.ts` の既存テスト `研究パネルはステージ全域を占有し持ち物とキーヒントが上に重なる`（138行目付近、`});` まで）を、次のテスト2本で丸ごと置き換える:

```ts
test("研究パネルは持ち物の右隣から画面端までを占有し持ち物と重ならない", async ({ page }) => {
  await setUiState(page, "ResearchTree");
  await page.goto("/");
  const tree = page.getByTestId("research-tree");
  const stageBox = await page.getByTestId("app-stage").boundingBox();
  const treeBox = await tree.boundingBox();
  const inventoryBox = await page.getByTestId("main-grid").boundingBox();
  // 上・右・下はstage端に密着（誤差1.5px）
  // Top, right and bottom hug the stage edges (1.5px tolerance)
  expect(Math.abs(treeBox!.y - stageBox!.y)).toBeLessThan(1.5);
  expect(Math.abs(treeBox!.x + treeBox!.width - (stageBox!.x + stageBox!.width))).toBeLessThan(1.5);
  expect(Math.abs(treeBox!.y + treeBox!.height - (stageBox!.y + stageBox!.height))).toBeLessThan(1.5);
  // 左端は「持ち物幅378px + 列gap35px」のscale後の位置（GamePanelにtestIdが無いため数値で押さえる）
  // The left edge is the scaled position of "inventory width 378px + column gap 35px" (GamePanel exposes no testId, so assert numerically)
  const scale = await page.getByTestId("app-stage").evaluate((element) => {
    const matrix = new DOMMatrixReadOnly(getComputedStyle(element).transform);
    return matrix.a;
  });
  expect(treeBox!.x - stageBox!.x).toBeCloseTo((378 + 35) * scale, 0);
  // スロットグリッド基準でも重なりが無いことを二重に押さえる
  // Double-check non-overlap against the slot grid as well
  await expectSeparatedHorizontally(page.getByTestId("main-grid"), tree);
  expect(inventoryBox!.x + inventoryBox!.width).toBeLessThanOrEqual(treeBox!.x);
  // 持ち物はクリック可のまま（棲み分け後もgrabは生きる）
  // The inventory stays clickable; grab survives the split
  await page.getByTestId("main-grid").locator(":scope > *").first().click({ trial: true });
  await expect(page.getByTestId("research-key-hints")).toBeVisible();
});

test("研究画面では持ち物がstage左paddingぶん左へ寄る", async ({ page }) => {
  await setUiState(page, "PlayerInventory");
  await page.goto("/");
  const onInventoryScreen = await page.getByTestId("main-grid").boundingBox();
  await setUiState(page, "ResearchTree");
  await expect(page.getByTestId("research-tree")).toBeVisible();
  const onResearchScreen = await page.getByTestId("main-grid").boundingBox();
  // stage拡縮がかかるため、左padding59.7pxのscale後の値と突き合わせる
  // The stage is scaled, so compare against the scaled value of the 59.7px left padding
  const scale = await page.getByTestId("app-stage").evaluate((element) => {
    const matrix = new DOMMatrixReadOnly(getComputedStyle(element).transform);
    return matrix.a;
  });
  expect(onInventoryScreen!.x - onResearchScreen!.x).toBeCloseTo(59.7 * scale, 0);
  // 縦位置は動かさない
  // The vertical position does not move
  expect(Math.abs(onResearchScreen!.y - onInventoryScreen!.y)).toBeLessThan(1.5);
});
```

同ファイル先頭のimport群へ `expectSeparatedHorizontally` を追加する（既存で `expectHitTestWithin` を `../../support/layoutAssertions` から取り込んでいるので、その行へ足す）。

- [x] **Step 2: テストを実行して失敗を確認する**

Run: `cd /Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/research-ui-refresh/moorestech_web/webui && npx playwright test e2e/tests/research/research.spec.ts -g "持ち物"`

Expected: FAIL 2件。1本目は `expectSeparatedHorizontally` が「研究パネルの左端(=stage左端)が持ち物の右端より左」で落ちる。2本目は差分が 0 で `toBeCloseTo(59.7 * scale)` に落ちる。

- [x] **Step 3: レイアウトトークンを追加する**

`moorestech_web/webui/src/app/tokens.css` の `--menu-content-height` を定義している行の直下へ追記する:

```css
  /* 持ち物パネルの幅。InventoryPanelのインラインstyleと研究パネルの左端導出が同じ値を見る */
  /* The inventory panel width; InventoryPanel's inline style and the research panel's left edge share this one value */
  --inventory-panel-width: 378px;
  /* stage上段3カラムの列間隔。研究パネルはこの間隔を挟んで持ち物の右へ並ぶ */
  /* The gap between the stage's three upper columns; the research panel sits to the inventory's right across it */
  --stage-column-gap: 2.1875rem;
```

- [x] **Step 4: stage の column-gap をトークン化し、研究画面用の修飾クラスを足す**

`moorestech_web/webui/src/app/App.module.css` の `.stage` 内の行を変更する。変更前:

```css
  column-gap: 2.1875rem;
```

変更後:

```css
  column-gap: var(--stage-column-gap);
```

続けて `.stage { ... }` ブロックの閉じ括弧の直後へ、新しいブロックを足す:

```css
/* 研究画面だけ左paddingを外し、持ち物を画面左端へ寄せて右側の空きを研究パネルへ渡す */
/* Drop the left padding on the research screen alone, pushing the inventory to the screen edge and handing the freed space to the research panel */
.stageResearch {
  padding-left: 0;
}
```

- [x] **Step 5: App.tsx で修飾クラスを当て、InventoryPanel の prop を外す**

`moorestech_web/webui/src/app/App.tsx` の stage の div を変更する。変更前:

```tsx
      <div ref={stageRef} className={styles.stage} data-testid="app-stage" data-web-ui-transparent>
        {screenAllowsGrab(screen) && <InventoryPanel screen={screen} />}
```

変更後:

```tsx
      <div ref={stageRef} className={`${styles.stage}${researchScreen ? ` ${styles.stageResearch}` : ""}`} data-testid="app-stage" data-web-ui-transparent>
        {screenAllowsGrab(screen) && <InventoryPanel />}
```

（`researchScreen` は同ファイル内に既存の `const researchScreen = screen === "researchTree";` があるのでそのまま使う）

- [x] **Step 6: InventoryPanel から重畳ハックを撤去し、幅をトークン化する**

`moorestech_web/webui/src/features/inventory/InventoryPanel/index.tsx` から次を削除する:
- import 行 `import type { UiScreen } from "@/shared/uiState";`
- 型定義 `type Props = { screen: UiScreen };`
- `chromeZ` の宣言2行（`// chrome z の重畳は…` / `// The chrome z-overlay is…` のコメント2行を含む）

関数シグネチャを変更する。変更前:

```tsx
export default function InventoryPanel({ screen }: Props) {
```

変更後:

```tsx
export default function InventoryPanel() {
```

`GamePanel` のインラインstyleを変更する。変更前（該当部分の抜粋）:

```tsx
style={{ justifySelf: "start", alignSelf: "start", width: 378, minHeight: 452.391, transform: "translate(0.783px, 0.783px)", ...chromeZ, "--panel-left": "-2.22px", ... } as CSSProperties}
```

変更後（`width` をトークン参照へ、`...chromeZ` を削除）:

```tsx
style={{ justifySelf: "start", alignSelf: "start", width: "var(--inventory-panel-width)", minHeight: 452.391, transform: "translate(0.783px, 0.783px)", "--panel-left": "-2.22px", ... } as CSSProperties}
```

（`...` の部分は既存の `--panel-right` 以降をそのまま残す。消してはいけない）

- [x] **Step 7: researchArea の左端をトークンで確定する**

`moorestech_web/webui/src/features/research/style.module.css` の先頭8行（`.researchArea` ブロックとその上のコメント）を、次で置き換える:

```css
/* 研究エリア: 持ち物パネルの右隣から画面端までを占有する（ADR 0014 決定2・裁定2026-08-19） */
/* Research area spans from the inventory panel's right edge to the screen edges (ADR 0014 decision 2) */
/* 絶対配置の基準はstageのpadding boxなので、上右下の0はstageのpaddingを跨いで実画面端へ届く */
/* The containing block is the stage's padding box, so 0 on top/right/bottom reaches the real screen edges across the stage padding */
.researchArea {
  position: absolute;
  top: 0;
  right: 0;
  bottom: 0;
  left: calc(var(--inventory-panel-width) + var(--stage-column-gap));
  z-index: var(--z-stage-overlay-panel);
  min-width: 0;
}
```

- [x] **Step 8: テストを実行して通ることを確認する**

Run: `cd /Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/research-ui-refresh/moorestech_web/webui && npx playwright test e2e/tests/research/research.spec.ts`

Expected: 全PASS。特に「研究パネルは持ち物の右隣から画面端までを占有し持ち物と重ならない」「研究画面では持ち物がstage左paddingぶん左へ寄る」「研究パネル展開中も常駐チャレンジHUDとキーヒントが遮蔽されない」「研究パネル展開中も採掘進捗バーが遮蔽されない」の4本が通ること。

- [x] **Step 9: researchViewport.spec.ts の古い前提コメントを直す**

`moorestech_web/webui/e2e/tests/research/researchViewport.spec.ts` の以下2行のコメントを差し替える。変更前:

```ts
// 研究パネルのステージ全域化で右下は常時HUD(装備スロット)と重なるため、右上の空白を使う
// The research panel now spans the full stage, so the bottom-right overlaps the always-on equipment HUD; use the top-right instead
```

変更後:

```ts
// 右下は詳細ペインや将来のHUD復帰と競合しうるため、ノードの居ない右上の空白を安定した起点として使う
// The bottom-right can collide with the detail pane or a future HUD, so the node-free top-right is the stable drag origin
```

さらに55行目付近の回帰ガードコメントを差し替える。変更前:

```ts
  // ドラッグが実際にパンを起こしたことを検証（装備スロットとの衝突で起点が死んでいた回帰の再発防止）
  // Verify the drag actually panned (regression guard: the drag origin previously collided with the equipment slot and produced zero movement)
```

変更後:

```ts
  // ドラッグが実際にパンを起こしたことを検証（起点が他要素に食われて無反応になる回帰の再発防止）
  // Verify the drag actually panned (regression guard against a drag origin swallowed by another element, producing zero movement)
```

- [x] **Step 10: webui全体のテストとlintを実行する**

Run: `cd /Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/research-ui-refresh/moorestech_web/webui && npx vitest run && npx eslint . && npx playwright test`

Expected: vitest 全PASS、eslint clean、playwright は既知の恒常赤（既定ロケールjapaneseと英語literal期待の10件・bd moorestech-2lh.1）以外すべてPASS。**inventory系・hotbar系・equipment系のspecが新たに落ちていないことを必ず確認する**（落ちていればR4/R8違反なので直す）。

- [x] **Step 11: コミットする**

```bash
cd /Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/research-ui-refresh
git add moorestech_web/webui/src moorestech_web/webui/e2e
git commit -m "fix: 研究パネルを持ち物の右側へ棲み分けさせ重畳をやめる"
```

---

### Task 4: ホットバーの役割をコード側コメントへ明文化する（bd moorestech-4ed）

設計対話で agent がホットバーの仕様を誤読した事故の再発防止。役割を書いた場所は、誤読が起きた導線（ドラッグ元の型・HUDの冒頭・配信DTO）に置く。

**Files:**
- Modify: `moorestech_web/webui/src/features/hotbar/hotbarDnd.ts:5-8`
- Modify: `moorestech_web/webui/src/features/hotbar/HotbarPanel/index.tsx:11-14`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/Hotbar/HotbarDtos.cs:7-12`

**Interfaces:**
- Consumes: なし（コメントのみ。ロジック・シグネチャを一切変えない）
- Produces: なし

- [x] **Step 1: 着手をbdへ記録する**

```bash
cd /Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/research-ui-refresh
bd update moorestech-4ed --claim
```

- [x] **Step 2: hotbarDnd.ts のドラッグ元の型へ役割を書く**

`moorestech_web/webui/src/features/hotbar/hotbarDnd.ts` の既存コメント2行を探す:

```ts
// ドラッグ元。枠外から掴むことはないのでoutsideを持たない
// A drag source; nothing is ever grabbed from outside a slot, so "outside" is not one of these
```

その直前へ次を挿入する（既存2行は残す）:

```ts
// ホットバーの枠が持つのは配置対象(ブロック・列車車両・接続ツール・BP・BPコピー)だけで、持ち物のアイテムは入らない
// A hotbar slot only ever holds a placement target (block, train car, connect tool, blueprint, blueprint copy); inventory items never enter it
// 割当が生まれる唯一の経路はビルドメニューのエントリのドラッグで、持ち物からのドロップは仕様として存在しない
// The one and only path that creates an assignment is dragging a build-menu entry; dropping from the inventory does not exist by design
```

- [x] **Step 3: HotbarPanel の冒頭コメントへ役割を書く**

`moorestech_web/webui/src/features/hotbar/HotbarPanel/index.tsx` の既存コメント4行を探す:

```ts
// 常時表示のホットバーHUD
// 数字キーは一切listenしない
// Always-on hotbar HUD; it only subscribes to local_player.hotbar (independent of UIState).
// Digit keys are unified into the Unity-side HotbarKeyInput, so this panel never listens for keys
```

その直前へ次を挿入する（既存4行は残す）:

```ts
// ホットバーは配置対象を9枠へ割り当てて選ぶHUDで、持ち物のアイテム欄ではない(割当元はビルドメニューのみ)
// The hotbar assigns and selects placement targets across 9 slots; it is not an inventory item bar (only the build menu assigns into it)
```

- [x] **Step 4: HotbarDtos.cs の docstring へ役割を書く**

`moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/Hotbar/HotbarDtos.cs` の `HotbarTopicDto` の docstring を変更する。変更前:

```csharp
    /// <summary>
    /// local_player.hotbar の配信 DTO
    /// Payload DTOs for local_player.hotbar
    /// </summary>
```

変更後:

```csharp
    /// <summary>
    /// local_player.hotbar の配信 DTO。枠が持つのは配置対象9件のみで、持ち物のアイテムは入らない
    /// Payload DTOs for local_player.hotbar; the 9 slots hold placement targets only, never inventory items
    /// </summary>
```

- [x] **Step 5: コンパイルを実行する（.cs変更のため必須）**

Run: `cd /Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/research-ui-refresh && uloop compile --project-path ./moorestech_client`

Expected: errors 0。「Unity is reloading (Domain Reload in progress)」が出たら45秒待ってリトライする。Editorが立っていない場合は `moores-wt` で用意したこのworktreeのEditorを `uloop launch` で起動してから再実行する。

- [x] **Step 6: webuiのlintとユニットテストを実行する**

Run: `cd /Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/research-ui-refresh/moorestech_web/webui && npx eslint src/features/hotbar && npx vitest run src/features/hotbar`

Expected: eslint clean、vitest 全PASS（`hotbarDnd.test.ts` / `useHotbarDragSource.test.ts`）。

- [x] **Step 7: コミットしてbdを閉じる**

```bash
cd /Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/research-ui-refresh
git add moorestech_web/webui/src/features/hotbar moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/Hotbar/HotbarDtos.cs
git commit -m "docs: ホットバーの役割をコード側コメントへ明文化する"
bd close moorestech-4ed --reason="hotbarDnd.ts・HotbarPanel・HotbarDtos.csへ役割コメントを追加。配置対象専用で持ち物アイテムは入らないこと、割当元がビルドメニューのみであることを明記"
```

---

### Task 5: PR用スクリーンショットを撮り直し、PR本文を更新する

**Files:**
- Modify: `docs/pr-assets/research-ui-refresh/overview-full.png`
- Modify: `docs/pr-assets/research-ui-refresh/overview-wide.png`
- Modify: `docs/pr-assets/research-ui-refresh/detail-researchable.png`
- Modify: `docs/pr-assets/research-ui-refresh/detail-item-lacking.png`

**Interfaces:**
- Consumes: Task 3 完了後のレイアウト
- Produces: PR #1176 本文が参照する画像4枚

- [x] **Step 1: 撮影スクリプトを走らせる**

Run: `cd /Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/research-ui-refresh/moorestech_web/webui && CAPTURE_OUT_DIR=/tmp/research-qa-2026-08-19 npx tsx e2e/capture-research-qa.ts`

Expected: `/tmp/research-qa-2026-08-19/` に4枚のpngが出力される。ポート5412が他セッションと衝突する場合は `CAPTURE_PORT` を空きポートへ変えて再実行する。

- [x] **Step 2: 撮れた画像を目視で検分する**

`/tmp/research-qa-2026-08-19/overview-wide.png` を開き、次の3点を必ず確認する:
1. 持ち物パネルと研究ツリーのノードが**一切重なっていない**
2. ホットバーと装備HUDが**写っていない**
3. 研究パネルの上下右端が画面端に達している

1つでも満たさなければ Task 3 の実装に戻る（画像を差し替えて先へ進んではいけない）。

- [x] **Step 3: リポジトリへ反映する**

```bash
cd /Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/research-ui-refresh
cp /tmp/research-qa-2026-08-19/*.png docs/pr-assets/research-ui-refresh/
git add docs/pr-assets/research-ui-refresh
git commit -m "docs: 棲み分けレイアウトでPR用スクリーンショットを撮り直す"
```

- [x] **Step 4: PR本文を更新する**

`gh pr edit 1176 --body-file` で本文を差し替える。差し替える箇所は次の2つだけで、他の節（Summary の解放物まわり・Test plan）は現行の記述を保つ:
- Summary 1行目の「研究パネルをステージ全域占有にして持ち物・ヒントを上層へ重畳する」を「研究パネルを持ち物の右側へ広げて棲み分けさせ、研究画面ではホットバー・装備HUDを引っ込める（ADR 0014 決定2を2026-08-19に差し替え）」へ書き換える
- 「## スクリーンショット」節の各URLの commit SHA を、Step 3 のコミットSHAへ更新する（`git rev-parse HEAD` で取得）

- [x] **Step 5: pushする**

```bash
cd /Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/research-ui-refresh
git push
```

---

### Task 6: 全ブランチレビュー（必須・省略不可）

**Files:**
- Modify: レビュー指摘に応じて変更する

**Interfaces:**
- Consumes: Task 1〜5 の全成果
- Produces: レビュー通過状態のブランチ

- [x] **Step 1: moores-code-review を実行する**

`moores-code-review` スキルを起動し、`feature/research-ui-refresh` の全ブランチ差分（master...HEAD）をレビュー対象として実行する。ゴール文言による省略は不可。

- [x] **Step 2: 機械的な指摘を適用する**

決定論チェック・規約違反（コメント規約・行数・トークン直書き等）の指摘をすべて適用する。

- [x] **Step 3: 設計判断の指摘をユーザーへ諮る**

設計判断を要する指摘は AskUserQuestion でまとめて裁定を仰ぐ。裁定は `.decisions/` へ記録する。

- [x] **Step 4: 再検証してコミット・pushする**

```bash
cd /Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/research-ui-refresh/moorestech_web/webui
npx vitest run && npx eslint . && npx playwright test
cd /Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/research-ui-refresh
uloop compile --project-path ./moorestech_client
git add -A && git commit -m "fix: レビュー指摘へ対応する" && git push
```

---

## 判断記録（ADR）

**設計正本:**
- `docs/adr/0014-research-ui-four-states-fullstage-unlock-sections.md` 決定2（2026-08-19差し替え版）
- `.decisions/2026-08-19-研究パネルは持ち物と棲み分け重畳をやめる.md`
- `.decisions/2026-08-19-研究画面のときだけ持ち物を画面左端へ寄せる.md`
- `.decisions/2026-08-19-研究画面ではホットバーと装備HUDを非表示にする.md`
- 失効: `.decisions/2026-08-18-研究画面はステージ全域を占有しHUD重畳を許容する.md`（上下右の密着のみ生存）、`.decisions/2026-08-18-研究画面全面化でも持ち物パネルは今まで通り重畳表示する.md`（全体失効）

**planning中に生じた判断:**

1. **HUDの出し分けはコンポーネント自己ゲート（hook）で行い、App.tsx の描画ゲートにはしない。**
   - 出所: agent前提（役割同型の前例一致）。`HotbarPanel` は既に `useBlockingSkitActive()` で自分を消しており、「常時表示HUDが全体条件で引っ込む」役割の前例はこの自己ゲート。App側ゲート（`screenAllowsGrab(screen) && <InventoryPanel />`）は「画面固有パネルの出し分け」の前例で役割が違う。
2. **画面述語の置き場は `shared/uiState/uiScreenRouting.ts`。**
   - 出所: agent前提（`screenAllowsGrab` / `uiStateAcceptsHotbarSelect` と同じ場所。ここが画面名を知る唯一の層で、feature側へ画面名リテラルを漏らさない）。
3. **持ち物の左寄せは `.stage` の修飾クラスで行い、`InventoryPanel` へ画面名を戻さない。**
   - 出所: agent前提（R5で撤去した `screen` prop を別名で復活させるのは同じ設計ミスの再演。レイアウトの持ち主は stage であり、App は既に `researchScreen` を持っている）。
4. **`--inventory-panel-width` と `--stage-column-gap` をトークン化する。**
   - 出所: agent前提（DRY。持ち物幅378pxと列gap 35pxを研究パネルの左端導出と二重管理すると、片方だけ動いたとき無言でレイアウトがずれる）。
5. **`.researchArea` の `z-index: var(--z-stage-overlay-panel)` は据え置く。**
   - 出所: agent前提（重畳は解消したが、`.viewportOverlay`(31) が研究パネル(30) を上回る関係は採掘進捗バーの遮蔽ガードe2eが依存しているため、下げも上げもしない）。
6. **Task 1（様式ドキュメント）を実装より前に置く。**
   - 出所: ADR 0014 の「様式が先、実装が後の原則」に従う。

**この計画で意図的に触らないもの:** サーバー側ロジック・プロトコル・研究DTO契約・ノードカードの4状態・種類別解放セクション。
