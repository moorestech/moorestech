import { expect, type Locator } from "@playwright/test";

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
