import { test, expect, type Locator, type Page } from "@playwright/test";
import { allActions } from "../support/actions";
import { setSkitStage, setUiState } from "../support/mockControl";

// ポーズ中は会話窓・選択肢・ツールの全入力口がWeb側で閉じ、再開後は開く
// While paused every skit input (window, choices, tools) closes on the Web side, and reopens on resume
const SKIT_ACTIONS = ["skit.advance", "skit.select", "skit.set_auto", "skit.skip", "skit.set_ui_hidden"];

test.beforeEach(async ({ page }) => {
  await setSkitStage(page, "none");
});

test.afterEach(async ({ page }) => {
  await setSkitStage(page, "none");
  await setUiState(page, "GameScreen");
});

async function skitActionCount(page: Page): Promise<number> {
  return (await allActions(page)).filter((action) => SKIT_ACTIONS.includes(action.type)).length;
}

// locator.clickは被さった要素への操作を待って拒否するので、中心座標へ実マウスを直接押す。押す前に最前面がスキット内かを返す
// locator.click waits on and refuses covered targets, so press the real mouse at the center; report whether the topmost element was inside the skit
async function clickCenterDirectly(page: Page, target: Locator): Promise<boolean> {
  const box = await target.boundingBox();
  expect(box).not.toBeNull();
  const x = box!.x + box!.width / 2;
  const y = box!.y + box!.height / 2;
  const topmostInsideSkit = await page.evaluate(([px, py]) => {
    const topmost = document.elementFromPoint(px, py);
    return topmost !== null && topmost.closest('[data-skit-root="true"]') !== null;
  }, [x, y]);
  await page.mouse.click(x, y);
  return topmostInsideSkit;
}

// 会話窓とツールバーを共に含む最小の祖先をスキットUIの根として印を付ける
// Mark the smallest ancestor holding both the window and the toolbar as the skit UI root
async function markSkitRoot(page: Page) {
  await page.getByTestId("blocking-skit").evaluate((element) => {
    const toolbarButton = document.querySelector('button[aria-label="自動"]');
    let node: Element | null = element;
    while (node && toolbarButton && !node.contains(toolbarButton)) node = node.parentElement;
    node?.setAttribute("data-skit-root", "true");
  });
}

test("ポーズ中は会話窓とツールを実クリックしても全文表示も操作も出さず、再開後は操作できる", async ({ page }) => {
  await page.goto("/");
  await setSkitStage(page, "text");
  const skit = page.getByTestId("blocking-skit");
  await expect(skit).toBeVisible();
  await markSkitRoot(page);
  await setUiState(page, "Story", "PauseMenuScreen");
  await expect(page.getByTestId("pause-menu")).toBeVisible();

  // 文字送り中の窓クリックは全文表示になり得るので、表示が進まないことも見る
  // A window click while typing could reveal the full body, so also check the text does not jump
  const countBefore = await skitActionCount(page);
  const targets = [skit, ...["自動", "スキップ", "UIを非表示"].map((name) => page.getByRole("button", { name }))];
  const topmostInsideSkit: boolean[] = [];
  for (const target of targets) topmostInsideSkit.push(await clickCenterDirectly(page, target));
  expect(topmostInsideSkit, "窓・自動・スキップ・UI非表示の最前面").toEqual([false, false, false, false]);
  await page.waitForTimeout(300);
  await expect(skit).not.toContainText("Blocking message");
  await expect(skit).toBeVisible();
  expect(await skitActionCount(page)).toBe(countBefore);

  // 再開後は窓クリックで全文表示し、ツールも操作を送る
  // After resuming, a window click reveals the full body and the tools send actions
  await setUiState(page, "Story", "GameScreen");
  await expect(page.getByTestId("pause-menu")).toHaveCount(0);
  expect(await clickCenterDirectly(page, skit)).toBe(true);
  await expect(skit).toContainText("Blocking message");
  expect(await clickCenterDirectly(page, page.getByRole("button", { name: "自動" }))).toBe(true);
  await expect.poll(() => skitActionCount(page)).toBe(countBefore + 1);
});

test("ポーズ中は選択肢を実クリックしても選ばず、再開後は選べる", async ({ page }) => {
  await page.goto("/");
  await setSkitStage(page, "choices");
  const routeB = page.getByRole("button", { name: "Route B" });
  await expect(routeB).toBeVisible();
  await markSkitRoot(page);
  await setUiState(page, "Story", "PauseMenuScreen");
  await expect(page.getByTestId("pause-menu")).toBeVisible();

  const countBefore = await skitActionCount(page);
  expect(await clickCenterDirectly(page, routeB)).toBe(false);
  await page.waitForTimeout(300);
  await expect(routeB).toBeVisible();
  expect(await skitActionCount(page)).toBe(countBefore);

  await setUiState(page, "Story", "GameScreen");
  await expect(page.getByTestId("pause-menu")).toHaveCount(0);
  expect(await clickCenterDirectly(page, routeB)).toBe(true);
  await expect.poll(() => skitActionCount(page)).toBe(countBefore + 1);
});
