import { describe, expect, it } from "vitest";
import { computeTreeCanvasBounds, lineBetween, toTreeCanvasPoint } from "./treeGeometry";

describe("treeGeometry", () => {
  it("maps Y-up tree coordinates into CSS canvas coordinates", () => {
    const bounds = computeTreeCanvasBounds([{ id: "a", x: -100, y: 50 }, { id: "b", x: 100, y: -50 }], 20);
    expect(bounds).toEqual({ width: 240, height: 140, offsetX: 120, offsetY: 70 });
    expect(toTreeCanvasPoint({ x: -100, y: 50 }, bounds)).toEqual({ x: 20, y: 20 });
  });

  it("returns a padded canvas for an empty tree", () => {
    expect(computeTreeCanvasBounds([], 20)).toEqual({ width: 40, height: 40, offsetX: 20, offsetY: 20 });
  });

  it("creates a line rooted at the child point", () => {
    expect(lineBetween({ x: 2, y: 3 }, { x: 2, y: 13 })).toEqual({
      x: 2, y: 3, length: 10, angleDeg: 90,
    });
  });
});
