import { test, expect } from "@playwright/test";
import { setMiningProgress, setTopicScenario } from "../support/mockControl";

test.afterEach(async ({ page }) => {
  await setTopicScenario(page, "miningHidden");
});

test("ラベル付き汎用進捗を維持する", async ({ page }) => {
  await setTopicScenario(page, "progressLabeled");
  await page.goto("/");
  await expect(page.getByTestId("progress-bar")).toBeVisible();
  await expect(page.getByText("Crafting", { exact: true })).toBeVisible();
});

test("採掘進捗は対象名を出さずホットバー全幅の12px上に1本だけ描画する", async ({ page }) => {
  await setTopicScenario(page, "mining");
  await page.goto("/");

  // 採掘HUD一本化の回帰を検証
  // Verify unified mining HUD regressions
  const wrapper = page.locator('[data-tutorial-anchor~="mining.hud"]');
  const gauge = page.getByTestId("progress-gauge");
  const hotbar = page.getByTestId("hotbar-grid");
  const firstNumberTab = hotbar.locator("> div").first().locator("> span").first();
  await expect(page.getByText(/Mining Target/i)).toHaveCount(0);
  await expect(page.getByText("Iron Ore", { exact: true })).toHaveCount(0);
  await expect(page.getByRole("progressbar")).toHaveCount(1);
  await expect(wrapper).toHaveCSS("pointer-events", "none");
  await expect(wrapper).toHaveCSS("position", "absolute");
  await expect(gauge).toHaveAttribute("aria-valuenow", "0.65");

  // 共有配置を実座標で検証
  // Verify shared layout using rendered coordinates
  const wrapperBox = await wrapper.boundingBox();
  const hotbarBox = await hotbar.boundingBox();
  const numberTabBox = await firstNumberTab.boundingBox();
  expect(wrapperBox).not.toBeNull();
  expect(hotbarBox).not.toBeNull();
  expect(numberTabBox).not.toBeNull();
  expect(Math.abs(wrapperBox!.width - hotbarBox!.width)).toBeLessThanOrEqual(0.5);
  expect(Math.abs(
    wrapperBox!.x + wrapperBox!.width / 2 - (hotbarBox!.x + hotbarBox!.width / 2),
  )).toBeLessThanOrEqual(0.5);
  expect(numberTabBox!.y - (wrapperBox!.y + wrapperBox!.height)).toBeCloseTo(12, 1);

  // 溝は寒色トークン、充填は世界背景から分離する白（--color-content-primary）へ上書きされる
  // The groove keeps the cool token while the fill is overridden to the world-separating white (--color-content-primary)
  const fill = gauge.locator("> div");
  const contentPrimary = await page.evaluate(() => {
    const probe = document.createElement("div");
    probe.style.backgroundColor = "var(--color-content-primary)";
    document.body.append(probe);
    const color = getComputedStyle(probe).backgroundColor;
    probe.remove();
    return color;
  });
  await expect(gauge).toHaveCSS("background-color", "rgba(10, 14, 27, 0.8)");
  await expect(fill).toHaveCSS("background-color", contentPrimary);
  expect(await gauge.getAttribute("class")).not.toContain("mantine");

  // 同一接続で更新と撤去を検証
  // Verify updates and removal on one connection
  await setMiningProgress(page, 0.2);
  await expect(gauge).toHaveAttribute("aria-valuenow", "0.2");
  await setMiningProgress(page, 1);
  await expect(gauge).toHaveAttribute("aria-valuenow", "1");
  await setTopicScenario(page, "miningHidden");
  await expect(wrapper).toHaveCount(0);
  await expect(page.getByRole("progressbar")).toHaveCount(0);
});

test("縮小viewportでも採掘進捗とホットバーの相対配置を維持する", async ({ page }) => {
  await page.setViewportSize({ width: 960, height: 540 });
  await setTopicScenario(page, "mining");
  await page.goto("/");

  // stageの一様縮小後も共有寸法が同じ倍率で描画される
  // Shared dimensions remain aligned after uniform stage scaling
  const wrapper = page.getByTestId("progress-bar");
  const hotbar = page.getByTestId("hotbar-grid");
  const firstNumberTab = hotbar.locator("> div").first().locator("> span").first();
  const wrapperBox = await wrapper.boundingBox();
  const hotbarBox = await hotbar.boundingBox();
  const numberTabBox = await firstNumberTab.boundingBox();
  const uiScale = await page.locator("html").evaluate((element) => (
    Number(getComputedStyle(element).getPropertyValue("--ui-scale"))
  ));
  expect(wrapperBox).not.toBeNull();
  expect(hotbarBox).not.toBeNull();
  expect(numberTabBox).not.toBeNull();
  expect(uiScale).toBeCloseTo(0.75, 2);
  expect(Math.abs(wrapperBox!.width - hotbarBox!.width)).toBeLessThanOrEqual(0.5);
  expect(Math.abs(
    wrapperBox!.x + wrapperBox!.width / 2 - (hotbarBox!.x + hotbarBox!.width / 2),
  )).toBeLessThanOrEqual(0.5);
  expect(numberTabBox!.y - (wrapperBox!.y + wrapperBox!.height)).toBeCloseTo(12 * uiScale, 1);
});
