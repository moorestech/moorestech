import { expect, test, type Page } from "@playwright/test";
import { resetResearch, setTopicScenario, setUiState } from "../../support/mockControl";
import { settleBoundingBox } from "../../support/panSettle";
import { researchableNodeGuid } from "../../mock-host/researchFixtures";

const RESEARCH_NODE = `research-node-${researchableNodeGuid}`;
// mock-host の tutorialResearchNode シナリオの paddingPx と同じ値
// Same value as paddingPx in the mock host's tutorialResearchNode scenario
const PADDING_PX = 8;
// TutorialOverlay の HIGHLIGHT_GLOW_PX と同じ値。切れていない辺は枠がここまで外へ出る
// Same value as HIGHLIGHT_GLOW_PX in TutorialOverlay; intact sides extend this far outward
const GLOW_PX = 4;
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

// ハイライトの実可視領域を boundingBox と computed clip-path から復元する
// Reconstruct the highlight's actually visible region from its bounding box and computed clip-path
async function highlightVisibleRect(page: Page) {
  return page.evaluate(() => {
    const element = document.querySelector('[data-testid="tutorial-overlay"] [data-kind="outline"]');
    if (!element) return null;
    const box = element.getBoundingClientRect();
    // 計算値はCSS短縮形へ畳まれる（inset(-4px) / inset(10px 6px) 等）ので1〜4値を展開する
    // The computed value collapses to CSS shorthand (inset(-4px), inset(10px 6px)...), so expand 1..4 values
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

// クリップされた辺ではハイライトの可視端がアンカーの可視端に一致し、切れていない辺ではpadding+glow分だけ外側になる
// On clipped sides the highlight's visible edge matches the anchor's; on intact sides it sits padding+glow outside
function expectMaskedLikeAnchor(highlight: Rect, anchor: { full: Rect; visible: Rect }) {
  const sides = [
    { key: "left" as const, sign: -1 }, { key: "top" as const, sign: -1 },
    { key: "right" as const, sign: 1 }, { key: "bottom" as const, sign: 1 },
  ];
  for (const side of sides) {
    const clipped = Math.abs(anchor.visible[side.key] - anchor.full[side.key]) > TOLERANCE_PX;
    const expected = clipped
      ? anchor.visible[side.key]
      : anchor.full[side.key] + side.sign * (PADDING_PX + GLOW_PX);
    expect(Math.abs(highlight[side.key] - expected), `${side.key} (clipped=${clipped})`).toBeLessThanOrEqual(TOLERANCE_PX);
  }
}

// 空背景を掴んでキャンバスをパンする。ノード上から始めるとクリック扱いになる
// Pan the canvas by grabbing the empty background; starting on a node would count as a click
async function dragViewport(page: Page, viewportBox: { x: number; y: number; width: number; height: number }, dx: number, dy: number) {
  const start = { x: viewportBox.x + viewportBox.width - 40, y: viewportBox.y + viewportBox.height - 40 };
  await page.mouse.move(start.x, start.y);
  await page.mouse.down();
  await page.mouse.move(start.x + dx, start.y + dy, { steps: 10 });
  // PAN_RELEASE_STALL_MS(80ms)より長く静止させ慣性フリングを起こさず狙った位置で止める
  // Stall longer than PAN_RELEASE_STALL_MS (80ms) so releasing doesn't trigger inertial fling past the target
  await page.waitForTimeout(120);
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

  // 1. ノードがビューポート中央にある状態: 枠は全周が描かれる
  // 1. The node sits centered in the viewport: the frame is drawn on all four sides
  const inside = await anchorRects(page, RESEARCH_NODE);
  expect(inside?.visible).not.toBeNull();
  expectMaskedLikeAnchor((await highlightVisibleRect(page))!, { full: inside!.full, visible: inside!.visible! });
  await page.screenshot({ path: testInfo.outputPath("clip-1-inside.png") });

  // 2. ノードがビューポート端をまたぐまでパンする: 枠がノードと同じ位置で切られる
  // 2. Pan until the node straddles the viewport edge: the frame is cut where the node is
  const viewportBox = (await page.getByTestId("research-viewport").boundingBox())!;
  await dragViewport(page, viewportBox, viewportBox.width / 2 - 40, 0);
  await settleBoundingBox(page, node);
  const partial = await anchorRects(page, RESEARCH_NODE);
  expect(partial?.visible).not.toBeNull();
  expect(partial!.visible!.right).toBeLessThan(partial!.full.right - TOLERANCE_PX);
  expectMaskedLikeAnchor((await highlightVisibleRect(page))!, { full: partial!.full, visible: partial!.visible! });
  await page.screenshot({ path: testInfo.outputPath("clip-2-partial.png") });

  // 3. ノードを完全に押し出す: 枠は要素ごと消える
  // 3. Push the node fully out: the frame disappears element and all
  await dragViewport(page, viewportBox, viewportBox.width, 0);
  await page.waitForFunction(() =>
    document.querySelector('[data-testid="tutorial-overlay"] [data-kind="outline"]') === null);
  const outside = await anchorRects(page, RESEARCH_NODE);
  expect(outside!.visible).toBeNull();
  await page.screenshot({ path: testInfo.outputPath("clip-3-outside.png") });
});
