import { test, expect } from "@playwright/test";
import { payloadsOf } from "../support/actions";
import { expectCraftGrip } from "../support/craftChromeAssertions";
import { resetResearch, setUiState } from "../support/mockControl";

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

test("研究報酬itemの個数をtopic payloadどおり詳細ペインで表示する", async ({ page }) => {
  await setUiState(page, "ResearchTree");
  await page.goto("/");
  // ノード選択で詳細ペインを開き、報酬個数はカードでなくペイン側に出る
  // 初期表示で中央に来る researchable ノード（報酬 item100×2）を選択する
  // Selecting the node opens the detail pane; the reward count now lives in the pane, not the card
  // Pick the researchable node (reward item100×2) which is centered on open
  await page.getByTestId("research-node-33333333-3333-4333-8333-333333333333").click();
  const pane = page.getByTestId("research-detail-pane");
  await expect(pane.getByText("2", { exact: true })).toBeVisible();
  await expectCraftGrip(pane.locator(':scope > [data-variant="craft"]'), false);
});

test("translate後のグリップ矩形だけに重なる境界buttonをexpectCraftGripが検出する", async ({ page }) => {
  await setUiState(page, "ResearchTree");
  await page.goto("/");
  await page.getByTestId("research-node-33333333-3333-4333-8333-333333333333").click();
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
  await page.getByTestId("research-node-33333333-3333-4333-8333-333333333333").click();
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
  const researchableGuid = "33333333-3333-4333-8333-333333333333";
  // 研究実行ボタンは選択時の詳細ペイン内にあるため、先にノードを選択する
  // The research button lives in the selection detail pane, so select the node first
  await page.getByTestId(`research-node-${researchableGuid}`).click();
  await page.getByTestId(`research-button-${researchableGuid}`).click();
  await expect
    .poll(async () => {
      const payloads = await payloadsOf(page, "research.complete");
      return payloads[0];
    })
    .toEqual({ researchGuid: researchableGuid });
  // mock が completed へ書換えて push → ボタンが研究済みに変わる
  // The mock rewrites the node to completed and pushes; the button flips to the completed label
  await expect(page.getByTestId(`research-button-${researchableGuid}`)).toContainText("研究済み");
});
