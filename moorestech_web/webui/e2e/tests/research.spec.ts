import { test, expect } from "@playwright/test";

type ActionRecord = { type: string; payload: unknown };

// 各テスト後に研究ツリーと ui_state を既定へ戻し、状態漏れを防ぐ
// Reset the research tree and ui_state to defaults after each test to prevent state leakage
test.afterEach(async ({ page }) => {
  await page.request.get("/__research");
  await page.request.get("/__uistate?state=PlayerInventory");
});

test("research tree renders nodes when uiState enters ResearchTree", async ({ page }) => {
  await page.request.get("/__uistate?state=ResearchTree");
  await page.goto("/");
  await expect(page.getByTestId("research-tree")).toBeVisible();
  await expect(page.getByTestId("research-node-11111111-1111-1111-1111-111111111111")).toBeVisible();
});

test("research button sends research.complete and node becomes completed", async ({ page }) => {
  await page.request.get("/__research");
  await page.request.get("/__uistate?state=ResearchTree");
  await page.goto("/");
  const researchableGuid = "33333333-3333-3333-3333-333333333333";
  await page.getByTestId(`research-button-${researchableGuid}`).click();
  await expect
    .poll(async () => {
      const actions: ActionRecord[] = await page.request.get("/__actions").then((r) => r.json());
      return actions.find((a) => a.type === "research.complete")?.payload;
    })
    .toEqual({ researchGuid: researchableGuid });
  // mock が completed へ書換えて push → ボタンが研究済みに変わる
  // The mock rewrites the node to completed and pushes; the button flips to the completed label
  await expect(page.getByTestId(`research-button-${researchableGuid}`)).toContainText("研究済み");
});
