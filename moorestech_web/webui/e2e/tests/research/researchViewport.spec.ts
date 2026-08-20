import { test, expect, type Page } from "@playwright/test";
import { resetResearch, setUiState } from "../../support/mockControl";
import { settleBoundingBox, waitForFrame } from "../../support/panSettle";
import { researchableNodeGuid } from "../../mock-host/researchFixtures";
import { PAN_FRICTION_TAU_MS, PAN_MAX_FLING_SPEED } from "../../../src/shared/treeView/viewport/viewport";

// 中央寄せ対象ノード(SSOT参照)
// The centering target node (fixture SSOT)
const RESEARCHABLE_NODE = `research-node-${researchableNodeGuid}`;

// 滑走距離上限=速度上限×時定数
// Max glide distance = speed cap × time constant
const MAX_GLIDE_PX = PAN_MAX_FLING_SPEED * PAN_FRICTION_TAU_MS;

// 各テスト後に研究ツリーと ui_state を既定へ戻し、状態漏れを防ぐ
// Reset the research tree and ui_state to defaults after each test to prevent state leakage
test.afterEach(async ({ page }) => {
  await resetResearch(page);
  await setUiState(page, "PlayerInventory");
});


// 掴み点は「viewport自身が最前面で、ノードもボタンも装備HUDも被っていない」点でなければ pan が起きない
// The grip only pans where the viewport itself is frontmost, with no node, button, or equipment HUD on top
async function findEmptyBackgroundPoint(page: Page, box: { x: number; y: number; width: number; height: number }) {
  const point = await page.evaluate(([left, top, width, height]) => {
    for (let inset = 40; inset < Math.min(width, height) / 2; inset += 8) {
      const x = left + width - inset;
      const y = top + height - inset;
      const element = document.elementFromPoint(x, y);
      if (!element || !element.closest('[data-testid="research-viewport"]')) continue;
      if (element.closest('[data-testid^="research-node-"], button')) continue;
      return { x, y };
    }
    return null;
  }, [box.x, box.y, box.width, box.height]);
  expect(point, "研究ツリーに空背景の掴み点が無い / no empty background grip point in the research tree").not.toBeNull();
  return point!;
}

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
  const dragStart = await findEmptyBackgroundPoint(page, viewportBox!);
  const beforePan = await settleBoundingBox(page, node);
  await page.mouse.move(dragStart.x, dragStart.y);
  await page.mouse.down();
  await page.mouse.move(dragStart.x - 60, dragStart.y + 30, { steps: 5 });
  await page.mouse.up();
  const settled = await settleBoundingBox(page, node);
  // ドラッグが実際にパンを起こしたことを検証（起点が他要素に食われて無反応になる回帰の再発防止）
  // Verify the drag actually panned (regression guard against a drag origin swallowed by another element, producing zero movement)
  expect(settled.x - beforePan.x).toBeLessThanOrEqual(-59.5);
  expect(settled.x - beforePan.x).toBeGreaterThan(-60 - MAX_GLIDE_PX - 1);
  expect(settled.y - beforePan.y).toBeGreaterThanOrEqual(29.5);
  expect(settled.y - beforePan.y).toBeLessThan(30 + MAX_GLIDE_PX + 1);

  // 閉じ直してもパン位置は復元
  // Reopening restores the pan position, not re-centered
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

  // ドラッグ距離以上、慣性で滑走後停止
  // Moves at least the drag distance, glides, then stops
  // 角からの固定オフセットはノードやパネル寸法が動くとノードの上に落ちる。空背景を実測で選ぶ
  // A fixed corner offset lands on a node whenever node or panel geometry moves, so probe for real empty background
  const dragStart = await findEmptyBackgroundPoint(page, viewportBox!);
  const beforePan = await settleBoundingBox(page, node);
  await page.mouse.move(dragStart.x, dragStart.y);
  await page.mouse.down();
  await page.mouse.move(dragStart.x - 80, dragStart.y - 50, { steps: 5 });
  await page.mouse.up();
  const afterPan = await settleBoundingBox(page, node);
  expect(afterPan.x - beforePan.x).toBeLessThanOrEqual(-79.5);
  expect(afterPan.y - beforePan.y).toBeLessThanOrEqual(-49.5);
  // 滑走距離は速度上限×時定数で有限
  // Glide distance is bounded by cap × time constant
  expect(afterPan.x - beforePan.x).toBeGreaterThan(-80 - MAX_GLIDE_PX - 1);
  expect(afterPan.y - beforePan.y).toBeGreaterThan(-50 - MAX_GLIDE_PX - 1);

  const beforeRightDrag = await node.boundingBox();
  await page.mouse.move(dragStart.x, dragStart.y);
  await page.mouse.down({ button: "right" });
  await page.mouse.move(dragStart.x - 80, dragStart.y - 50, { steps: 5 });
  await page.mouse.up({ button: "right" });
  await waitForFrame(page);
  const afterRightDrag = await node.boundingBox();
  expect(afterRightDrag!.x).toBe(beforeRightDrag!.x);
  expect(afterRightDrag!.y).toBe(beforeRightDrag!.y);

  const beforeNodeDrag = await node.boundingBox();
  const nodeDragStart = { x: beforeNodeDrag!.x + 16, y: beforeNodeDrag!.y + 16 };
  await page.mouse.move(nodeDragStart.x, nodeDragStart.y);
  await page.mouse.down();
  await page.mouse.move(nodeDragStart.x - 80, nodeDragStart.y - 50, { steps: 5 });
  await page.mouse.up();
  await waitForFrame(page);
  const afterNodeDrag = await node.boundingBox();
  expect(afterNodeDrag!.x).toBe(beforeNodeDrag!.x);
  expect(afterNodeDrag!.y).toBe(beforeNodeDrag!.y);
});
