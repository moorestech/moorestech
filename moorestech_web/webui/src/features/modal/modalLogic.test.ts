import { describe, it, expect } from "vitest";
import { respondPayload, buttonColor, canConfirm } from "./modalLogic";

describe("respondPayload", () => {
  it("id と confirm 結果をそのまま返す", () => {
    expect(respondPayload("m1", "confirm")).toEqual({ id: "m1", result: "confirm" });
  });
  it("cancel 結果も同様に返す", () => {
    expect(respondPayload("x", "cancel")).toEqual({ id: "x", result: "cancel" });
  });
});

describe("respondPayload with text", () => {
  it("text 付き confirm を組み立てる", () => {
    expect(respondPayload("m2", "confirm", "家")).toEqual({ id: "m2", result: "confirm", text: "家" });
  });
  it("text 省略時は text キーを含めない", () => {
    expect(respondPayload("m1", "cancel")).toEqual({ id: "m1", result: "cancel" });
  });
});

describe("canConfirm", () => {
  it("非inputモーダルは常に確定可", () => {
    expect(canConfirm(undefined, "")).toBe(true);
  });
  it("inputモーダルは空白のみを確定不可にする", () => {
    expect(canConfirm(true, "   ")).toBe(false);
    expect(canConfirm(true, " 家 ")).toBe(true);
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
