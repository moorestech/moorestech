import { expect, type Locator } from "@playwright/test";

export async function expectCraftFramedPlacementHud(hud: Locator) {
  // 外側配置と内側共通枠を分けて検証する
  // Verify fixed outer placement separately from the shared inner frame
  await expect(hud).toBeVisible();
  await expect(hud).toHaveCSS("pointer-events", "none");
  await expect(hud).toHaveCSS("top", "24px");
  await expect(hud).toHaveCSS("right", "24px");
  await expect(hud).toHaveCSS("width", "288px");
  await expect(hud.locator('[aria-hidden="true"]')).toHaveCount(1);

  const frame = hud.locator(':scope > [data-variant="craft"]');
  await expect(frame).toHaveCSS("background-color", "rgba(7, 9, 18, 0.8)");
  await expect(frame).toHaveCSS("border-width", "1px");
  await expect(frame).toHaveCSS("border-radius", "0px");
  await expect(frame).not.toHaveCSS("box-shadow", "none");

  // ラベルと本文の視覚階層を固定する
  // Lock the label-detail visual hierarchy
  const label = hud.getByTestId("operation-mode-label");
  const detail = hud.getByTestId("operation-mode-detail").first();
  await expect(label).toHaveCSS("color", "rgb(166, 167, 171)");
  await expect(label).toHaveCSS("font-size", "12px");
  await expect(label).toHaveCSS("font-weight", "400");
  await expect(label).toHaveCSS("letter-spacing", "1px");
  await expect(detail).toHaveCSS("color", "rgb(255, 255, 255)");
  await expect(detail).toHaveCSS("font-size", "17px");

  // 枠中央の入力透過を確認する
  // Verify input pass-through at the frame center
  const hitTargetInsideHud = await hud.evaluate((element) => {
    const rect = element.getBoundingClientRect();
    const target = document.elementFromPoint(rect.left + rect.width / 2, rect.top + rect.height / 2);
    return target !== null && element.contains(target);
  });
  expect(hitTargetInsideHud).toBe(false);

  // 明暗背景の文字コントラストを確認する
  // Verify text contrast over bright and dark backgrounds
  const contrastRatios = await frame.evaluate((element, backgrounds) => {
    const parseColor = (value: string) => {
      const channels = value.match(/[\d.]+/g)?.map(Number) ?? [];
      return { red: channels[0], green: channels[1], blue: channels[2], alpha: channels[3] ?? 1 };
    };
    const blend = (face: ReturnType<typeof parseColor>, world: ReturnType<typeof parseColor>) => ({
      red: face.red * face.alpha + world.red * (1 - face.alpha),
      green: face.green * face.alpha + world.green * (1 - face.alpha),
      blue: face.blue * face.alpha + world.blue * (1 - face.alpha),
      alpha: 1,
    });
    const luminance = (color: ReturnType<typeof parseColor>) => {
      const linear = [color.red, color.green, color.blue].map((channel) => {
        const normalized = channel / 255;
        return normalized <= 0.04045 ? normalized / 12.92 : ((normalized + 0.055) / 1.055) ** 2.4;
      });
      return linear[0] * 0.2126 + linear[1] * 0.7152 + linear[2] * 0.0722;
    };
    const contrast = (foreground: ReturnType<typeof parseColor>, background: ReturnType<typeof parseColor>) => {
      const lighter = Math.max(luminance(foreground), luminance(background));
      const darker = Math.min(luminance(foreground), luminance(background));
      return (lighter + 0.05) / (darker + 0.05);
    };

    const frameColor = parseColor(getComputedStyle(element).backgroundColor);
    const labelColor = parseColor(getComputedStyle(element.querySelector('[data-testid="operation-mode-label"]')!).color);
    const detailColor = parseColor(getComputedStyle(element.querySelector('[data-testid="operation-mode-detail"]')!).color);
    return backgrounds.map((background) => {
      const composite = blend(frameColor, parseColor(background));
      return {
        label: contrast(labelColor, composite),
        detail: contrast(detailColor, composite),
      };
    });
  }, ["rgb(225, 229, 235)", "rgb(12, 15, 22)"]);
  for (const ratios of contrastRatios) {
    expect(ratios.label).toBeGreaterThanOrEqual(4.5);
    expect(ratios.detail).toBeGreaterThanOrEqual(4.5);
  }

  const visualContract = await frame.evaluate((element) => {
    const frameStyle = getComputedStyle(element);
    const afterStyle = getComputedStyle(element, "::after");
    const detailStyle = getComputedStyle(element.querySelector('[data-testid="operation-mode-detail"]')!);
    return {
      animationName: frameStyle.animationName,
      afterContent: afterStyle.content,
      afterImage: afterStyle.backgroundImage,
      detailFontWeight: detailStyle.fontWeight,
      detailLineHeight: detailStyle.lineHeight,
    };
  });
  expect(visualContract).toEqual({
    animationName: "none",
    afterContent: "\"\"",
    afterImage: expect.stringContaining("linear-gradient"),
    detailFontWeight: "400",
    detailLineHeight: "25px",
  });
}
