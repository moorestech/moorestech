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

test("GameScreenのホイールは最新hotbar値から次スロットを選ぶ", async ({ page }) => {
  await setUiState(page, "GameScreen");
  await page.goto("/");
  const hotbar = page.getByTestId("hotbar-grid");
  await expect(hotbar).toBeVisible();
  await hotbar.hover();
  await page.mouse.wheel(0, 100);

  await expect.poll(() => payloadsOf(page, "inventory.select_hotbar")).toContainEqual({ index: 1 });
  await expect(hotbar.locator("> div > div").nth(1)).toHaveAttribute("data-selected", "true");
});
