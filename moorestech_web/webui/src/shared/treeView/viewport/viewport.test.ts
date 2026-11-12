import { describe, expect, it } from "vitest";
import {
  PAN_FRICTION_TAU_MS,
  PAN_MAX_FLING_SPEED,
  blendPanVelocity,
  centerViewportOn,
  clampPanVelocity,
  decayPanVelocity,
  shiftViewportForResize,
  zoomViewportAt,
} from "./viewport";

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

describe("centerViewportOn", () => {
  it("places the canvas point at the viewport center", () => {
    const result = centerViewportOn({ x: 800, y: 200 }, { width: 400, height: 300 }, 1);
    expect(result).toEqual({ x: -600, y: -50, scale: 1 });
  });

  it("accounts for scale when centering", () => {
    const result = centerViewportOn({ x: 100, y: 100 }, { width: 400, height: 300 }, 2);
    // 変換後の点はビューポート中央
    // The transformed point lands at the center
    expect(result.x + 100 * 2).toBe(200);
    expect(result.y + 100 * 2).toBe(150);
  });
});

describe("shiftViewportForResize", () => {
  it("縮んだぶんの半分だけ平行移動し、中央に見えていた点を中央に保つ", () => {
    const before = centerViewportOn({ x: 200, y: 200 }, { width: 1248, height: 484 }, 1);
    const after = shiftViewportForResize(before, { width: 1248, height: 484 }, { width: 1248, height: 449 });
    expect(after).toEqual(centerViewportOn({ x: 200, y: 200 }, { width: 1248, height: 449 }, 1));
  });

  it("拡縮率は変えない", () => {
    expect(shiftViewportForResize({ x: 10, y: 20, scale: 1.5 }, { width: 100, height: 100 }, { width: 140, height: 60 })).toEqual({ x: 30, y: 0, scale: 1.5 });
  });
});

describe("pan inertia math", () => {
  it("decays speed to 1/e after one time constant", () => {
    const decayed = decayPanVelocity({ x: 1, y: -2 }, PAN_FRICTION_TAU_MS);
    expect(decayed.x).toBeCloseTo(Math.exp(-1));
    expect(decayed.y).toBeCloseTo(-2 * Math.exp(-1));
  });

  it("blends toward the instantaneous velocity without overshooting", () => {
    const blended = blendPanVelocity({ x: 0, y: 0 }, 16, -8, 16);
    expect(blended.x).toBeGreaterThan(0);
    expect(blended.x).toBeLessThan(1);
    expect(blended.y).toBeLessThan(0);
    expect(blended.y).toBeGreaterThan(-0.5);
  });

  it("clamps the fling speed while preserving direction", () => {
    const clamped = clampPanVelocity({ x: 30, y: 40 });
    expect(Math.hypot(clamped.x, clamped.y)).toBeCloseTo(PAN_MAX_FLING_SPEED);
    expect(clamped.y / clamped.x).toBeCloseTo(40 / 30);
    expect(clampPanVelocity({ x: 0.5, y: 0 })).toEqual({ x: 0.5, y: 0 });
  });
});
