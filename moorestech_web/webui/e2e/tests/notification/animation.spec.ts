import { test, expect } from "@playwright/test";
import { setTopicScenario, setUiState } from "../../support/mockControl";

test.afterEach(async ({ page }) => {
  // 他specへ漏らさず空へ戻す
  // Reset to empty so it doesn't leak to other specs
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

  // 入場退場の2本、遅延は退場のみ
  // Enter/exit pair; only exit carries the delay
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

  // keyframesで始点終点を固定
  // Lock keyframe start/end via getAnimations()
  const keyframes = await row.evaluate((element) =>
    element.getAnimations().map((animation) => ({
      name: (animation as CSSAnimation).animationName,
      frames: (animation.effect as KeyframeEffect).getKeyframes()
        .map((frame) => ({ opacity: String(frame.opacity), transform: String(frame.transform) })),
    })));
  const enterFrames = keyframes.find((entry) => /notificationEnter/.test(entry.name))!.frames;
  const exitFrames = keyframes.find((entry) => /notificationExit/.test(entry.name))!.frames;
  expect(enterFrames[0].opacity).toBe("0");
  expect(enterFrames[0].transform).toContain("-12px");
  expect(enterFrames[enterFrames.length - 1].opacity).toBe("1");
  expect(exitFrames[exitFrames.length - 1].opacity).toBe("0");
  expect(exitFrames[exitFrames.length - 1].transform).toContain("-12px");

  // 入場完了後は不透明・移動量ゼロへ落ち着く
  // After the enter finishes it settles at full opacity with no offset
  await expect.poll(async () => row.evaluate((element) => getComputedStyle(element).opacity)).toBe("1");
  const settled = await row.evaluate((element) => getComputedStyle(element).transform);
  expect(settled === "none" || settled === "matrix(1, 0, 0, 1, 0, 0)").toBe(true);

  // 生存尺経過でDOMから消える
  // It's removed from the DOM when the lifetime elapses
  await expect(page.getByTestId("notification-row")).toHaveCount(0, { timeout: 9000 });
});
