import { test, expect } from "@playwright/test";
import { setTopicScenario } from "../support/mockControl";

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

  // 採掘HUDの可視情報を一本化し、対象名と重複バーの再発を検出する
  // Keep mining HUD information in one place and catch target-label or duplicate-bar regressions
  const wrapper = page.locator('[data-tutorial-anchor="mining.hud"]');
  const gauge = page.getByTestId("progress-gauge");
  const hotbar = page.getByTestId("hotbar-grid");
  const firstNumberTab = hotbar.locator("> div").first().locator("> span").first();
  await expect(page.getByText(/Mining Target/i)).toHaveCount(0);
  await expect(page.getByText("Iron Ore", { exact: true })).toHaveCount(0);
  await expect(page.getByRole("progressbar")).toHaveCount(1);
  await expect(wrapper).toHaveCSS("pointer-events", "none");
  await expect(wrapper).toHaveCSS("position", "absolute");
  await expect(gauge).toHaveAttribute("aria-valuenow", "0.65");

  // ホットバーと同じ中心・幅を使い、番号タブとの空隙を実座標で検証する
  // Verify the shared center and width plus the number-tab clearance using rendered coordinates
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

  // 共通GaugeBarの寒色トークンを使い、旧Mantine緑ゲージへの逆戻りを防ぐ
  // Require the shared GaugeBar cool tokens and prevent a return to the old Mantine green gauge
  const fill = gauge.locator("> div");
  await expect(gauge).toHaveCSS("background-color", "rgba(10, 14, 27, 0.8)");
  await expect(fill).toHaveCSS("background-color", "rgb(104, 106, 120)");
  expect(await gauge.getAttribute("class")).not.toContain("mantine");
});
