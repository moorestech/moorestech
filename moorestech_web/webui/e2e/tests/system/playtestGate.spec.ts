import { test, expect } from "@playwright/test";
import { setTopicScenario } from "../../support/mockControl";

// ゲートは全画面を塞ぐため、待機を残したまま抜けると後続specの操作が全て通らなくなる
// A gate blocks the whole screen, so leaving it waiting would make every later spec's interaction fail
test.afterEach(async ({ page }) => {
  await setTopicScenario(page, "consentGateClosed");
  await setTopicScenario(page, "crashReportGateClosed");
});

test("同意ゲートは待機中だけ全画面で描かれる", async ({ page }) => {
  await setTopicScenario(page, "consentGateWaiting");
  await page.goto("/");

  await expect(page.getByTestId("playtest-consent-gate")).toBeVisible();
  await expect(page.getByTestId("playtest-consent-gate-title")).toBeVisible();
  await expect(page.getByTestId("playtest-consent-body")).toBeVisible();
  await expect(page.getByTestId("playtest-consent-agree")).toBeVisible();

  // 待っていないゲートは描かない（無条件マウントでも本体は出ない）
  // A gate that is not waiting draws nothing, even though it mounts unconditionally
  await expect(page.getByTestId("crash-report-gate")).toHaveCount(0);
});

test("前回異常終了ゲートは待機中だけ全画面で描かれる", async ({ page }) => {
  await setTopicScenario(page, "crashReportGateWaiting");
  await page.goto("/");

  await expect(page.getByTestId("crash-report-gate")).toBeVisible();
  await expect(page.getByTestId("crash-report-gate-title")).toBeVisible();
  await expect(page.getByTestId("crash-report-description")).toBeVisible();
  await expect(page.getByTestId("crash-report-send")).toBeVisible();
  await expect(page.getByTestId("crash-report-skip")).toBeVisible();
  await expect(page.getByTestId("playtest-consent-gate")).toHaveCount(0);
});

// 2枚とも無条件マウントなので、排他が無いと不透明な全画面が重なって下の1枚が押せなくなる
// Both mount unconditionally, so without the exclusion two opaque full screens stack and the lower one cannot be pressed
test("2枚が同時に待っても起動順の手前（同意ゲート）だけを描く", async ({ page }) => {
  await setTopicScenario(page, "consentGateWaiting");
  await setTopicScenario(page, "crashReportGateWaiting");
  await page.goto("/");

  await expect(page.getByTestId("playtest-consent-gate")).toBeVisible();
  await expect(page.getByTestId("crash-report-gate")).toHaveCount(0);
});

// 待機が解ければゲートは消え、下のゲーム画面が操作できる状態へ戻る
// Once the wait is released the gate disappears and the game screen underneath becomes operable again
test("待機が解ければゲートは消える", async ({ page }) => {
  await setTopicScenario(page, "crashReportGateWaiting");
  await page.goto("/");
  await expect(page.getByTestId("crash-report-gate")).toBeVisible();

  await setTopicScenario(page, "crashReportGateClosed");

  await expect(page.getByTestId("crash-report-gate")).toHaveCount(0);
});
