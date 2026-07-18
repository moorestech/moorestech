import { describe, expect, it } from "vitest";
import { clampTooltipPosition } from "./tooltipPosition";

describe("clampTooltipPosition", () => {
  it("keeps the tooltip inside the viewport", () => {
    expect(clampTooltipPosition(790, 590, 180, 80, 800, 600)).toEqual({ x: 608, y: 508 });
  });
});
