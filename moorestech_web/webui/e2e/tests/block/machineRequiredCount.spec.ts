import { test, expect } from "@playwright/test";
import { payloadsOf } from "../../support/actions";
import { setBlock, setTopicScenario } from "../../support/mockControl";

test.afterEach(async ({ page }) => {
  await setBlock(page, "closed");
  await setTopicScenario(page, "machineRecipesDefault");
});

// 実物入りでも必要数を出し続ける
// A filled slot keeps showing its requirement
test("必要数を満たした素材スロットは入っている数/必要数を黒字で出し、個数バッジを重ねない", async ({ page }) => {
  await setBlock(page, "machine");
  await page.goto("/");
  const input = page.getByTestId("machine-input-slots");
  const summary = input.locator('[class*="_shortageCount_"]');
  await expect(summary).toHaveText("5/3");
  await expect(summary).not.toHaveAttribute("data-lack", "true");
  await expect(input.locator('[class*="_count_"]')).toHaveCount(0);
});

test("不足している素材スロットは数字だけ赤くし、入れたアイテムは減光しない", async ({ page }) => {
  await setBlock(page, "machineShortInput");
  await page.goto("/");
  const input = page.getByTestId("machine-input-slots");
  const summary = input.locator('[class*="_shortageCount_"]');
  await expect(summary).toHaveText("1/3");
  await expect(summary).toHaveAttribute("data-lack", "true");
  await expect(input.locator('[data-insufficient="true"]')).toHaveCount(0);
  await expect(input.locator('[data-filled="true"]')).toHaveCSS("opacity", "1");

  // 数字は枠内に収まる
  // The numbers stay inside the slot frame
  const frame = await input.locator('[data-filled="true"]').boundingBox();
  const text = await summary.boundingBox();
  expect(text!.x).toBeGreaterThanOrEqual(frame!.x);
  expect(text!.x + text!.width).toBeLessThanOrEqual(frame!.x + frame!.width);
});

test("出力スロットには実物が入っていても必要数を出さない", async ({ page }) => {
  await setBlock(page, "machineFilledOutput");
  await page.goto("/");
  const output = page.getByTestId("machine-output-slots");
  await expect(output.locator('[class*="_count_"]')).toHaveText("5");
  await expect(output.locator('[class*="_shortageCount_"]')).toHaveCount(0);
});

// 数字は右クリックを遮らない
// The numbers never intercept right-clicks
test("必要数の数字の上で右クリックしても split を送る", async ({ page }) => {
  await setBlock(page, "machine");
  await page.goto("/");
  const box = await page.getByTestId("machine-input-slots").locator('[class*="_shortageCount_"]').boundingBox();
  await page.mouse.click(box!.x + box!.width / 2, box!.y + box!.height / 2, { button: "right" });
  await expect
    .poll(() => payloadsOf(page, "block_inventory.split"))
    .toContainEqual({ from: { area: "block", slot: 0 } });
});
