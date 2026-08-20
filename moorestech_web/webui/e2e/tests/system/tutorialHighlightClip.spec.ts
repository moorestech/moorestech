import { expect, test, type Page } from "@playwright/test";
import { resetResearch, setTopicScenario, setUiState } from "../../support/mockControl";
import { settleBoundingBox } from "../../support/panSettle";
import { researchableNodeGuid } from "../../mock-host/researchFixtures";
import { TUTORIAL_RESEARCH_NODE_PADDING_PX } from "../../mock-host/topics/topicControls";
import { PAN_RELEASE_STALL_MS } from "../../../src/shared/treeView/viewport/viewport";

const RESEARCH_NODE = `research-node-${researchableNodeGuid}`;
const TOLERANCE_PX = 1.5;

type Rect = { left: number; top: number; right: number; bottom: number };

test.afterEach(async ({ page }) => {
  await setTopicScenario(page, "tutorialEmpty");
  await resetResearch(page);
  await setUiState(page, "PlayerInventory");
});

// アンカーの可視矩形はブラウザ自身に計算させる。祖先クリップ規則の正本はブラウザである
// Let the browser compute the anchor's visible rect; the browser is the authority on ancestor clipping
async function anchorRects(page: Page, testId: string) {
  return page.evaluate(async (id) => {
    const element = document.querySelector(`[data-testid="${id}"]`);
    if (!element) return null;
    const full = element.getBoundingClientRect();
    const entry = await new Promise<IntersectionObserverEntry>((resolve) => {
      const observer = new IntersectionObserver((entries) => { observer.disconnect(); resolve(entries[0]); }, { threshold: 0 });
      observer.observe(element);
    });
    const visible = entry.intersectionRect;
    return {
      full: { left: full.left, top: full.top, right: full.right, bottom: full.bottom },
      visible: visible.width <= 0.01 || visible.height <= 0.01
        ? null
        : { left: visible.left, top: visible.top, right: visible.right, bottom: visible.bottom },
    };
  }, testId);
}

// boundingBoxとclip-pathから可視矩形を復元
// Reconstruct the visible rect from boundingBox and clip-path
async function highlightVisibleRect(page: Page) {
  return page.evaluate(() => {
    const element = document.querySelector('[data-testid="tutorial-overlay"] [data-kind="outline"]');
    if (!element) return null;
    const box = element.getBoundingClientRect();
    // CSS短縮inset値を1〜4値に展開
    // Expand the CSS shorthand inset value to 1..4 numbers
    const matched = /^inset\(([^)]*)\)$/.exec(getComputedStyle(element).clipPath);
    if (!matched) return { left: box.left, top: box.top, right: box.right, bottom: box.bottom };
    const parts = matched[1].trim().split(/\s+/).map((value) => Number(value.replace("px", "")));
    const [top, right = top, bottom = top, left = right] = parts;
    return {
      left: box.left + left, top: box.top + top,
      right: box.right - right, bottom: box.bottom - bottom,
    };
  });
}

// 期待端=padding+glow分広げclipでクランプ
// Expected edge = anchor expanded by padding+glow, clamped to clip
function expectMaskedLikeAnchor(highlight: Rect, anchor: { full: Rect }, clip: Rect, glowPx: number) {
  const sides = [
    { key: "left" as const, sign: -1, clamp: Math.max },
    { key: "top" as const, sign: -1, clamp: Math.max },
    { key: "right" as const, sign: 1, clamp: Math.min },
    { key: "bottom" as const, sign: 1, clamp: Math.min },
  ];
  for (const side of sides) {
    const expanded = anchor.full[side.key] + side.sign * (TUTORIAL_RESEARCH_NODE_PADDING_PX + glowPx);
    const expected = side.clamp(expanded, clip[side.key]);
    expect(Math.abs(highlight[side.key] - expected), side.key).toBeLessThanOrEqual(TOLERANCE_PX);
  }
}

// TutorialOverlay と同じCSS変数を読む。切れていない辺は枠がここまで外へ出る
// Reads the same CSS variable as TutorialOverlay; intact sides extend this far outward
async function readGlowPx(page: Page) {
  return page.evaluate(() => {
    const raw = getComputedStyle(document.documentElement).getPropertyValue("--tutorial-highlight-glow");
    return Number.parseFloat(raw);
  });
}

// 空背景を掴んでキャンバスをパンする。ノード上から始めるとクリック扱いになる
// Pan the canvas by grabbing the empty background; starting on a node would count as a click
async function dragViewport(page: Page, viewportBox: { x: number; y: number; width: number; height: number }, dx: number, dy: number) {
  const start = { x: viewportBox.x + viewportBox.width - 40, y: viewportBox.y + viewportBox.height - 40 };
  await page.mouse.move(start.x, start.y);
  await page.mouse.down();
  await page.mouse.move(start.x + dx, start.y + dy, { steps: 10 });
  // PAN_RELEASE_STALL_MSより長く静止させ慣性フリングを起こさず狙った位置で止める
  // Stall longer than PAN_RELEASE_STALL_MS so releasing doesn't trigger inertial fling past the target
  await page.waitForTimeout(PAN_RELEASE_STALL_MS + 40);
  await page.mouse.up();
}

test("研究ノードのハイライトが祖先のoverflowクリップに合わせてマスクされる", async ({ page }, testInfo) => {
  await setUiState(page, "ResearchTree");
  await page.goto("/");
  await setTopicScenario(page, "tutorialResearchNode");

  const node = page.getByTestId(RESEARCH_NODE);
  await expect(node).toBeVisible();
  const highlight = page.locator('[data-testid="tutorial-overlay"] [data-kind="outline"]');
  await expect(highlight).toBeVisible();
  const glowPx = await readGlowPx(page);

  // クリップ矩形はビューポートのpadding box。.viewportはborder 0なのでboundingBoxをそのまま使える
  // The clip rect is the viewport's padding box; .viewport has border:0 so boundingBox doubles as it
  const viewportBox = (await page.getByTestId("research-viewport").boundingBox())!;
  const clip: Rect = {
    left: viewportBox.x, top: viewportBox.y,
    right: viewportBox.x + viewportBox.width, bottom: viewportBox.y + viewportBox.height,
  };

  // 1. 中央: 全周描画
  // 1. Centered: drawn on all sides
  const inside = await anchorRects(page, RESEARCH_NODE);
  expect(inside?.visible).not.toBeNull();
  expectMaskedLikeAnchor((await highlightVisibleRect(page))!, { full: inside!.full }, clip, glowPx);
  await page.screenshot({ path: testInfo.outputPath("clip-1-inside.png") });

  // 2. 端跨ぎ: 同位置で切れる
  // 2. Straddling the edge: cut at the same position
  await dragViewport(page, viewportBox, viewportBox.width / 2 - 40, 0);
  await settleBoundingBox(page, node);
  const partial = await anchorRects(page, RESEARCH_NODE);
  expect(partial?.visible).not.toBeNull();
  expect(partial!.visible!.right).toBeLessThan(partial!.full.right - TOLERANCE_PX);
  expectMaskedLikeAnchor((await highlightVisibleRect(page))!, { full: partial!.full }, clip, glowPx);
  await page.screenshot({ path: testInfo.outputPath("clip-2-partial.png") });

  // 3. 押し出し: 枠ごと消える
  // 3. Pushed out: the frame disappears too
  await dragViewport(page, viewportBox, viewportBox.width, 0);
  await page.waitForFunction(() =>
    document.querySelector('[data-testid="tutorial-overlay"] [data-kind="outline"]') === null);
  const outside = await anchorRects(page, RESEARCH_NODE);
  expect(outside!.visible).toBeNull();
  await page.screenshot({ path: testInfo.outputPath("clip-3-outside.png") });
});
