import { expect, test, type Locator } from "@playwright/test";
import { setTopicScenario, setUiState } from "../../support/mockControl";

// 共有UI状態を各テスト後に戻す
// Reset shared UI state after every test
test.afterEach(async ({ page }) => {
  await setTopicScenario(page, "placement");
  await setTopicScenario(page, "delete");
  await setUiState(page, "PlayerInventory");
});

test("配置モードHUDを面なしの情報階層で表示する", async ({ page }) => {
  await setTopicScenario(page, "placement");
  await setUiState(page, "PlaceBlock");
  await page.goto("/");

  const hud = page.getByTestId("placement-mode-hud");
  await expect(hud).toContainText("Placement Mode");
  await expect(hud).toContainText("Selected: Assembler");
  await expect(hud).toContainText("Height: 3");
  await expectFacelessOperationHud(hud);

  // 配置不能理由も同じ警告階層へ反映する
  // Reflect placement failures in the same warning hierarchy
  await setTopicScenario(page, "placementUnavailable");
  await expect(hud.getByTestId("operation-mode-warning")).toHaveText("Blocked by terrain");
  await expect(hud.getByTestId("operation-mode-warning")).toHaveCSS("color", "rgb(255, 107, 107)");
});

test("削除モードHUDの操作不能理由だけを警告色で表示する", async ({ page }) => {
  await setTopicScenario(page, "delete");
  await setUiState(page, "DeleteBar");
  await page.goto("/");

  const hud = page.getByTestId("delete-mode-hud");
  await expect(hud).toContainText("Delete Mode");
  await expect(hud).toContainText("Drag to select objects to delete");
  await expect(hud.getByTestId("operation-mode-warning")).toHaveCSS("color", "rgb(255, 107, 107)");
  await expectFacelessOperationHud(hud);
});

async function expectFacelessOperationHud(hud: Locator) {
  // 面なし契約と固定値を検証する
  // Verify the faceless contract and fixed values
  await expect(hud).toBeVisible();
  await expect(hud).toHaveCSS("background-color", "rgba(0, 0, 0, 0)");
  await expect(hud).toHaveCSS("background-image", "none");
  await expect(hud).toHaveCSS("pointer-events", "none");
  await expect(hud).toHaveCSS("top", "24px");
  await expect(hud).toHaveCSS("left", "24px");
  await expect(hud).toHaveCSS("width", "288px");
  await expect(hud).toHaveCSS("text-shadow", "rgba(0, 0, 0, 0.85) 0px 1px 2px");
  await expect(hud.locator('[aria-hidden="true"]')).toHaveCount(1);

  // ラベルと本文の視覚階層を固定する
  // Lock the label-detail visual hierarchy
  const label = hud.getByTestId("operation-mode-label");
  const detail = hud.getByTestId("operation-mode-detail").first();
  await expect(label).toHaveCSS("color", "rgb(166, 167, 171)");
  await expect(label).toHaveCSS("font-size", "12px");
  await expect(label).toHaveCSS("font-weight", "400");
  await expect(label).toHaveCSS("letter-spacing", "1px");
  await expect(detail).toHaveCSS("color", "rgb(255, 255, 255)");
  await expect(detail).toHaveCSS("font-size", "17px");

  const visualContract = await hud.evaluate((element) => {
    const hudStyle = getComputedStyle(element);
    const beforeStyle = getComputedStyle(element, "::before");
    const afterStyle = getComputedStyle(element, "::after");
    const detailStyle = getComputedStyle(element.querySelector('[data-testid="operation-mode-detail"]')!);
    return {
      animationName: hudStyle.animationName,
      beforeContent: beforeStyle.content,
      beforeImage: beforeStyle.backgroundImage,
      afterContent: afterStyle.content,
      afterImage: afterStyle.backgroundImage,
      borderRadius: hudStyle.borderRadius,
      borderWidth: hudStyle.borderWidth,
      boxShadow: hudStyle.boxShadow,
      detailFontWeight: detailStyle.fontWeight,
      detailLineHeight: detailStyle.lineHeight,
    };
  });
  expect(visualContract).toEqual({
    animationName: "none",
    beforeContent: "none",
    beforeImage: "none",
    afterContent: "none",
    afterImage: "none",
    borderRadius: "0px",
    borderWidth: "0px",
    boxShadow: "none",
    detailFontWeight: "400",
    detailLineHeight: "25px",
  });
}
