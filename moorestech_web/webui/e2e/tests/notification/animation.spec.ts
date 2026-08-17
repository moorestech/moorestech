import { test, expect } from "@playwright/test";
import { setTopicScenario, setUiState } from "../../support/mockControl";

test.afterEach(async ({ page }) => {
  // 通知トピックは値が残るため、他specへ漏らさないよう空へ戻す
  // The notification topic is sticky, so reset it to empty and keep other specs clean
  await setTopicScenario(page, "notificationClear");
  // uiStateも他specと同じ前例に倣いデフォルトへ戻す（未リセットだとCRAFT RECIPE系specが汚染される）
  // Also reset uiState to the default, matching sibling specs (leaving it set pollutes the CRAFT RECIPE specs)
  await setUiState(page, "PlayerInventory");
});

test("通知は左からのスライドとフェードで入場し生存尺の終端で退場する", async ({ page }) => {
  await setUiState(page, "GameScreen");
  await page.goto("/");
  await setTopicScenario(page, "notificationAchievement");

  const row = page.getByTestId("notification-row").first();
  await expect(row).toBeVisible();

  // 入場・退場の2本が載り、退場だけが生存尺から逆算した遅延を持つ
  // Two animations are attached and only the exit carries the lifetime-derived delay
  const animation = await row.evaluate((element) => {
    const style = getComputedStyle(element);
    return {
      name: style.animationName,
      duration: style.animationDuration,
      delay: style.animationDelay,
      fillMode: style.animationFillMode,
      timingFunction: style.animationTimingFunction,
    };
  });
  expect(animation.name).toMatch(/notificationEnter/);
  expect(animation.name).toMatch(/notificationExit/);
  expect(animation.duration).toBe("0.16s, 0.2s");
  expect(animation.delay).toBe("0s, 6.8s");
  expect(animation.fillMode).toBe("both, forwards");
  expect(animation.timingFunction).toBe("ease-out, ease-in");

  // 入場完了後は不透明・移動量ゼロへ落ち着く
  // After the enter finishes it settles at full opacity with no offset
  await expect.poll(async () => row.evaluate((element) => getComputedStyle(element).opacity)).toBe("1");
  const settled = await row.evaluate((element) => getComputedStyle(element).transform);
  expect(settled === "none" || settled === "matrix(1, 0, 0, 1, 0, 0)").toBe(true);

  // 生存尺の経過でDOMから消える（退場アニメの終端と一致する）
  // It leaves the DOM when the lifetime elapses, matching the end of the exit animation
  await expect(page.getByTestId("notification-row")).toHaveCount(0, { timeout: 9000 });
});
