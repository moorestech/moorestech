import { test, expect } from "@playwright/test";
import { payloadsOf } from "../support/actions";

test("正本どおりクラフト時間を選択枠内に置き、中央プレビューを表示しない", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "CRAFT RECIPE" })).toBeVisible();
  await page.getByTestId("item-list-grid").locator("> div").first().click();

  // クラフト時間は素材行と同じ選択枠の内側に表示する
  // Show the craft time inside the same selection frame as the material row
  const recipeBox = page.getByTestId("craft-recipe-box");
  await expect(recipeBox.getByText("0.2秒")).toBeVisible();

  // 正本に存在しない完成品プレビュー要素を中央余白へ追加しない
  // Do not add a crafted-result preview element to the center space absent from the reference
  await expect(page.locator('[class*="_craftPreview_"]')).toHaveCount(0);
});

test("正本のヘッダ順序、常時スクロールバー、主要構造を保つ", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "CRAFT RECIPE" })).toBeVisible();
  await page.getByTestId("item-list-grid").locator("> div").first().click();

  // 品名の直後を装飾線、その次をレシピツリーボタンに固定する
  // Keep the divider immediately after the name and the recipe-tree button after it
  const itemName = page.getByText("Plank", { exact: true });
  const divider = itemName.locator("xpath=following-sibling::*[1]");
  await expect(divider).toHaveAttribute("aria-hidden", "true");
  await expect(divider.locator("xpath=following-sibling::*[1]")).toHaveRole("button");

  // 選択枠DOMを画像測定用に保つ
  // Keep the selection-frame DOM available for image measurement
  const recipeBox = page.getByTestId("craft-recipe-box");
  const craftPanel = recipeBox.locator("xpath=ancestor::div[contains(@class, '_panel_')][1]");
  await expect(craftPanel).toBeVisible();
  await expect(page.getByRole("button", { name: "レシピツリーで表示" })).toBeVisible();
  await expect(page.getByRole("button", { name: "Craft" })).toBeVisible();

  // 短いfixtureでも縦バーを保つ
  // Preserve the vertical scrollbar even with a short fixture
  const scrollRoot = page.getByTestId("item-list-grid").locator("xpath=ancestor::*[contains(@class, 'mantine-ScrollArea-root')][1]");
  const viewport = scrollRoot.locator(".mantine-ScrollArea-viewport");
  await expect(viewport).toHaveCSS("overflow-y", "scroll");
  await expect(scrollRoot.locator('.mantine-ScrollArea-scrollbar[data-orientation="vertical"]')).toBeVisible();
});

test("アイテム選択でレシピ表示、長押しで素材が尽きるまで連続クラフトする", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "CRAFT RECIPE" })).toBeVisible();
  // 右リストの先頭 Plank(100) を選択
  // Select the first item Plank(100) in the right list
  await page.getByTestId("item-list-grid").locator("> div").first().click();

  const craftButton = page.getByRole("button", { name: "Craft" });
  await expect(craftButton).toBeEnabled();

  // ボタンを押し下げ保持して連続クラフトを発火させる
  // Hold the button down to fire continuous crafts
  const box = await craftButton.boundingBox();
  if (box === null) throw new Error("craft button has no bounding box");
  await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
  await page.mouse.down();

  // mock が素材を消費するため、保持し続けると素材が尽きてボタンが disabled 化する
  // The mock consumes materials, so holding until they run out disables the button
  await expect(craftButton).toBeDisabled({ timeout: 5000 });
  await page.mouse.up();

  // 連続で複数回クラフト要求が送られ、全て対象レシピ宛であること
  // Multiple craft requests were sent continuously, all targeting the shown recipe
  const payloads = await payloadsOf(page, "craft.execute");
  expect(payloads.length).toBeGreaterThanOrEqual(2);
  for (const payload of payloads) {
    expect((payload as { recipeGuid?: string }).recipeGuid).toBe("g-craft-1");
  }
});

test("押下後にボタンから外れるとクラフトが止まり経過時間がリセットされる", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "CRAFT RECIPE" })).toBeVisible();
  await page.getByTestId("item-list-grid").locator("> div").first().click();

  const craftButton = page.getByRole("button", { name: "Craft" });
  await expect(craftButton).toBeEnabled();
  const box = await craftButton.boundingBox();
  if (box === null) throw new Error("craft button has no bounding box");

  // これまでの送信数を控える（recorder はテスト横断で蓄積するため差分で判定）
  // Snapshot the prior send count (the recorder accumulates across tests, so assert on the delta)
  const before = (await payloadsOf(page, "craft.execute")).length;

  // 進捗が半分ほど溜まる程度だけ保持（craftTime=0.2s 未満）してからボタン外へ移動
  // Hold only long enough to fill the arrow partway (< craftTime=0.2s), then move off the button
  await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
  await page.mouse.down();
  await page.waitForTimeout(100);
  await page.mouse.move(box.x + box.width / 2, box.y - 100);

  // 外れた時点で経過がリセットされるため、craftTime を超えて待っても1回もクラフトされない
  // Leaving resets the elapsed time, so nothing crafts even after waiting past craftTime
  await page.waitForTimeout(500);
  await page.mouse.up();
  const after = (await payloadsOf(page, "craft.execute")).length;
  expect(after).toBe(before);
});
