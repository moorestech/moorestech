import { expect, type Locator, type Page } from "@playwright/test";

// ScrollAreaのDOM契約を1箇所に集約
// Centralizes the ScrollArea DOM contract in one place
export function scrollAreaRootOf(page: Page, contentTestId: string): Locator {
  return page.getByTestId(contentTestId).locator("xpath=ancestor::*[contains(@class, 'mantine-ScrollArea-root')][1]");
}

export function scrollAreaViewport(scrollRoot: Locator): Locator {
  return scrollRoot.locator(".mantine-ScrollArea-viewport");
}

export function scrollAreaVerticalBar(scrollRoot: Locator): Locator {
  return scrollRoot.locator('.mantine-ScrollArea-scrollbar[data-orientation="vertical"]');
}

export function scrollAreaHorizontalBar(scrollRoot: Locator): Locator {
  return scrollRoot.locator('.mantine-ScrollArea-scrollbar[data-orientation="horizontal"]');
}

// 溢れ有無でバーと高さ不変を検査。不変は視口のclientHeightで見る（パネル高はCSS固定値なので件数に依らず動かず番人にならない）
// Checks the bar and height invariant across overflow, measuring the viewport's clientHeight since panel heights are CSS constants and cannot guard anything
export async function expectScrollsOnlyWhenOverflowing(
  scrollRoot: Locator,
  overflowScenario: () => Promise<void>,
) {
  const viewport = scrollAreaViewport(scrollRoot);
  const bar = scrollAreaVerticalBar(scrollRoot);

  const settledClientHeight = await viewport.evaluate((element) => element.clientHeight);
  await expect(bar).toBeHidden();
  expect(await viewport.evaluate((element) => element.scrollHeight - element.clientHeight)).toBe(0);

  await overflowScenario();
  await expect(bar).toBeVisible();
  // 器いっぱいに伸びたままなら視口の高さは件数で動かない。潰れる退行はここで落ちる
  // A scroller that keeps filling its container holds this height whatever the row count; a collapse fails here
  expect(await viewport.evaluate((element) => element.clientHeight)).toBeCloseTo(settledClientHeight, 1);
  expect(await viewport.evaluate((element) => element.scrollHeight - element.clientHeight)).toBeGreaterThan(0);
}

export async function expectSeparatedHorizontally(left: Locator, right: Locator) {
  const leftBox = await left.boundingBox();
  const rightBox = await right.boundingBox();
  expect(leftBox).not.toBeNull();
  expect(rightBox).not.toBeNull();
  expect(leftBox!.x + leftBox!.width).toBeLessThanOrEqual(rightBox!.x);
}

export async function expectCenteredHorizontally(locator: Locator, container: Locator) {
  const [box, containerBox] = await Promise.all([locator.boundingBox(), container.boundingBox()]);
  expect(box).not.toBeNull();
  expect(containerBox).not.toBeNull();
  expect(box!.x + box!.width / 2).toBe(containerBox!.x + containerBox!.width / 2);
}

export async function expectAbove(upper: Locator, lower: Locator) {
  const [upperBox, lowerBox] = await Promise.all([upper.boundingBox(), lower.boundingBox()]);
  expect(upperBox).not.toBeNull();
  expect(lowerBox).not.toBeNull();
  expect(upperBox!.y + upperBox!.height).toBeLessThanOrEqual(lowerBox!.y);
}

export async function expectNoHorizontalOverflow(locators: Locator) {
  const layouts = await locators.evaluateAll((elements) => elements.map((element) => ({
    clientWidth: element.clientWidth,
    scrollWidth: element.scrollWidth,
  })));
  for (const layout of layouts) expect(layout.scrollWidth).toBeLessThanOrEqual(layout.clientWidth);
}

export async function expectNoVerticalOverflow(locators: Locator) {
  const layouts = await locators.evaluateAll((elements) => elements.map((element) => ({
    clientHeight: element.clientHeight,
    scrollHeight: element.scrollHeight,
  })));
  for (const layout of layouts) expect(layout.scrollHeight).toBeLessThanOrEqual(layout.clientHeight);
}

export async function expectWithinViewport(locator: Locator) {
  const layout = await locator.evaluate((element) => {
    const box = element.getBoundingClientRect();
    return { left: box.left, top: box.top, right: box.right, bottom: box.bottom, width: window.innerWidth, height: window.innerHeight };
  });
  expect(layout.left).toBeGreaterThanOrEqual(0);
  expect(layout.top).toBeGreaterThanOrEqual(0);
  expect(layout.right).toBeLessThanOrEqual(layout.width);
  expect(layout.bottom).toBeLessThanOrEqual(layout.height);
}

// 中心hit-testで遮蔽有無を検証。
// HUD自体はpointer-events:noneでelementFromPointが素通りするため、判定中だけ一時的にautoへ戻す
// Verify occlusion via a center-point hit-test.
// The HUD is pointer-events: none so elementFromPoint would skip past it; flip it to auto only for the measurement
export async function expectHitTestWithin(locator: Locator) {
  const isUnoccluded = await locator.evaluate((element) => {
    const originalPointerEvents = element.style.pointerEvents;
    element.style.pointerEvents = "auto";
    const rect = element.getBoundingClientRect();
    const target = document.elementFromPoint(rect.left + rect.width / 2, rect.top + rect.height / 2);
    element.style.pointerEvents = originalPointerEvents;
    return target !== null && element.contains(target);
  });
  expect(isUnoccluded).toBe(true);
}

export async function expectAtViewportTopCorner(
  locator: Locator,
  horizontalEdge: "left" | "right",
  maximumGap: number,
) {
  const layout = await locator.evaluate((element, edge) => {
    const box = element.getBoundingClientRect();
    return {
      horizontalGap: edge === "left" ? box.left : window.innerWidth - box.right,
      topGap: box.top,
    };
  }, horizontalEdge);
  expect(layout.horizontalGap).toBeGreaterThanOrEqual(0);
  expect(layout.horizontalGap).toBeLessThan(maximumGap);
  expect(layout.topGap).toBeGreaterThanOrEqual(0);
  expect(layout.topGap).toBeLessThan(maximumGap);
}
