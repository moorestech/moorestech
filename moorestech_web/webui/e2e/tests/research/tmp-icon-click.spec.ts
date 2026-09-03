import { test, expect } from "@playwright/test";
import { setUiState } from "../../support/mockControl";
import { researchableNodeGuid } from "../../mock-host/researchFixtures";

test("tmp: 実画像アイコン上のクリック", async ({ page }) => {
  await setUiState(page, "ResearchTree");
  await page.goto("/");
  const node = page.getByTestId(`research-node-${researchableNodeGuid}`);
  await expect(node).toBeVisible();
  console.log("NODE HTML", await node.innerHTML());
  const img = node.locator("img");
  console.log("IMG COUNT", await img.count());
  const b = await (await img.count() ? img : node.locator("div").last()).boundingBox();
  console.log("ICON BOX", JSON.stringify(b));
  if (b) {
    const el = await page.evaluate(({ x, y }) => {
      const e = document.elementFromPoint(x, y);
      return e ? `${e.tagName}.${e.className}` : "null";
    }, { x: b.x + b.width / 2, y: b.y + b.height / 2 });
    console.log("ELEMENT AT ICON CENTER:", el);
    await page.mouse.click(b.x + b.width / 2, b.y + b.height / 2);
  }
  console.log("PANE AFTER ICON CLICK:", await page.getByTestId("research-detail-pane").count());
  expect(await page.getByTestId("research-detail-pane").count()).toBe(1);
});
