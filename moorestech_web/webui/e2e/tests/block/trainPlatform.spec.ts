import { test, expect } from "@playwright/test";
import { payloadsOf } from "../../support/actions";
import { setBlock } from "../../support/mockControl";

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

test("液体PFはマスタ容量と卸しモードを表示する", async ({ page }) => {
  await setBlock(page, "trainFluidPlatform");
  await page.goto("/");

  await expect(page.getByTestId("train-platform-fluid-capacity")).toContainText("1000");
  await expect(page.getByTestId("train-platform-mode")).toContainText("卸し");
});
