import { test, expect } from "@playwright/test";

test("素材不足スロットはdata-insufficientと40% opacityを持つ", async ({ page }) => {
  await page.goto("/");
  await page.getByTestId("item-list-grid").locator("> div").nth(1).click();

  const insufficient = page.getByTestId("craft-recipe-box").locator('[data-insufficient="true"]');
  await expect(insufficient).toHaveCount(1);
  await expect(insufficient).toHaveCSS("opacity", "0.4");
});

test("name propなしのインベントリスロットがマスタ名をtooltip表示する", async ({ page }) => {
  await page.goto("/");
  await page.getByTestId("main-grid").locator("> div").first().hover();

  await expect(page.getByRole("tooltip")).toContainText("Wood");
});
