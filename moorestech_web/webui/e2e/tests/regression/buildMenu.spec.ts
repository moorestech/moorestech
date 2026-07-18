import { test, expect } from "@playwright/test";
import { payloadsOf } from "../../support/actions";
import { setUiState } from "../../support/mockControl";

test.afterEach(async ({ page }) => {
  await setUiState(page, "PlayerInventory");
});

test("ui_stateでビルドメニューを開閉し全エントリを表示する", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.goto("/");

  await expect(page.getByTestId("build-menu-panel")).toBeVisible();
  await expect(page.getByTestId("build-menu-grid").locator("> div")).toHaveCount(3);
  await expect(page.getByTestId("build-menu-entry-block-wood-chest")).toBeVisible();
  await expect(page.getByTestId("build-menu-entry-trainCar-cargo-car")).toBeVisible();
  await expect(page.getByTestId("build-menu-entry-blueprint-starter-base")).toBeVisible();

  await setUiState(page, "GameScreen");
  await expect(page.getByTestId("build-menu-panel")).toBeHidden();
});

test("左クリックはselect、blueprint右クリックはdeleteを送る", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.goto("/");
  await page.getByTestId("build-menu-entry-block-wood-chest").click();
  await page.getByTestId("build-menu-entry-blueprint-starter-base").click({ button: "right" });

  await expect.poll(() => payloadsOf(page, "build_menu.select")).toContainEqual({ entryType: "block", entryKey: "wood-chest" });
  await expect.poll(() => payloadsOf(page, "blueprint.delete")).toContainEqual({ name: "starter-base" });
});

test("閉じるボタンはGameScreen遷移を要求する", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.goto("/");
  await page.getByTestId("build-menu-close").click();

  await expect.poll(() => payloadsOf(page, "ui_state.request")).toContainEqual({ state: "GameScreen" });
  await expect(page.getByTestId("build-menu-panel")).toBeHidden();
});
