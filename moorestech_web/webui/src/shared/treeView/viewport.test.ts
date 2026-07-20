import { describe, expect, it } from "vitest";
import { zoomViewportAt } from "./viewport";

describe("zoomViewportAt", () => {
  it("keeps the world point below the cursor fixed", () => {
    const result = zoomViewportAt({ x: 10, y: 20, scale: 1 }, { x: 110, y: 120 }, -100);
    expect((110 - result.x) / result.scale).toBeCloseTo(100);
    expect((120 - result.y) / result.scale).toBeCloseTo(100);
  });

  it("clamps zoom scale", () => {
    expect(zoomViewportAt({ x: 0, y: 0, scale: 1 }, { x: 0, y: 0 }, 100000).scale).toBe(0.4);
    expect(zoomViewportAt({ x: 0, y: 0, scale: 1 }, { x: 0, y: 0 }, -100000).scale).toBe(2.5);
  });
});
