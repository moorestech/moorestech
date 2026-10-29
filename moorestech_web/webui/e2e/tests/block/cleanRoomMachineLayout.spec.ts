import { test, expect, type Locator, type Page } from "@playwright/test";
import { setBlock } from "../../support/mockControl";
import { scrollAreaRootOf, scrollAreaViewport } from "../../support/layoutAssertions";

test.afterEach(async ({ page }) => {
  await setBlock(page, "closed");
});

async function boxOf(locator: Locator) {
  const box = await locator.boundingBox();
  expect(box).not.toBeNull();
  return box!;
}

type Rect = { x: number; y: number; width: number; height: number };

function intersectionOf(a: Rect, b: Rect): Rect {
  const x = Math.max(a.x, b.x);
  const y = Math.max(a.y, b.y);
  return { x, y, width: Math.min(a.x + a.width, b.x + b.width) - x, height: Math.min(a.y + a.height, b.y + b.height) - y };
}

// 本文側はスクロール視口で切った可視矩形で比べる
// Compare the body side by its rect clipped to the ancestor scroll viewport; before the fix there was no viewport and the raw rect was visible
async function visibleBoxOf(locator: Locator) {
  const box = await boxOf(locator);
  const viewport = locator.locator("xpath=ancestor::*[contains(@class, 'mantine-ScrollArea-viewport')][1]");
  if (await viewport.count() === 0) return box;
  return intersectionOf(box, await boxOf(viewport));
}

// 可視矩形が面積を持って交わるか（接触は除く）
// Whether two visible rects overlap with area; merely touching edges is not an intersection
async function expectNoVisibleIntersection(body: Locator, footer: Locator) {
  const overlap = intersectionOf(await visibleBoxOf(body), await boxOf(footer));
  expect(overlap.width > 0 && overlap.height > 0, `overlap ${overlap.width}x${overlap.height}`).toBe(false);
}

// フッタがパネル内にあり、本文の可視域がその上で終わるか
// The footer's state and rate rows stay inside the panel, and the body scroller's visible area ends above the footer
async function expectFooterClearOfBody(page: Page) {
  const panel = await boxOf(page.getByTestId("block-inventory"));
  const viewport = await boxOf(scrollAreaViewport(scrollAreaRootOf(page, "machine-inventory-body")));
  for (const testId of ["machine-state-label", "machine-power-rate"]) {
    const footer = await boxOf(page.getByTestId(testId));
    expect(footer.y + footer.height).toBeLessThanOrEqual(panel.y + panel.height);
    expect(viewport.y + viewport.height).toBeLessThanOrEqual(footer.y);
  }
}

test("清浄機2台の室内機械で液体行が電力行・状態行に重ならない", async ({ page }) => {
  await setBlock(page, "cleanRoomMachine");
  await page.goto("/");
  await expect(page.getByTestId("block-inventory")).toHaveAttribute("data-large", "true");
  await expect(page.getByTestId("clean-room-air-filter-1")).toBeVisible();

  const fluids = page.getByTestId("machine-fluid-slots");
  await expectNoVisibleIntersection(fluids, page.getByTestId("machine-power-rate"));
  await expectNoVisibleIntersection(fluids, page.getByTestId("machine-state-label"));
  await expectFooterClearOfBody(page);
});

test("低いウィンドウで清浄機が多い部屋でもフッタは見え、本文はスクロールで液体行へ届く", async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 600 });
  await setBlock(page, "cleanRoomMachineManyFilters");
  await page.goto("/");
  await expect(page.getByTestId("clean-room-air-filter-7")).toBeVisible();

  await expect(page.getByTestId("machine-state-label")).toBeVisible();
  await expect(page.getByTestId("machine-power-rate")).toBeVisible();
  await expectFooterClearOfBody(page);

  // 溢れた本文はスクロールで液体行へ届く
  // The overflowing body scrolls within its container and can reach the fluid row
  const viewport = scrollAreaViewport(scrollAreaRootOf(page, "machine-inventory-body"));
  expect(await viewport.evaluate((element) => element.scrollHeight - element.clientHeight)).toBeGreaterThan(0);
  const fluids = page.getByTestId("machine-fluid-slots");
  await fluids.scrollIntoViewIfNeeded();
  await expectNoVisibleIntersection(fluids, page.getByTestId("machine-power-rate"));
  const fluidBox = await boxOf(fluids);
  const viewportBox = await boxOf(viewport);
  expect(fluidBox.y + fluidBox.height).toBeLessThanOrEqual(viewportBox.y + viewportBox.height + 0.5);
});
