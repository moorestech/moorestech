import { test, expect } from "@playwright/test";
import { payloadsOf } from "../../support/actions";
import { setBlock } from "../../support/mockControl";
import { expectAbove, expectReachableInScrollViewport } from "../../support/layoutAssertions";

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

// 高さ600では本文スクロールで末尾へ届く
// At height 600 the body scrolls to reach the last slot
test("last direction's filter slot is reachable by scrolling on a short window", async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 600 });
  await setBlock(page, "filterSplitter");
  await page.goto("/");
  await expectAbove(page.getByTestId("block-inventory"), page.getByTestId("hotbar-grid"));
  const lastSlot = page.getByTestId("filter-slot-2-1");
  await expectReachableInScrollViewport(lastSlot);
  await lastSlot.click({ button: "right" });
  await expect
    .poll(async () => {
      // 記録は共有のため末尾を見る
      // The log is shared, so read the newest entry
      const payloads = await payloadsOf(page, "filter_splitter.set_filter_item");
      return payloads.at(-1);
    })
    .toEqual({ directionIndex: 2, slotIndex: 1, clear: true });
});
