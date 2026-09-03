import { expect, test } from "@playwright/test";

// ワールドピンHUD描画契約を検証
// Verify the world-pin HUD rendering contract
test.afterEach(async ({ request }) => {
  await request.get("/__worldpin?clear=1");
});

test("on-screen world pin renders with its marker tip at the projected viewport position", async ({ page, request }) => {
  await page.goto("/");
  await expect(page.getByTestId("hotbar-grid")).toBeVisible();

  await request.get("/__worldpin?x=0.25&y=0.4");
  const pin = page.getByTestId("world-pin-map-object-pin");
  await expect(pin).toBeVisible();
  await expect(pin).toContainText("小石を拾う");

  // マーカー先端が正規化座標に一致
  // The marker tip lands on the normalized coords
  const viewport = page.viewportSize()!;
  const box = (await pin.boundingBox())!;
  expect(Math.abs(box.x + box.width / 2 - viewport.width * 0.25)).toBeLessThanOrEqual(1.5);
  expect(Math.abs(box.y + box.height - viewport.height * 0.4)).toBeLessThanOrEqual(1.5);
});

test("world pin follows updated projections", async ({ page, request }) => {
  await page.goto("/");
  await expect(page.getByTestId("hotbar-grid")).toBeVisible();

  await request.get("/__worldpin?x=0.25&y=0.4");
  const pin = page.getByTestId("world-pin-map-object-pin");
  await expect(pin).toBeVisible();

  await request.get("/__worldpin?x=0.75&y=0.6");
  const viewport = page.viewportSize()!;
  await expect(async () => {
    const box = (await pin.boundingBox())!;
    expect(Math.abs(box.x + box.width / 2 - viewport.width * 0.75)).toBeLessThanOrEqual(1.5);
    expect(Math.abs(box.y + box.height - viewport.height * 0.6)).toBeLessThanOrEqual(1.5);
  }).toPass();
});

test("off-screen world pin renders a 56px filled shaft arrow at the screen edge", async ({ page, request }) => {
  await page.goto("/");
  await expect(page.getByTestId("hotbar-grid")).toBeVisible();

  await request.get("/__worldpin?on=0&dx=1&dy=0");
  const arrow = page.getByTestId("world-pin-arrow-map-object-pin");
  await expect(arrow).toBeVisible();
  await expect(page.getByTestId("world-pin-map-object-pin")).toHaveCount(0);

  // 40px余白と中央配置を検証
  // Verify the arrow center lands at the 40px right-edge margin and vertical center
  const viewport = page.viewportSize()!;
  const margin = 40;
  const box = (await arrow.boundingBox())!;
  expect(box.width).toBeCloseTo(56, 0);
  expect(box.height).toBeCloseTo(56, 0);
  expect(Math.abs(box.x + box.width / 2 - (viewport.width - margin))).toBeLessThanOrEqual(1.5);
  expect(Math.abs(box.y + box.height / 2 - viewport.height / 2)).toBeLessThanOrEqual(1.5);
  await expect(arrow.locator("path")).toHaveAttribute("d", "M2 8 H13 V3 L22 12 L13 21 V16 H2 Z");
  const visualStyle = await arrow.locator("svg").evaluate((svg) => {
    const style = getComputedStyle(svg);
    return { fill: style.fill, stroke: style.stroke, filter: style.filter };
  });
  expect(visualStyle.fill).toBe("rgb(255, 0, 0)");
  expect(visualStyle.stroke).toBe("rgba(10, 14, 27, 0.8)");
  expect(visualStyle.filter).toBe("drop-shadow(rgba(0, 0, 0, 0.65) 0px 2px 3px)");
});

test("off-screen arrow follows a diagonal direction to the corner region", async ({ page, request }) => {
  await page.goto("/");
  await expect(page.getByTestId("hotbar-grid")).toBeVisible();

  // 左上斜め方向 → 上端マージンへクランプされ、水平位置は方向比で決まる
  // A diagonal up-left direction clamps to the top margin with x set by the direction ratio
  await request.get("/__worldpin?on=0&dx=-0.4&dy=-0.9");
  const arrow = page.getByTestId("world-pin-arrow-map-object-pin");
  await expect(arrow).toBeVisible();

  const viewport = page.viewportSize()!;
  const margin = 40;
  const scale = (viewport.height / 2 - margin) / 0.9;
  const expectedX = viewport.width / 2 - 0.4 * scale;
  const box = (await arrow.boundingBox())!;
  expect(Math.abs(box.y + box.height / 2 - margin)).toBeLessThanOrEqual(1.5);
  expect(Math.abs(box.x + box.width / 2 - expectedX)).toBeLessThanOrEqual(1.5);

  // 回転後も画面内に収める
  // Verify the rotated bounding box remains inside the viewport without corner clipping
  expect(box.x).toBeGreaterThanOrEqual(0);
  expect(box.y).toBeGreaterThanOrEqual(0);
  expect(box.x + box.width).toBeLessThanOrEqual(viewport.width);
  expect(box.y + box.height).toBeLessThanOrEqual(viewport.height);
});

test("off-screen arrow remains inside the viewport at all four diagonal corners", async ({ page, request }) => {
  await page.goto("/");
  await expect(page.getByTestId("hotbar-grid")).toBeVisible();
  const viewport = page.viewportSize()!;
  const arrow = page.getByTestId("world-pin-arrow-map-object-pin");
  const directions = [
    { x: 1, y: 1 },
    { x: -1, y: 1 },
    { x: -1, y: -1 },
    { x: 1, y: -1 },
  ];

  for (const direction of directions) {
    await request.get(`/__worldpin?on=0&dx=${direction.x}&dy=${direction.y}`);
    await expect(arrow).toBeVisible();
    const expectedAngle = (Math.atan2(direction.y, direction.x) * 180) / Math.PI;
    await expect(arrow).toHaveAttribute("style", new RegExp(`rotate\\(${expectedAngle}deg\\)`));
    const box = (await arrow.boundingBox())!;
    expect(box.x).toBeGreaterThanOrEqual(0);
    expect(box.y).toBeGreaterThanOrEqual(0);
    expect(box.x + box.width).toBeLessThanOrEqual(viewport.width);
    expect(box.y + box.height).toBeLessThanOrEqual(viewport.height);
  }
});

test("clearing world pins removes the overlay", async ({ page, request }) => {
  await page.goto("/");
  await expect(page.getByTestId("hotbar-grid")).toBeVisible();

  await request.get("/__worldpin?x=0.5&y=0.5");
  await expect(page.getByTestId("world-pin-overlay")).toBeVisible();

  await request.get("/__worldpin?clear=1");
  await expect(page.getByTestId("world-pin-overlay")).toHaveCount(0);
});
