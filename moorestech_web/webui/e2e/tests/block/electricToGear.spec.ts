import { test, expect } from "@playwright/test";
import { payloadsOf } from "../../support/actions";
import { setBlock } from "../../support/mockControl";

test.afterEach(async ({ page }) => {
  await setBlock(page, "closed");
});

test("出力モード行とStateDetail値を表示し、選択indexをaction送信する", async ({ page }) => {
  await setBlock(page, "electricToGear");
  await page.goto("/");

  await expect(page.getByTestId("electric-to-gear-view")).toBeVisible();
  await expect(page.getByTestId("electric-to-gear-mode-0")).toContainText("10 rpm / 10 trq / 10 W");
  await expect(page.getByTestId("electric-to-gear-consumed-power")).toContainText("10 W");

  await page.getByTestId("electric-to-gear-mode-0").click();
  await expect.poll(async () => {
    const payloads = await payloadsOf(page, "electric_to_gear.set_output_mode");
    return payloads[0];
  }).toEqual({ modeIndex: 0 });
});
