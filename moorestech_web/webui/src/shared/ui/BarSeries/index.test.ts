// BarSeriesのクランプ済み充填高さ・古い順の並び・値無し棒の印を検証する
// Verifies BarSeries' clamped fill heights, oldest-first order and the marker on bars without a value
import { createElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import BarSeries from "./index";

describe("BarSeries", () => {
  it("renders one bar per value with clamped heights, oldest first", () => {
    const markup = renderToStaticMarkup(createElement(BarSeries, { values: [-0.2, 0.375, 1.4, Number.NaN], testId: "bars" }));

    expect(markup).toContain('data-testid="bars"');
    const heights = [...markup.matchAll(/height:([0-9.]+%)/g)].map((m) => m[1]);
    expect(heights).toEqual(["0%", "37.5%", "100%", "0%"]);
  });

  it("marks a null value as an empty bar with only its track", () => {
    const markup = renderToStaticMarkup(createElement(BarSeries, { values: [null, 0.5] }));

    expect(markup.match(/data-empty="true"/g)).toHaveLength(1);
    expect([...markup.matchAll(/height:([0-9.]+%)/g)].map((m) => m[1])).toEqual(["0%", "50%"]);
  });
});
