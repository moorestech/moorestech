import { expect, test } from "@playwright/test";
import { payloadsOf } from "../../support/actions";
import { setUiState } from "../../support/mockControl";

test.afterEach(async ({ page }) => {
  await setUiState(page, "PlayerInventory");
});

// 1280×720 と高さ制約型の横長で、設定項目が増えても枠が画面に収まり戻るボタンと全項目に届く
// At 1280×720 and a height-bound wide viewport, the frame stays on screen and the back button and every item stay reachable
for (const viewport of [{ width: 1280, height: 720 }, { width: 2432, height: 786 }]) {
  test(`設定画面は${viewport.width}x${viewport.height}で枠内に収まり本文だけスクロールする`, async ({ page }) => {
    await page.setViewportSize(viewport);
    await setUiState(page, "PauseMenu");
    await page.goto("/");
    const menu = page.getByTestId("pause-menu");
    await menu.getByTestId("pause-menu-open-settings").click();
    await expect(menu.getByTestId("graphics-ocean")).toBeAttached();

    const frame = await menu.boundingBox();
    expect(frame!.y).toBeGreaterThanOrEqual(0);
    expect(frame!.y + frame!.height).toBeLessThanOrEqual(viewport.height);

    const overflow = await menu.getByTestId("pause-menu-body").locator(".mantine-ScrollArea-viewport").evaluate((e) => [e.scrollHeight, e.clientHeight]);
    expect(overflow[0]).toBeGreaterThan(overflow[1]);

    const back = menu.getByTestId("pause-menu-back");
    const backBox = await back.boundingBox();
    expect(backBox!.y + backBox!.height).toBeLessThanOrEqual(frame!.y + frame!.height);

    // 最後の項目までスクロールして操作でき、戻るも押せる
    // Scroll to the last item and operate it; the back button stays pressable
    await menu.getByTestId("graphics-ocean-off").scrollIntoViewIfNeeded();
    await menu.getByTestId("graphics-ocean-off").click();
    await expect.poll(async () => (await payloadsOf(page, "settings.setGraphicsQuality")).at(-1)).toMatchObject({ settings: { ocean: false } });
    await back.click();
    await expect.poll(async () => (await payloadsOf(page, "pause_menu.show_page")).at(-1)).toEqual({ page: "top" });
  });
}
