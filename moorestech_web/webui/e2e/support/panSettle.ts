import type { Locator, Page } from "@playwright/test";

// 描画1フレームぶん待つ（2連rAFで反映済みを保証）
// Waits one rendered frame (double rAF guarantees the paint landed)
export function waitForFrame(page: Page) {
  return page.evaluate(() => new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve()))));
}

// 慣性滑走の静止をrAF間隔でbbox監視し返す
// Polls the bbox per frame until the glide stops, then returns it
export async function settleBoundingBox(page: Page, target: Locator) {
  let previous = await target.boundingBox();
  let stableReads = 0;
  for (let i = 0; i < 200; i++) {
    await waitForFrame(page);
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
