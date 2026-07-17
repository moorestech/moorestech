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

test("正本のヘッダ順序、非表示スクロールバー、主要クロームを保つ", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "CRAFT RECIPE" })).toBeVisible();
  await page.getByTestId("item-list-grid").locator("> div").first().click();

  // 品名の直後を装飾線、その次をレシピツリーボタンに固定する
  // Keep the divider immediately after the name and the recipe-tree button after it
  const itemName = page.getByText("Plank", { exact: true });
  const divider = itemName.locator("xpath=following-sibling::*[1]");
  await expect(divider).toHaveAttribute("aria-hidden", "true");
  await expect(divider.locator("xpath=following-sibling::*[1]")).toHaveRole("button");

  // 選択枠と中央パネルの角・透過・コーナーブラケットを検証する
  // Verify the selection frame and center panel corners, translucency, and brackets
  const recipeBox = page.getByTestId("craft-recipe-box");
  const recipeChrome = await recipeBox.evaluate((element) => {
    const style = getComputedStyle(element);
    const corner = getComputedStyle(element, "::before");
    return { borderWidth: style.borderTopWidth, borderRadius: style.borderRadius, cornerImage: corner.backgroundImage };
  });
  expect(recipeChrome.borderWidth).toBe("2px");
  expect(recipeChrome.borderRadius).toBe("0px");
  expect(recipeChrome.cornerImage).toContain("rgb(146, 248, 255)");

  const craftPanel = recipeBox.locator("xpath=ancestor::div[contains(@class, '_panel_')][1]");
  await expect(craftPanel).toHaveCSS("border-radius", "0px");
  const panelChrome = await craftPanel.evaluate((element) => getComputedStyle(element).boxShadow);
  expect(panelChrome).toContain("inset");
  expect(panelChrome).toContain("rgba(156, 166, 180, 0.28)");
  expect(panelChrome).toContain("rgba(0, 0, 0, 0.28)");
  await expect(craftPanel).toHaveCSS("background-color", "rgba(6, 12, 16, 0.498)");

  // 両ボタンの基準色と幅を固定し、暗色への退行を検出する
  // Pin both buttons' reference colors and width to catch regressions to dark fills
  const treeButton = page.getByRole("button", { name: "レシピツリーで表示" });
  const treeButtonStyle = await treeButton.evaluate((element) => getComputedStyle(element).backgroundImage);
  expect(treeButtonStyle).toContain("rgb(20, 120, 201)");
  expect(treeButtonStyle).toContain("rgb(21, 204, 227)");
  await expect(treeButton).toHaveCSS("border-radius", "0px");
  const craftButton = page.getByRole("button", { name: "Craft" });
  const craftStyle = await craftButton.evaluate((element) => getComputedStyle(element).backgroundImage);
  expect(craftStyle).toContain("rgb(111, 176, 242)");
  expect(craftStyle).toContain("rgb(74, 144, 230)");
  await expect(craftButton).toHaveCSS("max-width", "28%");

  // スロットと番号タグの塗り・文字を正本の高コントラストへ固定する
  // Pin slot and number-tag fills and text to the reference's high contrast
  const filledSlot = page.getByTestId("main-grid").locator("> div").first();
  await expect(filledSlot).toHaveCSS("background-color", "rgba(202, 207, 216, 0.82)");
  // アイコン欠落時のフォールバック span と区別するため個数バッジ span を明示指定する
  // Target the count-badge span explicitly to disambiguate it from the icon-fallback span
  const countBadge = filledSlot.locator('span[class*="_count_"]');
  await expect(countBadge).toHaveCSS("font-size", "10px");
  await expect(countBadge).toHaveCSS("color", "rgb(17, 17, 17)");
  const hotbarNumber = page.getByTestId("hotbar-grid").locator("span").first();
  await expect(hotbarNumber).toHaveCSS("background-color", "rgba(55, 57, 65, 0.7)");
  await expect(hotbarNumber).toHaveCSS("font-size", "10px");

  // ビューポートのホイールスクロールを残し、視覚スクロールバーだけ描画しない
  // Preserve wheel scrolling on the viewport while rendering no visual scrollbar
  const scrollRoot = page.getByTestId("item-list-grid").locator("xpath=ancestor::*[contains(@class, 'mantine-ScrollArea-root')][1]");
  const viewport = scrollRoot.locator(".mantine-ScrollArea-viewport");
  await expect(viewport).toHaveCSS("overflow-y", "scroll");
  // type="never" はスクロールバー要素を残しつつ display:none で隠すため、非表示であることを検証する
  // type="never" keeps the scrollbar elements but hides them via display:none, so assert they are hidden
  const scrollbars = scrollRoot.locator(".mantine-ScrollArea-scrollbar");
  const scrollbarCount = await scrollbars.count();
  for (let i = 0; i < scrollbarCount; i++) {
    await expect(scrollbars.nth(i)).toBeHidden();
  }
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
