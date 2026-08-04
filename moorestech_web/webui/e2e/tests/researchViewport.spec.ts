import { test, expect } from "@playwright/test";
import { resetResearch, setUiState } from "../support/mockControl";
import { settleBoundingBox } from "../support/panSettle";

// researchable な中央寄せ対象ノード（fixtureの3ノード目）
// The researchable centering target (3rd node in the fixture)
const RESEARCHABLE_NODE = "research-node-33333333-3333-4333-8333-333333333333";

test.afterEach(async ({ page }) => {
  await resetResearch(page);
  await setUiState(page, "PlayerInventory");
});

test("research tree opens centered on the researchable node", async ({ page }) => {
  await setUiState(page, "ResearchTree");
  await page.goto("/");
  const viewportBox = await page.getByTestId("research-viewport").boundingBox();
  const nodeBox = await page.getByTestId(RESEARCHABLE_NODE).boundingBox();
  expect(viewportBox).not.toBeNull();
  expect(nodeBox).not.toBeNull();
  // カードはノード座標中心アンカーなので、bbox中心がビューポート中心に一致する
  // The card is anchored at its node point, so its bbox center matches the viewport center
  expect(Math.abs(nodeBox!.x + nodeBox!.width / 2 - (viewportBox!.x + viewportBox!.width / 2))).toBeLessThanOrEqual(1.5);
  expect(Math.abs(nodeBox!.y + nodeBox!.height / 2 - (viewportBox!.y + viewportBox!.height / 2))).toBeLessThanOrEqual(1.5);
});

test("research tree keeps its pan position across close and reopen", async ({ page }) => {
  await setUiState(page, "ResearchTree");
  await page.goto("/");
  const node = page.getByTestId(RESEARCHABLE_NODE);
  await expect(node).toBeVisible();
  const viewportBox = await page.getByTestId("research-viewport").boundingBox();
  const dragStart = { x: viewportBox!.x + viewportBox!.width - 40, y: viewportBox!.y + viewportBox!.height - 40 };
  await page.mouse.move(dragStart.x, dragStart.y);
  await page.mouse.down();
  await page.mouse.move(dragStart.x - 60, dragStart.y + 30, { steps: 5 });
  await page.mouse.up();
  const settled = await settleBoundingBox(page, node);

  // 画面を閉じて開き直しても、パン位置が復元される（再センタリングしない）
  // Close and reopen the screen: the pan position is restored, not re-centered
  await setUiState(page, "PlayerInventory");
  await expect(page.getByTestId("research-tree")).toHaveCount(0);
  await setUiState(page, "ResearchTree");
  await expect(node).toBeVisible();
  const reopened = await node.boundingBox();
  expect(Math.abs(reopened!.x - settled.x)).toBeLessThanOrEqual(1);
  expect(Math.abs(reopened!.y - settled.y)).toBeLessThanOrEqual(1);
});

test("research tree zooms with the wheel and pans by dragging its empty background", async ({ page }) => {
  await page.setViewportSize({ width: 960, height: 540 });
  await setUiState(page, "ResearchTree");
  await page.goto("/");
  const viewport = page.getByTestId("research-viewport");
  const node = page.getByTestId(RESEARCHABLE_NODE);
  const viewportBox = await viewport.boundingBox();
  const beforeZoom = await node.boundingBox();
  expect(viewportBox).not.toBeNull();
  expect(beforeZoom).not.toBeNull();

  const zoomCursor = {
    x: beforeZoom!.x + beforeZoom!.width / 2,
    y: beforeZoom!.y + beforeZoom!.height / 2,
  };
  await page.mouse.move(zoomCursor.x, zoomCursor.y);
  await page.mouse.wheel(0, -240);
  await expect.poll(async () => (await node.boundingBox())!.width).toBeGreaterThan(beforeZoom!.width);
  await expect.poll(async () => {
    const box = await node.boundingBox();
    return box!.x + box!.width / 2;
  }).toBeCloseTo(zoomCursor.x, 0);
  await expect.poll(async () => {
    const box = await node.boundingBox();
    return box!.y + box!.height / 2;
  }).toBeCloseTo(zoomCursor.y, 0);
  const afterZoomWidth = (await node.boundingBox())!.width;
  await page.mouse.wheel(0, 240);
  await expect.poll(async () => (await node.boundingBox())!.width).toBeLessThan(afterZoomWidth);

  // ドラッグ距離ぶん以上動く（速い操作は慣性で滑走してから静止する）
  // Moves at least the drag distance (fast drags glide with inertia before settling)
  const dragStart = {
    x: viewportBox!.x + viewportBox!.width - 40,
    y: viewportBox!.y + viewportBox!.height - 40,
  };
  const beforePan = await settleBoundingBox(page, node);
  await page.mouse.move(dragStart.x, dragStart.y);
  await page.mouse.down();
  await page.mouse.move(dragStart.x - 80, dragStart.y - 50, { steps: 5 });
  await page.mouse.up();
  const afterPan = await settleBoundingBox(page, node);
  expect(afterPan.x - beforePan.x).toBeLessThanOrEqual(-79.5);
  expect(afterPan.y - beforePan.y).toBeLessThanOrEqual(-49.5);
  // 慣性の滑走距離は速度上限×時定数で有限に収まる
  // The glide distance is bounded by the speed cap times the time constant
  expect(afterPan.x - beforePan.x).toBeGreaterThan(-80 - 1100);
  expect(afterPan.y - beforePan.y).toBeGreaterThan(-50 - 1100);

  const beforeRightDrag = await node.boundingBox();
  await page.mouse.move(dragStart.x, dragStart.y);
  await page.mouse.down({ button: "right" });
  await page.mouse.move(dragStart.x - 80, dragStart.y - 50, { steps: 5 });
  await page.mouse.up({ button: "right" });
  await page.evaluate(() => new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve()))));
  const afterRightDrag = await node.boundingBox();
  expect(afterRightDrag!.x).toBe(beforeRightDrag!.x);
  expect(afterRightDrag!.y).toBe(beforeRightDrag!.y);

  const beforeNodeDrag = await node.boundingBox();
  const nodeDragStart = { x: beforeNodeDrag!.x + 16, y: beforeNodeDrag!.y + 16 };
  await page.mouse.move(nodeDragStart.x, nodeDragStart.y);
  await page.mouse.down();
  await page.mouse.move(nodeDragStart.x - 80, nodeDragStart.y - 50, { steps: 5 });
  await page.mouse.up();
  await page.evaluate(() => new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve()))));
  const afterNodeDrag = await node.boundingBox();
  expect(afterNodeDrag!.x).toBe(beforeNodeDrag!.x);
  expect(afterNodeDrag!.y).toBe(beforeNodeDrag!.y);
});
