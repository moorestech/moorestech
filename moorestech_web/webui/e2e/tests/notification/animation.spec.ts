import { test, expect } from "@playwright/test";
import { setTopicScenario, setUiState } from "../../support/mockControl";
import { NOTIFICATION_DISPLAY_MS } from "../../../src/features/notification/notificationStore";

// ms/s混在のCSS時間値を秒へ揃える
// Normalizes CSS time values (ms or s) to seconds
function seconds(cssTime: string) {
  const value = Number.parseFloat(cssTime);
  return cssTime.trim().endsWith("ms") ? value / 1000 : value;
}

test.afterEach(async ({ page }) => {
  // 他specへ漏らさず空へ戻す
  // Reset to empty so it doesn't leak to other specs
  await setTopicScenario(page, "notificationClear");
  // 未リセットだとCRAFT RECIPE系specが汚染される
  // Leaving uiState set pollutes the CRAFT RECIPE specs
  await setUiState(page, "PlayerInventory");
});

test("通知は左からのスライドとフェードで入場し生存尺の終端で退場する", async ({ page }) => {
  await setUiState(page, "GameScreen");
  await page.goto("/");
  await setTopicScenario(page, "notificationAchievement");

  const row = page.getByTestId("notification-row").first();
  await expect(row).toBeVisible();

  // 期待値は尺トークンと生存尺定数から組む
  // Expectations derive from the duration tokens and the lifetime constant
  const motionTokens = await page.evaluate(() => {
    const rootStyle = getComputedStyle(document.documentElement);
    return {
      enter: rootStyle.getPropertyValue("--notification-enter-duration"),
      exit: rootStyle.getPropertyValue("--notification-exit-duration"),
      shift: rootStyle.getPropertyValue("--notification-shift"),
    };
  });
  const enterSeconds = seconds(motionTokens.enter);
  const exitSeconds = seconds(motionTokens.exit);
  const exitDelaySeconds = NOTIFICATION_DISPLAY_MS / 1000 - exitSeconds;

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
  expect(animation.duration).toBe(`${enterSeconds}s, ${exitSeconds}s`);
  expect(animation.delay).toBe(`0s, ${exitDelaySeconds}s`);
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
  const shiftedTransform = `-${Number.parseFloat(motionTokens.shift)}px`;
  expect(enterFrames[0].opacity).toBe("0");
  expect(enterFrames[0].transform).toContain(shiftedTransform);
  expect(enterFrames[enterFrames.length - 1].opacity).toBe("1");
  expect(exitFrames[exitFrames.length - 1].opacity).toBe("0");
  expect(exitFrames[exitFrames.length - 1].transform).toContain(shiftedTransform);

  // 入場完了後は不透明・移動量ゼロへ落ち着く
  // After the enter finishes it settles at full opacity with no offset
  await expect.poll(async () => row.evaluate((element) => getComputedStyle(element).opacity)).toBe("1");
  const settled = await row.evaluate((element) => getComputedStyle(element).transform);
  expect(settled === "none" || settled === "matrix(1, 0, 0, 1, 0, 0)").toBe(true);

  // 退場の完了でDOMから消える
  // It leaves the DOM when the exit animation finishes
  await expect(page.getByTestId("notification-row")).toHaveCount(0, { timeout: NOTIFICATION_DISPLAY_MS + 2000 });
});
