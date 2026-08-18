import { test, expect, type Locator } from "@playwright/test";
import { payloadsOf } from "../../support/actions";
import { expectCraftGrip } from "../../support/craftChromeAssertions";
import { resetResearch, setTopicScenario, setUiState } from "../../support/mockControl";
import { researchableNodeGuid, itemLackingNodeGuid } from "../../mock-host/researchFixtures";

// 中心座標のhit-testが対象要素の子孫を指すか（=遮蔽されていないか）を検証する。
// HUD自体はpointer-events:noneでelementFromPointが素通りするため、判定中だけ一時的にautoへ戻す
// Verify the element under the point's center is a descendant of the target (i.e. not occluded).
// The HUD is pointer-events: none so elementFromPoint would skip past it; flip it to auto only for the measurement
async function expectHitTestWithin(locator: Locator) {
  const isUnoccluded = await locator.evaluate((element) => {
    const originalPointerEvents = element.style.pointerEvents;
    element.style.pointerEvents = "auto";
    const rect = element.getBoundingClientRect();
    const target = document.elementFromPoint(rect.left + rect.width / 2, rect.top + rect.height / 2);
    element.style.pointerEvents = originalPointerEvents;
    return target !== null && element.contains(target);
  });
  expect(isUnoccluded).toBe(true);
}

// 各テスト後に研究ツリーと ui_state を既定へ戻し、状態漏れを防ぐ
// Reset the research tree and ui_state to defaults after each test to prevent state leakage
test.afterEach(async ({ page }) => {
  await resetResearch(page);
  await setUiState(page, "PlayerInventory");
});

test("research tree renders nodes when uiState enters ResearchTree", async ({ page }) => {
  await setUiState(page, "ResearchTree");
  await page.goto("/");
  await expect(page.getByTestId("research-tree")).toBeVisible();
  await expect(page.getByTestId("research-node-11111111-1111-4111-8111-111111111111")).toBeVisible();
});

test("ノードカードが4状態のdata属性で描き分けられる", async ({ page }) => {
  await setUiState(page, "ResearchTree");
  await page.goto("/");
  const completed = page.getByTestId("research-node-11111111-1111-4111-8111-111111111111");
  const locked = page.getByTestId("research-node-22222222-2222-4222-8222-222222222222");
  const ready = page.getByTestId(`research-node-${researchableNodeGuid}`);
  const lacking = page.getByTestId(`research-node-${itemLackingNodeGuid}`);
  await expect(completed).toHaveAttribute("data-completed", "true");
  await expect(locked).toHaveAttribute("data-locked", "true");
  await expect(ready).toHaveAttribute("data-researchable", "true");
  await expect(lacking).not.toHaveAttribute("data-researchable", "true");
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
  // 空種類のセクションは出ない（ノード3はunlockItemIdsが空）
  // Empty kinds render nothing (node 3 has no unlockItemIds)
  await expect(pane.getByTestId("research-unlock-craft-recipes")).toHaveCount(0);
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
  // mock が completed へ書換えて push → ボタンが研究済みに変わる
  // The mock rewrites the node to completed and pushes; the button flips to the completed label
  await expect(page.getByTestId(`research-button-${researchableNodeGuid}`)).toContainText("研究済み");
});

test("研究パネルはステージ全域を占有し持ち物とキーヒントが上に重なる", async ({ page }) => {
  await setUiState(page, "ResearchTree");
  await page.goto("/");
  const tree = page.getByTestId("research-tree");
  const stageBox = await page.locator(".stage, [class*='stage']").first().boundingBox();
  const treeBox = await tree.boundingBox();
  // stage全域一致（一様スケール後の実px。誤差1px許容）
  // Full-stage match in post-scale pixels with 1px tolerance
  expect(Math.abs(treeBox!.x - stageBox!.x)).toBeLessThan(1.5);
  expect(Math.abs(treeBox!.y - stageBox!.y)).toBeLessThan(1.5);
  expect(Math.abs(treeBox!.width - stageBox!.width)).toBeLessThan(1.5);
  expect(Math.abs(treeBox!.height - stageBox!.height)).toBeLessThan(1.5);
  // 持ち物パネルとキーヒントは可視のまま（重畳・裁定2026-08-18）
  // Inventory panel and key hints stay visible on top (adjudicated 2026-08-18)
  await expect(page.getByTestId("main-grid")).toBeVisible();
  await expect(page.getByTestId("research-key-hints")).toBeVisible();
  // 持ち物グリッドがクリックを受ける（最前面確認。trialは重なり判定のみ行う）
  // The inventory grid receives clicks (front-most check; trial only verifies hit-testing)
  await page.getByTestId("main-grid").locator(":scope > *").first().click({ trial: true });
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
  await expectHitTestWithin(page.getByTestId("research-key-hints"));
  await setTopicScenario(page, "japanese");
});

test("研究パネル展開中も採掘進捗バーが遮蔽されない（.viewportOverlayのz封じ込めに依存する回帰ガード）", async ({ page }) => {
  // ChallengeHud/研究キーヒントは自身が--z-overlay-panel(-chrome)を明示するため.viewportOverlayの
  // z付与が無くても偶然勝つ。採掘進捗バーは--z-screen(20)しか持たず研究パネルの--z-overlay-panel(30)に
  // 単独では負けるため、この一本だけがApp.module.css:59のz-index行に実際に依存する
  // ChallengeHud and the research key hints each declare their own --z-overlay-panel(-chrome), so they
  // happen to win even without .viewportOverlay's z-index. The mining progress bar only carries
  // --z-screen(20) and alone loses to the research panel's --z-overlay-panel(30), so it is the one
  // assertion that genuinely depends on the z-index line at App.module.css:59
  await setUiState(page, "ResearchTree");
  await page.goto("/");
  await expect(page.getByTestId("research-tree")).toBeVisible();
  await setTopicScenario(page, "mining");
  const progressBar = page.getByTestId("progress-bar");
  await expect(progressBar).toBeVisible();
  await expectHitTestWithin(progressBar);
  await setTopicScenario(page, "miningHidden");
});
