import { test, expect, type Page } from "@playwright/test";
import { setBlock } from "../../../support/mockControl";
import {
  expectHitTestWithin,
  expectReachableInScrollViewport,
  scrollAreaAncestorViewportOf,
  scrollAreaRootOf,
  scrollAreaViewport,
} from "../../../support/layoutAssertions";

// 部屋の清浄機台数に上限は無い。最大体積のクラスAで14台以上が要るため、16・20台を合法な多台数として測る
// Purifiers per room are uncapped; a max-volume class-A room needs 14+, so 16 and 20 are the legal many-unit shapes
const WINDOW_HEIGHTS = [600, 720, 1080];
const MANY_FILTER_ROOMS = [{ filters: 16, fixture: "cleanRoomMachine16Filters" }, { filters: 20, fixture: "cleanRoomMachine20Filters" }];
const BOUNDARY_ROOMS = [{ filters: 0, fixture: "cleanRoomMachineNoFilters" }, { filters: 2, fixture: "cleanRoomMachine" }, { filters: 8, fixture: "cleanRoomMachineManyFilters" }];
// 本文の視口が操作できる高さを保つ下限。潰れた退行（0px）を落とす
// Floor for a usable body viewport; catches the collapsed (0px) regression
const MIN_BODY_VIEWPORT_HEIGHT = 120;

test.afterEach(async ({ page }) => {
  await setBlock(page, "closed");
});

async function openBlock(page: Page, fixture: string, windowHeight: number) {
  await page.setViewportSize({ width: 1280, height: windowHeight });
  await setBlock(page, fixture);
  await page.goto("/");
  await expect(page.getByTestId("block-inventory")).toHaveAttribute("data-large", "true");
}

// フッタはスクロール器の外でパネル内に収まり、中心をクリックできる
// The footer sits outside the scroller, inside the panel, and is hit at its center
async function expectFooterReadable(page: Page) {
  const panel = (await page.getByTestId("block-inventory").boundingBox())!;
  for (const testId of ["machine-state-label", "machine-power-rate"]) {
    const footer = page.getByTestId(testId);
    await expect(scrollAreaAncestorViewportOf(footer)).toHaveCount(0);
    const box = (await footer.boundingBox())!;
    expect(box.y).toBeGreaterThanOrEqual(panel.y);
    expect(box.y + box.height).toBeLessThanOrEqual(panel.y + panel.height);
    await expectHitTestWithin(footer);
  }
}

// 部屋欄と本文が同じ一つの視口に入り、視口は使える高さでフッタより上に終わる。入れ子の縦スクロールは無い
// Room section and body share one viewport of usable height ending above the footer, with no nested vertical scroller
async function expectSharedBodyViewport(page: Page, bodyTestId: string) {
  const viewport = scrollAreaViewport(scrollAreaRootOf(page, bodyTestId));
  const viewportBox = (await viewport.boundingBox())!;
  expect(viewportBox.height, "body viewport height").toBeGreaterThanOrEqual(MIN_BODY_VIEWPORT_HEIGHT);
  const footer = (await page.getByTestId("machine-state-label").boundingBox())!;
  expect(viewportBox.y + viewportBox.height).toBeLessThanOrEqual(footer.y);
  await expect(viewport.getByTestId("clean-room-section")).toHaveCount(1);
  await expect(scrollAreaAncestorViewportOf(viewport)).toHaveCount(0);
}

async function expectInventoryBodyReachable(page: Page) {
  await expectFooterReadable(page);
  for (const testId of ["machine-selected-recipe", "machine-input-slots", "machine-output-slots", "machine-fluid-slots"]) {
    await expectReachableInScrollViewport(page.getByTestId(testId));
  }
  await expectSharedBodyViewport(page, "machine-inventory-body");
}

for (const room of MANY_FILTER_ROOMS) {
  for (const windowHeight of WINDOW_HEIGHTS) {
    test(`清浄機${room.filters}台・高さ${windowHeight}でも部屋欄ごと本文がスクロールし、レシピ・スロット・液体に届きフッタが読める`, async ({ page }) => {
      await openBlock(page, room.fixture, windowHeight);
      await expect(page.getByTestId(`clean-room-air-filter-${room.filters - 1}`)).toBeAttached();
      await expectInventoryBodyReachable(page);
    });
  }
}

for (const windowHeight of WINDOW_HEIGHTS) {
  test(`清浄機20台・高さ${windowHeight}のレシピ選択でも部屋欄と同じ視口で最後のレシピ行へ届く`, async ({ page }) => {
    await openBlock(page, "cleanRoomMachine20FiltersSelecting", windowHeight);
    await expectFooterReadable(page);
    const rows = page.getByTestId("machine-recipe-selection").locator(":scope > *");
    await expectReachableInScrollViewport(rows.first());
    await expectReachableInScrollViewport(rows.last());
    await expectSharedBodyViewport(page, "machine-recipe-selection");
  });
}

for (const room of BOUNDARY_ROOMS) {
  test(`清浄機${room.filters}台・高さ720の境目でも本文とフッタが使える`, async ({ page }) => {
    await openBlock(page, room.fixture, 720);
    await expectInventoryBodyReachable(page);
  });
}

test("部屋の無い大型機械は部屋欄なしで本文に届き、フッタが読める", async ({ page }) => {
  await openBlock(page, "machine", 720);
  await expect(page.getByTestId("clean-room-section")).toHaveCount(0);
  for (const testId of ["machine-selected-recipe", "machine-input-slots", "machine-fluid-slots"]) {
    await expectReachableInScrollViewport(page.getByTestId(testId));
  }
  await expectFooterReadable(page);
});
