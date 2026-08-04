import type { Locator, Page } from "@playwright/test";

// 慣性滑走が静止するまでrAF間隔でbboxを監視し、静止後の矩形を返す
// Watches the bbox at rAF intervals until inertial gliding stops, then returns the settled box
export async function settleBoundingBox(page: Page, target: Locator) {
  let previous = await target.boundingBox();
  let stableReads = 0;
  for (let i = 0; i < 200; i++) {
    await page.evaluate(() => new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve()))));
    const current = await target.boundingBox();
    if (current && previous && current.x === previous.x && current.y === previous.y) {
      // 減衰末尾の誤検知を避けるため2回連続の静止を要求する
      // Require two consecutive still reads to avoid false settles near the decay tail
      stableReads++;
      if (stableReads >= 2) return current;
    } else {
      stableReads = 0;
    }
    previous = current;
  }
  throw new Error("pan glide did not settle");
}
