import { test, expect, type Locator } from "@playwright/test";
import { setUiState } from "../../support/mockControl";
import { buildMenuEntryIds } from "../../mock-host/fixtures";

// モックの設置対象アイコンは404のため、中央寄せを測れる実画像（正方SVG）へ差し替える
// The mock placement icons 404, so serve a real square SVG that can be measured for centering
const SQUARE_ICON_SVG = '<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256"><rect width="256" height="256" fill="#c33"/></svg>';

test.beforeEach(async ({ page }) => {
  await page.route("**/api/block-icons/*", (route) => route.fulfill({ status: 200, contentType: "image/svg+xml", body: SQUARE_ICON_SVG }));
});

test.afterEach(async ({ page }) => {
  await setUiState(page, "PlayerInventory");
});

// 枠に対するアイコンの上下左右の余白を測る
// Measure the icon's margins against its frame on all four sides
async function iconMargins(frame: Locator) {
  return frame.evaluate((element) => {
    const slot = element.getBoundingClientRect();
    const image = element.querySelector("img")!;
    const icon = image.getBoundingClientRect();
    // 描画面は img の padding を除いた内容箱。object-fit: contain の正方アイコンはそこへ収まる
    // The drawn area is the img content box (padding removed); a square icon with object-fit: contain fills it
    const style = getComputedStyle(image);
    const inset = (side: string) => parseFloat(style.getPropertyValue(`padding-${side}`));
    return {
      left: icon.left + inset("left") - slot.left,
      right: slot.right - (icon.right - inset("right")),
      top: icon.top + inset("top") - slot.top,
      bottom: slot.bottom - (icon.bottom - inset("bottom")),
    };
  });
}

function expectCentered(margins: { left: number; right: number; top: number; bottom: number }) {
  expect(margins.left).toBeGreaterThan(0);
  expect(Math.abs(margins.left - margins.right)).toBeLessThan(0.5);
  expect(Math.abs(margins.top - margins.bottom)).toBeLessThan(0.5);
  expect(Math.abs(margins.left - margins.top)).toBeLessThan(0.5);
}

// 設置対象アイコンは共有のアイコン規則に乗り、ビルドメニュー・ホットバーとも枠の中央に描かれる
// The placement icon rides the shared icon rule and is centered in its frame in both the build menu and the hotbar
test("ビルドメニューのアイコンは枠の中央に描かれる", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.goto("/");
  const frame = page.getByTestId(`build-menu-entry-block-${buildMenuEntryIds.woodChest}`);
  await expect(frame.locator("img")).toBeVisible();
  const margins = await iconMargins(frame);
  console.log("build-menu margins", JSON.stringify(margins));
  expectCentered(margins);
});

test("ホットバーのアイコンは枠の中央に描かれる", async ({ page }) => {
  await page.goto("/");
  const frame = page.locator('[data-testid^="hotbar-slot-"]:has(img)').first();
  await expect(frame).toBeVisible();
  const margins = await iconMargins(frame);
  console.log("hotbar margins", JSON.stringify(margins));
  expectCentered(margins);
});
