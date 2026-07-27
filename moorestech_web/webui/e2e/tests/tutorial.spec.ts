import { expect, test } from "@playwright/test";
import { setTopicScenario } from "../support/mockControl";

test.afterEach(async ({ page }) => {
  await setTopicScenario(page, "tutorialEmpty");
});

test("tutorial outline highlights the target without dimming the rest of the screen", async ({ page }) => {
  await page.goto("/");
  await setTopicScenario(page, "tutorialOutline");

  const highlight = page.getByTestId("tutorial-overlay").locator("[data-kind='outline']");
  await expect(highlight).toBeVisible();
  await expect(highlight).toHaveCSS("border-top-color", "rgb(255, 221, 87)");
  expect(await highlight.evaluate((element) => getComputedStyle(element).boxShadow)).not.toContain("9999px");
});
