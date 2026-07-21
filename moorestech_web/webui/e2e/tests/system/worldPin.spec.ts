import { expect, test } from "@playwright/test";

// ワールドピンHUD: Unity射影済み座標のDOM描画契約を検証する
// World-pin HUD: verifies the DOM rendering contract for Unity-projected coordinates
test.afterEach(async ({ request }) => {
  await request.get("/__worldpin?clear=1");
});

test("on-screen world pin renders with its marker tip at the projected viewport position", async ({ page, request }) => {
  await page.goto("/");
  await expect(page.getByTestId("hotbar-grid")).toBeVisible();

  await request.get("/__worldpin?x=0.25&y=0.4&text=PickPebbles");
  const pin = page.getByTestId("world-pin-map-object-pin");
  await expect(pin).toBeVisible();
  await expect(pin).toContainText("PickPebbles");

  // マーカー先端（下端中央）が正規化座標×ビューポート寸法に一致すること
  // The marker tip (bottom center) must land on normalized coords × viewport size
  const viewport = page.viewportSize()!;
  const box = (await pin.boundingBox())!;
  expect(Math.abs(box.x + box.width / 2 - viewport.width * 0.25)).toBeLessThanOrEqual(1.5);
  expect(Math.abs(box.y + box.height - viewport.height * 0.4)).toBeLessThanOrEqual(1.5);
});

test("world pin follows updated projections", async ({ page, request }) => {
  await page.goto("/");
  await expect(page.getByTestId("hotbar-grid")).toBeVisible();

  await request.get("/__worldpin?x=0.25&y=0.4&text=Move");
  const pin = page.getByTestId("world-pin-map-object-pin");
  await expect(pin).toBeVisible();

  await request.get("/__worldpin?x=0.75&y=0.6&text=Move");
  const viewport = page.viewportSize()!;
  await expect(async () => {
    const box = (await pin.boundingBox())!;
    expect(Math.abs(box.x + box.width / 2 - viewport.width * 0.75)).toBeLessThanOrEqual(1.5);
    expect(Math.abs(box.y + box.height - viewport.height * 0.6)).toBeLessThanOrEqual(1.5);
  }).toPass();
});

test("off-screen world pin clamps a direction arrow to the screen edge", async ({ page, request }) => {
  await page.goto("/");
  await expect(page.getByTestId("hotbar-grid")).toBeVisible();

  // 右方向(1,0)の画面外ターゲット → 矢印中心が右端マージン位置・垂直中央に来ること
  // An off-screen target to the right (1,0) puts the arrow center at the right-edge margin, vertically centered
  await request.get("/__worldpin?on=0&dx=1&dy=0&text=Far");
  const arrow = page.getByTestId("world-pin-arrow-map-object-pin");
  await expect(arrow).toBeVisible();
  await expect(page.getByTestId("world-pin-map-object-pin")).toHaveCount(0);

  const viewport = page.viewportSize()!;
  const margin = 28;
  const box = (await arrow.boundingBox())!;
  expect(Math.abs(box.x + box.width / 2 - (viewport.width - margin))).toBeLessThanOrEqual(1.5);
  expect(Math.abs(box.y + box.height / 2 - viewport.height / 2)).toBeLessThanOrEqual(1.5);
});

test("off-screen arrow follows a diagonal direction to the corner region", async ({ page, request }) => {
  await page.goto("/");
  await expect(page.getByTestId("hotbar-grid")).toBeVisible();

  // 左上斜め方向 → 上端マージンへクランプされ、水平位置は方向比で決まる
  // A diagonal up-left direction clamps to the top margin with x set by the direction ratio
  await request.get("/__worldpin?on=0&dx=-0.4&dy=-0.9&text=Far");
  const arrow = page.getByTestId("world-pin-arrow-map-object-pin");
  await expect(arrow).toBeVisible();

  const viewport = page.viewportSize()!;
  const margin = 28;
  const scale = (viewport.height / 2 - margin) / 0.9;
  const expectedX = viewport.width / 2 - 0.4 * scale;
  const box = (await arrow.boundingBox())!;
  expect(Math.abs(box.y + box.height / 2 - margin)).toBeLessThanOrEqual(1.5);
  expect(Math.abs(box.x + box.width / 2 - expectedX)).toBeLessThanOrEqual(1.5);
});

test("clearing world pins removes the overlay", async ({ page, request }) => {
  await page.goto("/");
  await expect(page.getByTestId("hotbar-grid")).toBeVisible();

  await request.get("/__worldpin?x=0.5&y=0.5&text=Gone");
  await expect(page.getByTestId("world-pin-overlay")).toBeVisible();

  await request.get("/__worldpin?clear=1");
  await expect(page.getByTestId("world-pin-overlay")).toHaveCount(0);
});
