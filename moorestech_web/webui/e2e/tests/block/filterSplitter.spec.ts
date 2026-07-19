import { test, expect } from "@playwright/test";
import { payloadsOf } from "../../support/actions";
import { setBlock } from "../../support/mockControl";

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
