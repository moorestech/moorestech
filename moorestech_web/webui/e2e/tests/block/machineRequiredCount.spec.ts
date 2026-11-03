import { test, expect } from "@playwright/test";
import { setBlock, setTopicScenario } from "../../support/mockControl";

test.afterEach(async ({ page }) => {
  await setBlock(page, "closed");
  await setTopicScenario(page, "machineRecipesDefault");
});

// 実物が入った素材スロットも「入っている数/必要数」で必要数を出し続ける（2026-11-03裁定）
// A material slot holding a real item keeps showing its requirement as "held/required" (2026-11-03 ruling)
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

  // 数字はスロットの枠内（右下）に収め、隣のスロットや矢印へはみ出さない
  // The numbers stay inside the slot frame (bottom-right) and never bleed into neighbors or the arrow
  const frame = await input.locator('[data-filled="true"]').boundingBox();
  const text = await summary.boundingBox();
  expect(text!.x).toBeGreaterThanOrEqual(frame!.x);
  expect(text!.x + text!.width).toBeLessThanOrEqual(frame!.x + frame!.width);
});

test("出力スロットには必要数を出さない", async ({ page }) => {
  await setBlock(page, "machine");
  await page.goto("/");
  await expect(page.getByTestId("machine-output-slots").locator('[class*="_shortageCount_"]')).toHaveCount(0);
});
