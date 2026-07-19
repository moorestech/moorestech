// GaugeBarのクランプ済み表示幅とテスト識別子を検証する
// Verifies GaugeBar's clamped fill width and test identifier
import { createElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import GaugeBar from "./index";

describe("GaugeBar", () => {
  it.each([
    { value: -0.2, width: "0%" },
    { value: 0.375, width: "37.5%" },
    { value: 1.4, width: "100%" },
    { value: Number.NaN, width: "0%" },
  ])("value=$valueを$widthへクランプして描画する", ({ value, width }) => {
    const markup = renderToStaticMarkup(createElement(GaugeBar, { value, testId: "gauge" }));

    expect(markup).toContain('data-testid="gauge"');
    expect(markup).toContain(`width:${width}`);
  });
});
