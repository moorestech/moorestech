import { expect, test } from "@playwright/test";
import { setTopicScenario, setUiState } from "../../support/mockControl";
import { expectAtViewportTopCorner } from "../../support/layoutAssertions";
import { expectCraftFramedPlacementHud } from "../../support/operationHudAssertions";

// 共有UI状態を各テスト後に戻す
// Reset shared UI state after every test
test.afterEach(async ({ page }) => {
  await setTopicScenario(page, "placementEmpty");
  await setUiState(page, "PlayerInventory");
});

test("配置モードHUDを右上のクラフト枠で表示する", async ({ page }) => {
  await setTopicScenario(page, "placement");
  await setUiState(page, "PlaceBlock");
  await page.goto("/");

  const hud = page.getByTestId("placement-mode-hud");
  await expect(hud).toContainText("Placement Mode");
  await expect(hud).toContainText("Selected: Assembler");
  await expect(hud).toContainText("Height: 3");
  await expectCraftFramedPlacementHud(hud);

  // 配置不能理由も同じ警告階層へ反映する
  // Reflect placement failures in the same warning hierarchy
  await setTopicScenario(page, "placementUnavailable");
  await expect(hud.getByTestId("operation-mode-warning")).toHaveText("Blocked by terrain");
  await expect(hud.getByTestId("operation-mode-warning")).toHaveCSS("color", "rgb(255, 120, 120)");
});

test("配置対象connectToolをGuidだけの配信から辞書表示名へ解決する", async ({ page }) => {
  await setTopicScenario(page, "placementConnectTool");
  await setUiState(page, "PlaceBlock");
  await page.goto("/");

  // ホストはlabelを運ばずGuidのみ配信する（表示名の正はWeb辞書）
  // The host ships no label and delivers only the GUID; the web dictionary owns the display name
  await expect(page.getByTestId("placement-mode-hud")).toContainText("電線接続ツール");
});

test("配置対象trainCarをGuidだけの配信から辞書表示名へ解決する", async ({ page }) => {
  await setTopicScenario(page, "placementTrainCar");
  await setUiState(page, "PlaceBlock");
  await page.goto("/");

  // 車両もaddressablePath末尾ではなくマスタnameの辞書解決で表示する
  // Train cars display via the master name dictionary, not the addressablePath tail
  await expect(page.getByTestId("placement-mode-hud")).toContainText("貨物車両");
});

test("横長画面でも配置モードHUDを実画面右上へ固定する", async ({ page }) => {
  await page.setViewportSize({ width: 2432, height: 786 });
  await setTopicScenario(page, "placement");
  await setUiState(page, "PlaceBlock");
  await page.goto("/");

  // stage端への後退を端距離で検出する
  // Catch regressions to the stage edge through the real viewport gap
  const hud = page.getByTestId("placement-mode-hud");
  await expectAtViewportTopCorner(hud, "right", 40);
  const hudBox = await hud.boundingBox();
  expect(hudBox).not.toBeNull();
  expect(hudBox!.width).toBeCloseTo(288 * 786 / 720, 1);
});

test("削除モードをuGUI準拠の上下警告帯だけで表示する", async ({ page }) => {
  await setUiState(page, "DeleteBar");
  await page.goto("/");

  const warning = page.getByTestId("delete-mode-warning");
  await expect(warning).toBeVisible();
  await expect(warning).toHaveAttribute("role", "status");
  await expect(warning).toHaveAttribute("aria-label", "Delete Mode");
  await expect(warning).toHaveCSS("pointer-events", "none");
  await expect(page.getByTestId("delete-mode-hud")).toHaveCount(0);

  const bands = warning.getByTestId("delete-mode-warning-band");
  await expect(bands).toHaveCount(2);
  await expect(bands.first()).toHaveCSS("top", "0px");
  await expect(bands.last()).toHaveCSS("bottom", "0px");
  // アンカーはトークン列なので and() でトークン一致を見る
  // The anchor attribute is a token list, so match by token via and()
  await expect(bands.last().and(page.locator('[data-tutorial-anchor~="delete.hud"]'))).toHaveCount(1);
  await expect(bands.last()).not.toHaveAttribute("aria-hidden");
  const expectedPattern = "repeating-linear-gradient(117deg, rgb(255, 187, 36) 0px, rgb(255, 187, 36) 32px, rgb(0, 0, 0) 32px, rgb(0, 0, 0) 64px)";
  for (const band of await bands.all()) {
    await expect(band).toHaveCSS("height", "20px");
    const background = await band.evaluate((element) => getComputedStyle(element).backgroundImage);
    expect(background).toBe(expectedPattern);
  }
});
