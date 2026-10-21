import { expect, test } from "@playwright/test";
import { payloadsOf } from "../../support/actions";
import { setTopicScenario, setUiState } from "../../support/mockControl";

test.afterEach(async ({ page }) => {
  await setTopicScenario(page, "pauseConnected");
  await expect(page.getByTestId("pause-menu-open-settings")).toBeVisible();
  await setTopicScenario(page, "graphicsRecoveredClear");
  await setUiState(page, "PlayerInventory");
});

// 画面内に収め全項目・戻るへ到達
// Keep the frame on screen and every item and Back reachable
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

    // 末尾項目の操作と戻るを確認
    // Verify operating the last item and returning
    await menu.getByTestId("graphics-ocean-off").scrollIntoViewIfNeeded();
    await menu.getByTestId("graphics-ocean-off").click();
    await expect.poll(async () => (await payloadsOf(page, "settings.setGraphicsQuality")).at(-1)).toMatchObject({ item: "ocean", value: false });
    await back.click();
    await expect.poll(async () => (await payloadsOf(page, "pause_menu.show_page")).at(-1)).toEqual({ page: "top" });
  });
}

for (const viewport of [{ width: 1280, height: 720 }, { width: 2432, height: 786 }]) {
  test(`復旧表示は${viewport.width}x${viewport.height}で収まり同値操作と解除通知を受ける`, async ({ page }, testInfo) => {
    await page.setViewportSize(viewport);
    await setUiState(page, "PauseMenu");
    await setTopicScenario(page, "graphicsRecovered");
    await page.goto("/");
    const menu = page.getByTestId("pause-menu");
    await menu.getByTestId("pause-menu-open-settings").click();
    const notice = menu.getByTestId("graphics-recovered");
    await notice.scrollIntoViewIfNeeded();
    await expect(notice).toContainText("画質設定を中に復旧しました");
    const frame = await menu.boundingBox();
    const text = await notice.boundingBox();
    expect(text!.x).toBeGreaterThanOrEqual(frame!.x);
    expect(text!.x + text!.width).toBeLessThanOrEqual(frame!.x + frame!.width);
    expect(text!.y + text!.height).toBeLessThanOrEqual(frame!.y + frame!.height);
    await page.screenshot({ path: testInfo.outputPath(`graphics-recovered-${viewport.width}.png`) });
    await menu.getByTestId("graphics-shadows-on").click();
    await expect.poll(async () => (await payloadsOf(page, "settings.setGraphicsQuality")).at(-1)).toEqual({ item: "shadows", value: true });
    await expect(notice).toBeAttached();
    // mockは解除通知だけを注入し、実ホストの通知はUnity契約試験で検証する
    // Inject only the mock acknowledgment; Unity contract tests verify actual host publication
    await setTopicScenario(page, "graphicsRecoveredClear");
    await expect(notice).toHaveCount(0);
  });
}
