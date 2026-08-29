import { expect, test } from "@playwright/test";
import { setTopicScenario, setUiState } from "../../support/mockControl";
import { freezeAttentionPulse } from "../../support/pulseFreeze";

// stage外のPortal層が--ui-scaleへ追従し、解像度が変わっても見かけの大きさが一定であることを検証する
// Verify that portal layers outside the stage follow --ui-scale so their apparent size stays constant across resolutions
const BASE = { width: 1280, height: 720 } as const;
const HIGH = { width: 2560, height: 1440 } as const;
// 基準stageに対する倍率。HIGHは縦横とも2倍なので--ui-scaleは2になる
// Scale relative to the reference stage; HIGH doubles both axes, so --ui-scale is 2
const HIGH_SCALE = 2;
const TOLERANCE_PX = 2;

test.afterEach(async ({ page, request }) => {
  await request.get("/__worldpin?clear=1");
  await setTopicScenario(page, "notificationClear");
  await setTopicScenario(page, "tutorialEmpty");
  await setUiState(page, "PlayerInventory");
});

test("通知の幅はstage比のまま解像度に依らず、高解像度でも画面幅の2割に収まる", async ({ page }) => {
  await setUiState(page, "GameScreen");
  await page.goto("/");
  await setTopicScenario(page, "notificationItemEarned");

  const row = page.getByTestId("notification-row");
  await expect(row).toHaveCount(1);

  await page.setViewportSize(BASE);
  const baseWidth = (await row.boundingBox())!.width;

  // vw指定だと--ui-scaleと二重に掛かり、ここで4倍へ膨らんで画面の4割を超える
  // A vw-based width would compound with --ui-scale, swelling 4x here and covering over 40% of the screen
  await page.setViewportSize(HIGH);
  await expect(async () => {
    const highWidth = (await row.boundingBox())!.width;
    expect(Math.abs(highWidth - baseWidth * HIGH_SCALE)).toBeLessThanOrEqual(TOLERANCE_PX);
    expect(highWidth / HIGH.width).toBeCloseTo(0.2, 2);
  }).toPass();
});

test("ワールドピンは高解像度で拡大しても先端が射影座標に留まる", async ({ page, request }) => {
  await page.goto("/");
  await expect(page.getByTestId("hotbar-grid")).toBeVisible();
  await request.get("/__worldpin?x=0.25&y=0.4");

  const pin = page.getByTestId("world-pin-map-object-pin");
  await expect(pin).toBeVisible();

  await page.setViewportSize(BASE);
  const baseHeight = (await pin.boundingBox())!.height;

  await page.setViewportSize(HIGH);
  await expect(async () => {
    const box = (await pin.boundingBox())!;
    expect(Math.abs(box.height - baseHeight * HIGH_SCALE)).toBeLessThanOrEqual(TOLERANCE_PX);
    // 下端中央原点なので、拡大しても先端は射影座標のまま
    // The bottom-center origin keeps the tip on the projected point even when enlarged
    expect(Math.abs(box.x + box.width / 2 - HIGH.width * 0.25)).toBeLessThanOrEqual(TOLERANCE_PX);
    expect(Math.abs(box.y + box.height - HIGH.height * 0.4)).toBeLessThanOrEqual(TOLERANCE_PX);
  }).toPass();
});

test("チュートリアルの枠線ラベルは高解像度で拡大しても枠線に付いたまま", async ({ page }) => {
  await setUiState(page, "GameScreen");
  await page.goto("/");
  await expect(page.getByTestId("hotbar-grid")).toBeVisible();
  await setTopicScenario(page, "tutorialOutlineWithLabel");

  const label = page.getByTestId("tutorial-highlight-label");
  const ring = page.getByTestId("tutorial-overlay").locator("[data-kind='outline']");
  await expect(label).toBeVisible();

  await page.setViewportSize(BASE);
  const baseLabel = (await label.boundingBox())!;

  // 枠はstage拡縮済みのアンカー実測値から作られるため、ラベルだけ固定pxだと高解像度で豆粒になる
  // The ring is built from a stage-scaled anchor measurement, so a fixed-px label alone shrinks to a speck at high resolutions
  await page.setViewportSize(HIGH);
  await expect(async () => {
    const highLabel = (await label.boundingBox())!;
    expect(Math.abs(highLabel.height - baseLabel.height * HIGH_SCALE)).toBeLessThanOrEqual(TOLERANCE_PX);
    // 拡大しても枠線の下辺に付いたままで、離れも食い込みもしない
    // Even enlarged it stays attached below the ring, neither drifting away nor overlapping it
    await freezeAttentionPulse(page);
    const highRing = (await ring.boundingBox())!;
    expect(highLabel.y).toBeGreaterThanOrEqual(highRing.y + highRing.height);
    expect(highLabel.y - (highRing.y + highRing.height)).toBeLessThanOrEqual(8 * HIGH_SCALE);
  }).toPass();
});

test("画面端矢印は高解像度で拡大しても余白ごと広がり画面内に収まる", async ({ page, request }) => {
  await page.goto("/");
  await expect(page.getByTestId("hotbar-grid")).toBeVisible();
  await request.get("/__worldpin?on=0&dx=1&dy=0");

  const arrow = page.getByTestId("world-pin-arrow-map-object-pin");
  await expect(arrow).toBeVisible();

  await page.setViewportSize(HIGH);
  await expect(async () => {
    const box = (await arrow.boundingBox())!;
    expect(box.width).toBeCloseTo(56 * HIGH_SCALE, 0);
    // 余白を固定のままにすると半幅56pxが余白40pxを超えて右端からはみ出す
    // Leaving the margin fixed would push the 56px half-width past the 40px margin and off the right edge
    expect(box.x + box.width).toBeLessThanOrEqual(HIGH.width);
    expect(Math.abs(box.x + box.width / 2 - (HIGH.width - 40 * HIGH_SCALE))).toBeLessThanOrEqual(TOLERANCE_PX);
  }).toPass();
});
