import { test, expect } from "@playwright/test";
import { payloadsOf } from "../support/actions";
import { resetResearch, setUiState } from "../support/mockControl";

// 各テスト後に研究ツリーと ui_state を既定へ戻し、状態漏れを防ぐ
// Reset the research tree and ui_state to defaults after each test to prevent state leakage
test.afterEach(async ({ page }) => {
  await resetResearch(page);
  await setUiState(page, "PlayerInventory");
});

test("research tree renders nodes when uiState enters ResearchTree", async ({ page }) => {
  await setUiState(page, "ResearchTree");
  await page.goto("/");
  await expect(page.getByTestId("research-tree")).toBeVisible();
  await expect(page.getByTestId("research-node-11111111-1111-1111-1111-111111111111")).toBeVisible();
});

test("research button sends research.complete and node becomes completed", async ({ page }) => {
  await resetResearch(page);
  await setUiState(page, "ResearchTree");
  await page.goto("/");
  const researchableGuid = "33333333-3333-3333-3333-333333333333";
  await page.getByTestId(`research-button-${researchableGuid}`).click();
  await expect
    .poll(async () => {
      const payloads = await payloadsOf(page, "research.complete");
      return payloads[0];
    })
    .toEqual({ researchGuid: researchableGuid });
  // mock が completed へ書換えて push → ボタンが研究済みに変わる
  // The mock rewrites the node to completed and pushes; the button flips to the completed label
  await expect(page.getByTestId(`research-button-${researchableGuid}`)).toContainText("研究済み");
});

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
  await expect.poll(async () => (await node.boundingBox())!.width).toBeGreaterThan(beforeZoom!.width);
  const afterZoomWidth = (await node.boundingBox())!.width;
  await page.mouse.wheel(0, 240);
  await expect.poll(async () => (await node.boundingBox())!.width).toBeLessThan(afterZoomWidth);

  const dragStart = {
    x: viewportBox!.x + viewportBox!.width - 40,
    y: viewportBox!.y + viewportBox!.height - 40,
  };
  const beforePan = await node.boundingBox();
  await page.mouse.move(dragStart.x, dragStart.y);
  await page.mouse.down();
  await page.mouse.move(dragStart.x - 80, dragStart.y - 50, { steps: 5 });
  await page.mouse.up();
  await expect.poll(async () => (await node.boundingBox())!.x - beforePan!.x).toBeCloseTo(-80, 0);
  await expect.poll(async () => (await node.boundingBox())!.y - beforePan!.y).toBeCloseTo(-50, 0);
});
