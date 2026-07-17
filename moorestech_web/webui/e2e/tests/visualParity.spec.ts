import { expect, test } from "@playwright/test";

// 複層クロームと装飾を固定する
// Pin layered chrome and ornaments
test("uGUIの複層パネルとクラフト装飾を保つ", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "CRAFT RECIPE" })).toBeVisible();
  await page.getByTestId("item-list-grid").locator("> div").first().click();

  const recipeBox = page.getByTestId("craft-recipe-box");
  const panel = recipeBox.locator("xpath=ancestor::div[contains(@class, '_panel_')][1]");
  const chrome = await panel.evaluate((element) => {
    const style = getComputedStyle(element);
    return {
      background: style.backgroundColor,
      borderRadius: style.borderRadius,
      panelShadow: style.boxShadow,
    };
  });
  const triangleChrome = await page.getByTestId("recipe-content").evaluate((element) => {
    const triangle = getComputedStyle(element, "::after");
    return { image: triangle.backgroundImage, width: triangle.width };
  });

  expect(chrome.background).toBe("rgba(6, 12, 16, 0.498)");
  expect(chrome.borderRadius).toBe("0px");
  expect(chrome.panelShadow).toContain("inset");
  expect(chrome.panelShadow).toContain("rgba(156, 166, 180, 0.28)");
  expect(chrome.panelShadow).toContain("rgba(0, 0, 0, 0.28)");
  expect(triangleChrome.image).toContain("rgba(183, 189, 204, 0.84)");
  expect(triangleChrome.image).toContain("rgba(56, 65, 83, 0.74)");
  expect(parseFloat(triangleChrome.width)).toBeGreaterThan(40);

  const ornament = page.getByTestId("recipe-divider-ornament");
  await expect(ornament).toBeVisible();
  const ornamentBox = await ornament.boundingBox();
  const panelBox = await panel.boundingBox();
  expect(ornamentBox).not.toBeNull();
  expect(panelBox).not.toBeNull();
  expect(Math.abs(ornamentBox!.x + ornamentBox!.width / 2 - (panelBox!.x + panelBox!.width / 2))).toBeLessThanOrEqual(3);
});

// 番号位置と選択グローを検証する
// Verify label placement and selection glow
test("ホットバー番号と選択枠をuGUI配置で保つ", async ({ page }) => {
  await page.goto("/");
  await page.getByTestId("item-list-grid").locator("> div").first().click();
  const firstCell = page.getByTestId("hotbar-grid").locator("> div").first();
  const label = firstCell.locator("span").first();
  const slot = firstCell.locator("> div");

  const labelBox = await label.boundingBox();
  const slotBox = await slot.boundingBox();
  expect(labelBox).not.toBeNull();
  expect(slotBox).not.toBeNull();
  expect(labelBox!.y + labelBox!.height).toBeLessThan(slotBox!.y);
  expect(Math.abs(labelBox!.x + labelBox!.width / 2 - (slotBox!.x + slotBox!.width / 2))).toBeLessThanOrEqual(3);

  const selectedChrome = await page.getByTestId("craft-recipe-box").evaluate((element) => {
    const style = getComputedStyle(element);
    const corner = getComputedStyle(element, "::before");
    return {
      borderRadius: style.borderRadius,
      shadow: style.boxShadow,
      cornerImage: corner.backgroundImage,
      cornerPosition: corner.backgroundPosition,
    };
  });
  expect(selectedChrome.borderRadius).toBe("0px");
  expect(selectedChrome.shadow).toContain("rgba(9, 55, 82, 0.88)");
  expect(selectedChrome.shadow).toContain("rgba(32, 130, 170, 0.34)");
  expect(selectedChrome.shadow).toContain("rgba(18, 182, 238, 0.38)");
  expect(selectedChrome.cornerImage).toContain("rgb(146, 248, 255)");
  expect(selectedChrome.cornerPosition).toContain("100% 0%");
});
