import { test, expect, type Page } from "@playwright/test";
import { payloadsOf } from "../../support/actions";
import { expectCraftGrip } from "../../support/craftChromeAssertions";

// GUID単位で前方一致に束ねる
// Group testIds by prefix per recipe GUID
const craftEntry = (page: Page) => page.locator('[data-testid^="craft-recipe-entry"]');

test("秒数は矢印の上に出し、クラフトボタンは秒数を持たない", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "CRAFT RECIPE" })).toBeVisible();
  await page.getByTestId("item-list-grid").locator("> div").first().click();

  // 秒数は矢印上、ボタンは操作名のみ
  // The duration sits above the arrow and the button carries only the action name
  await expect(craftEntry(page).locator('[data-testid$="-duration"]')).toHaveText("0.2秒");
  await expect(craftEntry(page).getByRole("button")).toHaveText("クラフト");
});

test("正本のヘッダ装飾、1段時の無スクロールバー、主要構造を保つ", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "CRAFT RECIPE" })).toBeVisible();
  await page.getByTestId("item-list-grid").locator("> div").first().click();

  // 品名直後の装飾線をヘッダ末尾に固定する
  // Keep the divider immediately after the name as the last header element
  const itemName = page.getByText("Plank", { exact: true });
  const divider = itemName.locator("xpath=following-sibling::*[1]");
  await expect(divider).toHaveAttribute("aria-hidden", "true");
  await expect(divider.locator("xpath=following-sibling::*")).toHaveCount(0);

  // 選択枠DOMを画像測定用に保つ
  // Keep the selection-frame DOM available for image measurement
  const recipeBox = page.locator('[data-testid^="craft-recipe-box"]');
  const craftPanel = recipeBox.locator('xpath=ancestor::*[@data-variant="craft"][1]');
  await expect(craftPanel).toBeVisible();
  await expectCraftGrip(craftPanel, false);
  await expect(craftEntry(page).getByRole("button")).toBeVisible();

  // 装飾タブは完全に廃止され存在しない
  // The decorative tab was fully removed and must not exist
  await expect(page.getByTestId("craft-tab")).toHaveCount(0);

  // 短いfixture(5件=1段)ではどちらのバーも出さない。個数バッジのはみ出しを内側で吸収し偽の溢れを作らない
  // A short fixture (5 items = 1 row) shows neither bar: the count badge's bleed is reserved inside, so no phantom overflow
  const scrollRoot = page.getByTestId("item-list-grid").locator("xpath=ancestor::*[contains(@class, 'mantine-ScrollArea-root')][1]");
  await expect(scrollRoot.locator('.mantine-ScrollArea-scrollbar[data-orientation="vertical"]')).toBeHidden();
  await expect(scrollRoot.locator('.mantine-ScrollArea-scrollbar[data-orientation="horizontal"]')).toBeHidden();
  const overflow = await scrollRoot.locator(".mantine-ScrollArea-viewport").evaluate((el) => ({
    y: el.scrollHeight - el.clientHeight, x: el.scrollWidth - el.clientWidth,
  }));
  expect(overflow).toEqual({ y: 0, x: 0 });
});

test("アイテム一覧は7段まで溢れず、8段目で縦バーだけが出る", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "CRAFT RECIPE" })).toBeVisible();
  const grid = page.getByTestId("item-list-grid");
  const scrollRoot = grid.locator("xpath=ancestor::*[contains(@class, 'mantine-ScrollArea-root')][1]");
  const verticalBar = scrollRoot.locator('.mantine-ScrollArea-scrollbar[data-orientation="vertical"]');
  const horizontalBar = scrollRoot.locator('.mantine-ScrollArea-scrollbar[data-orientation="horizontal"]');

  // mah=381.2 の境界を押さえる。DOM複製で段数だけを動かし、fixture件数に依存させない
  // Pin the mah=381.2 boundary by cloning cells so only the row count varies, independent of fixture size
  const fillRows = (rows: number) => grid.evaluate((el, target: number) => {
    const proto = el.children[0];
    while (el.children.length < target * 6) el.appendChild(proto.cloneNode(true));
  }, rows);

  await fillRows(7);
  await expect(verticalBar).toBeHidden();
  await expect(horizontalBar).toBeHidden();

  await fillRows(8);
  await expect(verticalBar).toBeVisible();
  // 横は常に溢れないので8段目でも水平バーは出さない
  // Horizontal never overflows, so the eighth row must not raise a horizontal bar
  await expect(horizontalBar).toBeHidden();
});

test("アイテム選択でレシピ表示、長押しで素材が尽きるまで連続クラフトする", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "CRAFT RECIPE" })).toBeVisible();
  // 右リストの先頭 Plank(100) を選択
  // Select the first item Plank(100) in the right list
  await page.getByTestId("item-list-grid").locator("> div").first().click();

  const craftButton = craftEntry(page).getByRole("button");
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
    expect((payload as { recipeGuid?: string }).recipeGuid).toBe("83000000-0000-4000-8000-000000000001");
  }
});

test("リスト上をドラッグしてもアイテム選択は変わらない（スクロール操作扱い）", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "CRAFT RECIPE" })).toBeVisible();

  // 中央は未選択のまま。ドラッグは選択ではなくスクロール操作として扱われるべき
  // Center starts unselected; a drag must count as scrolling, not selection
  const prompt = page.getByText("右のリストからアイテムを選択してください");
  await expect(prompt).toBeVisible();

  // 先頭スロット上で押下し、閾値を十分超えて上方向へドラッグしてから離す
  // Press on the first slot, drag well past the threshold upward, then release
  const firstSlot = page.getByTestId("item-list-grid").locator("> div").first();
  const box = await firstSlot.boundingBox();
  if (box === null) throw new Error("first slot has no bounding box");
  await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
  await page.mouse.down();
  await page.mouse.move(box.x + box.width / 2, box.y - 60, { steps: 8 });
  await page.mouse.up();

  // ドラッグ確定のためタップ選択は発火せず、未選択プロンプトが残る
  // The drag commits so no tap-selection fires; the unselected prompt remains
  await expect(prompt).toBeVisible();

  // 対照として、単純クリック（移動なし）はタップ選択として発火する
  // As a control, a plain click (no movement) fires as a tap selection
  await firstSlot.click();
  await expect(prompt).toHaveCount(0);
});

test("押下後にボタンから外れるとクラフトが止まり経過時間がリセットされる", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "CRAFT RECIPE" })).toBeVisible();
  await page.getByTestId("item-list-grid").locator("> div").first().click();

  const craftButton = craftEntry(page).getByRole("button");
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

test("複数レシピはクラフト優先の単一リストで同時に表示される", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "CRAFT RECIPE" })).toBeVisible();
  // Plank100はクラフト/機械両方
  // Plank(100) has both craft and machine recipes
  await page.getByTestId("item-list-grid").locator("> div").first().click();

  const list = page.getByTestId("recipe-entry-list");
  const entries = list.locator('[data-testid*="-recipe-entry-"]');

  // 先頭は常にcraftエントリ
  // Leading entry is always craft
  await expect(entries.first()).toHaveAttribute("data-testid", /^craft-recipe-entry-/);
  // 機械エントリも同一リストに存在
  // Machine entry exists in the same list
  await expect(list.locator('[data-testid^="machine-recipe-entry"]').first()).toBeVisible();

  // タブ・ページャは廃止され存在しない
  // The tab/pager UI was removed and must not exist
  await expect(page.locator(".mantine-Tabs-root")).toHaveCount(0);
});

test("クラフトエントリが複数でもチュートリアルアンカーは1件だけ付く", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "CRAFT RECIPE" })).toBeVisible();
  // Plank102はクラフト2件持つ
  // Plank(102) has two craft recipes
  await page.getByTestId("item-list-grid").locator('[data-item-id="102"]').click();

  const list = page.getByTestId("recipe-entry-list");
  await expect(list.locator('[data-testid^="craft-recipe-entry"]')).toHaveCount(2);
  await expect(list.locator('[data-tutorial-anchor~="recipe.craft-button"]')).toHaveCount(1);

  // GUID単位で2件目を厳密指定
  // Exact-match the second recipe by its GUID testId
  const secondEntry = list.getByTestId("craft-recipe-entry-83000000-0000-4000-8000-000000000004");
  await expect(secondEntry.locator('[data-testid$="-duration"]')).toHaveText("0.4秒");
  await expect(secondEntry.getByRole("button")).toHaveText("クラフト");
});

test("クラフト可能数0のアイテムは個数バッジを出さない", async ({ page }) => {
  await page.goto("/");
  const grid = page.getByTestId("item-list-grid");
  await expect(grid).toBeVisible();

  // 101は常に0個、100はバッジ出る
  // 101 always yields 0; 100 shows the count badge
  await expect(grid.locator('[data-item-id="101"]').locator('[class*="_count_"]')).toHaveCount(0);
  await expect(grid.locator('[data-item-id="100"]').locator('[class*="_count_"]')).toHaveCount(1);
});
