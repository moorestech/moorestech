// 矢印ゲージのクランプ済み充填幅と、複数描画時のclip id衝突回避を検証する
// Verifies the arrow gauge's clamped fill width and that multiple instances never share a clip id
import { createElement, Fragment } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import CraftProgressArrow from "./CraftProgressArrow";

describe("CraftProgressArrow", () => {
  it.each([
    { value: -0.2, width: "0" },
    { value: 0, width: "0" },
    { value: 0.5, width: "58.5" },
    { value: 1, width: "117" },
    { value: 1.4, width: "117" },
    { value: Number.NaN, width: "0" },
  ])("value=$valueを充填幅$widthへクランプする", ({ value, width }) => {
    const markup = renderToStaticMarkup(createElement(CraftProgressArrow, { value }));

    expect(markup).toContain(`width="${width}"`);
    expect(markup).toContain(`aria-valuenow="${Math.min(1, Math.max(0, Number.isNaN(value) ? 0 : value))}"`);
  });

  it("同一ページに複数並んでもclip idが衝突しない", () => {
    const markup = renderToStaticMarkup(
      createElement(Fragment, null, createElement(CraftProgressArrow, { value: 0.25 }), createElement(CraftProgressArrow, { value: 0.75 })),
    );

    // clipPathのidを全て拾い、重複が無いこと（衝突すると両方が同じ進捗で描かれる）
    // Collect every clipPath id and require them to be distinct; a collision would draw both arrows at one progress
    const ids = [...markup.matchAll(/<clipPath id="([^"]+)"/g)].map((m) => m[1]);
    expect(ids).toHaveLength(2);
    expect(new Set(ids).size).toBe(2);
    // clip参照も各自のidを指していること
    // Each clip reference must point at its own id
    for (const id of ids) expect(markup).toContain(`url(#${id})`);
  });
});
