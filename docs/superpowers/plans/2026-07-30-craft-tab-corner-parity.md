---
spec: docs/superpowers/specs/2026-07-30-craft-tab-corner-parity-design.md
---

# Craft Tab and Corner Grip Visual Parity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 中央クラフトパネル上部のタブと右下グリップを3270×1844pxのuGUI正本と数値・目視の両方で一致させ、開発時の `Ping Action` ボタンを削除する。

**Architecture:** uGUI PNGは測定資料だけに限定し、タブは `ItemHeader.tsx` 内の5層インラインSVG、グリップは共有 `GamePanel variant="craft"` の単色疑似要素として再構成する。PlaywrightでDOM契約と共有consumerの非破壊性を固定し、パネル原点を正規化する専用画像比較器と独立subagentの目視判定を往復して収束させる。

**Tech Stack:** React 18 / TypeScript / CSS Modules / Vite / Vitest / Playwright / Python 3 + Pillow + NumPy

## Global Constraints

- 実装開始前に本planのユーザー承認を得る。承認後は `superpowers:subagent-driven-development` で実装担当subagentへタスク単位に委譲する。
- 正本は `docs/webui-parity/reference-player-inventory-3270x1844.png`。比較解像度は3270×1844pxに固定する。
- PNGは測定にだけ使い、Web配信用アセット、base64、CSS `url()` として追加しない。装飾はCSSとインラインSVGだけで描く。
- タブとグリップの寸法・位置・色は `src/app/tokens.css` の専用トークンへ集約する。機能CSSへ新しい色を直書きしない。
- `GamePanel variant="craft"` のグリップは中央、`PlacementModeHud`、`ResearchDetailPane` の3箇所へ共有する。consumer側へ複製しない。
- チャレンジHUDによる全画面の縦移動は対象外。画像比較ではクラフトパネル左上を原点に揃えるが、装飾とパネルの相対位置差は残す。
- `debug.echo` のAction型、ホスト処理、WebSocket ping/pongは削除しない。
- `.cs`、Unity YAML、`.meta` は変更しない。したがってUnityコンパイルは不要。必要が生じた場合は範囲外として停止する。
- 新規コードは `e2e/craft-chrome/` と `src/features/inventory/__tests__/` に分け、1ディレクトリ10ファイル以下を維持する。全ファイル200行以下にする。
- 既に211行ある `GamePanel/style.module.css` は、変更時に古い反復コメントを圧縮して200行以下へ戻す。
- 各実装タスクで失敗するテストを先に確認し、通過後にコミットする。最終的にworktreeをcleanにする。

## File Structure

新規:

- `moorestech_web/webui/e2e/craft-chrome/compare.py` — パネル原点正規化、対象bbox/色判定、crop・blend・diff生成
- `moorestech_web/webui/e2e/support/craftChromeAssertions.ts` — 共有craftグリップの計算済みCSSと内容非重複を検証
- `moorestech_web/webui/src/features/inventory/__tests__/InventoryScreenChrome.test.ts` — 開発用ボタンのソース契約

変更:

- `moorestech_web/webui/src/features/inventory/InventoryScreenChrome.tsx`
- `moorestech_web/webui/src/features/recipe/views/ItemHeader.tsx`
- `moorestech_web/webui/src/features/recipe/views/ItemHeader.module.css`
- `moorestech_web/webui/src/shared/ui/GamePanel/style.module.css`
- `moorestech_web/webui/src/app/tokens.css`
- `moorestech_web/webui/e2e/tests/inventory.spec.ts`
- `moorestech_web/webui/e2e/tests/recipe.spec.ts`
- `moorestech_web/webui/e2e/tests/research.spec.ts`
- `moorestech_web/webui/e2e/support/operationHudAssertions.ts`
- `docs/webui-parity/iteration-log.md`

削除:

- `moorestech_web/webui/src/features/inventory/DebugActionButton.tsx`

---

### Task 1: 対象専用の画像比較器を追加して現状不一致を再現する

**Files:**

- Create: `moorestech_web/webui/e2e/craft-chrome/compare.py`
- Read: `moorestech_web/webui/e2e/parity-check.py`
- Read: `moorestech_web/webui/e2e/parity_targets.py`
- Read: `moorestech_web/webui/e2e/capture-eval.ts`
- Test input: `docs/webui-parity/reference-player-inventory-3270x1844.png`
- Test input: `/tmp/webui-craft-current.png`

- [ ] **Step 1: 正規化比較器を実装する**

`compare.py` は次の定数と判定を持たせる。

```python
EXPECTED_SIZE = (3270, 1844)
TAB_SIZE = (166, 70)
TAB_LEFT_DELTA = 0
TAB_BOTTOM_GAP = (0, 3)
HAMMER_BOX = (44, -52, 99, 0)
GRIP_SIZE = (22, 22)
GRIP_RIGHT_GAP = 19
GRIP_BOTTOM_GAP = 19
GEOMETRY_TOLERANCE = 1
HAMMER_TOLERANCE = 2
COLOR_TOLERANCE = 15
```

座標は各画像で検出したクラフトパネル `(left, top, right, bottom)` に対する相対値へ変換する。パネル検出は既存 `parity-check.py` と同じ暗色50%規則を使う。

```python
def detect_panel(image: np.ndarray) -> tuple[int, int, int, int]:
    dark = image.max(axis=2) < 110
    zone = dark[250:1550, 1150:2150]
    columns = np.where(zone.mean(axis=0) > 0.5)[0]
    rows = np.where(zone.mean(axis=1) > 0.5)[0]
    if not len(columns) or not len(rows):
        raise ValueError("craft panel was not detected")
    return (
        int(columns[0] + 1150),
        int(rows[0] + 250),
        int(columns[-1] + 1150),
        int(rows[-1] + 250),
    )
```

タブはパネル左端から `x=-8..190`、上端から `y=-100..-1` の `max(R,G,B)<120` を、グリップはパネル右下80px四方の「各チャンネル差35未満・明度70〜190」の連結成分を測る。グリップではパネル枠に接する成分を除外し、幅・高さが5px以上の最大成分を選ぶ。ハンマーはタブ領域内の低彩度かつ明度68〜100の成分から、タブ左端+35〜110pxに中心を持つ最大成分を選ぶ。

bbox寸法は包含端点として `max-min+1`、パネル内側の隙間は `panel_edge-decoration_edge-1` で算出する。色はタブ前面 `(panelLeft+105,panelTop-45)`、背面 `(panelLeft+8,panelTop-60)`、右斜面 `(panelLeft+140,panelTop-20)`、ハンマー `(panelLeft+70,panelTop-30)`、グリップ `(panelRight-27,panelBottom-27)` の5×5px中央値を正本と比較する。

出力は各判定の `[PASS]` / `[FAIL]`、検出bbox、最大差とし、1件でも不合格ならexit 1にする。加えて `--out` 指定時は次を生成する。

- `tab-ref.png`, `tab-cur.png`, `tab-blend.png`, `tab-diff.png`
- `grip-ref.png`, `grip-cur.png`, `grip-blend.png`, `grip-diff.png`

cropはパネル相対でタブ `(-20,-110,210,30)`、グリップ `(right-120,bottom-120,right+20,bottom+20)` とし、正本・現状を同寸にする。ファイル全体を200行以下に収める。

- [ ] **Step 2: 自己比較で比較器の健全性を確認する**

Run:

```bash
cd moorestech_web/webui
python3 e2e/craft-chrome/compare.py \
  --ref ../../docs/webui-parity/reference-player-inventory-3270x1844.png \
  --cur ../../docs/webui-parity/reference-player-inventory-3270x1844.png \
  --out /tmp/webui-craft-chrome-self
```

Expected: exit 0、全判定PASS、blendが正本と同一、diffが全黒。

- [ ] **Step 3: 現状キャプチャに対して不一致を確認する**

Run:

```bash
cd moorestech_web/webui
python3 e2e/craft-chrome/compare.py \
  --ref ../../docs/webui-parity/reference-player-inventory-3270x1844.png \
  --cur /tmp/webui-craft-current.png \
  --out /tmp/webui-craft-chrome-baseline
```

Expected: exit 1。少なくともハンマー形状とグリップ22×22px判定がFAILし、8枚の比較画像が生成される。

- [ ] **Step 4: 比較器だけをコミットする**

```bash
git add moorestech_web/webui/e2e/craft-chrome/compare.py
git commit -m "test: add normalized craft chrome visual comparator"
```

---

### Task 2: `Ping Action` を開発ビルドからも削除する

**Files:**

- Create: `moorestech_web/webui/src/features/inventory/__tests__/InventoryScreenChrome.test.ts`
- Modify: `moorestech_web/webui/src/features/inventory/InventoryScreenChrome.tsx`
- Modify: `moorestech_web/webui/e2e/tests/inventory.spec.ts`
- Delete: `moorestech_web/webui/src/features/inventory/DebugActionButton.tsx`
- Verify only: `moorestech_web/webui/src/bridge/transport/actionContract.ts`
- Verify only: `moorestech_web/webui/src/bridge/transport/actions.test.ts`
- Verify only: `moorestech_web/webui/src/bridge/transport/webSocketClient.test.ts`

- [ ] **Step 1: 失敗するソース契約テストを書く**

テスト内に削除対象の可視文字列をそのまま残さないため、名前は断片から組み立てる。

```ts
import { existsSync, readFileSync } from "node:fs";
import { join } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";

const inventoryDirectory = fileURLToPath(new URL("..", import.meta.url));
const chromeSource = readFileSync(new URL("../InventoryScreenChrome.tsx", import.meta.url), "utf8");
const removedComponent = ["Debug", "Action", "Button"].join("");
const removedLabel = ["Ping", "Action"].join(" ");

describe("InventoryScreenChrome development controls", () => {
  it("削除済みの疎通確認ボタンをソースと描画経路へ戻さない", () => {
    expect(chromeSource).not.toContain(removedComponent);
    expect(chromeSource).not.toContain(removedLabel);
    expect(existsSync(join(inventoryDirectory, `${removedComponent}.tsx`))).toBe(false);
  });
});
```

Run:

```bash
cd moorestech_web/webui
pnpm exec vitest run src/features/inventory/__tests__/InventoryScreenChrome.test.ts
```

Expected: FAIL。遅延importと `DebugActionButton.tsx` が残っている。

- [ ] **Step 2: 表示経路と不要ファイルを削除する**

`InventoryScreenChrome.tsx` から `lazy`、`Suspense`、条件付きimport、描画ブロックを削除し、整理ボタンとキーヒントだけを残す。`DebugActionButton.tsx` を削除する。`debug.echo` のbridge契約には触れない。

- [ ] **Step 3: Playwrightへ画面上の不在ガードを追加する**

`inventory.spec.ts` の最初のテストへ、整理ボタンの存在確認と削除済みボタンの不在確認を追加する。削除済みラベルは同じく断片から作る。

```ts
const removedDevelopmentLabel = ["Ping", "Action"].join(" ");
await expect(page.getByRole("button", { name: "整理" })).toBeVisible();
await expect(page.getByRole("button", { name: removedDevelopmentLabel })).toHaveCount(0);
```

- [ ] **Step 4: 関連テストを通す**

Run:

```bash
pnpm exec vitest run \
  src/features/inventory/__tests__/InventoryScreenChrome.test.ts \
  src/bridge/transport/actions.test.ts \
  src/bridge/transport/webSocketClient.test.ts
pnpm exec playwright test --config e2e/playwright.config.ts e2e/tests/inventory.spec.ts
rg -n 'DebugActionButton|Ping Action' src --glob '!**/*.test.*'
```

Expected: VitestとPlaywrightがPASS。`rg` は0件でexit 1。`debug.echo` 関連テストは引き続きPASS。

- [ ] **Step 5: コミットする**

```bash
git add moorestech_web/webui/src/features/inventory moorestech_web/webui/e2e/tests/inventory.spec.ts
git commit -m "refactor: remove inventory ping action button"
```

---

### Task 3: 共有craftグリップを単色22×22pxへ修正する

**Files:**

- Create: `moorestech_web/webui/e2e/support/craftChromeAssertions.ts`
- Modify: `moorestech_web/webui/e2e/support/operationHudAssertions.ts`
- Modify: `moorestech_web/webui/e2e/tests/recipe.spec.ts`
- Modify: `moorestech_web/webui/e2e/tests/research.spec.ts`
- Modify: `moorestech_web/webui/src/app/tokens.css`
- Modify: `moorestech_web/webui/src/shared/ui/GamePanel/style.module.css`

- [ ] **Step 1: 共有グリップの失敗するE2E契約を書く**

`craftChromeAssertions.ts` に次のhelperを置く。

```ts
import { expect, type Locator } from "@playwright/test";

export async function expectCraftGrip(frame: Locator) {
  const contract = await frame.evaluate((element) => {
    const frameBox = element.getBoundingClientRect();
    const grip = getComputedStyle(element, "::after");
    const width = Number.parseFloat(grip.width);
    const height = Number.parseFloat(grip.height);
    const right = Number.parseFloat(grip.right);
    const bottom = Number.parseFloat(grip.bottom);
    const gripBox = {
      left: frameBox.right - right - width,
      top: frameBox.bottom - bottom - height,
      right: frameBox.right - right,
      bottom: frameBox.bottom - bottom,
    };
    const contentBoxes = Array.from(element.querySelectorAll("button,h1,h2,h3,p,img"))
      .filter((child) => getComputedStyle(child).display !== "none")
      .map((child) => child.getBoundingClientRect());
    const overlaps = contentBoxes.some((box) =>
      box.left < gripBox.right && box.right > gripBox.left &&
      box.top < gripBox.bottom && box.bottom > gripBox.top);
    return {
      content: grip.content,
      width, height, right, bottom,
      clipPath: grip.clipPath,
      backgroundColor: grip.backgroundColor,
      backgroundImage: grip.backgroundImage,
      boxShadow: grip.boxShadow,
      overlaps,
    };
  });
  expect(contract).toEqual({
    content: "\"\"",
    width: 9,
    height: 9,
    right: 7,
    bottom: 7,
    clipPath: "polygon(100% 0px, 100% 100%, 0px 100%)",
    backgroundColor: "rgba(146, 148, 167, 0.98)",
    backgroundImage: "none",
    boxShadow: "none",
    overlaps: false,
  });
}
```

ブラウザの `clipPath` 文字列表現が実測と異なる場合は、意味を緩めず `toContain("polygon")` と3頂点の数値個別検証へ変更する。

`recipe.spec.ts` では選択後の中央 `[data-variant="craft"]`、`operationHudAssertions.ts` では配置HUDのframe、`research.spec.ts` ではノード選択後の詳細ペイン直下frameに `expectCraftGrip` を適用する。

Run:

```bash
cd moorestech_web/webui
pnpm exec playwright test --config e2e/playwright.config.ts \
  e2e/tests/recipe.spec.ts \
  e2e/tests/modeHud/operation-mode-hud.spec.ts \
  e2e/tests/research.spec.ts
```

Expected: FAIL。現状は24×18px、3帯gradient、box-shadowあり。

- [ ] **Step 2: 専用トークンを追加する**

`tokens.css` の `--panel-edge-fade` 直後へ追加する。

```css
  /* クラフト枠の右下グリップを正本の固定寸法と単色面へ揃える */
  /* Match the craft-frame grip to the reference's fixed size and single-color face */
  --craft-grip-size: 9px;
  --craft-grip-inset: 7px;
  --craft-grip-face: rgb(146 148 167 / 98%);
```

- [ ] **Step 3: 疑似要素を単一三角形へ置換する**

`GamePanel/style.module.css` の `.craft::after` を次へ置換する。

```css
.craft::after {
  position: absolute;
  right: var(--craft-grip-inset);
  bottom: var(--craft-grip-inset);
  width: var(--craft-grip-size);
  height: var(--craft-grip-size);
  clip-path: polygon(100% 0, 100% 100%, 0 100%);
  background: var(--craft-grip-face);
  content: "";
  pointer-events: none;
}
```

旧gradient、box-shadowを削除する。同時に履歴化済みのiterコメントを短い日英2行へ圧縮し、ファイル全体を200行以下にする。パネル本体、`bottomDeco`、default/skit variantの宣言値は変えない。

- [ ] **Step 4: E2Eを通してコミットする**

Run:

```bash
pnpm exec playwright test --config e2e/playwright.config.ts \
  e2e/tests/recipe.spec.ts \
  e2e/tests/modeHud/operation-mode-hud.spec.ts \
  e2e/tests/research.spec.ts
wc -l src/shared/ui/GamePanel/style.module.css
```

Expected: 全テストPASS。CSSは200行以下。

```bash
git add moorestech_web/webui/src/app/tokens.css \
  moorestech_web/webui/src/shared/ui/GamePanel/style.module.css \
  moorestech_web/webui/e2e/support \
  moorestech_web/webui/e2e/tests/recipe.spec.ts \
  moorestech_web/webui/e2e/tests/research.spec.ts
git commit -m "fix: match shared craft panel grip to uGUI"
```

---

### Task 4: クラフトタブを5層SVGへ置換する

**Files:**

- Modify: `moorestech_web/webui/e2e/tests/recipe.spec.ts`
- Modify: `moorestech_web/webui/src/app/tokens.css`
- Modify: `moorestech_web/webui/src/features/recipe/views/ItemHeader.tsx`
- Modify: `moorestech_web/webui/src/features/recipe/views/ItemHeader.module.css`

- [ ] **Step 1: 失敗する構造・初期寸法テストを書く**

`recipe.spec.ts` のヘッダ装飾テストへ次を追加する。

```ts
const craftTab = page.getByTestId("craft-tab");
await expect(craftTab).toHaveAttribute("viewBox", "0 0 166 70");
await expect(craftTab).toHaveAttribute("aria-hidden", "true");
await expect(craftTab.locator("path")).toHaveCount(5);
const tabStyle = await craftTab.evaluate((element) => {
  const style = getComputedStyle(element);
  return {
    width: Number.parseFloat(style.width),
    height: Number.parseFloat(style.height),
    backgroundImage: style.backgroundImage,
  };
});
expect(tabStyle.width).toBeCloseTo(64.978, 2);
expect(tabStyle.height).toBeCloseTo(27.397, 2);
expect(tabStyle.backgroundImage).toBe("none");
```

Run: `pnpm exec playwright test --config e2e/playwright.config.ts e2e/tests/recipe.spec.ts`

Expected: FAIL。`data-testid="craft-tab"` と5層SVGが存在しない。

- [ ] **Step 2: タブ専用トークンを追加する**

`tokens.css` のグリップトークン直後へ追加する。

```css
  /* クラフトタブの正本寸法、パネル原点からの位置、各層色を一元化する */
  /* Centralize the craft tab's reference size, panel-relative placement, and layer colors */
  --craft-tab-width: 64.978px;
  --craft-tab-height: 27.397px;
  --craft-tab-margin-top: -37.18px;
  --craft-tab-margin-left: -11px;
  --craft-tab-margin-bottom: 6.46px;
  --craft-tab-back: rgb(7 9 18 / 90%);
  --craft-tab-side: rgb(24 22 25 / 92%);
  --craft-tab-face: rgb(58 60 75 / 94%);
  --craft-tab-edge: rgb(87 89 107 / 96%);
  --craft-tab-hammer: rgb(75 75 75);
```

- [ ] **Step 3: OS依存絵文字をインラインSVGへ置換する**

`ItemHeader.tsx` から `useI18n` と `t("🔨")` を削除し、`toolTab` を次へ置換する。

```tsx
<svg
  className={styles.toolTab}
  data-testid="craft-tab"
  viewBox="0 0 166 70"
  aria-hidden="true"
  focusable="false"
>
  <path className={styles.toolTabBack} d="M15 0H125L166 70H0V10H15Z" />
  <path className={styles.toolTabSide} d="M125 0H143L166 70H145Z" />
  <path className={styles.toolTabFace} d="M24 10H115L135 70H24Z" />
  <path className={styles.toolTabEdge} d="M24 10H115L135 70H24Z" />
  <path className={styles.toolTabHammer} d="M46 66L79 33L75 29L82 22L87 27L90 24L99 33L96 36L101 41L94 48L88 42L85 45L82 42L55 70Z" />
</svg>
```

5pathの順序は背面→右斜面→前面→縁→ハンマーとし、正本の重なり順を固定する。ハンマー末端はタブ下端まで届かせる。

- [ ] **Step 4: CSSをSVGレイヤーへ置換する**

`.toolTab` のborder、gradient、clip-path、`::before`、`::after`、font指定を削除し、次へ置換する。

```css
.toolTab {
  display: block;
  align-self: flex-start;
  width: var(--craft-tab-width);
  height: var(--craft-tab-height);
  margin-top: var(--craft-tab-margin-top);
  margin-left: var(--craft-tab-margin-left);
  margin-bottom: var(--craft-tab-margin-bottom);
  overflow: visible;
}

.toolTabBack { fill: var(--craft-tab-back); }
.toolTabSide { fill: var(--craft-tab-side); }
.toolTabFace { fill: var(--craft-tab-face); }
.toolTabEdge {
  fill: none;
  stroke: var(--craft-tab-edge);
  stroke-width: 1;
}
.toolTabHammer { fill: var(--craft-tab-hammer); }
```

主要宣言の前には規約どおり日英2行コメントを置く。品名・区切り線の宣言値は変えない。

- [ ] **Step 5: 構造テストを通してコミットする**

Run:

```bash
pnpm exec playwright test --config e2e/playwright.config.ts e2e/tests/recipe.spec.ts
pnpm exec vitest run src/features/recipe/views/CraftProgressArrow.test.ts
```

Expected: PASS。カラー絵文字はDOMにもソースにも残らない。

```bash
git add moorestech_web/webui/src/app/tokens.css \
  moorestech_web/webui/src/features/recipe/views/ItemHeader.tsx \
  moorestech_web/webui/src/features/recipe/views/ItemHeader.module.css \
  moorestech_web/webui/e2e/tests/recipe.spec.ts
git commit -m "fix: rebuild craft tab from uGUI vector layers"
```

---

### Task 5: subagent反復で2箇所のレンダリングを一致させる

**Files:**

- Tune only: `moorestech_web/webui/src/app/tokens.css`
- Tune only if shape verdict requires it: `moorestech_web/webui/src/features/recipe/views/ItemHeader.tsx`
- Tune only if shape verdict requires it: `moorestech_web/webui/src/features/recipe/views/ItemHeader.module.css`
- Tune only: `moorestech_web/webui/src/shared/ui/GamePanel/style.module.css`
- Modify: `docs/webui-parity/iteration-log.md`

- [ ] **Step 1: 3270×1844pxで再撮影する**

```bash
cd moorestech_web/webui
CAPTURE_VIEWPORT_W=1635 \
CAPTURE_VIEWPORT_H=922 \
CAPTURE_OUT=/tmp/webui-craft-current.png \
pnpm exec tsx e2e/capture-eval.ts
```

Expected: `/tmp/webui-craft-current.png` が3270×1844pxで生成される。

- [ ] **Step 2: 機械比較と比較素材生成を行う**

```bash
python3 e2e/craft-chrome/compare.py \
  --ref ../../docs/webui-parity/reference-player-inventory-3270x1844.png \
  --cur /tmp/webui-craft-current.png \
  --out /tmp/webui-craft-chrome
python3 ~/.agents/skills/visual-criteria-cross-eyes/scripts/grid-overlay.py \
  ../../docs/webui-parity/reference-player-inventory-3270x1844.png \
  /tmp/webui-craft-reference-grid.png
python3 ~/.agents/skills/visual-criteria-cross-eyes/scripts/grid-overlay.py \
  /tmp/webui-craft-current.png \
  /tmp/webui-craft-current-grid.png
```

Expected: タブ166×70px、左差±1px、下隙間0〜3px、ハンマーbbox各辺±2px、グリップ22×22px・右下隙間各19±1px、代表色RGB差各15以内がすべてPASS。

- [ ] **Step 3: fresh visual-review subagentへ独立判定を依頼する**

実装に参加していないsubagentへ、正本・現状・両grid・`tab-*`・`grip-*` のcrop/blend/diffを渡す。質問は次に固定する。

> タブと右下グリップだけを等倍で比較してください。輪郭、層数、面積比、パネル相対位置、色の差を列挙し、アンチエイリアス境界1px以内かつRGB差15以内だけを許容してください。どちらにも目視可能な差がなければ「両要素とも区別できる差なし」と明記してください。

- [ ] **Step 4: 1変数ずつ修正して再判定する**

機械FAILまたはsubagent指摘が1件でもあれば、実装担当subagentへ数値とcropを返す。1回の修正では次のいずれか1変数だけを変更する。

- 位置差: `--craft-tab-margin-*` または `--craft-grip-inset`
- 寸法差: `--craft-tab-width/height` または `--craft-grip-size`
- 面色差: 対応する専用色トークン
- 輪郭・層差: SVG path 1本だけ

修正ごとにTask 5 Step 1〜3を繰り返す。機械判定が全PASSし、fresh subagentが「両要素とも区別できる差なし」と判定するまで終了しない。単に全画面parity点が上がったことを終了条件にしない。

- [ ] **Step 5: 反復結果を記録してコミットする**

`docs/webui-parity/iteration-log.md` に各反復の変更変数、前後bbox、色差、subagent判定、最終成果物パスを追記する。

```bash
git add moorestech_web/webui/src/app/tokens.css \
  moorestech_web/webui/src/features/recipe/views/ItemHeader.tsx \
  moorestech_web/webui/src/features/recipe/views/ItemHeader.module.css \
  moorestech_web/webui/src/shared/ui/GamePanel/style.module.css \
  docs/webui-parity/iteration-log.md
git commit -m "fix: converge craft chrome rendering with uGUI"
```

---

### Task 6: 全回帰確認と最終コミット監査を行う

**Files:**

- Verify: all files changed above

- [ ] **Step 1: Web UIの全静的検証を実行する**

```bash
cd moorestech_web/webui
pnpm test
pnpm lint
pnpm build
pnpm exec tsc -p e2e/tsconfig.json --noEmit
```

Expected: 全コマンドexit 0。

- [ ] **Step 2: 関連Playwrightを再実行する**

```bash
pnpm exec playwright test --config e2e/playwright.config.ts \
  e2e/tests/inventory.spec.ts \
  e2e/tests/recipe.spec.ts \
  e2e/tests/modeHud/operation-mode-hud.spec.ts \
  e2e/tests/research.spec.ts
```

Expected: 全テストPASS。中央・配置HUD・研究詳細の各craft frameでグリップ契約が通る。

- [ ] **Step 3: 禁止事項とファイル制約を監査する**

```bash
rg -n 'DebugActionButton|Ping Action|🔨' src --glob '!**/*.test.*'
rg -n 'url\\(|base64' src/features/recipe/views/ItemHeader.module.css src/shared/ui/GamePanel/style.module.css
wc -l \
  e2e/craft-chrome/compare.py \
  e2e/support/craftChromeAssertions.ts \
  src/features/inventory/__tests__/InventoryScreenChrome.test.ts \
  src/features/recipe/views/ItemHeader.tsx \
  src/features/recipe/views/ItemHeader.module.css \
  src/shared/ui/GamePanel/style.module.css
```

Expected: 先頭2つの `rg` は0件でexit 1。全ファイル200行以下。

- [ ] **Step 4: 最終画像判定をもう一度実行する**

Task 5 Step 1〜3をfresh subagentで再実行する。Expected: 機械判定全PASSかつ「両要素とも区別できる差なし」。

- [ ] **Step 5: worktreeとコミットを監査する**

```bash
cd ../../
git status --short
git log --oneline -6
```

未コミット変更があれば対象別にコミットする。Expected: `git status --short` が空。仕様書、plan、実装、テスト、比較器、反復ログがすべてコミット済み。
