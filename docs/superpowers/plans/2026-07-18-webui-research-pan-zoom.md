# WebUI Research Pan and Zoom Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Web版研究ツリーを、空き背景の左ドラッグで移動し、マウスホイールでカーソル中心に拡大縮小できるようにする。

**Architecture:** `ResearchTreePanel` がローカルviewport状態を所有し、研究キャンバスへCSS transformを適用する。ズーム座標演算は `researchLogic.ts` の純関数へ分離し、VitestとPlaywrightの両方から挙動を固定する。

**Tech Stack:** React 18、TypeScript、CSS Modules、Vitest、Playwright

## Global Constraints

- 空き背景のprimary pointer左ドラッグだけがパンを開始し、研究ノード上では開始しない。
- wheel上方向で拡大、下方向で縮小し、カーソル直下のツリー座標を固定する。
- 拡大率は `0.4` 以上 `2.5` 以下、初期viewportは `{ x: 0, y: 0, scale: 1 }`。
- wheel倍率は `Math.exp(-deltaY * 0.0015)` を使う。
- native scrollbarは廃止し、viewport外をクリップする。
- pointer upとlost pointer captureの両方でパンを終了する。
- `--ui-scale` でstageが縮小されても、client座標を `offsetWidth / getBoundingClientRect().width` でCSS座標へ補正する。
- 新規依存、通信変更、C#変更、永続化、グローバルstore追加は禁止。
- 変更するコードファイルは1ファイル200行以下を維持する。
- TypeScript/TSXの主要処理コメントは日本語・英語の2行セットにする。

---

### Task 1: Viewportズーム純関数

**Files:**
- Modify: `moorestech_web/webui/src/features/research/researchLogic.ts`
- Modify: `moorestech_web/webui/src/features/research/researchLogic.test.ts`

**Interfaces:**
- Consumes: `ViewportTransform = { x: number; y: number; scale: number }`
- Produces: `zoomViewportAt(viewport, cursor, deltaY): ViewportTransform`

- [ ] **Step 1: 失敗する単体テストを書く**

`researchLogic.test.ts` に次を追加する。

```ts
describe("zoomViewportAt", () => {
  it("zooms in for wheel-up while keeping the world point under the cursor fixed", () => {
    const current = { x: 40, y: -20, scale: 1 };
    const cursor = { x: 300, y: 180 };
    const next = zoomViewportAt(current, cursor, -120);
    const worldBefore = {
      x: (cursor.x - current.x) / current.scale,
      y: (cursor.y - current.y) / current.scale,
    };
    expect(next.scale).toBeGreaterThan(current.scale);
    expect((cursor.x - next.x) / next.scale).toBeCloseTo(worldBefore.x);
    expect((cursor.y - next.y) / next.scale).toBeCloseTo(worldBefore.y);
  });

  it("clamps wheel zoom to the supported minimum and maximum", () => {
    expect(zoomViewportAt({ x: 0, y: 0, scale: 1 }, { x: 0, y: 0 }, -100000).scale).toBe(2.5);
    expect(zoomViewportAt({ x: 0, y: 0, scale: 1 }, { x: 0, y: 0 }, 100000).scale).toBe(0.4);
  });
});
```

- [ ] **Step 2: REDを確認する**

Run:

```bash
cd moorestech_web/webui
pnpm vitest run src/features/research/researchLogic.test.ts
```

Expected: `zoomViewportAt` が未定義または未exportのためFAIL。

- [ ] **Step 3: 最小実装を追加する**

`researchLogic.ts` に次の公開契約を追加する。

```ts
export const MIN_VIEW_SCALE = 0.4;
export const MAX_VIEW_SCALE = 2.5;
export const WHEEL_ZOOM_SENSITIVITY = 0.0015;

export type ViewportTransform = { x: number; y: number; scale: number };
export type Point = { x: number; y: number };

export function zoomViewportAt(
  viewport: ViewportTransform,
  cursor: Point,
  deltaY: number,
): ViewportTransform {
  const scale = Math.min(
    MAX_VIEW_SCALE,
    Math.max(MIN_VIEW_SCALE, viewport.scale * Math.exp(-deltaY * WHEEL_ZOOM_SENSITIVITY)),
  );
  const worldX = (cursor.x - viewport.x) / viewport.scale;
  const worldY = (cursor.y - viewport.y) / viewport.scale;
  return {
    x: cursor.x - worldX * scale,
    y: cursor.y - worldY * scale,
    scale,
  };
}
```

- [ ] **Step 4: GREENを確認する**

Run:

```bash
cd moorestech_web/webui
pnpm vitest run src/features/research/researchLogic.test.ts
```

Expected: 全テストPASS。

- [ ] **Step 5: コミットする**

```bash
git add moorestech_web/webui/src/features/research/researchLogic.ts moorestech_web/webui/src/features/research/researchLogic.test.ts
git commit -m "feat(webui): 研究ツリーのズーム座標計算を追加"
```

---

### Task 2: 背景パン・ホイールズームUIとE2E

**Files:**
- Modify: `moorestech_web/webui/src/features/research/ResearchTreePanel.tsx`
- Modify: `moorestech_web/webui/src/features/research/style.module.css`
- Modify: `moorestech_web/webui/src/features/research/ResearchNodeCard.tsx`
- Modify: `moorestech_web/webui/e2e/tests/research.spec.ts`

**Interfaces:**
- Consumes: `zoomViewportAt(viewport, cursor, deltaY): ViewportTransform`
- Produces: `data-testid="research-viewport"`、`data-testid="research-canvas"`、実ブラウザのpan/zoom操作

- [ ] **Step 1: 失敗するPlaywrightテストを書く**

`research.spec.ts` に、`ResearchTree`でページを開いてから次を確認するテストを追加する。

```ts
test("research tree zooms with the wheel and pans by dragging its empty background", async ({ page }) => {
  await page.setViewportSize({ width: 960, height: 540 });
  await setUiState(page, "ResearchTree");
  await page.goto("/");
  const viewport = page.getByTestId("research-viewport");
  const node = page.getByTestId("research-node-11111111-1111-1111-1111-111111111111");
  const viewportBox = await viewport.boundingBox();
  const beforeZoom = await node.boundingBox();
  expect(viewportBox).not.toBeNull();
  expect(beforeZoom).not.toBeNull();

  await page.mouse.move(viewportBox!.x + viewportBox!.width - 40, viewportBox!.y + viewportBox!.height - 40);
  await page.mouse.wheel(0, -240);
  const afterZoom = await node.boundingBox();
  expect(afterZoom!.width).toBeGreaterThan(beforeZoom!.width);

  const dragStart = {
    x: viewportBox!.x + viewportBox!.width - 40,
    y: viewportBox!.y + viewportBox!.height - 40,
  };
  const beforePan = await node.boundingBox();
  await page.mouse.move(dragStart.x, dragStart.y);
  await page.mouse.down();
  await page.mouse.move(dragStart.x - 80, dragStart.y - 50, { steps: 5 });
  await page.mouse.up();
  const afterPan = await node.boundingBox();
  expect(afterPan!.x - beforePan!.x).toBeCloseTo(-80, 0);
  expect(afterPan!.y - beforePan!.y).toBeCloseTo(-50, 0);
});
```

- [ ] **Step 2: REDを確認する**

Run:

```bash
cd moorestech_web/webui
pnpm playwright test --config e2e/playwright.config.ts e2e/tests/research.spec.ts
```

Expected: `research-viewport` が存在しないためFAIL。

- [ ] **Step 3: viewport入力処理とCSS transformを実装する**

- `ScrollArea` を通常の `div` viewportへ置換する。
- `useState` で `{ x: 0, y: 0, scale: 1 }` を保持する。
- wheel位置は `currentTarget.getBoundingClientRect()` と `currentTarget.offsetWidth` からviewport内CSS座標へ変換し、`zoomViewportAt`へ渡す。
- primary pointerかつ研究ノード外でだけpointer captureを取得する。`ResearchNodeCard` ルートへ `data-research-node` を付け、`target.closest("[data-research-node]")` で除外する。
- pointer moveのclient座標差分へ `currentTarget.offsetWidth / currentTarget.getBoundingClientRect().width` を掛け、viewportのCSS座標へ変換して `x/y` に加算する。
- pointer up / pointer cancel / lost pointer captureでパン状態を解除する。
- canvasへ次を適用する。

```tsx
style={{
  width: bounds.width,
  height: bounds.height,
  transform: `translate(${viewport.x}px, ${viewport.y}px) scale(${viewport.scale})`,
}}
```

- viewport CSSは `overflow: hidden; touch-action: none; cursor: grab;`、ドラッグ中は `cursor: grabbing;` とする。
- canvas CSSは `transform-origin: 0 0;` とする。

- [ ] **Step 4: PlaywrightをGREENにする**

Run:

```bash
cd moorestech_web/webui
pnpm playwright test --config e2e/playwright.config.ts e2e/tests/research.spec.ts
```

Expected: 既存研究テストを含め全PASS。

- [ ] **Step 5: WebUI全体の回帰確認を行う**

Run:

```bash
cd moorestech_web/webui
pnpm vitest run
pnpm build
```

Expected: 全テストPASS、TypeScriptとVite build成功。

- [ ] **Step 6: コミットする**

```bash
git add moorestech_web/webui/src/features/research/ResearchTreePanel.tsx moorestech_web/webui/src/features/research/ResearchNodeCard.tsx moorestech_web/webui/src/features/research/style.module.css moorestech_web/webui/e2e/tests/research.spec.ts
git commit -m "feat(webui): 研究ツリーをパンとホイールズーム対応"
```
