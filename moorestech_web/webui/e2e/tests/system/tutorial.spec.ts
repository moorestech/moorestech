import { expect, test } from "@playwright/test";
import { setTopicScenario } from "../../support/mockControl";

const expectedOutlineBoxShadow = "rgba(255, 221, 87, 0.24) 0px 0px 0px 4px";

test.afterEach(async ({ page }) => {
  await setTopicScenario(page, "tutorialEmpty");
});

test("tutorial outline highlights the target without dimming the rest of the screen", async ({ page }) => {
  await page.goto("/");
  await setTopicScenario(page, "tutorialOutline");

  const overlay = page.getByTestId("tutorial-overlay");
  const highlight = overlay.locator("[data-kind]");
  await expect(highlight).toBeVisible();
  await expect(highlight).toHaveAttribute("data-kind", "outline");
  await expect(highlight).toHaveCSS("border-top-color", "rgb(255, 221, 87)");
  await expect(highlight).toHaveCSS("box-shadow", expectedOutlineBoxShadow);
  await expect(overlay).toHaveCSS("background-color", "rgba(0, 0, 0, 0)");
  await expect(overlay).toHaveCSS("background-image", "none");
  await expect(overlay).toHaveCSS("filter", "none");
  await expect(overlay).toHaveCSS("backdrop-filter", "none");

  // 廃止済みkindへの変更でも暗転CSSが復活しないことを直接検査する
  // Probe the removed kind directly so restored dimming CSS cannot escape the test
  await highlight.evaluate((element) => element.setAttribute("data-kind", "spotlight"));
  await expect(highlight).toHaveCSS("box-shadow", expectedOutlineBoxShadow);
});
