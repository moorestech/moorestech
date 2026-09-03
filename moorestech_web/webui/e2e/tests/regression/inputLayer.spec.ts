import { test, expect } from "@playwright/test";
import { payloadsOf } from "../../support/actions";
import { setBlock, setUiState } from "../../support/mockControl";

test.afterEach(async ({ page }) => {
  await setUiState(page, "PlayerInventory");
  await setBlock(page, "closed");
});

test("block inventory上のEscapeはGameScreen遷移を要求する", async ({ page }) => {
  await setBlock(page, "chest");
  await setUiState(page, "SubInventory");
  await page.goto("/");
  await expect(page.getByTestId("block-inventory")).toBeVisible();
  const before = (await payloadsOf(page, "ui_state.request")).length;
  await page.keyboard.press("Escape");

  await expect.poll(async () => (await payloadsOf(page, "ui_state.request")).slice(before)).toContainEqual({ state: "GameScreen" });
  await expect(page.getByTestId("block-inventory")).toBeHidden();
});

test("Tabはブラウザのフォーカスを動かさない", async ({ page }) => {
  await setUiState(page, "PlayerInventory");
  await page.goto("/");
  await expect(page.getByTestId("app-stage")).toBeVisible();
  const activeTagName = () => page.evaluate(() => document.activeElement?.tagName ?? null);
  const before = await activeTagName();

  // 前進・後退どちらのフォーカス移動もWeb UIの選択表示と競合するため封じる
  // Both forward and backward traversal fight the web UI's selection rendering, so both are suppressed
  await page.keyboard.press("Tab");
  expect(await activeTagName()).toBe(before);
  await page.keyboard.press("Shift+Tab");
  expect(await activeTagName()).toBe(before);
});

test("GameScreenのホイールは最新equipment値から次スロットを選ぶ", async ({ page }) => {
  await setUiState(page, "GameScreen");
  await page.goto("/");
  const equipment = page.getByTestId("equipment-slots");
  await expect(equipment).toBeVisible();
  await page.mouse.wheel(0, 100);

  // fixture の selectedEquipment:0 から1段進むと次のスロットが選ばれる
  // Stepping once from the fixture's selectedEquipment:0 selects the next slot
  await expect.poll(() => payloadsOf(page, "inventory.select_equipment")).toContainEqual({ index: 1 });
  await expect(equipment.locator("> div").nth(1)).toHaveAttribute("data-selected", "true");
});
