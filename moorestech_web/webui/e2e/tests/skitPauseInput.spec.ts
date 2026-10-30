import { test, expect, type Locator, type Page } from "@playwright/test";
import { allActions } from "../support/actions";
import { setSkitStage, setUiState } from "../support/mockControl";

// ポーズで全会話入力を閉じ再開で開く
// Close all skit inputs on pause and reopen them on resume
const SKIT_ACTIONS = ["skit.advance", "skit.select", "skit.set_auto", "skit.skip", "skit.set_ui_hidden"];

test.beforeEach(async ({ page }) => {
  await setSkitStage(page, "none");
});

// 後続specへ漏らさないよう、mock既定の持ち物画面へ戻す
// Restore the mock's default inventory screen so nothing leaks into later specs
test.afterEach(async ({ page }) => {
  await setSkitStage(page, "none");
  await setUiState(page, "PlayerInventory");
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

// 窓とツールバーの最小共通祖先を根とする
// Use the smallest common ancestor of the window and toolbar as the root
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

  // 再開後は窓で全文表示・ツールで送信
  // On resume, the window reveals the body and tools send actions
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

for (const emptyIntents of [false, true]) {
  test(`フォーカス後のポーズ中はEnterで全文表示しない（許可空=${emptyIntents}）`, async ({ page }) => {
    // 空の許可集合でもローカル全文表示の入口を検査する
    // Exercise the local reveal entry even with an empty allowed-intent set
    await page.routeWebSocket("**/ws", (socket) => {
      const server = socket.connectToServer();
      server.onMessage((message) => {
        const packet = JSON.parse(String(message));
        if (emptyIntents && packet.topic === "skit.presentation" && packet.data) packet.data.allowedIntents = [];
        socket.send(JSON.stringify(packet));
      });
    });
    await page.goto("/");
    await page.clock.install();
    await page.clock.pauseAt(new Date(Date.now() + 1000));
    await setSkitStage(page, "text");
    const skit = page.getByTestId("blocking-skit");
    await expect(skit).toBeVisible();
    await page.clock.runFor(1000);
    await expect(skit).toContainText("B");
    await skit.focus();
    await expect(skit).toBeFocused();

    // 時計を止め、自然な文字送りとEnterの全文表示を区別する
    // Freeze the clock to distinguish natural typing from Enter's immediate reveal
    await setUiState(page, "Story", "PauseMenuScreen");
    await expect(page.getByTestId("pause-menu")).toBeVisible();
    await expect(skit).toBeFocused();
    await expect(skit).toHaveAttribute("tabindex", "-1");
    const textBefore = await skit.textContent();
    const countBefore = await skitActionCount(page);
    await page.keyboard.press("Enter");
    await expect(skit).toHaveText(textBefore!);
    await expect(skit).not.toContainText("Blocking message");
    expect(await skitActionCount(page)).toBe(countBefore);

    // 再開時は同じEnterで全文表示でき、操作送信は増えない
    // On resume the same Enter reveals the body without sending an action
    await setUiState(page, "Story", "GameScreen");
    await expect(page.getByTestId("pause-menu")).toHaveCount(0);
    await page.keyboard.press("Enter");
    await expect(skit).toContainText("Blocking message");
    expect(await skitActionCount(page)).toBe(countBefore);
  });
}
