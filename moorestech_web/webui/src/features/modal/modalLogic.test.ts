import { describe, it, expect } from "vitest";
import { respondPayload, buttonColor } from "./modalLogic";

describe("respondPayload", () => {
  it("id と confirm 結果をそのまま返す", () => {
    expect(respondPayload("m1", "confirm")).toEqual({ id: "m1", result: "confirm" });
  });
  it("cancel 結果も同様に返す", () => {
    expect(respondPayload("x", "cancel")).toEqual({ id: "x", result: "cancel" });
  });
});

// variant→Mantine color の対応。confirm は青、error は赤（uGUI の色分け準拠）
// variant→Mantine color mapping; confirm is blue, error is red, mirroring uGUI
describe("buttonColor", () => {
  it("confirm variant は blue", () => {
    expect(buttonColor("confirm")).toBe("blue");
  });
  it("error variant は red", () => {
    expect(buttonColor("error")).toBe("red");
  });
});
