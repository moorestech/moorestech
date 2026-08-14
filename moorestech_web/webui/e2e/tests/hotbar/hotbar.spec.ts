import { test, expect } from "@playwright/test";
import { payloadsOf } from "../../support/actions";
import { setUiState } from "../../support/mockControl";
import { buildMenuEntryIds } from "../../mock-host/fixtures";

// 新ホットバーHUD(local_player.hotbar購読)の実ブラウザ回帰。数字キー選択は撤去済みのためここでは検証しない
// Real-browser regression for the new hotbar HUD (subscribes to local_player.hotbar); digit-key selection was removed, so it is not covered here
test.afterEach(async ({ page }) => {
  await setUiState(page, "PlayerInventory");
});

test("ホットバー9枠が常時表示され、fixtureのselectedSlotがハイライトされる", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByTestId("hotbar-grid")).toBeVisible();
  for (let i = 0; i < 9; i += 1) {
    await expect(page.getByTestId(`hotbar-slot-${i}`)).toBeVisible();
  }
  await expect(page.getByTestId("hotbar-slot-0")).toHaveAttribute("data-selected", "true");
  await expect(page.getByTestId("hotbar-slot-1")).not.toHaveAttribute("data-selected", "true");
});

test("空き枠のクリックはhotbar.select{index}を送る", async ({ page }) => {
  await page.goto("/");
  await page.getByTestId("hotbar-slot-3").click();
  await expect.poll(() => payloadsOf(page, "hotbar.select")).toContainEqual({ index: 3 });
});

test("ビルドメニューエントリを空き枠へドラッグするとhotbar.assign{slot,id}を送る", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.goto("/");

  const source = page.getByTestId(`build-menu-entry-block-${buildMenuEntryIds.ironChest}`);
  const target = page.getByTestId("hotbar-slot-1");
  const sourceBox = await source.boundingBox();
  const targetBox = await target.boundingBox();
  if (!sourceBox || !targetBox) throw new Error("drag endpoints not measurable");

  await page.mouse.move(sourceBox.x + sourceBox.width / 2, sourceBox.y + sourceBox.height / 2);
  await page.mouse.down();
  // 5pxしきい値を確実に超えるステップ移動でドラッグとして確定させる
  // Move in steps well past the 5px threshold so the gesture commits to a drag
  await page.mouse.move(targetBox.x + targetBox.width / 2, targetBox.y + targetBox.height / 2, { steps: 10 });
  await page.mouse.up();

  await expect.poll(() => payloadsOf(page, "hotbar.assign")).toContainEqual({ slot: 1, id: buildMenuEntryIds.ironChest });
  // ドラッグ確定なので、押下起点のbuild_menu.select(即時建築モード遷移)は飛んでいない
  // Since the gesture committed to a drag, the press-origin build_menu.select (immediate build-mode entry) must not fire
  await expect.poll(() => payloadsOf(page, "build_menu.select")).toEqual([]);
});

test("未解決の割当枠は使用不可表示になり、枠外ドラッグで外せる", async ({ page }) => {
  await page.goto("/");

  // 割当済みなので空枠ではなく面が埋まり、減光で使用不可を示す
  // It is assigned, so the face is filled rather than empty, and the dimming marks it unusable
  await expect(page.locator('[data-hotbar-slot-index="4"]')).toHaveAttribute("data-unresolved", "true");
  await expect(page.getByTestId("hotbar-slot-4")).toHaveAttribute("data-filled", "true");

  const source = page.getByTestId("hotbar-slot-4");
  const sourceBox = await source.boundingBox();
  if (!sourceBox) throw new Error("drag source not measurable");

  await page.mouse.move(sourceBox.x + sourceBox.width / 2, sourceBox.y + sourceBox.height / 2);
  await page.mouse.down();
  await page.mouse.move(sourceBox.x + sourceBox.width / 2, 5, { steps: 10 });
  await page.mouse.up();

  await expect.poll(() => payloadsOf(page, "hotbar.clear")).toContainEqual({ slot: 4 });
});

test("枠外へドラッグするとhotbar.clear{slot}を送る", async ({ page }) => {
  await page.goto("/");

  const source = page.getByTestId("hotbar-slot-0");
  const sourceBox = await source.boundingBox();
  if (!sourceBox) throw new Error("drag source not measurable");

  await page.mouse.move(sourceBox.x + sourceBox.width / 2, sourceBox.y + sourceBox.height / 2);
  await page.mouse.down();
  // 画面上端(パネル・ホットバーの外)へ十分離してドロップする
  // Drop far up near the screen top, outside any panel/hotbar element
  await page.mouse.move(sourceBox.x + sourceBox.width / 2, 5, { steps: 10 });
  await page.mouse.up();

  await expect.poll(() => payloadsOf(page, "hotbar.clear")).toContainEqual({ slot: 0 });
});
