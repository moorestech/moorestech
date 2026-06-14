import { test, expect } from "@playwright/test";

test("接続後に進捗バーが描画される", async ({ page }) => {
  await page.goto("/");
  // mock host が接続時に { visible:true, progress:0.4, label:"Crafting" } を配信する
  // The mock host serves { visible:true, progress:0.4, label:"Crafting" } on connect
  await expect(page.getByTestId("progress-bar")).toBeVisible();
});

test("ラベル Crafting が表示される", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByText("Crafting")).toBeVisible();
});

test("フィルの幅が 40% になる", async ({ page }) => {
  await page.goto("/");
  // progress 0.4 → インラインスタイル width:40%（トラックに対する割合）
  // progress 0.4 → inline style width:40% (proportion of the track)
  await expect(page.getByTestId("progress-fill")).toHaveAttribute("style", /40%/);
});
