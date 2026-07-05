import { test, expect } from "@playwright/test";

// 各テスト冒頭で tank を配信状態へリセットしてから接続する（決定的に panel を出すため）
// Reset the served block to tank before connecting so the panel deterministically shows
// 終了後は閉に戻し、後続テストファイルへ open 状態を漏らさない
// Reset to closed afterwards so the open state never leaks into later test files
test.afterEach(async ({ page }) => {
  await page.request.get("/__block?type=closed");
});

test("tank の block inventory パネルに Fluid Tank が表示される", async ({ page }) => {
  await page.request.get("/__block?type=tank");
  await page.goto("/");
  await expect(page.getByTestId("block-inventory")).toBeVisible();
  await expect(page.getByText("Fluid Tank")).toBeVisible();
});

test("水の流体スロットが量 500 を表示する", async ({ page }) => {
  await page.request.get("/__block?type=tank");
  await page.goto("/");
  await expect(page.getByTestId("generic-block-fluids")).toBeVisible();
  // fluidId=10,amount=500 の流体スロットに整形済みの 500 が出る
  // The fluid slot for fluidId=10, amount=500 shows the formatted 500
  await expect(page.getByTestId("fluid-slot").filter({ hasText: "500" })).toBeVisible();
});

test("Water 名のツールチップが hover で表示される", async ({ page }) => {
  await page.request.get("/__block?type=tank");
  await page.goto("/");
  await expect(page.getByTestId("generic-block-fluids")).toBeVisible();
  // Mantine Tooltip は hover 時のみマウントされるため、開いてから可視検証する
  // The Mantine Tooltip mounts only on hover, so open it before asserting visibility
  await page.getByTestId("fluid-slot").first().hover();
  await expect(page.getByText("Water")).toBeVisible();
});

test("progress-arrow のフィル幅が 50% になる", async ({ page }) => {
  await page.request.get("/__block?type=tank");
  await page.goto("/");
  await expect(page.getByTestId("generic-block-fluids")).toBeVisible();
  // progress 0.5 → フィル要素のインラインスタイル width:50%
  // progress 0.5 → fill element inline style width:50%
  const fill = page.getByTestId("progress-arrow").locator("> div");
  await expect(fill).toHaveAttribute("style", /50%/);
});
