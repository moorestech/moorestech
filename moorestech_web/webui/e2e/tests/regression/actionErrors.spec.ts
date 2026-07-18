import { test, expect } from "@playwright/test";
import { payloadsOf } from "../../support/actions";
import { clearActionError, injectActionError, setBlock } from "../../support/mockControl";

test.afterEach(async ({ page }) => {
  await clearActionError(page);
  await setBlock(page, "closed");
});

for (const error of ["grab_not_empty", "empty_slot"] as const) {
  test(`block_inventory.splitの${error}はtoastを出さない`, async ({ page }) => {
    await setBlock(page, "chest");
    await injectActionError(page, "block_inventory.split", error);
    await page.goto("/");
    const before = (await payloadsOf(page, "block_inventory.split")).length;
    await page.getByTestId("chest-grid").locator("> div").first().click({ button: "right" });

    await expect.poll(async () => (await payloadsOf(page, "block_inventory.split")).length).toBeGreaterThan(before);
    await expect(page.getByTestId("toast-host").getByText(/block_inventory\.split failed/)).toHaveCount(0, { timeout: 2000 });
  });
}

test("block_inventory.splitのinvalid_slotはtoastを出す", async ({ page }) => {
  await setBlock(page, "chest");
  await injectActionError(page, "block_inventory.split", "invalid_slot");
  await page.goto("/");
  await page.getByTestId("chest-grid").locator("> div").first().click({ button: "right" });

  await expect(page.getByTestId("toast-host")).toContainText("block_inventory.split failed: invalid_slot");
});
