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

test("GameScreenのホイールは最新equipment値から次スロットを選ぶ", async ({ page }) => {
  await setUiState(page, "GameScreen");
  await page.goto("/");
  const equipment = page.getByTestId("equipment-slots");
  await expect(equipment).toBeVisible();
  await page.mouse.wheel(0, 100);

  // fixture の selectedEquipment:-1（素手）から1段進むと先頭スロットが選ばれる
  // Stepping once from the fixture's selectedEquipment:-1 (bare hands) selects the first slot
  await expect.poll(() => payloadsOf(page, "inventory.select_equipment")).toContainEqual({ index: 0 });
  await expect(equipment.locator("> div").nth(0)).toHaveAttribute("data-selected", "true");
});
