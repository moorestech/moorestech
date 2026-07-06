import { test, expect } from "@playwright/test";

type ActionRecord = { type: string; payload: unknown };

test.afterEach(async ({ page }) => {
  await page.request.get("/__block?type=closed");
});

// fixture の direction0 は whitelist → モードボタンは次モード blacklist を明示送信する
// Direction0 in the fixture is whitelist, so the mode button sends the explicit next mode blacklist
test("mode button sends explicit next mode", async ({ page }) => {
  await page.request.get("/__block?type=filterSplitter");
  await page.goto("/");
  await page.getByTestId("filter-mode-0").click();
  await expect
    .poll(async () => {
      const actions: ActionRecord[] = await page.request.get("/__actions").then((r) => r.json());
      return actions.find((a) => a.type === "filter_splitter.set_mode")?.payload;
    })
    .toEqual({ directionIndex: 0, mode: "blacklist" });
});

// フィルタスロットの右クリックは clear:true を送る（grab の有無に依らない）
// Right-clicking a filter slot sends clear:true regardless of the grabbed item
test("right click on a filter slot sends clear", async ({ page }) => {
  await page.request.get("/__block?type=filterSplitter");
  await page.goto("/");
  await page.getByTestId("filter-slot-0-0").click({ button: "right" });
  await expect
    .poll(async () => {
      const actions: ActionRecord[] = await page.request.get("/__actions").then((r) => r.json());
      return actions.find((a) => a.type === "filter_splitter.set_filter_item")?.payload;
    })
    .toEqual({ directionIndex: 0, slotIndex: 0, clear: true });
});
