# Web UI uGUI Visual Parity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** インベントリ・クラフトWeb UIをuGUI正本の半透明・複層直角フレーム・装飾表現へ近づけ、Playwright比較の全MUSTと90/100以上を満たす。

**Architecture:** 既存の `GamePanel`、`ItemSlot`、`RecipeViewer`、`HotbarPanel` がそれぞれ所有する見た目をCSS Modulesで変更する。疑似要素を主体とし、ダガー・ダイヤ装飾だけ必要最小限の装飾DOMを追加する。既存の状態、通信、`data-selected`、テストIDは変更しない。

**Tech Stack:** React 18、TypeScript、CSS Modules、Mantine 8、Playwright 1.60、Vitest 2

## Global Constraints

- Playwrightは正本と同じviewport、同じUI状態、同じcropで撮影する。
- 座標許容はcrop比±1.5pt、線厚±2px、主要bbox±3px、色差はRGB各ch±15とする。
- 9項目のMUSTをすべて満たし、合計90/100以上を合格とする。
- `.cs`、Unity固有YAML、`.meta` は変更しない。
- `partial`、デフォルト引数、新しい状態管理・通信経路は追加しない。
- 既存の未追跡 `.mso/` は変更・コミットしない。

## File Structure and Placement Review

| File | Responsibility | Placement precedent |
|---|---|---|
| `src/shared/ui/GamePanel/style.module.css` | 共通パネルの透過背景、複層直角枠、見出し罫線 | 既存 `GamePanel` の視覚責務を維持 |
| `src/shared/ui/ItemSlot/style.module.css` | 通常・選択スロットの枠とグロー | 既存 `ItemSlot` の視覚責務を維持 |
| `src/features/recipe/RecipeViewer.module.css` | クラフト固有の三角・ダガー・ダイヤ・選択枠 | 既存recipe feature内の視覚責務を維持 |
| `src/features/recipe/views/ItemHeader.tsx` | 境界装飾に必要な非意味DOM | 既存見出し構造の所有者 |
| `src/features/inventory/HotbarPanel/style.module.css` | 番号容器の外側配置 | 既存Hotbarの視覚責務を維持 |
| `e2e/tests/visualParity.spec.ts` | 構造・計算済みCSSの回帰契約 | 既存Playwright E2E配置 |
| `e2e/capture-eval.ts` | 正本同寸の採点画像出力 | 既存採点ハーネスを維持 |

新しい型、public API、共有状態、通信、永続化、依存関係は追加しない。したがって層責務・イベント機構・マスタアクセスに関する配置変更はない。

---

### Task 1: Visual Contract Tests

**Files:**
- Create: `moorestech_web/webui/e2e/tests/visualParity.spec.ts`
- Modify: `moorestech_web/webui/e2e/capture-eval.ts`

**Interfaces:**
- Consumes: `data-testid="hotbar-grid"`, `data-testid="craft-recipe-box"`, `data-selected`
- Produces: 正本の構造条件を計算済みCSSとbboxで検証するPlaywrightテスト

- [ ] **Step 1: Write failing structural assertions**

```ts
test("uses layered square panels and external hotbar labels", async ({ page }) => {
  await openDemoInventory(page);
  const panel = page.getByRole("heading", { name: "CRAFT RECIPE" }).locator("..").locator("..");
  await expect(panel).toHaveCSS("border-radius", "0px");

  const hotbar = page.getByTestId("hotbar-grid");
  const firstCell = hotbar.locator("> div").first();
  const label = firstCell.locator("span").first();
  const slot = firstCell.locator("[data-filled]").first();
  const labelBox = await label.boundingBox();
  const slotBox = await slot.boundingBox();
  expect(labelBox).not.toBeNull();
  expect(slotBox).not.toBeNull();
  expect(labelBox!.y + labelBox!.height).toBeLessThan(slotBox!.y);
  expect(Math.abs(labelBox!.x + labelBox!.width / 2 - (slotBox!.x + slotBox!.width / 2))).toBeLessThanOrEqual(3);
});
```

- [ ] **Step 2: Run the focused E2E test and confirm failure**

Run: `cd moorestech_web/webui && pnpm exec playwright test --config e2e/playwright.config.ts e2e/tests/visualParity.spec.ts`

Expected: FAIL because the current hotbar label overlaps the slot and panel styling lacks the required layers.

- [ ] **Step 3: Extend the capture harness with stable crop outputs**

Add element screenshots for the inventory heading, recipe heading, center panel, hotbar, recipe frame, divider decoration, and selected slot under `CAPTURE_CROP_DIR`.

- [ ] **Step 4: Type-check the harness**

Run: `cd moorestech_web/webui && pnpm exec tsc -p e2e/tsconfig.json --noEmit`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add moorestech_web/webui/e2e
git commit -m "test(webui): add uGUI visual parity contracts"
```

### Task 2: Panel Chrome and Header Rules

**Files:**
- Modify: `moorestech_web/webui/src/shared/ui/GamePanel/style.module.css`
- Modify: `moorestech_web/webui/src/shared/ui/GamePanel/index.tsx`

**Interfaces:**
- Consumes: `variant="default" | "craft"`, title presence
- Produces: 半透明背景、直角二層枠、短い硬質影、複層フェード罫線

- [ ] **Step 1: Capture the failing baseline**

Run: `cd moorestech_web/webui && CAPTURE_OUT=/tmp/webui-before.png CAPTURE_CROP_DIR=/tmp/webui-before pnpm tsx e2e/capture-eval.ts`

Expected: current single rules, rounded/thin panels, and opaque-looking surfaces are visible.

- [ ] **Step 2: Implement layered panel chrome**

Use a transparent border plus inset lines and a short pixel shadow:

```css
.panel {
  border: 4px solid rgb(80 86 96 / 58%);
  border-radius: 0;
  background: rgb(9 13 18 / 58%);
  box-shadow: inset 0 0 0 4px rgb(16 20 27 / 72%), 8px 8px 0 rgb(0 0 0 / 28%);
}
```

- [ ] **Step 3: Implement independent fading header bands**

Use layered linear gradients on `.decoLine` and a title-specific upper band. Both bands must fade at their ends and must not create a left vertical edge.

- [ ] **Step 4: Run the focused E2E test**

Run: `cd moorestech_web/webui && pnpm exec playwright test --config e2e/playwright.config.ts e2e/tests/visualParity.spec.ts`

Expected: panel and header assertions PASS.

- [ ] **Step 5: Commit**

```bash
git add moorestech_web/webui/src/shared/ui/GamePanel
git commit -m "feat(webui): restore layered uGUI panel chrome"
```

### Task 3: Recipe Decorations

**Files:**
- Modify: `moorestech_web/webui/src/features/recipe/RecipeViewer.module.css`
- Modify: `moorestech_web/webui/src/features/recipe/views/ItemHeader.tsx`
- Modify: `moorestech_web/webui/src/features/recipe/views/RecipeContent.tsx`

**Interfaces:**
- Consumes: current item header and recipe box structure
- Produces: 右下階段三角、中央ダガー・ダイヤ罫線、直角クラフト枠

- [ ] **Step 1: Add decoration contract assertions**

Assert that `[data-testid="recipe-divider-ornament"]` exists, has a centered bbox, and that the recipe box has zero border radius.

- [ ] **Step 2: Run and confirm failure**

Run: `cd moorestech_web/webui && pnpm exec playwright test --config e2e/playwright.config.ts e2e/tests/visualParity.spec.ts`

Expected: FAIL because the divider ornament does not exist.

- [ ] **Step 3: Add minimal decorative markup**

```tsx
<div className={styles.itemHeaderRule} data-testid="recipe-divider-ornament">
  <span className={styles.dividerDiamond} aria-hidden="true" />
</div>
```

- [ ] **Step 4: Implement the multi-band ornament and stepped corner**

Build the inward dagger lines with layered gradients and the center diamond with nested borders/glow. Build the lower-right stepped triangle from multiple background gradients, keeping the field corner square.

- [ ] **Step 5: Run focused E2E**

Run: `cd moorestech_web/webui && pnpm exec playwright test --config e2e/playwright.config.ts e2e/tests/visualParity.spec.ts`

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add moorestech_web/webui/src/features/recipe moorestech_web/webui/e2e/tests/visualParity.spec.ts
git commit -m "feat(webui): restore uGUI recipe ornaments"
```

### Task 4: Selected Slot and Hotbar

**Files:**
- Modify: `moorestech_web/webui/src/shared/ui/ItemSlot/style.module.css`
- Modify: `moorestech_web/webui/src/features/inventory/HotbarPanel/style.module.css`

**Interfaces:**
- Consumes: `data-selected`, `.cell > .num + ItemSlot`
- Produces: 五層直角選択グロー、外側上方中央の番号ラベル

- [ ] **Step 1: Add selected-slot computed-style assertions**

Assert zero border radius, multiple box-shadow layers, and a label-to-slot vertical gap greater than zero.

- [ ] **Step 2: Run and confirm failure**

Run: `cd moorestech_web/webui && pnpm exec playwright test --config e2e/playwright.config.ts e2e/tests/visualParity.spec.ts`

Expected: FAIL on label position or selected-frame layer count.

- [ ] **Step 3: Implement the selected frame**

Use border, inset shadows, outer glow, and `::before`/`::after` L-shaped corner gradients. Preserve `data-selected`.

- [ ] **Step 4: Move number labels above their slots**

Set `.cell` top padding, center `.num` with `left: 50%` and `translateX(-50%)`, and leave an 8px visual gap above the slot.

- [ ] **Step 5: Run focused E2E**

Run: `cd moorestech_web/webui && pnpm exec playwright test --config e2e/playwright.config.ts e2e/tests/visualParity.spec.ts`

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add moorestech_web/webui/src/shared/ui/ItemSlot/style.module.css moorestech_web/webui/src/features/inventory/HotbarPanel/style.module.css moorestech_web/webui/e2e/tests/visualParity.spec.ts
git commit -m "feat(webui): match uGUI slot and hotbar framing"
```

### Task 5: Visual Scoring Loop

**Files:**
- Modify as required by measured failures: the CSS/TSX files from Tasks 2–4
- Create: `docs/evidence/2026-07-17-webui-visual-parity/scorecard.md`
- Create: `docs/evidence/2026-07-17-webui-visual-parity/final.png`
- Create: `docs/evidence/2026-07-17-webui-visual-parity/crops/*.png`

**Interfaces:**
- Consumes: the nine acceptance axes and Playwright capture harness
- Produces: requirement-by-requirement evidence proving all MUST conditions and score ≥90/100

- [ ] **Step 1: Build and run automated tests**

Run:

```bash
cd moorestech_web/webui
pnpm build
pnpm test
pnpm exec playwright test --config e2e/playwright.config.ts e2e/tests/visualParity.spec.ts
```

Expected: all commands PASS.

- [ ] **Step 2: Capture the full render and crops**

Run: `cd moorestech_web/webui && CAPTURE_OUT=../../../docs/evidence/2026-07-17-webui-visual-parity/final.png CAPTURE_CROP_DIR=../../../docs/evidence/2026-07-17-webui-visual-parity/crops pnpm tsx e2e/capture-eval.ts`

Expected: full screenshot and all seven diagnostic crops exist.

- [ ] **Step 3: Score every MUST**

Record A–I with measured bbox, line count, corner shape, opacity/continuity, and pass/fail evidence in `scorecard.md`. Do not award points to a failed MUST.

- [ ] **Step 4: Fix every failed axis and repeat Steps 1–3**

Continue until A–I are all PASS and the total is at least 90/100. A first-pass score with no failures triggers an equal-scale crop reinspection.

- [ ] **Step 5: Run final repository QA**

Run:

```bash
git diff --check
git status --short
cd moorestech_web/webui
pnpm build
pnpm test
pnpm test:e2e
```

Expected: no whitespace errors; build, unit tests, and complete E2E suite PASS.

- [ ] **Step 6: Commit final evidence and adjustments**

```bash
git add moorestech_web/webui docs/evidence/2026-07-17-webui-visual-parity
git commit -m "feat(webui): achieve uGUI visual parity"
```
