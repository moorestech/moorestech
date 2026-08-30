import { test, expect } from "@playwright/test";
import type { Page } from "@playwright/test";
import { payloadsOf } from "../../support/actions";
import { expectCraftGrip } from "../../support/craftChromeAssertions";
import { expectHitTestWithin, expectSeparatedHorizontally } from "../../support/layoutAssertions";
import { resetResearch, setTopicScenario, setUiState } from "../../support/mockControl";
import { researchableNodeGuid, itemLackingNodeGuid } from "../../mock-host/researchFixtures";

// 各テスト後に研究ツリー・ui_state・このファイルが動かしたtopicシナリオを既定へ戻し、状態漏れを防ぐ(W9)
// After each test, reset the research tree, ui_state, and every topic scenario this file can touch (W9)
test.afterEach(async ({ page }) => {
  await resetResearch(page);
  await setUiState(page, "PlayerInventory");
  await setTopicScenario(page, "challengeActive");
  await setTopicScenario(page, "miningHidden");
  await setTopicScenario(page, "japanese");
});

test("research tree renders nodes when uiState enters ResearchTree", async ({ page }) => {
  await setUiState(page, "ResearchTree");
  await page.goto("/");
  await expect(page.getByTestId("research-tree")).toBeVisible();
  await expect(page.getByTestId("research-node-11111111-1111-4111-8111-111111111111")).toBeVisible();
});

test("ノードカードが4状態のdata属性で描き分けられる", async ({ page }) => {
  // 所持シナリオを明示設定する(共有mock hostの既定値への暗黙依存を断つ・W10)
  // Explicitly set the owned-items scenario, rather than relying on the shared mock host's implicit default (W10)
  await setTopicScenario(page, "researchOwnedItems");
  await setUiState(page, "ResearchTree");
  await page.goto("/");
  const completed = page.getByTestId("research-node-11111111-1111-4111-8111-111111111111");
  const locked = page.getByTestId("research-node-22222222-2222-4222-8222-222222222222");
  const ready = page.getByTestId(`research-node-${researchableNodeGuid}`);
  const lacking = page.getByTestId(`research-node-${itemLackingNodeGuid}`);
  await expect(completed).toHaveAttribute("data-completed", "true");
  await expect(locked).toHaveAttribute("data-locked", "true");
  await expect(ready).toHaveAttribute("data-ready", "true");
  await expect(lacking).not.toHaveAttribute("data-ready", "true");
  await expect(lacking).not.toHaveAttribute("data-locked", "true");
});

test("研究報酬itemの個数をtopic payloadどおり詳細ペインで表示する", async ({ page }) => {
  await setUiState(page, "ResearchTree");
  await page.goto("/");
  // ノード選択で詳細ペインを開き、報酬個数はカードでなくペイン側に出る
  // 中央寄せ対象ノードを選択
  // Selecting the node opens the detail pane; the reward count now lives in the pane, not the card
  // Pick the centered target node
  await page.getByTestId(`research-node-${researchableNodeGuid}`).click();
  const pane = page.getByTestId("research-detail-pane");
  await expect(pane.getByText("2", { exact: true })).toBeVisible();
  await expectCraftGrip(pane.locator(':scope > [data-variant="craft"]'), false);
});

test("詳細ペインに解放物が種類別ラベル付きで並ぶ", async ({ page }) => {
  await setUiState(page, "ResearchTree");
  await page.goto("/");
  await page.getByTestId(`research-node-${researchableNodeGuid}`).click();
  const pane = page.getByTestId("research-detail-pane");
  await expect(pane.getByTestId("research-consume-items")).toBeVisible();
  await expect(pane.getByTestId("research-unlock-blocks")).toBeVisible();
  await expect(pane.getByTestId("research-unlock-machine-recipes")).toBeVisible();
  await expect(pane.getByTestId("research-reward-items")).toBeVisible();
  await expect(pane.getByTestId("research-unlock-others")).toBeVisible();
  // 空種類非表示(ノード3)
  // Empty kinds stay hidden (node 3)
  await expect(pane.getByTestId("research-unlock-items")).toHaveCount(0);
});

test("translate後のグリップ矩形だけに重なる境界buttonをexpectCraftGripが検出する", async ({ page }) => {
  await setUiState(page, "ResearchTree");
  await page.goto("/");
  await page.getByTestId(`research-node-${researchableNodeGuid}`).click();
  const pane = page.getByTestId("research-detail-pane");
  const craftPanel = pane.locator(':scope > [data-variant="craft"]');

  // 旧矩形の外かつ0.4px対角translate後だけに入る可視buttonを注入する
  // Inject a visible button outside the old box but inside only after the 0.4px diagonal translate
  await craftPanel.evaluate((element) => {
    const button = document.createElement("button");
    button.setAttribute("aria-label", "grip-overlap-probe");
    button.style.position = "absolute";
    button.style.right = "6.7px";
    button.style.bottom = "6.7px";
    button.style.width = "0.25px";
    button.style.height = "0.25px";
    button.style.padding = "0";
    button.style.border = "0";
    button.style.background = "rgb(255 0 0)";
    element.appendChild(button);
  });

  await expectCraftGrip(craftPanel, true);
});

test("padding box外の境界buttonをexpectCraftGripが重なりなしと判定する", async ({ page }) => {
  await setUiState(page, "ResearchTree");
  await page.goto("/");
  await page.getByTestId(`research-node-${researchableNodeGuid}`).click();
  const craftPanel = page.getByTestId("research-detail-pane").locator(':scope > [data-variant="craft"]');

  // padding box基準の実矩形より右下へ外れる境界buttonを置く
  // Place a boundary button beyond the actual padding-box-based rectangle
  await craftPanel.evaluate((element) => {
    const button = document.createElement("button");
    button.style.position = "absolute";
    button.style.right = "5.7px";
    button.style.bottom = "5.7px";
    button.style.width = "0.25px";
    button.style.height = "0.25px";
    button.style.padding = "0";
    button.style.border = "0";
    element.appendChild(button);
  });

  await expectCraftGrip(craftPanel, false);
});

test("research button sends research.complete and node becomes completed", async ({ page }) => {
  await resetResearch(page);
  await setUiState(page, "ResearchTree");
  await page.goto("/");
  // 研究実行ボタンは選択時の詳細ペイン内にあるため、先にノードを選択する
  // The research button lives in the selection detail pane, so select the node first
  await page.getByTestId(`research-node-${researchableNodeGuid}`).click();
  await page.getByTestId(`research-button-${researchableNodeGuid}`).click();
  await expect
    .poll(async () => {
      const payloads = await payloadsOf(page, "research.complete");
      return payloads[0];
    })
    .toEqual({ researchGuid: researchableNodeGuid });
  // mock が completed へ書換えて push → ボタンが完了済みに変わる
  // The mock rewrites the node to completed and pushes; the button flips to the completed label
  await expect(page.getByTestId(`research-button-${researchableNodeGuid}`)).toContainText("完了済み");
});

test("研究パネルは持ち物の右隣から画面端までを占有し持ち物と重ならない", async ({ page }) => {
  await setUiState(page, "ResearchTree");
  await page.goto("/");
  const tree = page.getByTestId("research-tree");
  const stageBox = await page.getByTestId("app-stage").boundingBox();
  const treeBox = await tree.boundingBox();
  const inventoryBox = await page.getByTestId("main-grid").boundingBox();
  // 上右下はstage端に密着
  // Top/right/bottom hug the stage edges
  expect(Math.abs(treeBox!.y - stageBox!.y)).toBeLessThan(1.5);
  expect(Math.abs(treeBox!.x + treeBox!.width - (stageBox!.x + stageBox!.width))).toBeLessThan(1.5);
  expect(Math.abs(treeBox!.y + treeBox!.height - (stageBox!.y + stageBox!.height))).toBeLessThan(1.5);
  // 左端は「持ち物幅 + 列gap」のscale後の位置。GamePanelにtestIdが無いため、寸法の正であるtokens.cssの値を
  // stageから実測して突き合わせる（数値をテストへ焼くと持ち物スロット倍率の変更のたびに嘘になる）
  // The left edge is the scaled position of "inventory width + column gap". GamePanel exposes no testId, so read
  // the authoritative token values off the stage instead of baking numbers that rot whenever the slot scale moves
  const scale = await stageScale(page);
  const { inventoryPanelWidth, stageColumnGap } = await page.getByTestId("app-stage").evaluate((stage) => {
    const probe = document.createElement("div");
    probe.style.cssText = "position:absolute;visibility:hidden;width:var(--inventory-panel-width)";
    stage.appendChild(probe);
    // offsetWidthはレイアウトpx。getBoundingClientRectはstageのtransform:scaleが乗るため使わない
    // offsetWidth is layout px; getBoundingClientRect would already carry the stage's transform: scale
    const inventoryPanelWidth = probe.offsetWidth;
    probe.remove();
    return { inventoryPanelWidth, stageColumnGap: parseFloat(getComputedStyle(stage).columnGap) };
  });
  expect(treeBox!.x - stageBox!.x).toBeCloseTo((inventoryPanelWidth + stageColumnGap) * scale, 0);
  // 重なり無しを二重に確認
  // Double-check non-overlap
  await expectSeparatedHorizontally(page.getByTestId("main-grid"), tree);
  expect(inventoryBox!.x + inventoryBox!.width).toBeLessThanOrEqual(treeBox!.x);
  // 持ち物はクリック可能なまま
  // The inventory stays clickable
  await page.getByTestId("main-grid").locator(":scope > *").first().click({ trial: true });
  await expect(page.getByTestId("key-hints")).toBeVisible();
});

test("研究画面では持ち物がstage左paddingぶん左へ寄る", async ({ page }) => {
  await setUiState(page, "PlayerInventory");
  await page.goto("/");
  const onInventoryScreen = await page.getByTestId("main-grid").boundingBox();
  // 研究画面では.stageの左paddingが0へ落ちるため、比較対象のpadding値は切替前に読む
  // The research screen zeroes the stage's left padding, so read the value to compare against before switching
  const stagePaddingLeft = await page.getByTestId("app-stage").evaluate((stage) => parseFloat(getComputedStyle(stage).paddingLeft));
  await setUiState(page, "ResearchTree");
  await expect(page.getByTestId("research-tree")).toBeVisible();
  const onResearchScreen = await page.getByTestId("main-grid").boundingBox();
  // stage拡縮がかかるため、左paddingのscale後の値と突き合わせる
  // The stage is scaled, so compare against the scaled left padding
  const scale = await stageScale(page);
  expect(onInventoryScreen!.x - onResearchScreen!.x).toBeCloseTo(stagePaddingLeft * scale, 0);
  // 縦位置は動かさない
  // The vertical position does not move
  expect(Math.abs(onResearchScreen!.y - onInventoryScreen!.y)).toBeLessThan(1.5);
});

test("研究パネル展開中も常駐チャレンジHUDとキーヒントが遮蔽されない", async ({ page }) => {
  // pointer-events: none のためclick trialが使えず、中心座標のhit-testで遮蔽有無を検証する
  // Click trial cannot be used because the HUD is pointer-events: none, so hit-test its center point instead
  await setTopicScenario(page, "challengeActive");
  await setUiState(page, "ResearchTree");
  await page.goto("/");
  await expect(page.getByTestId("research-tree")).toBeVisible();
  const challengeHud = page.getByTestId("challenge-hud");
  await expect(challengeHud).toBeVisible();
  await expectHitTestWithin(challengeHud);
  await expectHitTestWithin(page.getByTestId("key-hints"));
});

test("研究パネル展開中も採掘進捗バーが遮蔽されない（.viewportOverlayのz封じ込めに依存する回帰ガード）", async ({ page }) => {
  // ChallengeHud/研究キーヒントは自身が--z-stage-overlay-panel(-chrome)を明示するため.viewportOverlayの
  // z付与が無くても偶然勝つ。採掘進捗バーは--z-stage-screen(20)しか持たず研究パネルの--z-stage-overlay-panel(30)に
  // 単独では負けるため、この一本だけがApp.module.css:59のz-index行に実際に依存する
  // ChallengeHud and the research key hints each declare their own --z-stage-overlay-panel(-chrome), so they
  // happen to win even without .viewportOverlay's z-index. The mining progress bar only carries
  // --z-stage-screen(20) and alone loses to the research panel's --z-stage-overlay-panel(30), so it is the one
  // assertion that genuinely depends on the z-index line at App.module.css:59
  await setUiState(page, "ResearchTree");
  await page.goto("/");
  await expect(page.getByTestId("research-tree")).toBeVisible();
  await setTopicScenario(page, "mining");
  const progressBar = page.getByTestId("progress-bar");
  await expect(progressBar).toBeVisible();
  await expectHitTestWithin(progressBar);
});

test("研究画面ではホットバーと装備HUDを描画しない", async ({ page }) => {
  await setUiState(page, "ResearchTree");
  await page.goto("/");
  await expect(page.getByTestId("research-tree")).toBeVisible();
  await expect(page.getByTestId("hotbar-grid")).toHaveCount(0);
  await expect(page.getByTestId("equipment-slots")).toHaveCount(0);

  // 持ち物画面で両HUD復帰
  // Both HUDs return on the inventory screen
  await setUiState(page, "PlayerInventory");
  await expect(page.getByTestId("hotbar-grid")).toBeVisible();
  await expect(page.getByTestId("equipment-slots")).toBeVisible();
});

// stageは実viewportへ一様拡縮されるため、基準stage座標のpx期待値はこの倍率を掛けて突き合わせる
// The stage is uniformly scaled into the real viewport, so px expectations in stage coordinates are multiplied by this factor
async function stageScale(page: Page): Promise<number> {
  return page.getByTestId("app-stage").evaluate((element) => new DOMMatrixReadOnly(getComputedStyle(element).transform).a);
}
