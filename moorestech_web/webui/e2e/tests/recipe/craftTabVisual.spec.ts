import { expect, test } from "@playwright/test";

test.beforeEach(async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "CRAFT RECIPE" })).toBeVisible();
  await page.getByTestId("item-list-grid").locator("> div").first().click();
  await expect(page.getByTestId("craft-tab")).toBeVisible();
});

test("クラフトタブの右斜辺を階段形状にしない", async ({ page }) => {
  const side = page.getByTestId("craft-tab").locator("path").nth(3);
  const rowEndpoints = await side.evaluate((path) => {
    const geometry = path as SVGGeometryElement;
    const rows: Array<{ left: number; right: number }> = [];

    // 右側成分を各行の塗り領域として走査する
    // Scan the right component as a filled interval on every row
    for (let y = 10; y < 70; y += 1) {
      const filled = [];
      for (let x = 100; x < 150; x += 1) {
        if (geometry.isPointInFill(new DOMPoint(x + 0.5, y + 0.5))) filled.push(x);
      }
      if (filled.length > 0) rows.push({ left: filled[0], right: filled.at(-1)! });
    }
    return rows;
  });

  // 全走査行を確保し、旧段差の行欠落を検知する
  // Require every scanned row so missing rows from the old stepped edge fail
  expect(rowEndpoints).toHaveLength(60);

  // 滑らかな斜辺では左右端が各行で最大1pxだけ下方へ進む
  // Both edges of a smooth slope advance downward by at most one pixel per row
  for (let index = 1; index < rowEndpoints.length; index += 1) {
    const leftDelta = rowEndpoints[index].left - rowEndpoints[index - 1].left;
    const rightDelta = rowEndpoints[index].right - rowEndpoints[index - 1].right;
    expect(leftDelta).toBeGreaterThanOrEqual(0);
    expect(leftDelta).toBeLessThanOrEqual(1);
    expect(rightDelta).toBeGreaterThanOrEqual(0);
    expect(rightDelta).toBeLessThanOrEqual(1);
  }
});

test("クラフトタブの下端をパネル上端へ接触させる", async ({ page }) => {
  const craftTab = page.getByTestId("craft-tab");
  const craftPanel = craftTab.locator('xpath=ancestor::*[@data-variant="craft"][1]');
  const back = craftTab.locator("path").first();

  // 背面からパネルまで描画経路を検証
  // Verify the paint path from the back to the panel
  const backState = await back.evaluate((path) => {
    const geometry = path as SVGGeometryElement;
    const style = getComputedStyle(path);
    const panelAncestorStyles: Array<{ display: string; visibility: string; opacity: number; clipPath: string; overflow: string }> = [];
    let currentElement: Element | null = path;
    let effectiveOpacity = 1;
    let reachedCraftPanel = false;

    // パネルまで祖先の表示・クリップを収集
    // Collect ancestor display and clipping up to the panel
    while (currentElement !== null) {
      const currentStyle = getComputedStyle(currentElement);
      const opacity = Number.parseFloat(currentStyle.opacity);
      panelAncestorStyles.push({
        display: currentStyle.display,
        visibility: currentStyle.visibility,
        opacity,
        clipPath: currentStyle.clipPath,
        overflow: currentStyle.overflow,
      });
      effectiveOpacity *= opacity;
      if (currentElement.getAttribute("data-variant") === "craft") {
        reachedCraftPanel = true;
        break;
      }
      currentElement = currentElement.parentElement;
    }

    // 背面の塗りと下端ヒットを返す
    // Return back fill and lower hit
    return {
      fill: style.fill,
      fillOpacity: Number.parseFloat(style.fillOpacity),
      fillsLowerInnerPoint: geometry.isPointInFill(new DOMPoint(75, 71)),
      panelAncestorStyles,
      effectiveOpacity,
      reachedCraftPanel,
    };
  });
  expect(backState.fill).not.toBe("none");
  expect(backState.fillOpacity).toBe(1);
  expect(backState.fillsLowerInnerPoint).toBe(true);
  expect(backState.reachedCraftPanel).toBe(true);
  for (const ancestorStyle of backState.panelAncestorStyles) {
    expect(ancestorStyle.display).not.toBe("none");
    expect(ancestorStyle.visibility).toBe("visible");
    expect(ancestorStyle.opacity).toBeGreaterThan(0);
    expect(ancestorStyle.clipPath).toBe("none");
    expect(ancestorStyle.overflow).toBe("visible");
  }
  expect(backState.effectiveOpacity).toBe(1);

  // 外接矩形でもパネル上端との隙間を許容範囲内に固定する
  // Keep the bounding-box gap to the panel top within the permitted range
  const gap = await back.evaluate((path, panel) => {
    return (panel as Element).getBoundingClientRect().top - path.getBoundingClientRect().bottom;
  }, await craftPanel.elementHandle());

  expect(gap).toBeLessThanOrEqual(0);
  expect(gap).toBeGreaterThanOrEqual(-0.5);
});
