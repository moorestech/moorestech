import { expect, test, type Page } from "@playwright/test";
import { setTopicScenario } from "../../support/mockControl";

// スロットのツールチップはPortalへ出るためスクロール祖先のクリップが効かない。放置すると内容と一緒に
// 滑ってパネルの外まで出ていくため、祖先がスクロールしたら引っ込める（ユーザー指摘 2026-08-22）
// Slot tooltips render into a Portal, so no scrolling ancestor clips them; left alone they slide out of the
// panel with the content, so an ancestor scroll retracts them (user report 2026-08-22)

const tooltip = (page: Page) => page.locator(".mantine-Tooltip-tooltip");

async function hoverCenter(page: Page, itemId: number, offset = 0) {
  const box = (await page.locator(`[data-item-id="${itemId}"]`).boundingBox())!;
  await page.mouse.move(box.x + box.width / 2 + offset, box.y + box.height / 2 + offset);
}

test.afterEach(async ({ page }) => {
  await setTopicScenario(page, "itemListDefault");
});

test("ツールチップはMantine既定の白い角丸ではなくCursorTooltipと同じ面を使う", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "CRAFT RECIPE" })).toBeVisible();
  await hoverCenter(page, 100);
  await expect(tooltip(page)).toBeVisible();

  // 面・角丸・書式は共有トークン由来。§9「Mantine標準テーマ剥き出し」を踏まない
  // Face, radius and format come from the shared tokens, never §9's bare Mantine default
  await expect(tooltip(page)).toHaveCSS("border-radius", "0px");
  const shared = await page.evaluate(() => {
    const read = (name: string) => getComputedStyle(document.documentElement).getPropertyValue(name).trim();
    return { face: read("--tooltip-face"), fontSize: read("--tooltip-font-size") };
  });
  await expect(tooltip(page)).toHaveCSS("background-color", "rgba(16, 20, 28, 0.94)");
  expect(shared.face).toBe("rgb(16 20 28 / 94%)");
  await expect(tooltip(page)).toHaveCSS("font-size", shared.fontSize);
});

test("祖先のスクロールでツールチップを引っ込め、ポインタを動かせば開き直す", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "CRAFT RECIPE" })).toBeVisible();
  await setTopicScenario(page, "itemListLarge");

  await hoverCenter(page, 1);
  await expect(tooltip(page)).toHaveText("Wood");

  // ホイールで一覧が動いたら引っ込む（内容と一緒に滑らせない）
  // A wheel-driven list move retracts it instead of letting it slide with the content
  await page.mouse.wheel(0, 120);
  await expect(tooltip(page)).toHaveCount(0);

  // 同じセルへ載ったままでも、ポインタが動けば開き直る（disabledで畳むとここが二度と開かない）
  // It reopens on pointer movement even while resting on the same cell; collapsing via disabled never would
  await page.evaluate(() => {
    const grid = document.querySelector('[data-testid="item-list-grid"]')!;
    grid.closest(".mantine-ScrollArea-viewport")!.scrollTop = 0;
  });
  await hoverCenter(page, 1, 5);
  await expect(tooltip(page)).toHaveText("Wood");
});
