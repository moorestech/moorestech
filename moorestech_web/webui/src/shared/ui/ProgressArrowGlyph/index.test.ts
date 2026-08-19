// クランプ・3層・id一意化を検証
// Verifies fill-width clamping, layer roles, and clip id uniqueness
import { createElement, Fragment } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import ProgressArrowGlyph from "./index";

describe("ProgressArrowGlyph", () => {
  it.each([
    { value: -0.2, width: "0", valuenow: "0" },
    { value: 0, width: "0", valuenow: "0" },
    { value: 0.5, width: "58.5", valuenow: "0.5" },
    { value: 1, width: "117", valuenow: "1" },
    { value: 1.4, width: "117", valuenow: "1" },
    { value: Number.NaN, width: "0", valuenow: "0" },
  ])("value=$valueを充填幅$widthへクランプする", ({ value, width, valuenow }) => {
    const markup = renderToStaticMarkup(createElement(ProgressArrowGlyph, { value, testId: "arrow" }));

    expect(markup).toContain(`width="${width}"`);
    expect(markup).toContain(`aria-valuenow="${valuenow}"`);
  });

  // クリップ矩形が矢印pathの水平範囲(x=2..119)と縦全域を覆うこと。x/heightがズレると満杯でも先端が残る
  // The clip rect must span the path extent (x=2..119) and the full height; a wrong x/height leaves the tip unfilled
  it("クリップ矩形が矢印pathの全域を覆う", () => {
    const markup = renderToStaticMarkup(createElement(ProgressArrowGlyph, { value: 1, testId: "arrow" }));

    expect(markup).toContain('<rect x="2" y="0" width="117" height="78">');
  });

  // 溝/充填/輪郭の取り違えを塞ぐ。clipが充填以外に付くと進捗が見えなくなる
  // Guards against swapping the layers; clipping anything but the fill hides progress entirely
  it("clipは充填層だけに付き、輪郭は最上層でclipされない", () => {
    const markup = renderToStaticMarkup(createElement(ProgressArrowGlyph, { value: 0.5, testId: "arrow" }));
    const paths = [...markup.matchAll(/<path[^>]*>/g)].map((m) => m[0]);

    expect(paths).toHaveLength(3);
    expect(paths[0]).not.toContain("clip-path");
    expect(paths[1]).toContain("clip-path");
    expect(paths[2]).not.toContain("clip-path");
    // クラス名はCSS Modulesでハッシュ化されるため、class属性内に元の名前を含むかで層を識別する
    // CSS Modules hashes class names, so identify each layer by the original name inside the class attribute
    expect(paths[0]).toMatch(/class="[^"]*track/);
    expect(paths[1]).toMatch(/class="[^"]*fill/);
    expect(paths[2]).toMatch(/class="[^"]*outline/);
  });

  // 進捗概念の無い呼び出し元(機械レシピ)が「0%で停止中のprogressbar」を名乗らないこと
  // A caller without any progress concept (machine recipes) must not pose as a progressbar stuck at 0%
  it("value=nullではprogressbarロールもaria値も出さない", () => {
    const markup = renderToStaticMarkup(createElement(ProgressArrowGlyph, { value: null, testId: "arrow" }));

    expect(markup).not.toContain("progressbar");
    expect(markup).not.toContain("aria-valuenow");
    expect(markup).not.toContain("aria-valuemin");
    expect(markup).not.toContain("aria-valuemax");
    expect(markup).toContain('data-testid="arrow"');
  });

  // 静止表示は溝+輪郭の2層のみ。充填層が残ると進捗0の表現と区別できなくなる
  // The static form draws only track + outline; a leftover fill layer would be indistinguishable from progress 0
  it("value=nullでは充填層とclipPathを描かない", () => {
    const markup = renderToStaticMarkup(createElement(ProgressArrowGlyph, { value: null, testId: "arrow" }));
    const paths = [...markup.matchAll(/<path[^>]*>/g)].map((m) => m[0]);

    expect(paths).toHaveLength(2);
    expect(paths[0]).toContain("track");
    expect(paths[1]).toContain("outline");
    expect(markup).not.toContain("clipPath");
    expect(markup).not.toContain("clip-path");
  });

  it("同一ページに複数並んでもclip idが衝突しない", () => {
    const markup = renderToStaticMarkup(
      createElement(
        Fragment,
        null,
        createElement(ProgressArrowGlyph, { value: 0.25, testId: "arrow-a" }),
        createElement(ProgressArrowGlyph, { value: 0.75, testId: "arrow-b" }),
      ),
    );

    // clipPathのidを全て拾い重複が無いこと。衝突すると両方が同じ進捗で描かれる
    // Collect every clipPath id and require them distinct; a collision draws both arrows at one progress
    const ids = [...markup.matchAll(/<clipPath id="([^"]+)"/g)].map((m) => m[1]);
    expect(ids).toHaveLength(2);
    expect(new Set(ids).size).toBe(2);
    // clip参照も各自のidを指していること
    // Each clip reference must point at its own id
    for (const id of ids) expect(markup).toContain(`url(#${id})`);
  });
});
