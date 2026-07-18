import { expect, test } from "@playwright/test";

test.afterEach(async ({ request }) => {
  await request.get("/__gamestate?state=InGame");
  await request.get("/__skit?show=0");
});

test("background skit snapshot renders speaker and body", async ({ page, request }) => {
  await page.goto("/");
  await request.get("/__skit?show=1");
  await expect(page.getByTestId("background-skit")).toContainText("Moore");
  await expect(page.getByTestId("background-skit")).toContainText("Background message");
});

test("cutscene state withdraws every web UI layer", async ({ page, request }) => {
  await page.goto("/");
  await expect(page.getByTestId("hotbar-grid")).toBeVisible();
  await request.get("/__gamestate?state=CutScene");
  await expect(page.getByTestId("hotbar-grid")).toBeHidden();
  await request.get("/__gamestate?state=InGame");
  await expect(page.getByTestId("hotbar-grid")).toBeVisible();
});
