import { test, expect, type Page } from "@playwright/test";
import { setBlock } from "../../../support/mockControl";
import { expectReachableInScrollViewport, scrollAreaAncestorViewportOf } from "../../../support/layoutAssertions";

// 清浄機自身の小型面は部屋の全台を載せる。台数に上限が無いので境目と多台数を持つ
// The purifier's own small panel lists every unit in the room; counts are uncapped, so keep both the boundary and many-unit shapes
const WINDOW_HEIGHTS = [600, 720, 1080];
const AIR_FILTER_ROOMS = [
  { filters: 2, fixture: "cleanRoomAirFilter2Filters" },
  { filters: 8, fixture: "cleanRoomAirFilter8Filters" },
  { filters: 14, fixture: "cleanRoomAirFilter14Filters" },
  { filters: 20, fixture: "cleanRoomAirFilter20Filters" },
  { filters: 30, fixture: "cleanRoomAirFilter30Filters" },
  { filters: 30, fixture: "cleanRoomAirFilter30FiltersUnconnected" },
];

test.afterEach(async ({ page }) => {
  await setBlock(page, "closed");
});

async function openBlock(page: Page, fixture: string, windowHeight: number) {
  await page.setViewportSize({ width: 1280, height: windowHeight });
  await setBlock(page, fixture);
  await page.goto("/");
  await expect(page.getByTestId("block-inventory")).toBeVisible();
  await expect(page.getByTestId("block-inventory")).not.toHaveAttribute("data-large", "true");
}

// 枠の下端はホットバーより上で止まる
// The panel frame stops above the hotbar however many rows the room adds
async function expectPanelAboveHotbar(page: Page) {
  const panel = (await page.getByTestId("block-inventory").boundingBox())!;
  const hotbar = (await page.getByTestId("hotbar-grid").boundingBox())!;
  expect(panel.y + panel.height, "panel bottom above hotbar").toBeLessThanOrEqual(hotbar.y);
}

for (const room of AIR_FILTER_ROOMS) {
  for (const windowHeight of WINDOW_HEIGHTS) {
    test(`清浄機パネル(${room.fixture})・高さ${windowHeight}で部屋欄の末尾とフィルタ枠へ届き、枠がホットバーへ潜らない`, async ({ page }) => {
      await openBlock(page, room.fixture, windowHeight);
      await expectPanelAboveHotbar(page);
      await expectReachableInScrollViewport(page.getByTestId(`clean-room-air-filter-${room.filters - 1}`));
      await expectReachableInScrollViewport(page.getByTestId("clean-room-filter-grid"));
      await expectPanelAboveHotbar(page);
      // 部屋欄とフィルタ枠は入れ子なしの同じ視口
      // Room rows and the filter grid share one viewport with no nested vertical scroller
      const viewport = scrollAreaAncestorViewportOf(page.getByTestId("clean-room-filter-grid"));
      await expect(viewport.getByTestId("clean-room-section")).toHaveCount(1);
      await expect(scrollAreaAncestorViewportOf(viewport)).toHaveCount(0);
    });
  }
}

// 少数台は自然高のまま溢れず、スクロールが立たない
// A few units keep the natural height: nothing overflows, so no scroll starts
test("清浄機2台・高さ720は自然高のままスクロールしない", async ({ page }) => {
  await openBlock(page, "cleanRoomAirFilter2Filters", 720);
  const viewport = scrollAreaAncestorViewportOf(page.getByTestId("clean-room-filter-grid"));
  const { scrollHeight, clientHeight } = await viewport.evaluate((el) => ({ scrollHeight: el.scrollHeight, clientHeight: el.clientHeight }));
  expect(scrollHeight).toBeLessThanOrEqual(clientHeight);
  await expectPanelAboveHotbar(page);
});
