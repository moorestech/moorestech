import { test, expect } from "@playwright/test";
import { payloadsOf } from "../support/actions";
import { expectCraftGrip } from "../support/craftChromeAssertions";

const CRAFT_TAB_PATHS = [
  "M15 0H125L166 70H0V10H15Z",
  "M25 10H115L129 73H25Z",
  "M25 10H115V16H117V22H119V28H121V34H123V40H125V46H127V50H125V51H127V52H129V56H127V57H129V58H131V62H129V63H131V64H133V68H131V69H133V70H135V72H25Z",
  "M117 9H126V15H117ZM119 15H128V21H119ZM121 21H128V25H121ZM121 25H130V27H121ZM123 27H130V33H123ZM125 33H132V37H125ZM125 37H134V41H125ZM127 41H134V45H127ZM127 45H136V48H127ZM126 48H136V50H126ZM123 50H136V51H123ZM128 51H138V54H128ZM128 54H138V56H128ZM125 56H138V57H125ZM130 57H138V60H130ZM130 60H138V61H130ZM130 61H140V62H130ZM127 62H140V63H127ZM132 63H140V66H132ZM132 66H140V68H132ZM129 68H140V69H129ZM134 69H142V72H134ZM15 9H24V72H15Z",
  "M78 20H80V22H78ZM76 22H82V24H76ZM74 24H84V26H74ZM72 26H84V28H72ZM74 28H88V30H74ZM76 30H90V32H76ZM80 32H92V34H80ZM80 34H94V36H80ZM78 36H96V38H78ZM76 38H98V42H76ZM78 42H100V44H78ZM72 44H76V46H72ZM80 44H86V46H80ZM90 44H100V46H90ZM70 46H78V48H70ZM82 46H84V48H82ZM92 46H100V48H92ZM68 48H80V50H68ZM92 48H100V50H92ZM66 50H78V52H66ZM94 50H100V52H94ZM66 52H76V54H66ZM96 52H100V54H96ZM60 54H64V56H60ZM68 54H74V56H68ZM96 54H100V56H96ZM58 56H66V58H58ZM70 56H72V58H70ZM56 58H68V60H56ZM54 60H70V62H54ZM52 62H68V64H52ZM50 64H66V66H50ZM48 66H64V68H48ZM46 68H62V70H46ZM44 70H60V72H44Z",
];

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

test("正本のヘッダ装飾、常時スクロールバー、主要構造を保つ", async ({ page }) => {
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
  const recipeBox = page.getByTestId("craft-recipe-box");
  const craftPanel = recipeBox.locator('xpath=ancestor::*[@data-variant="craft"][1]');
  await expect(craftPanel).toBeVisible();
  await expectCraftGrip(craftPanel, false);
  // クラフトタブのSVG構造と寸法を固定する
  // Lock craft-tab SVG structure and dimensions
  const craftTab = page.getByTestId("craft-tab");
  await expect(craftTab).toHaveAttribute("viewBox", "0 0 166 70");
  await expect(craftTab).toHaveAttribute("aria-hidden", "true");
  await expect(craftTab.locator("path")).toHaveCount(5);
  const tabStyle = await craftTab.evaluate((element) => {
    const style = getComputedStyle(element);
    const renderedBounds = element.getBoundingClientRect();
    return {
      authoredWidth: style.getPropertyValue("--craft-tab-width"),
      authoredHeight: style.getPropertyValue("--craft-tab-height"),
      renderedWidth: renderedBounds.width,
      renderedHeight: renderedBounds.height,
      backgroundImage: style.backgroundImage,
      marginTop: style.marginTop,
      marginLeft: style.marginLeft,
      marginBottom: style.marginBottom,
    };
  });
  expect(tabStyle.authoredWidth).toBe("64.978px");
  expect(tabStyle.authoredHeight).toBe("27.397px");
  expect(tabStyle.renderedWidth).toBeCloseTo(64.96875, 5);
  expect(tabStyle.renderedHeight).toBeCloseTo(27.390625, 5);
  expect(tabStyle.backgroundImage).toBe("none");
  expect(tabStyle.marginTop).toBe("-37.18px");
  expect(tabStyle.marginLeft).toBe("-11px");
  expect(tabStyle.marginBottom).toBe("6.46px");
  // 全レイヤーの形状と色を固定する
  // Lock every layer's geometry and color
  const tabLayers = await craftTab.locator("path").evaluateAll((paths) => paths.map((path) => {
    const style = getComputedStyle(path);
    return { d: path.getAttribute("d"), fill: style.fill, stroke: style.stroke, strokeWidth: style.strokeWidth };
  }));
  expect(tabLayers.map((layer) => layer.d)).toEqual(CRAFT_TAB_PATHS);
  expect(tabLayers).toMatchObject([
    { fill: "rgb(51, 43, 40)", stroke: "none", strokeWidth: "1px" },
    { fill: "rgb(58, 59, 72)", stroke: "none", strokeWidth: "1px" },
    { fill: "none", stroke: "rgb(73, 75, 120)", strokeWidth: "1px" },
    { fill: "rgb(16, 15, 21)", stroke: "none", strokeWidth: "1px" },
    { fill: "rgb(75, 75, 75)", stroke: "none", strokeWidth: "1px" },
  ]);
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
