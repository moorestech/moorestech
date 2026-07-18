import { test, expect } from "@playwright/test";
import { setBlock } from "../../support/mockControl";

test.afterEach(async ({ page }) => {
  await setBlock(page, "closed");
});

test("電柱専用ビューに発電量・需要量・消費者数・供給率を表示する", async ({ page }) => {
  await setBlock(page, "electricPole");
  await page.goto("/");

  await expect(page.getByTestId("electric-pole-view")).toBeVisible();
  await expect(page.getByTestId("electric-network-section")).toContainText("240");
  await expect(page.getByTestId("electric-network-section")).toContainText("180");
  await expect(page.getByTestId("electric-network-section")).toContainText("3");
  await expect(page.getByTestId("electric-network-section")).toContainText("100%");
});
