import { test, expect } from "@playwright/test";
import { resetResearch, setUiState } from "../../support/mockControl";
import { researchableNodeGuid } from "../../mock-host/researchFixtures";

// 研究ツリーとui_stateを既定へ戻し、他のspecへ状態を漏らさない
// Reset the research tree and ui_state so no state leaks into other specs
test.afterEach(async ({ page }) => {
  await resetResearch(page);
  await setUiState(page, "PlayerInventory");
});

test("研究ノードの詳細にスロット上限と製作上限の解放行が出る", async ({ page }) => {
  await setUiState(page, "ResearchTree");
  await page.goto("/");
  await page.getByTestId(`research-node-${researchableNodeGuid}`).click();

  // 「解放: その他」の節に、上限値と製作上限レベルが文として出る
  // The "Unlocks: Others" section shows the limit value and the craft-cap level as sentences
  const others = page.getByTestId("research-detail-pane").getByTestId("research-unlock-others");
  await expect(others.getByTestId("research-unlock-module-slot-stack-limit")).toContainText("6");
  await expect(others.getByTestId("research-unlock-craft-cap-2")).toContainText("Lv2");
});
