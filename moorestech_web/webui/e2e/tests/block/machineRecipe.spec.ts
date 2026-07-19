import { test, expect } from "@playwright/test";
import { setBlock } from "../../support/mockControl";

const firstRecipeTestId = "machine-recipe-aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
const selectedRecipeTestId = "machine-recipe-bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";

test.afterEach(async ({ page }) => {
  await setBlock(page, "closed");
});

test("機械レシピ3件を表示し、解除と再選択をblock topicへ反映する", async ({ page }) => {
  await setBlock(page, "machine");
  await page.goto("/");

  const selection = page.getByTestId("machine-recipe-selection");
  const slots = selection.locator('[data-testid^="machine-recipe-"]');
  await expect(selection).toBeVisible();
  await expect(slots).toHaveCount(3);
  await expect(selection.locator('[data-selected="true"]')).toHaveCount(1);
  await expect(page.getByTestId(selectedRecipeTestId)).toHaveAttribute("data-selected", "true");

  await page.getByTestId(selectedRecipeTestId).click({ button: "right" });
  await expect(selection.locator('[data-selected="true"]')).toHaveCount(0);

  await page.getByTestId(firstRecipeTestId).click();
  await expect(page.getByTestId(firstRecipeTestId)).toHaveAttribute("data-selected", "true");
});
