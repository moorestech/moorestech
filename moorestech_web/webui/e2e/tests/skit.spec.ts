import { test, expect } from "@playwright/test";
import { payloadsOf } from "../support/actions";
import { setSkitStage } from "../support/mockControl";

test.beforeEach(async ({ page }) => {
  await setSkitStage(page, "none");
});

test.afterEach(async ({ page }) => {
  await setSkitStage(page, "none");
});

test("blocking skit reveals, advances, and selects by choiceId", async ({ page }) => {
  const advanceCountBefore = (await payloadsOf(page, "skit.advance")).length;
  await page.goto("/");
  await setSkitStage(page, "text");
  const skit = page.getByTestId("blocking-skit");
  await expect(skit).toBeVisible();

  // 本文表示中のクリックはWeb内revealだけでUnity actionを送らない
  // A click during typing only reveals locally and sends no Unity action
  await skit.click();
  await expect(skit).toContainText("Blocking message");
  expect((await payloadsOf(page, "skit.advance")).length).toBe(advanceCountBefore);

  // 全文表示後のクリックでadvanceし、mock hostが選択肢snapshotへ進める
  // A click after full reveal advances and the mock host moves to the choice snapshot
  await skit.click();
  await expect(page.getByRole("button", { name: "Route B" })).toBeVisible();
  await page.getByRole("button", { name: "Route B" }).click();

  await expect(page.getByTestId("blocking-skit")).toHaveCount(0);
  await expect.poll(async () => {
    const values = await payloadsOf(page, "skit.select");
    return values[values.length - 1];
  }).toEqual({ sessionId: "blocking-1", sceneRevision: 2, choiceId: "route-b" });
});
