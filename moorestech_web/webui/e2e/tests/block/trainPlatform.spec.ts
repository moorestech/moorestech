import { test, expect } from "@playwright/test";
import { payloadsOf } from "../../support/actions";
import { setBlock } from "../../support/mockControl";
import { FLUID_ICON_PREFIX } from "../../../src/bridge/transport/httpEndpoints";
import { WATER_FLUID_GUID } from "../../mock-host/fixtures/contentLocalizationFixtures";

test.afterEach(async ({ page }) => {
  await setBlock(page, "closed");
});

test("PFスロットを表示し、積込から卸しへ目標モードActionを送る", async ({ page }) => {
  await setBlock(page, "trainPlatform");
  await page.goto("/");

  await expect(page.getByTestId("train-platform-item-slots")).toBeVisible();
  await expect(page.getByTestId("train-platform-item-slots").locator("> *")).toHaveCount(2);
  await page.getByTestId("train-platform-mode").getByText("卸し").click();

  await expect.poll(async () => {
    const payloads = await payloadsOf(page, "train_platform.set_transfer_mode");
    return payloads[0];
  }).toEqual({ mode: "unloadToPlatform" });
});

test("液体PFは液体名・アイコン・量容量と卸しモードを表示する", async ({ page }) => {
  // mock-hostの画像404を置換し、実スロットの画像読込を検証する
  // Replace the mock host image 404 to verify real slot image loading
  await page.route(`**${FLUID_ICON_PREFIX}${WATER_FLUID_GUID}.png`, (route) => route.fulfill({
    contentType: "image/svg+xml",
    body: '<svg xmlns="http://www.w3.org/2000/svg" width="64" height="64"><rect width="64" height="64" fill="#2A6FE0"/></svg>',
  }));
  await setBlock(page, "trainFluidPlatform");
  await page.goto("/");

  const fluids = page.getByTestId("train-platform-fluid-slots");
  await expect(fluids).toContainText("Water");
  await expect(fluids.getByRole("img", { name: "Water", exact: true })).toBeVisible();
  await expect(fluids).toContainText("250 / 1,000");
  await expect(page.getByTestId("train-platform-mode")).toContainText("卸し");
});
