import { test, expect } from "@playwright/test";
import { payloadsOf } from "../../support/actions";
import { setBlock } from "../../support/mockControl";
import { expectReachableInScrollViewport } from "../../support/layoutAssertions";

test.afterEach(async ({ page }) => {
  await setBlock(page, "closed");
});

// fixture の direction0 は whitelist。択一スイッチで blacklist を選び目標モードを明示送信する
// Direction0 in the fixture is whitelist; selecting blacklist sends that explicit target mode
test("mode switch sends explicitly selected mode", async ({ page }) => {
  await setBlock(page, "filterSplitter");
  await page.goto("/");
  await page.getByTestId("filter-mode-0").getByRole("button", { name: "ブラックリスト" }).click();
  await expect
    .poll(async () => {
      const payloads = await payloadsOf(page, "filter_splitter.set_mode");
      return payloads[0];
    })
    .toEqual({ directionIndex: 0, mode: "blacklist" });
});

// フィルタスロットの右クリックは clear:true を送る（grab の有無に依らない）
// Right-clicking a filter slot sends clear:true regardless of the grabbed item
test("right click on a filter slot sends clear", async ({ page }) => {
  await setBlock(page, "filterSplitter");
  await page.goto("/");
  await page.getByTestId("filter-slot-0-0").click({ button: "right" });
  await expect
    .poll(async () => {
      const payloads = await payloadsOf(page, "filter_splitter.set_filter_item");
      return payloads[0];
    })
    .toEqual({ directionIndex: 0, slotIndex: 0, clear: true });
});

// 3方向ぶんは高さ600でバンド高を越えるため、本文がスクロールして最後の方向のスロットまで操作できる
// Three directions exceed the band height at 600, so the body scrolls and the last direction's slot stays operable
test("last direction's filter slot is reachable by scrolling on a short window", async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 600 });
  await setBlock(page, "filterSplitter");
  await page.goto("/");
  const panel = (await page.getByTestId("block-inventory").boundingBox())!;
  const hotbar = (await page.getByTestId("hotbar-grid").boundingBox())!;
  expect(panel.y + panel.height).toBeLessThanOrEqual(hotbar.y);
  const lastSlot = page.getByTestId("filter-slot-2-1");
  await expectReachableInScrollViewport(lastSlot);
  await lastSlot.click({ button: "right" });
  await expect
    .poll(async () => {
      const payloads = await payloadsOf(page, "filter_splitter.set_filter_item");
      return payloads[0];
    })
    .toEqual({ directionIndex: 2, slotIndex: 1, clear: true });
});
