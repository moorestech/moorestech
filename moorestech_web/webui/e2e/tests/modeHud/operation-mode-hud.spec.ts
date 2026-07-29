import { expect, test } from "@playwright/test";
import { setTopicScenario, setUiState } from "../../support/mockControl";
import { expectCraftFramedPlacementHud } from "../../support/operationHudAssertions";

// 共有UI状態を各テスト後に戻す
// Reset shared UI state after every test
test.afterEach(async ({ page }) => {
  await setTopicScenario(page, "placementEmpty");
  await setTopicScenario(page, "deleteEmpty");
  await setUiState(page, "PlayerInventory");
});

test("配置モードHUDを右上のクラフト枠で表示する", async ({ page }) => {
  await setTopicScenario(page, "placement");
  await setUiState(page, "PlaceBlock");
  await page.goto("/");

  const hud = page.getByTestId("placement-mode-hud");
  await expect(hud).toContainText("Placement Mode");
  await expect(hud).toContainText("Selected: Assembler");
  await expect(hud).toContainText("Height: 3");
  await expectCraftFramedPlacementHud(hud);

  // 配置不能理由も同じ警告階層へ反映する
  // Reflect placement failures in the same warning hierarchy
  await setTopicScenario(page, "placementUnavailable");
  await expect(hud.getByTestId("operation-mode-warning")).toHaveText("Blocked by terrain");
  await expect(hud.getByTestId("operation-mode-warning")).toHaveCSS("color", "rgb(255, 120, 120)");
});

test("削除モードをuGUI準拠の上下警告帯だけで表示する", async ({ page }) => {
  await setTopicScenario(page, "delete");
  await setUiState(page, "DeleteBar");
  await page.goto("/");

  const warning = page.getByTestId("delete-mode-warning");
  await expect(warning).toBeVisible();
  await expect(warning).toHaveAttribute("role", "status");
  await expect(warning).toHaveAttribute("aria-label", "Delete Mode");
  await expect(warning).toHaveCSS("pointer-events", "none");
  await expect(page.getByTestId("delete-mode-hud")).toHaveCount(0);
  await expect(page.getByText("Protected area", { exact: true })).toHaveCount(0);

  const bands = warning.getByTestId("delete-mode-warning-band");
  await expect(bands).toHaveCount(2);
  await expect(bands.first()).toHaveCSS("top", "0px");
  await expect(bands.last()).toHaveCSS("bottom", "0px");
  await expect(bands.last()).toHaveAttribute("data-tutorial-anchor", "delete.hud");
  await expect(bands.last()).not.toHaveAttribute("aria-hidden");
  const expectedPattern = "repeating-linear-gradient(117deg, rgb(255, 187, 36) 0px, rgb(255, 187, 36) 32px, rgb(0, 0, 0) 32px, rgb(0, 0, 0) 64px)";
  for (const band of await bands.all()) {
    await expect(band).toHaveCSS("height", "20px");
    const background = await band.evaluate((element) => getComputedStyle(element).backgroundImage);
    expect(background).toBe(expectedPattern);
  }
});
